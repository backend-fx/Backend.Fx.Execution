using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Logging;
using Backend.Fx.Util;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution.Pipeline
{
    internal class BackendFxApplicationInvoker : IBackendFxApplicationInvoker
    {
        private readonly IBackendFxApplication _application;
        private readonly ILogger _logger = Log.Create<BackendFxApplicationInvoker>();

        public BackendFxApplicationInvoker(IBackendFxApplication application)
        {
            _application = application;
        }

        public async Task InvokeAsync(Func<IServiceProvider, CancellationToken, Task> awaitableAsyncAction,
            IIdentity identity = null,
            CancellationToken cancellationToken = default,
            bool allowInvocationDuringBoot = false)
        {
            if (!allowInvocationDuringBoot)
            {
                await AssertBootedApplicationAsync(cancellationToken).ConfigureAwait(false);
            }
            
            AssertFunctionalApplication();

            identity ??= new AnonymousIdentity();
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

        private void AssertFunctionalApplication()
        {
            if (_application.State == BackendFxApplicationState.BootFailed)
            {
                throw new InvalidOperationException("The application failed to start. Cannot execute invocations.");
            }
        }
        
        private async Task AssertBootedApplicationAsync(CancellationToken cancellationToken)
        {
            if (_application.State is BackendFxApplicationState.Initializing or BackendFxApplicationState.Booting)
            {
                _logger.LogInformation("Waiting for application to finish boot process");
                await _application.WaitForBootAsync(cancellationToken).ConfigureAwait(false);
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
}