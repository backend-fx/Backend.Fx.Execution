using System.Threading;
using System.Threading.Tasks;

namespace Backend.Fx.Execution.Features;

/// <summary>
/// Marks a <see cref="IFeature"/> to require stuff done during startup of the application
/// </summary>
public interface IBootableFeature
{
    public Task BootAsync(IBackendFxApplication application, CancellationToken cancellation = default);
}