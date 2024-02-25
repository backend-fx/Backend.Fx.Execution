using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.DependencyInjection;
using Backend.Fx.Execution.Features;
using Backend.Fx.Execution.Pipeline;
using Backend.Fx.Execution.Pipeline.Commands;
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
    
    /// <summary>
    /// The command executor runs a given command asynchronously in an application scope with injection facilities 
    /// </summary>
    IBackendFxApplicationCommandExecutor CommandExecutor { get; }

    /// <summary>
    /// The composition root of the dependency injection framework
    /// </summary>
    ICompositionRoot CompositionRoot { get; }

    /// <summary>
    /// The global exception logger of this application
    /// </summary>
    IExceptionLogger ExceptionLogger { get; }

    Assembly[] Assemblies { get; }

    /// <summary>
    /// allows synchronously awaiting application startup
    /// </summary>
    Task WaitForBootAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes and starts the application (async)
    /// </summary>
    /// <returns></returns>
    Task BootAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables an optional feature. Must be done before calling <see cref="BootAsync"/>.
    /// </summary>
    /// <param name="feature"></param>
    void EnableFeature(Feature feature);

    void RequireDependantFeature<TFeature>() where TFeature : Feature;
}