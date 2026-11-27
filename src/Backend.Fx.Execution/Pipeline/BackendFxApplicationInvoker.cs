using System;
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
    private readonly IBackendFxApplication _application;
    private readonly ApplicationLifecycle _lifecycle;
    private readonly ILogger _logger = Log.Create<BackendFxApplicationInvoker>();

    public BackendFxApplicationInvoker(
        IBackendFxApplication application,
        ApplicationLifecycle lifecycle
    )
    {
        _application = application;
        _lifecycle = lifecycle;
    }

    public async Task InvokeAsync(
        Func<IServiceProvider, CancellationToken, Task> awaitableAsyncAction,
        IIdentity? identity = null,
        CancellationToken cancellation = default
    )
    {
        identity ??= new AnonymousIdentity();

        await _lifecycle.AdmitAsync(identity, cancellation).ConfigureAwait(false);

        _logger.LogInformation("Invoking action as {Identity}", identity.Name);

        using var cancellationOrShutdownRequested = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation,
            _application.ShutdownRequested
        );

        using var serviceScope = _application.CompositionRoot.BeginScope();

        var operation = ResolveOperation(
            serviceScope,
            cancellationOrShutdownRequested.Token,
            identity
        );

        var correlation = serviceScope
            .ServiceProvider.GetRequiredService<ICurrentTHolder<Correlation>>()
            .Current;

        using var telemetry = InvocationTelemetry.Start(
            _logger,
            identity,
            operation.Counter,
            correlation
        );

        try
        {
            await operation
                .BeginAsync(serviceScope, cancellationOrShutdownRequested.Token)
                .ConfigureAwait(false);

            await awaitableAsyncAction
                .Invoke(serviceScope.ServiceProvider, cancellationOrShutdownRequested.Token)
                .ConfigureAwait(false);

            await operation
                .CompleteAsync(cancellationOrShutdownRequested.Token)
                .ConfigureAwait(false);

            telemetry.Succeeded();
        }
        catch (OperationCanceledException)
            when (cancellationOrShutdownRequested.Token.IsCancellationRequested)
        {
            telemetry.Canceled();
            await CancelSafelyAsync(operation, cancellationOrShutdownRequested.Token)
                .ConfigureAwait(false);

            throw;
        }
        catch (Exception ex)
        {
            telemetry.Faulted(ex);
            await CancelSafelyAsync(operation, cancellationOrShutdownRequested.Token)
                .ConfigureAwait(false);

            throw;
        }
    }

    private static IOperation ResolveOperation(
        IServiceScope serviceScope,
        CancellationToken invocationCancellation,
        IIdentity identity
    )
    {
        serviceScope
            .ServiceProvider.GetRequiredService<ICurrentTHolder<IIdentity>>()
            .ReplaceCurrent(identity);

        serviceScope
            .ServiceProvider.GetRequiredService<ICurrentTHolder<CancellationToken>>()
            .ReplaceCurrent(invocationCancellation);

        return serviceScope.ServiceProvider.GetRequiredService<IOperation>();
    }

    private async Task CancelSafelyAsync(IOperation operation, CancellationToken cancellation)
    {
        try
        {
            await operation.CancelAsync(cancellation).ConfigureAwait(false);
        }
        catch (Exception cancelEx)
        {
            _logger.LogError(cancelEx, "Failed to cancel the operation");
        }
    }
}
