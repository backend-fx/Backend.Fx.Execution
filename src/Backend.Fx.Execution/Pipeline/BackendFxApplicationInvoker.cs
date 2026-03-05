using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Logging;
using Backend.Fx.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution.Pipeline;

internal class BackendFxApplicationInvoker : IBackendFxApplicationInvoker
{
    private static readonly ActivitySource ActivitySource = new("Backend.Fx.Execution");
    private static readonly Meter Meter = new("Backend.Fx.Execution");
    private static readonly Counter<long> InvocationTotal = Meter.CreateCounter<long>("backendfx.invocations.total");

    private static readonly Counter<long> InvocationSucceeded =
        Meter.CreateCounter<long>("backendfx.invocations.succeeded");

    private static readonly Counter<long>
        InvocationFaulted = Meter.CreateCounter<long>("backendfx.invocations.faulted");

    private static readonly Counter<long> InvocationCanceled =
        Meter.CreateCounter<long>("backendfx.invocations.canceled");

    private static readonly Histogram<double> InvocationDurationMs =
        Meter.CreateHistogram<double>("backendfx.invocations.duration_ms");

    private readonly IBackendFxApplication _application;
    private readonly ILogger _logger = Log.Create<BackendFxApplicationInvoker>();

    public BackendFxApplicationInvoker(IBackendFxApplication application)
    {
        _application = application;
    }

