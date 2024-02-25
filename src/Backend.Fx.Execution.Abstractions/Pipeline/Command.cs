using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline;

[PublicAPI]
public interface ICommand
{
    IIdentity Identity { get; }

    CancellationToken CancellationToken { get; }

    Func<IServiceProvider, CancellationToken, Task> AsyncInvocation { get; }
}

[PublicAPI]
public interface IAuthorizedCommand
{
    Func<IServiceProvider, CancellationToken, bool> AsyncAuthorization { get; }
}

[PublicAPI]
public abstract class Command : ICommand
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
    
    public abstract Func<IServiceProvider, CancellationToken, Task> AsyncInvocation { get; }
}