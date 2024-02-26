using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline.Commands;

[PublicAPI]
public interface IInvokerCommand
{
    Func<IBackendFxApplicationInvoker, IIdentity, CancellationToken, Task> AsyncInvocation { get; }

    public IIdentity Identity { get; }

    public CancellationToken CancellationToken { get; }
}