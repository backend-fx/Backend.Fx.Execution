using System;
using System.Collections.Generic;
using Backend.Fx.Logging;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution.DependencyInjection
{
    [PublicAPI]
    public abstract class CompositionRoot : ICompositionRoot
    {
        private readonly ILogger _logger = Log.Create<CompositionRoot>();

        public abstract IServiceProvider ServiceProvider { get; }

        public abstract void Verify();

        public virtual void RegisterModules(params IModule[] modules)
        {
            foreach (IModule module in modules)
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
    }
}