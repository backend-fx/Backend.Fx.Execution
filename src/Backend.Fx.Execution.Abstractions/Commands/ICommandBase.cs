using System.Security.Principal;
using System.Threading;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Commands;

[PublicAPI]
public interface ICommandBase
{
    public IIdentity Identity { get; }

    public CancellationToken CancellationToken { get; }
}