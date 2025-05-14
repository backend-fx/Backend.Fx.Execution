using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Logging;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution.Pipeline;

[UsedImplicitly]
internal sealed class Operation : IOperation
{
    private readonly ILogger _logger = Log.Create<Operation>();
    public int Counter { get; }
    private bool? _isActive;
    private IDisposable? _lifetimeLogger;

    public Operation(Counter counter)
    {
        Counter = counter.Count();
    }

    public Task BeginAsync(IServiceScope serviceScope, CancellationToken cancellation = default)
    {
        if (_isActive != null)
        {
            throw new InvalidOperationException(
                $"Cannot begin an operation that is {(_isActive.Value ? "active" : "terminated")}");
        }

        _lifetimeLogger = _logger.LogDebugDuration($"Beginning operation #{Counter}",
            $"Terminating operation #{Counter}");
        _isActive = true;
        return Task.CompletedTask;
    }

    public Task CompleteAsync(CancellationToken cancellation = default)
    {
        _logger.LogInformation("Completing operation #{OperationId}", Counter);
        if (_isActive != true)
        {
            throw new InvalidOperationException(
                $"Cannot complete an operation that is {(_isActive == false ? "terminated" : "not active")}");
        }

        _isActive = false;
        _lifetimeLogger?.Dispose();
        _lifetimeLogger = null;
        return Task.CompletedTask;
    }

    public Task CancelAsync(CancellationToken cancellation = default)
    {
        _logger.LogInformation("Canceling operation #{OperationId}", Counter);
        _isActive = false;
        _lifetimeLogger?.Dispose();
        _lifetimeLogger = null;
        return Task.CompletedTask;
    }
}