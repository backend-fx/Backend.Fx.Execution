using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Fx.Execution.Pipeline;

/// <summary>
/// The basic interface of an operation invoked by the <see cref="IBackendFxApplicationInvoker"/>.
/// Decorate this interface to provide operation specific infrastructure services (like a database connection, a
/// database transaction an entry-exit logging etc.)
/// </summary>
[PublicAPI]
public interface IOperation
{
    int Counter { get; }
    
    Task BeginAsync(IServiceScope serviceScope, CancellationToken cancellation = default);
        
    Task CompleteAsync(CancellationToken cancellation = default);

    Task CancelAsync(CancellationToken cancellation = default);
}