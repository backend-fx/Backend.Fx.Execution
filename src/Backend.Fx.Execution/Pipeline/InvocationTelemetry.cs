using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution.Pipeline;

/// <summary>
/// Observes a single invocation: logging scope, tracing activity, metrics and exception enrichment.
/// Report the outcome via <see cref="Succeeded"/>, <see cref="Canceled"/> or <see cref="Faulted"/>;
/// everything is recorded on <see cref="Dispose"/>.
/// </summary>
internal sealed class InvocationTelemetry : IDisposable
{
    private static readonly ActivitySource ActivitySource = new("Backend.Fx.Execution");
    private static readonly Meter Meter = new("Backend.Fx.Execution");
    private static readonly Counter<long> InvocationTotal = Meter.CreateCounter<long>(
        "backendfx.invocations.total"
    );

    private static readonly Counter<long> InvocationSucceeded = Meter.CreateCounter<long>(
        "backendfx.invocations.succeeded"
    );

    private static readonly Counter<long> InvocationFaulted = Meter.CreateCounter<long>(
        "backendfx.invocations.faulted"
    );

    private static readonly Counter<long> InvocationCanceled = Meter.CreateCounter<long>(
        "backendfx.invocations.canceled"
    );

    private static readonly Histogram<double> InvocationDurationMs = Meter.CreateHistogram<double>(
        "backendfx.invocations.duration_ms"
    );

    private readonly ILogger _logger;
    private readonly IIdentity _identity;
    private readonly int _operationCounter;
    private readonly Correlation _correlation;
    private readonly string _identityType;
    private readonly IDisposable? _logScope;
    private readonly Activity? _activity;
    private readonly Stopwatch _duration = Stopwatch.StartNew();
    private string _outcome = "Succeeded";

    private InvocationTelemetry(
        ILogger logger,
        IIdentity identity,
        int operationCounter,
        Correlation correlation
    )
    {
        _logger = logger;
        _identity = identity;
        _operationCounter = operationCounter;
        _correlation = correlation;
        _identityType = GetIdentityType(identity);

        _logScope = logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["OperationCounter"] = operationCounter,
                ["CorrelationId"] = correlation.Id,
                ["Identity"] = identity.Name,
                ["Invoker"] = nameof(BackendFxApplicationInvoker),
            }
        );

        _activity = ActivitySource.StartActivity("InvokeAsync");
        _activity?.SetTag("backendfx.operation.counter", operationCounter);
        _activity?.SetTag("backendfx.correlation.id", correlation.Id.ToString());
        _activity?.SetTag("backendfx.identity.type", _identityType);

        _logger.LogInformation(
            "Starting invocation[{OperationCounter}] (correlation [{CorrelationId}]) for {Identity}",
            operationCounter,
            correlation.Id,
            identity.Name
        );
    }

    public static InvocationTelemetry Start(
        ILogger logger,
        IIdentity identity,
        int operationCounter,
        Correlation correlation
    ) => new(logger, identity, operationCounter, correlation);

    public void Succeeded()
    {
        _outcome = "Succeeded";
        _activity?.SetStatus(ActivityStatusCode.Ok);
    }

    public void Canceled()
    {
        _outcome = "Canceled";
        _activity?.SetTag("backendfx.canceled", true);
    }

    public void Faulted(Exception ex)
    {
        _outcome = "Faulted";
        _activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
        _activity?.SetTag("backendfx.exception.type", ex.GetType().FullName);

        TryAddData(ex, "OperationCounter", _operationCounter);
        TryAddData(ex, "Identity", _identity.Name);
        TryAddData(ex, "Correlation", _correlation.Id);
    }

    public void Dispose()
    {
        var elapsedMs = _duration.Elapsed.TotalMilliseconds;

        _activity?.SetTag("backendfx.outcome", _outcome);
        _activity?.SetTag("backendfx.duration.ms", elapsedMs);

        var metricTags = new KeyValuePair<string, object?>[]
        {
            new("outcome", _outcome),
            new("identity_type", _identityType),
            new("identity_name", _identity.Name),
        };

        InvocationTotal.Add(1, metricTags);
        InvocationDurationMs.Record(elapsedMs, metricTags);
        var outcomeCounter = _outcome switch
        {
            "Canceled" => InvocationCanceled,
            "Faulted" => InvocationFaulted,
            _ => InvocationSucceeded,
        };
        outcomeCounter.Add(1, metricTags);

        _logger.LogInformation(
            "Ended invocation[{OperationCounter}] (correlation [{CorrelationId}]) for {Identity} with outcome {Outcome} in {DurationMs} ms",
            _operationCounter,
            _correlation.Id,
            _identity.Name,
            _outcome,
            (long)elapsedMs
        );

        _activity?.Dispose();
        _logScope?.Dispose();
    }

    private void TryAddData(Exception ex, string key, object? value)
    {
        try
        {
            ex.Data[key] = value;
        }
        catch (Exception handlingEx)
        {
            _logger.LogWarning(handlingEx, "Failed to add {Key} to exception", key);
        }
    }

    private static string GetIdentityType(IIdentity identity) =>
        identity switch
        {
            SystemIdentity => "SystemIdentity",
            AnonymousIdentity => "AnonymousIdentity",
            _ => identity.GetType().Name,
        };
}
