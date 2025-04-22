using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline.Commands;

[PublicAPI]
public interface IBackendFxApplicationCommandExecutor
{
    /// <summary>
    /// Execute a command through the full execution pipeline, having its separate injection scope. If the command
    /// should return a result, it should be made available as a property on the command itself
    /// </summary>
    Task Execute(ICommand command,
        IIdentity? identity = null, 
        CancellationToken cancellation = default);
    
    /// <summary>
    /// Execute an invoker command through the full execution pipeline, giving the command full control over the
    /// injection scope handling. If the command should return a result, it should be made available as a property
    /// on the command itself
    /// </summary>
    Task Execute(IInvokerCommand command,
        IIdentity? identity = null, 
        CancellationToken cancellation = default);
}