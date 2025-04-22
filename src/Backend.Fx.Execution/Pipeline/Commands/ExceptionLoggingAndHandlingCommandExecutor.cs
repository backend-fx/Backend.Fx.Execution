using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Logging;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline.Commands;

[PublicAPI]
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
        CancellationToken cancellation = default)
    {
        try
        {
            await _executor.Execute(command, identity, cancellation).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _exceptionLogger.LogException(ex);
        }
    }

    public async Task Execute(
        IInvokerCommand command,
        IIdentity? identity = null, 
        CancellationToken cancellation = default)
    {
        try
        {
            await _executor.Execute(command, identity, cancellation).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _exceptionLogger.LogException(ex);
        }
    }
}