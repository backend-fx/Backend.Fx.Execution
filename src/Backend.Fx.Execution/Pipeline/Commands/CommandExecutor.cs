using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Fx.Execution.Pipeline.Commands;

public class CommandExecutor : IBackendFxApplicationCommandExecutor
{
    private readonly IBackendFxApplicationInvoker _invoker;

    public CommandExecutor(IBackendFxApplicationInvoker invoker)
    {
        _invoker = invoker;
    }


    public async Task Execute(
        ICommand command,
        IIdentity? identity = null,
        CancellationToken cancellationToken = default)
    {
        await _invoker.InvokeAsync(
            async (sp, ct) =>
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (command is IInitializableCommand initializableCommand)
                {
                    await initializableCommand.InitializableAsync(sp, ct).ConfigureAwait(false);
                }

                // ReSharper disable once SuspiciousTypeConversion.Global
                if (command is IAuthorizedCommand authorizedCommand)
                {
                    await authorizedCommand.AuthorizeAsync(sp, ct).ConfigureAwait(false);
                }

                await command.AsyncInvocation.Invoke(sp, ct).ConfigureAwait(false);
            },
            identity ?? new AnonymousIdentity(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task Execute(
        IInvokerCommand command,
        IIdentity? identity = null,
        CancellationToken cancellationToken = default)
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (command is IAuthorizedCommand authorizedCommand)
        {
            await _invoker.InvokeAsync(async (sp, ct) =>
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (command is IInitializableCommand initializableCommand)
                {
                    await initializableCommand.InitializableAsync(sp, ct).ConfigureAwait(false);
                }

                await authorizedCommand.AuthorizeAsync(sp, ct).ConfigureAwait(false);
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await command.AsyncInvocation.Invoke(
            // ReSharper disable once SuspiciousTypeConversion.Global
            new InitializingInvoker(_invoker, command as IInitializableCommand),
            command.Identity,
            command.CancellationToken).ConfigureAwait(false);
    }

    private class InitializingInvoker : IBackendFxApplicationInvoker
    {
        private readonly IBackendFxApplicationInvoker _invoker;
        private readonly IInitializableCommand? _initializableCommand;

        public InitializingInvoker(IBackendFxApplicationInvoker invoker, IInitializableCommand? initializableCommand)
        {
            _invoker = invoker;
            _initializableCommand = initializableCommand;
        }


        public Task InvokeAsync(Func<IServiceProvider, CancellationToken, Task> awaitableAsyncAction,
            IIdentity? identity = null, CancellationToken cancellationToken = default)
        {
            return _invoker.InvokeAsync(
                (provider, token) =>
                {
                    _initializableCommand?.InitializableAsync(provider, token);
                    return awaitableAsyncAction(provider, token);
                },
                identity,
                cancellationToken);
        }
    }
}