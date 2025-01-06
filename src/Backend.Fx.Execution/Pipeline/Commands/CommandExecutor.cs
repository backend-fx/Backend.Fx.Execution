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
        if (command is IAuthorizedCommand authorizedCommand)
        {
            await _invoker.InvokeAsync(async (sp, ct) =>
            {
                await authorizedCommand.AuthorizeAsync(sp, ct).ConfigureAwait(false);
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await command.AsyncInvocation.Invoke(
            _invoker,
            command.Identity ?? new AnonymousIdentity(),
            command.CancellationToken).ConfigureAwait(false);
    }
}