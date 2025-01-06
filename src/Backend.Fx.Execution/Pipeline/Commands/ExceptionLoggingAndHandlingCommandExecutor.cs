using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Logging;

namespace Backend.Fx.Execution.Pipeline.Commands;

public class ExceptionLoggingAndHandlingCommandExecutor : IBackendFxApplicationCommandExecutor
{
    private readonly IExceptionLogger _exceptionLogger;
    private readonly IBackendFxApplicationCommandExecutor _executor;

    public ExceptionLoggingAndHandlingCommandExecutor(IExceptionLogger exceptionLogger, IBackendFxApplicationCommandExecutor executor)
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
            await _executor.Execute(command, identity, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _exceptionLogger.LogException(ex);
        }
    }

    public async Task Execute(
        IInvokerCommand command,
        IIdentity? identity = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _executor.Execute(command, identity, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _exceptionLogger.LogException(ex);
        }
    }
}