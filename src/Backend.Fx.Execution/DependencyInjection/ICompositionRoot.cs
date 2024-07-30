using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Fx.Execution.DependencyInjection;

/// <summary>
/// Encapsulates the injection framework of choice. The implementation follows the Register/Resolve/Release pattern.
/// Usage of this interface is only allowed for framework integration (or tests). NEVER (!) access the injector from
/// the domain or application logic, this would result in the Service Locator anti pattern, described here:
/// http://blog.ploeh.dk/2010/02/03/ServiceLocatorisanAnti-Pattern/
/// </summary>
[PublicAPI]
public interface ICompositionRoot : IDisposable
{
    void Verify();

    void RegisterModules(params IModule[] modules);

    void Register(ServiceDescriptor serviceDescriptor);

    void RegisterDecorator(ServiceDescriptor serviceDescriptor);

    void RegisterCollection(IEnumerable<ServiceDescriptor> serviceDescriptors);

    IServiceScope BeginScope();

    /// <summary>
    /// Access to the container's resolution functionality
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}