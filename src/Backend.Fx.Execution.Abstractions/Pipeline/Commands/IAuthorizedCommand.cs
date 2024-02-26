using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline.Commands;

[PublicAPI]
public interface IAuthorizedCommand
{
    Func<IServiceProvider, CancellationToken, Task> AuthorizeAsync { get; }
}