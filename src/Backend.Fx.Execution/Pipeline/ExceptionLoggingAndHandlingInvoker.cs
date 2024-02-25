using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Logging;

namespace Backend.Fx.Execution.Pipeline
{
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
            IIdentity identity, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _invoker.InvokeAsync(awaitableAsyncAction, identity, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _exceptionLogger.LogException(ex);
            }
        }

        public async Task Execute(ICommand command)
        {
            try
            {
                await _invoker.Execute(command).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _exceptionLogger.LogException(ex);
            }
        }
    }
}