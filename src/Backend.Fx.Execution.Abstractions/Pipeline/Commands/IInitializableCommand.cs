using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline.Commands;

[PublicAPI]
public interface IInitializableCommand
{
    Func<IServiceProvider, CancellationToken, Task> InitializeAsync { get; }
}
