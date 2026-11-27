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
        using var serviceScope = BeginScope(identity);
        using var invocationCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation,
            _application.ShutdownRequested
        );
        var invocationCancellation = invocationCancellationSource.Token;
        serviceScope
            .ServiceProvider.GetRequiredService<ICurrentTHolder<CancellationToken>>()
            .ReplaceCurrent(invocationCancellation);
        var operation = BeginOperationAs(serviceScope, identity);
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
            await operation.BeginAsync(serviceScope, invocationCancellation).ConfigureAwait(false);
            await awaitableAsyncAction
                .Invoke(serviceScope.ServiceProvider, invocationCancellation)
                .ConfigureAwait(false);
            await operation.CompleteAsync(invocationCancellation).ConfigureAwait(false);
            telemetry.Succeeded();
        }
        catch (OperationCanceledException) when (invocationCancellation.IsCancellationRequested)
        {
            telemetry.Canceled();
            await CancelSafelyAsync(operation, invocationCancellation).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            telemetry.Faulted(ex);
            await CancelSafelyAsync(operation, invocationCancellation).ConfigureAwait(false);
            throw;
        }
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

    private IServiceScope BeginScope(IIdentity? identity = null)
    {
        identity ??= new AnonymousIdentity();

        _logger.LogTrace("Beginning scope for {Identity}", identity.Name);
        var serviceScope = _application.CompositionRoot.BeginScope();

        serviceScope
            .ServiceProvider.GetRequiredService<ICurrentTHolder<IIdentity>>()
            .ReplaceCurrent(identity);

        return serviceScope;
    }

    private static IOperation BeginOperationAs(
        IServiceScope serviceScope,
        IIdentity? identity = null
    )
    {
        identity ??= new AnonymousIdentity();
        var operation = serviceScope.ServiceProvider.GetRequiredService<IOperation>();
        serviceScope
            .ServiceProvider.GetRequiredService<ICurrentTHolder<IIdentity>>()
            .ReplaceCurrent(identity);
        return operation;
    }
}
