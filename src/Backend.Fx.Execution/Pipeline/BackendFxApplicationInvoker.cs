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
        using var durationLogger = UseDurationLogger(serviceScope, operation.Counter);
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
        }
        catch (Exception ex)
        {
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
}