using JetBrains.Annotations;

namespace Backend.Fx.Execution.DependencyInjection;

/// <summary>
/// A logically cohesive bunch of services
/// </summary>
[PublicAPI]
public interface IModule
{
    void Register(ICompositionRoot compositionRoot);
}