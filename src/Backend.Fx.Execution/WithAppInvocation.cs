using JetBrains.Annotations;

namespace Backend.Fx.Execution;

/// <summary>
/// <see cref="WithInvocation{TService}"/> bound to the <see cref="IBackendFxApplication.Invoker"/> of an application.
/// </summary>
[PublicAPI]
public class WithAppInvocation<TService> : WithInvocation<TService>
    where TService : class
{
    public WithAppInvocation(IBackendFxApplication application)
        : base(application.Invoker) { }
}
