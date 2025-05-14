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
    private readonly CancellationTokenSource _shutdownRequestedTokenSource = new();
    private readonly BackendFxApplicationStateMachine _stateMachine = new();
    private readonly ILogger _logger = Log.Create<BackendFxApplication>();
    private readonly List<IFeature> _features = [];
    private readonly Lazy<Task> _bootAction;

    /// <summary>
    /// Initializes the application's runtime instance
    /// </summary>
    /// <param name="compositionRoot">The composition root of the dependency injection framework</param>
    /// <param name="exceptionLogger"></param>
    /// <param name="assemblies"></param>
    public BackendFxApplication(
        ICompositionRoot compositionRoot,
        IExceptionLogger exceptionLogger,
        params Assembly[]? assemblies)
    {
        assemblies ??= [];

        _logger.LogInformation(
            "Initializing application with {CompositionRoot} providing services from [{Assemblies}]",
            compositionRoot.GetType().GetDetailedTypeName(),
            string.Join(", ", assemblies.Select(ass => ass.GetName().Name)));

        var invoker = new BackendFxApplicationInvoker(this);
        Invoker = new ExceptionLoggingInvoker(exceptionLogger, invoker);

        CompositionRoot = new LogRegistrationsDecorator(compositionRoot);
        ExceptionLogger = exceptionLogger;
        Assemblies = assemblies;
        CompositionRoot.RegisterModules(new ExecutionPipelineModule(withFrozenClockDuringExecution: true));

        _bootAction = new Lazy<Task>(async () =>
        {
            _logger.LogInformation("Booting application");

            try
            {
                CompositionRoot.Verify();

                _stateMachine.EnterSingeUserMode();

                // ReSharper disable once SuspiciousTypeConversion.Global - implemented in feature extensions
                foreach (var bootableFeature in _features.OfType<IBootableFeature>())
                {
                    await bootableFeature.BootAsync(this).ConfigureAwait(false);
                }

                _stateMachine.EnterMultiUserMode();
            }
            catch (Exception ex)
            {
                _stateMachine.EnterCrashed();
                _logger.LogCritical(ex, "Boot failed!");
                throw;
            }
        });
    }

    public Assembly[] Assemblies { get; }

    public IBackendFxApplicationInvoker Invoker { get; }

    public CancellationToken ShutdownRequested => _shutdownRequestedTokenSource.Token;

    public ICompositionRoot CompositionRoot { get; }

    public IExceptionLogger ExceptionLogger { get; }

    public BackendFxApplicationState State => _stateMachine.State;

    public virtual void EnableFeature(IFeature feature)
    {
        if (_bootAction.IsValueCreated)
        {
            throw new InvalidOperationException("Features must be enabled before booting the application");
        }

        feature.Enable(this);
        _features.Add(feature);
    }

    public TFeature? GetFeature<TFeature>() where TFeature : IFeature
    {
        return _features.OfType<TFeature>().SingleOrDefault();
    }
    
    public IDisposable UseSingleUserMode()
    {
        if (State == BackendFxApplicationState.SingleUserMode)
        {
            return new DelegateDisposable(() => { });
        }

        _stateMachine.EnterSingeUserMode();
        return new DelegateDisposable(() => _stateMachine.EnterMultiUserMode());
    }

    public async Task BootAsync(CancellationToken cancellation = default)
    {
        await _bootAction.Value.ConfigureAwait(false);
    }

    public async Task WaitForBootAsync(CancellationToken cancellation = default)
    {
        await Task.Run(async () =>
        {
            do
            {
                if (cancellation.IsCancellationRequested ||
                    _bootAction.IsValueCreated && _bootAction.Value.Status is TaskStatus.Canceled
                        or TaskStatus.Faulted or TaskStatus.RanToCompletion)
                {
                    return;
                }

                await Task.Delay(50, cancellation).ConfigureAwait(false);
            } while (true);
        }, cancellation).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _logger.LogInformation("Application shut down initialized");
        _stateMachine.EnterSingeUserMode();
        
        _shutdownRequestedTokenSource.Cancel();
        
        // ReSharper disable once SuspiciousTypeConversion.Global
        foreach (var disposableFeature in _features.OfType<IDisposable>())
        {
            disposableFeature.Dispose();
        }

        CompositionRoot.Dispose();
    }
}