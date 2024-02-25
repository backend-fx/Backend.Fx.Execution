using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.Pipeline;
using JetBrains.Annotations;

namespace Backend.Fx.Execution;

[PublicAPI]
public interface IBackendFxApplicationInvoker
{
    /// <summary>
    /// Run a delegate through the full execution pipeline, having its separate injection scope 
    /// </summary>
    /// <param name="awaitableAsyncAction">The async action to be invoked by the application</param>
    /// <param name="identity">The acting identity</param>
    /// <param name="cancellationToken">Pass an existing cancellation token (e.g. HttpContext.RequestAborted) to
    /// enable cancellation of the async invocation.</param>
    /// <returns>The <see cref="Task"/> representing the async invocation.</returns>
    Task InvokeAsync(
        Func<IServiceProvider, CancellationToken, Task> awaitableAsyncAction, 
        IIdentity identity = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a command through the full execution pipeline, having its separate injection scope. If the command
    /// should return a result, it should be made available as a property on the command itself
    /// </summary>
    Task Execute(ICommand command);
}