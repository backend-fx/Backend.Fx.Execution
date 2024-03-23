using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.DependencyInjection;
using Backend.Fx.Execution.Features;
using Backend.Fx.Execution.Pipeline;
using Backend.Fx.Execution.Pipeline.Commands;
using Backend.Fx.Logging;
using Backend.Fx.Util;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution
{
    [PublicAPI]
    public class BackendFxApplication : IBackendFxApplication
    {
        private readonly ILogger _logger = Log.Create<BackendFxApplication>();
        private readonly List<Feature> _features = new();
        private readonly Lazy<Task> _bootAction;

        /// <summary>
        /// Initializes the application's runtime instance
        /// </summary>
        /// <param name="compositionRoot">The composition root of the dependency injection framework</param>
        /// <param name="exceptionLogger"></param>
        /// <param name="assemblies"></param>
        public BackendFxApplication(ICompositionRoot compositionRoot, IExceptionLogger exceptionLogger,
            params Assembly[] assemblies)
        {
            assemblies ??= Array.Empty<Assembly>();

            _logger.LogInformation(
                "Initializing application with {CompositionRoot} providing services from [{Assemblies}]",
                compositionRoot.GetType().GetDetailedTypeName(),
                string.Join(", ", assemblies.Select(ass => ass.GetName().Name)));

            var invoker = new BackendFxApplicationInvoker(this);
            Invoker = new ExceptionLoggingInvoker(exceptionLogger, invoker);

            var commandExecutor = new CommandExecutor(invoker);
            CommandExecutor = new ExceptionLoggingCommandExecutor(exceptionLogger, commandExecutor);
            
            CompositionRoot = new LogRegistrationsDecorator(compositionRoot);
            ExceptionLogger = exceptionLogger;
            Assemblies = assemblies;
            CompositionRoot.RegisterModules(new ExecutionPipelineModule(withFrozenClockDuringExecution: true));

            _bootAction = new Lazy<Task>(async () =>
            {
                State = BackendFxApplicationState.Booting;
                _logger.LogInformation("Booting application");

                try
                {
                    CompositionRoot.Verify();

                    foreach (Feature feature in _features)
                    {
                        // ReSharper disable once SuspiciousTypeConversion.Global - implemented in feature extensions
                        if (feature is IBootableFeature bootableFeature)
                        {
                            await bootableFeature.BootAsync(this).ConfigureAwait(false);
                        }
                    }
                    
                    State = BackendFxApplicationState.Booted;
                }
                catch (Exception ex)
                {
                    State = BackendFxApplicationState.BootFailed;
                    _logger.LogCritical(ex, "Boot failed!");
                    throw;
                }
            });
        }

        public Assembly[] Assemblies { get; }

        public IBackendFxApplicationInvoker Invoker { get; }
        
        public IBackendFxApplicationCommandExecutor CommandExecutor { get; }

        public ICompositionRoot CompositionRoot { get; }

        public IExceptionLogger ExceptionLogger { get; }

        public BackendFxApplicationState State { get; set; } = BackendFxApplicationState.Initializing;

        public virtual void EnableFeature(Feature feature)
        {
            if (_bootAction.IsValueCreated)
            {
                throw new InvalidOperationException("Features must be enabled before booting the application");
            }

            feature.Enable(this);
            _features.Add(feature);
        }

        public void RequireDependantFeature<TFeature>() where TFeature : Feature
        {
            if (!_features.OfType<TFeature>().Any())
            {
                throw new InvalidOperationException(
                    $"This feature requires the {typeof(TFeature).Name} to be enabled first");
            }
        }

        public async Task BootAsync(CancellationToken cancellationToken = default)
        {
            await _bootAction.Value.ConfigureAwait(false);
        }

        public async Task WaitForBootAsync(CancellationToken cancellationToken = default)
        {
            await Task.Run(async () =>
            {
                do
                {
                    if (cancellationToken.IsCancellationRequested ||
                        _bootAction.IsValueCreated && _bootAction.Value.Status is TaskStatus.Canceled
                            or TaskStatus.Faulted or TaskStatus.RanToCompletion)
                    {
                        return;
                    }

                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                } while (true);
            }, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _logger.LogInformation("Application shut down initialized");
            foreach (Feature feature in _features)
            {
                feature.Dispose();
            }

            CompositionRoot?.Dispose();
        }
    }
}