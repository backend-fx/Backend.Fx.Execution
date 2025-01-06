using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Logging;

namespace Backend.Fx.Execution.Pipeline.Commands;

internal class ExceptionLoggingCommandExecutor : IBackendFxApplicationCommandExecutor
{
    private readonly IExceptionLogger _exceptionLogger;
    private readonly IBackendFxApplicationCommandExecutor _executor;

    public ExceptionLoggingCommandExecutor(IExceptionLogger exceptionLogger, IBackendFxApplicationCommandExecutor executor)
    {
        _exceptionLogger = exceptionLogger;
        _executor = executor;
    }
        
    public async Task Execute(
        ICommand command,
        IIdentity? identity = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _executor.Execute(
                command,
                identity ?? new AnonymousIdentity(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _exceptionLogger.LogException(ex);
            throw;
        }
    }

    public async Task Execute(
        IInvokerCommand command,
        IIdentity? identity = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _executor.Execute(
                command,
                identity ?? new AnonymousIdentity(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _exceptionLogger.LogException(ex);
            throw;
        }
    }
}