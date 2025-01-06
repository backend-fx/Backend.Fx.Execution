using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Logging;
using Backend.Fx.Util;
using JetBrains.Annotations;
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
                                  CancellationToken cancellationToken = default)
    {
        identity ??= new AnonymousIdentity();

        await AssertCorrectUserModeAsync(identity, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Invoking action as {Identity}", identity.Name);
        using IServiceScope serviceScope = BeginScope(identity);
        using IDisposable durationLogger = UseDurationLogger(serviceScope);
        var operation = serviceScope.ServiceProvider.GetRequiredService<IOperation>();
        try
        {
            _logger.LogTrace("Starting operation");
            await operation
                  .BeginAsync(serviceScope, cancellationToken)
                  .ConfigureAwait(false);
            _logger.LogTrace("operation started");

            _logger.LogTrace("Invoking action");
            await awaitableAsyncAction
                  .Invoke(serviceScope.ServiceProvider, cancellationToken)
                  .ConfigureAwait(false);
            _logger.LogTrace("Action invoked");

            _logger.LogTrace("Completing operation");
            await operation
                  .CompleteAsync(cancellationToken)
                  .ConfigureAwait(false);
            _logger.LogTrace("Operation completed");
        }
        catch
        {
            try
            {
                _logger.LogTrace("Canceling operation");
                await operation.CancelAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogTrace("Operation canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel the operation");
            }

            throw;
        }
    }

    private async Task AssertCorrectUserModeAsync(IIdentity identity, CancellationToken cancellationToken)
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
            await _application.WaitForBootAsync(cancellationToken).ConfigureAwait(false);
        }

        // the application must not be crashed at this point
        if (_application.State == BackendFxApplicationState.Crashed)
        {
            throw new InvalidOperationException("The application failed to start. Cannot execute invocations.");
        }
    }



    private IServiceScope BeginScope([CanBeNull] IIdentity identity)
    {
        identity ??= new AnonymousIdentity();

        _logger.LogTrace("Beginning scope for {Identity}", identity.Name);
        IServiceScope serviceScope = _application.CompositionRoot.BeginScope();

        serviceScope.ServiceProvider.GetRequiredService<ICurrentTHolder<IIdentity>>().ReplaceCurrent(identity);

        return serviceScope;
    }


    private IDisposable UseDurationLogger(IServiceScope serviceScope)
    {
        IIdentity identity = serviceScope.ServiceProvider.GetRequiredService<ICurrentTHolder<IIdentity>>().Current;
        Correlation correlation =
            serviceScope.ServiceProvider.GetRequiredService<ICurrentTHolder<Correlation>>().Current;
        return _logger.LogInformationDuration(
            $"Starting invocation (correlation [{correlation.Id}]) for {identity.Name}",
            $"Ended invocation (correlation [{correlation.Id}]) for {identity.Name}");
    }
}