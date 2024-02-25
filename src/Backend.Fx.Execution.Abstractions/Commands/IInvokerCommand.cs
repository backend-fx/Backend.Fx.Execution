using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Commands;

[PublicAPI]
public interface IInvokerCommand : ICommandBase
{
    Func<IBackendFxApplicationInvoker, CancellationToken, Task> AsyncInvocation { get; }
}