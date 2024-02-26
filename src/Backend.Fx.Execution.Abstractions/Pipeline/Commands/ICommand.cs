using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline.Commands;

[PublicAPI]
public interface ICommand
{
    Func<IServiceProvider, CancellationToken, Task> AsyncInvocation { get; }
}