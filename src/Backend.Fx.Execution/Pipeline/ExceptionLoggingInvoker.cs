using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Logging;

namespace Backend.Fx.Execution.Pipeline
{
    internal class ExceptionLoggingInvoker : IBackendFxApplicationInvoker
    {
        private readonly IExceptionLogger _exceptionLogger;
        private readonly IBackendFxApplicationInvoker _invoker;

        public ExceptionLoggingInvoker(IExceptionLogger exceptionLogger, IBackendFxApplicationInvoker invoker)
        {
            _exceptionLogger = exceptionLogger;
            _invoker = invoker;
        }

        public async Task InvokeAsync(
            Func<IServiceProvider, CancellationToken, Task> awaitableAsyncAction,
            IIdentity identity = null,
            CancellationToken cancellationToken = default,
            bool allowInvocationDuringBoot = false)
        {
            try
            {
                await _invoker.InvokeAsync(
                    awaitableAsyncAction,
                    identity ?? new AnonymousIdentity(),
                    cancellationToken, allowInvocationDuringBoot).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _exceptionLogger.LogException(ex);
                throw;
            }
        }
    }
}