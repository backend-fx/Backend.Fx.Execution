using System.Security.Principal;
using System.Threading;
using Backend.Fx.Execution.Pipeline;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Commands;

[PublicAPI]
public abstract class Command : ICommandBase
{
    protected Command()
    {
        Identity = new AnonymousIdentity();
        CancellationToken = default;
    }

    protected Command(IIdentity identity)
    {
        Identity = identity;
        CancellationToken = default;
    }

    protected Command(CancellationToken cancellationToken)
    {
        Identity = new AnonymousIdentity();
        CancellationToken = cancellationToken;
    }

    protected Command(IIdentity identity, CancellationToken cancellationToken)
    {
        Identity = identity;
        CancellationToken = cancellationToken;
    }
    
    public IIdentity Identity { get; }
    
    public CancellationToken CancellationToken { get; }
}