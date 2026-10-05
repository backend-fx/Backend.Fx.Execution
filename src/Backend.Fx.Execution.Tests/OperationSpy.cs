using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Fx.Execution.Tests;

public interface IOperationSpy : IOperation { }

public class OperationSpy : IOperation
{
    private readonly IOperationSpy _operationSpy;
    private readonly IOperation _operation;
    private int _counter;

    public int Counter => _counter;

    public OperationSpy(IOperationSpy operationSpy, IOperation operation)
    {
        _operationSpy = operationSpy;
        _operation = operation;
    }

    public async Task BeginAsync(
        IServiceScope serviceScope,
        CancellationToken cancellation = default
    )
    {
        _counter++;
        await _operationSpy.BeginAsync(serviceScope, cancellation);
        await _operation.BeginAsync(serviceScope, cancellation);
    }

    public async Task CompleteAsync(CancellationToken cancellation = default)
    {
        await _operationSpy.CompleteAsync(cancellation);
        await _operation.CompleteAsync(cancellation);
    }

    public async Task CancelAsync(CancellationToken cancellation = default)
    {
        await _operationSpy.CancelAsync(cancellation);
        await _operation.CancelAsync(cancellation);
    }
}