    public async Task InvokeAsync(Func<IServiceProvider, CancellationToken, Task> awaitableAsyncAction,
        IIdentity? identity = null,
        CancellationToken cancellation = default)
    {
        identity ??= new AnonymousIdentity();

        await AssertCorrectUserModeAsync(identity, cancellation).ConfigureAwait(false);

        _logger.LogInformation("Invoking action as {Identity}", identity.Name);
        using var serviceScope = BeginScope(identity);
        var operation = BeginOperationAs(serviceScope, identity);
        var correlation = serviceScope.ServiceProvider.GetRequiredService<ICurrentTHolder<Correlation>>().Current;
        using var invocationScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["OperationCounter"] = operation.Counter,
            ["CorrelationId"] = correlation.Id,
            ["Identity"] = identity.Name,
            ["Invoker"] = nameof(BackendFxApplicationInvoker)
        });
        using var durationLogger = UseDurationLogger(serviceScope, operation.Counter);
        var invocationDuration = Stopwatch.StartNew();
        var outcome = "Succeeded";
        var identityType = GetIdentityType(identity);
        using var invocationActivity = ActivitySource.StartActivity("backendfx.invocation");

        invocationActivity?.SetTag("backendfx.operation.counter", operation.Counter);
        invocationActivity?.SetTag("backendfx.correlation.id", correlation.Id.ToString());
        invocationActivity?.SetTag("backendfx.identity.type", identityType);
        invocationActivity?.SetTag("backendfx.app.state.start", _application.State.ToString());
        
        try
        {
            _logger.LogTrace("Starting operation");
            await operation
                .BeginAsync(serviceScope, cancellation)
                .ConfigureAwait(false);
            _logger.LogTrace("operation started");

            _logger.LogTrace("Invoking action");
            await awaitableAsyncAction
                .Invoke(serviceScope.ServiceProvider, cancellation)
                .ConfigureAwait(false);
            _logger.LogTrace("Action invoked");

            _logger.LogTrace("Completing operation");
            await operation
                .CompleteAsync(cancellation)
                .ConfigureAwait(false);
            _logger.LogTrace("Operation completed");

            invocationActivity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            outcome = "Canceled";
            invocationActivity?.SetTag("backendfx.canceled", true);

            try
            {
                _logger.LogTrace("Canceling operation");
                await operation.CancelAsync(cancellation).ConfigureAwait(false);
                _logger.LogTrace("Operation canceled");
            }
            catch (Exception cancelEx)
            {
                _logger.LogError(cancelEx, "Failed to cancel the operation");
            }

            throw;
        }
        catch (Exception ex)
        {
            outcome = "Faulted";
            invocationActivity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            invocationActivity?.SetTag("backendfx.exception.type", ex.GetType().FullName);

            try
            {
                ex.Data["OperationCounter"] = operation.Counter;
            }
            catch (Exception handlingEx)
            {
                _logger.LogWarning(handlingEx, "Failed to add operation counter to exception");
            }

            try
            {
                ex.Data["Identity"] = identity.Name;
            }
            catch (Exception handlingEx)
            {
                _logger.LogWarning(handlingEx, "Failed to add identity to exception");
            }

            try
            {
                ex.Data["Correlation"] = serviceScope.ServiceProvider.GetRequiredService<ICurrentTHolder<Correlation>>()
                    .Current.Id;
            }
            catch (Exception handlingEx)
            {
                _logger.LogWarning(handlingEx, "Failed to add correlation to exception");
            }

            try
            {
                _logger.LogTrace("Canceling operation");
                await operation.CancelAsync(cancellation).ConfigureAwait(false);
                _logger.LogTrace("Operation canceled");
            }
            catch (Exception cancelEx)
            {
                _logger.LogError(cancelEx, "Failed to cancel the operation");
            }

            throw;
        }
        finally
        {
            invocationActivity?.SetTag("backendfx.outcome", outcome);
            invocationActivity?.SetTag("backendfx.duration.ms", invocationDuration.Elapsed.TotalMilliseconds);
            invocationActivity?.SetTag("backendfx.app.state.end", _application.State.ToString());

            var metricTags = new KeyValuePair<string, object?>[]
            {
                new("outcome", outcome),
                new("identity_type", identityType),
                new("identity_name", identity.Name),
                new("app_state", _application.State.ToString())
            };

            InvocationTotal.Add(1, metricTags);
            InvocationDurationMs.Record(invocationDuration.Elapsed.TotalMilliseconds, metricTags);

            switch (outcome)
            {
                case "Succeeded":
                    InvocationSucceeded.Add(1, metricTags);
                    break;
                case "Canceled":
                    InvocationCanceled.Add(1, metricTags);
                    break;
                case "Faulted":
                    InvocationFaulted.Add(1, metricTags);
                    break;
            }

            _logger.LogInformation(
                "Invocation {OperationCounter} ended with outcome {Outcome} in {DurationMs} ms",
                operation.Counter,
                outcome,
                invocationDuration.ElapsedMilliseconds);
        }
    }

    private async Task AssertCorrectUserModeAsync(IIdentity identity, CancellationToken cancellation)
    {
        // SystemIdentity is allowed to run in SingleUserMode, too
        if (identity is SystemIdentity && _application.State is BackendFxApplicationState.SingleUserMode)
        {
            return;
        }

        // all other users must wait for MultiUserMode
        if (_application.State is BackendFxApplicationState.Halted or BackendFxApplicationState.SingleUserMode)
        {
            _logger.LogInformation("Waiting for multi user mode");
            await _application.WaitForBootAsync(cancellation).ConfigureAwait(false);
        }

        // the application must not be crashed at this point
        if (_application.State == BackendFxApplicationState.Crashed)
        {
            throw new InvalidOperationException("The application failed to start. Cannot execute invocations.");
        }
    }


    private IServiceScope BeginScope(IIdentity? identity = null)
    {
        identity ??= new AnonymousIdentity();

        _logger.LogTrace("Beginning scope for {Identity}", identity.Name);
        var serviceScope = _application.CompositionRoot.BeginScope();

        serviceScope.ServiceProvider.GetRequiredService<ICurrentTHolder<IIdentity>>().ReplaceCurrent(identity);

        return serviceScope;
    }

    private static IOperation BeginOperationAs(IServiceScope serviceScope, IIdentity? identity = null)
    {
        identity ??= new AnonymousIdentity();
        var operation = serviceScope.ServiceProvider.GetRequiredService<IOperation>();
        serviceScope.ServiceProvider.GetRequiredService<ICurrentTHolder<IIdentity>>().ReplaceCurrent(identity);
        return operation;
    }


    private IDisposable UseDurationLogger(IServiceScope serviceScope, int operationCounter)
    {
        var identity = serviceScope.ServiceProvider.GetRequiredService<ICurrentTHolder<IIdentity>>().Current;
        var correlation = serviceScope.ServiceProvider.GetRequiredService<ICurrentTHolder<Correlation>>().Current;
        return _logger.LogInformationDuration(
            $"Starting invocation[{operationCounter}] (correlation [{correlation.Id}]) for {identity.Name}",
            $"Ended invocation[{operationCounter}] (correlation [{correlation.Id}]) for {identity.Name}");
    }

    private static string GetIdentityType(IIdentity identity)
    {
        return identity switch
        {
            SystemIdentity => "SystemIdentity",
            AnonymousIdentity => "AnonymousIdentity",
            _ => identity.GetType().Name
        };
    }
}