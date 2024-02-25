using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Commands;

[PublicAPI]
public interface IAuthorizedCommand
{
    Func<IServiceProvider, CancellationToken, Task<bool>> AsyncAuthorization { get; }
}