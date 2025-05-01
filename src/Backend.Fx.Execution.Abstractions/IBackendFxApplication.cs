using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.DependencyInjection;
using Backend.Fx.Execution.Features;
using Backend.Fx.Execution.Pipeline;
using Backend.Fx.Logging;
using JetBrains.Annotations;

namespace Backend.Fx.Execution;

/// <summary>
/// The root object of the whole backend fx application framework
/// </summary>
[PublicAPI]
public interface IBackendFxApplication : IDisposable
{
    /// <summary>
    /// The invoker runs a given action asynchronously in an application scope with injection facilities
    /// </summary>
    IBackendFxApplicationInvoker Invoker { get; }
    
    CancellationToken ShutdownRequested { get; }

    /// <summary>
    /// The composition root of the dependency injection framework
    /// </summary>
    ICompositionRoot CompositionRoot { get; }

    /// <summary>
    /// The global exception logger of this application
    /// </summary>
    IExceptionLogger ExceptionLogger { get; }

    Assembly[] Assemblies { get; }

    BackendFxApplicationState State { get; }

    /// <summary>
    /// allows synchronously awaiting application startup
    /// </summary>
    Task WaitForBootAsync(CancellationToken cancellation = default);

    /// <summary>
    /// Initializes and starts the application (async)
    /// </summary>
    /// <returns></returns>
    Task BootAsync(CancellationToken cancellation = default);

    /// <summary>
    /// Enables an optional feature. Must be done before calling <see cref="BootAsync"/>.
    /// </summary>
    /// <param name="feature"></param>
    void EnableFeature(IFeature feature);

    TFeature? GetFeature<TFeature>() where TFeature : IFeature;

    IDisposable UseSingleUserMode();
}
