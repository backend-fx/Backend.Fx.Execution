using JetBrains.Annotations;

namespace Backend.Fx.Execution.Features;

/// <summary>
/// Base class for optional features that can be added to the Backend.Fx execution pipeline
/// </summary>
[PublicAPI]
public interface IFeature
{
    public void Enable(IBackendFxApplication application);
}