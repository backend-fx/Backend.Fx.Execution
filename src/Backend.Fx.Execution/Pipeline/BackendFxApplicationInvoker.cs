using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Exceptions;
using Backend.Fx.Execution.Commands;
using Backend.Fx.Logging;
using Backend.Fx.Util;
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
            IIdentity identity = null, CancellationToken cancellationToken = default)
        {
            identity ??= new AnonymousIdentity();
            _logger.LogInformation("Invoking action as {Identity}", identity.Name);
            using IServiceScope serviceScope = BeginScope(identity);
            using IDisposable durationLogger = UseDurationLogger(serviceScope);
            var operation = serviceScope.ServiceProvider.GetRequiredService<IOperation>();
            try
            {
                await operation.BeginAsync(serviceScope, cancellationToken).ConfigureAwait(false);
                await awaitableAsyncAction.Invoke(serviceScope.ServiceProvider, cancellationToken)
                    .ConfigureAwait(false);
                await operation.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    await operation.CancelAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cancel the operation");
                }

                throw;
            }
        }

        public async Task Execute(ICommand command)
        {
            await InvokeAsync(
                async (sp, ct) =>
                {
                    if (command is IAuthorizedCommand authorizedCommand &&
                        !await authorizedCommand.AsyncAuthorization(sp, ct).ConfigureAwait(false))
                    {
                        throw new ForbiddenException();
                    }

                    await command.AsyncInvocation.Invoke(sp, ct).ConfigureAwait(false);
                },
                command.Identity,
                command.CancellationToken);
        }

        public async Task Execute(IInvokerCommand command)
        {
            await InvokeAsync(async (sp, ct) =>
            {
                if (command is IAuthorizedCommand authorizedCommand &&
                    !await authorizedCommand.AsyncAuthorization(sp, ct).ConfigureAwait(false))
                {
                    throw new ForbiddenException();
                }
            });

            await command.AsyncInvocation.Invoke(this, command.CancellationToken).ConfigureAwait(false);
        }

        private IServiceScope BeginScope(IIdentity identity)
        {
            IServiceScope serviceScope = _application.CompositionRoot.BeginScope();

            identity ??= new AnonymousIdentity();
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