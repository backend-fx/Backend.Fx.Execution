using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Fx.Logging;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution.DependencyInjection;

[PublicAPI]
public abstract class CompositionRoot : ICompositionRoot
{
    private readonly ILogger _logger = Log.Create<CompositionRoot>();

    public abstract IServiceProvider ServiceProvider { get; }

    public abstract void Verify();

    public virtual void RegisterModules(params IModule[] modules)
    {
        foreach (var module in modules)
        {
            _logger.LogInformation("Registering {@Module}", module);
            module.Register(this);
        }
    }

    public abstract void Register(ServiceDescriptor serviceDescriptor);

    public abstract void RegisterDecorator(ServiceDescriptor serviceDescriptor);

    public abstract void RegisterCollection(IEnumerable<ServiceDescriptor> serviceDescriptors);

    public abstract IServiceScope BeginScope();

    protected abstract void Dispose(bool disposing);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override to asynchronously release the resources of the underlying injection framework (e.g. when its
    /// <see cref="IServiceProvider"/> implements <see cref="IAsyncDisposable"/>). The default implementation
    /// falls back to the synchronous <see cref="Dispose(bool)"/>.
    /// </summary>
    protected virtual ValueTask DisposeAsyncCore()
    {
        Dispose(true);
        return default;
    }
}
