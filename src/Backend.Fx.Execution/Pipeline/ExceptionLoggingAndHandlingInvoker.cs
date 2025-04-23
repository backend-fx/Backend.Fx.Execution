using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Logging;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline;

[PublicAPI]
public class ExceptionLoggingAndHandlingInvoker : IBackendFxApplicationInvoker
{
    private readonly IExceptionLogger _exceptionLogger;
    private readonly IBackendFxApplicationInvoker _invoker;

    public ExceptionLoggingAndHandlingInvoker(IExceptionLogger exceptionLogger, IBackendFxApplicationInvoker invoker)
    {
        _exceptionLogger = exceptionLogger;
        _invoker = invoker;
    }

    public async Task InvokeAsync(
        Func<IServiceProvider, CancellationToken, Task> awaitableAsyncAction,
        IIdentity? identity = null,
        CancellationToken cancellation = default)
    {
        try
        {
            await _invoker
                  .InvokeAsync(awaitableAsyncAction, identity, cancellation)
                  .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _exceptionLogger.LogException(ex);
        }
    }
}