using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.DependencyInjection;
using Backend.Fx.Execution.Features;
using Backend.Fx.Execution.Pipeline;
using Backend.Fx.Logging;
using Backend.Fx.Util;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution;

[PublicAPI]
public class BackendFxApplication : IBackendFxApplication
{
    private readonly ILogger _logger = Log.Create<BackendFxApplication>();
    private readonly ApplicationLifecycle _lifecycle;

    /// <summary>
    /// Initializes the application's runtime instance. Assemblies to provide services from are brought by
    /// features, see <see cref="IFeature.Assemblies"/>.
    /// </summary>
    /// <param name="compositionRoot">The composition root of the dependency injection framework</param>
    /// <param name="exceptionLogger">The global exception logger of this application</param>
    public BackendFxApplication(ICompositionRoot compositionRoot, IExceptionLogger exceptionLogger)
    {
        _logger.LogInformation(
            "Initializing application with {CompositionRoot}",
            compositionRoot.GetType().GetDetailedTypeName()
        );

        _lifecycle = new ApplicationLifecycle(this);

        var invoker = new BackendFxApplicationInvoker(this, _lifecycle);
        Invoker = new ExceptionLoggingInvoker(exceptionLogger, invoker);

        CompositionRoot = new LogRegistrationsDecorator(compositionRoot);
        ExceptionLogger = exceptionLogger;
        CompositionRoot.RegisterModules(
            new ExecutionPipelineModule(withFrozenClockDuringExecution: true)
        );
    }

    /// <summary>
    /// The assemblies brought by all added features
    /// </summary>
    public IEnumerable<Assembly> Assemblies =>
        _lifecycle.Features.SelectMany(feature => feature.Assemblies).Distinct();

    public IBackendFxApplicationInvoker Invoker { get; }

    public CancellationToken ShutdownRequested => _lifecycle.ShutdownRequested;

    public ICompositionRoot CompositionRoot { get; }

    public IExceptionLogger ExceptionLogger { get; }

    public BackendFxApplicationState State => _lifecycle.State;

    public virtual void EnableFeature(IFeature feature) => AddFeature(feature);

    public virtual void AddFeature(IFeature feature) => _lifecycle.AddFeature(feature);

    public TFeature? GetFeature<TFeature>()
        where TFeature : IFeature
    {
        return _lifecycle.Features.OfType<TFeature>().SingleOrDefault();
    }

    public IDisposable UseSingleUserMode() => _lifecycle.UseSingleUserMode();

    public Task BootAsync(CancellationToken cancellation = default) =>
        _lifecycle.BootAsync(cancellation);

    public Task WaitForBootAsync(CancellationToken cancellation = default) =>
        _lifecycle.WaitForBootAsync(cancellation);

    public void Dispose() => _lifecycle.Shutdown();

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.ShutdownAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
