using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Exceptions;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline.Commands;

[PublicAPI]
public interface IAuthorizedCommand
{
    /// <summary>
    /// This function may throw a <see cref="ForbiddenException">ForbiddenException</see> if the identity is not authorized
    /// to execute the command
    /// </summary>
    Func<IServiceProvider, CancellationToken, Task> AuthorizeAsync { get; }
}