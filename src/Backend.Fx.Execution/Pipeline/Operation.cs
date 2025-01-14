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
    private readonly int _instanceId;
    private bool? _isActive;
    private IDisposable? _lifetimeLogger;

    public Operation(Counter counter)
    {
        _instanceId = counter.Count();
    }

    public Task BeginAsync(IServiceScope serviceScope, CancellationToken cancellationToken = default)
    {
        if (_isActive != null)
        {
            throw new InvalidOperationException(
                $"Cannot begin an operation that is {(_isActive.Value ? "active" : "terminated")}");
        }

        _lifetimeLogger = _logger.LogDebugDuration($"Beginning operation #{_instanceId}",
            $"Terminating operation #{_instanceId}");
        _isActive = true;
        return Task.CompletedTask;
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Completing operation #{OperationId}", _instanceId);
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

    public Task CancelAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Canceling operation #{OperationId}", _instanceId);
        _isActive = false;
        _lifetimeLogger?.Dispose();
        _lifetimeLogger = null;
        return Task.CompletedTask;
    }
}