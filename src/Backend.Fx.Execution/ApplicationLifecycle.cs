using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.Features;
using Backend.Fx.Execution.Pipeline;
using Backend.Fx.Logging;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution;

/// <summary>
/// Owns the lifecycle of a <see cref="BackendFxApplication"/>: its <see cref="BackendFxApplicationState"/>, the
/// Feature phases (enable, boot, dispose), admission of invocations, single user mode and shutdown.
/// See <see cref="BackendFxApplicationState"/> for the legal transitions; everything else fails loudly.
/// </summary>
internal sealed class ApplicationLifecycle
{
    private readonly ILogger _logger = Log.Create<ApplicationLifecycle>();
    private readonly object _sync = new();
    private readonly IBackendFxApplication _application;
    private readonly List<IFeature> _features = [];
    private readonly CancellationTokenSource _shutdownSource = new();
    private readonly CancellationToken _shutdownToken;
    private readonly TaskCompletionSource<bool> _bootEnded = NewSignal();
    private TaskCompletionSource<bool> _stateChanged = NewSignal();
    private BackendFxApplicationState _state = BackendFxApplicationState.Halted;
    private bool _bootStarted;

    public ApplicationLifecycle(IBackendFxApplication application)
    {
        _application = application;
        _shutdownToken = _shutdownSource.Token;
    }

    public BackendFxApplicationState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public CancellationToken ShutdownRequested => _shutdownToken;

    public IReadOnlyList<IFeature> Features
    {
        get
        {
            lock (_sync)
            {
                return _features.ToArray();
            }
        }
    }

    public void AddFeature(IFeature feature)
    {
        lock (_sync)
        {
            if (_state != BackendFxApplicationState.Halted)
            {
                throw new InvalidOperationException(
                    $"Features must be added before booting the application (current state: {_state})"
                );
            }

            _features.Add(feature);
        }
    }

    #region boot

    public Task BootAsync(CancellationToken cancellation)
    {
        IFeature[] features;
        lock (_sync)
        {
            if (_state != BackendFxApplicationState.Halted)
            {
                throw new InvalidOperationException(
                    $"Cannot boot the application in state {_state}"
                );
            }

            _bootStarted = true;
            features = _features.ToArray();
            Transition(BackendFxApplicationState.Booting);
        }

        return RunBootAsync(features, cancellation);
    }

    private async Task RunBootAsync(IFeature[] features, CancellationToken cancellation)
    {
        _logger.LogInformation("Booting application");
        try
        {
            using var bootCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation,
                _shutdownToken
            );
            var bootCancellation = bootCancellationSource.Token;

            EnsureFeaturesBringAssemblies(features);

            foreach (var feature in features)
            {
                bootCancellation.ThrowIfCancellationRequested();
                feature.Enable(_application);
            }

            bootCancellation.ThrowIfCancellationRequested();
            _application.CompositionRoot.Verify();

            // ReSharper disable once SuspiciousTypeConversion.Global - implemented in feature extensions
            foreach (var bootableFeature in features.OfType<IBootableFeature>())
            {
                await bootableFeature
                    .BootAsync(_application, bootCancellation)
                    .ConfigureAwait(false);
            }

            bootCancellation.ThrowIfCancellationRequested();

            lock (_sync)
            {
                // a shutdown that started meanwhile wins
                if (_state == BackendFxApplicationState.Booting)
                {
                    Transition(BackendFxApplicationState.MultiUserMode);
                }
            }
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                if (_state == BackendFxApplicationState.Booting)
                {
                    Transition(BackendFxApplicationState.Crashed);
                }
            }

            _logger.LogCritical(ex, "Boot failed!");
            throw;
        }
        finally
        {
            _bootEnded.TrySetResult(true);
        }
    }

    /// <summary>
    /// Features scan <see cref="IBackendFxApplication.Assemblies"/> while being enabled. Without any assembly, they
    /// would silently register nothing.
    /// </summary>
    private static void EnsureFeaturesBringAssemblies(IFeature[] features)
    {
        if (!features.SelectMany(feature => feature.Assemblies).Any())
        {
            throw new InvalidOperationException(
                "None of the added features brings any assembly, so infrastructure features would register nothing. "
                    + "Add at least one feature representing a subdomain of your application that brings the "
                    + $"assemblies implementing it (see {nameof(IFeature)}.{nameof(IFeature.Assemblies)}). "
                    + "See docs/adr/0001-assemblies-come-only-from-features.md"
            );
        }
    }

    /// <summary>
    /// Completes when the boot ended (successfully or not), or when shutdown begins before booting.
    /// </summary>
    public Task WaitForBootAsync(CancellationToken cancellation) =>
        WaitAsync(_bootEnded.Task, cancellation);

    #endregion

    #region admission

    /// <summary>
    /// Returns when the <paramref name="identity"/> may run an invocation: the <see cref="SystemIdentity"/> in
    /// <see cref="BackendFxApplicationState.Booting"/>, <see cref="BackendFxApplicationState.SingleUserMode"/> and
    /// <see cref="BackendFxApplicationState.MultiUserMode"/>, any other identity only in
    /// <see cref="BackendFxApplicationState.MultiUserMode"/>. Waits otherwise.
    /// </summary>
    /// <exception cref="OperationCanceledException">The application is shutting down, or
    /// <paramref name="cancellation"/> was canceled while waiting.</exception>
    /// <exception cref="InvalidOperationException">The application crashed during boot.</exception>
    public async Task AdmitAsync(IIdentity identity, CancellationToken cancellation)
    {
        var loggedWaiting = false;
        while (true)
        {
            Task stateChanged;
            lock (_sync)
            {
                switch (_state)
                {
                    case BackendFxApplicationState.ShuttingDown:
                    case BackendFxApplicationState.Disposed:
                        throw new OperationCanceledException(
                            "The application is shutting down. Cannot execute invocations.",
                            _shutdownToken
                        );

                    case BackendFxApplicationState.Crashed:
                        throw new InvalidOperationException(
                            "The application failed to start. Cannot execute invocations."
                        );

                    case BackendFxApplicationState.MultiUserMode:
                    case BackendFxApplicationState.Booting
                    or BackendFxApplicationState.SingleUserMode when identity is SystemIdentity:
                        return;
                }

                stateChanged = _stateChanged.Task;
            }

            if (!loggedWaiting)
            {
                _logger.LogInformation("{Identity} waits for multi user mode", identity.Name);
                loggedWaiting = true;
            }

            await WaitAsync(stateChanged, cancellation).ConfigureAwait(false);
        }
    }

    #endregion

    #region single user mode

    public IDisposable UseSingleUserMode()
    {
        lock (_sync)
        {
            if (_state != BackendFxApplicationState.MultiUserMode)
            {
                throw new InvalidOperationException(
                    $"Single user mode can only be entered from multi user mode (current state: {_state})"
                );
            }

            Transition(BackendFxApplicationState.SingleUserMode);
        }

        return new SingleUserModeHandle(this);
    }

    private void LeaveSingleUserMode()
    {
        lock (_sync)
        {
            // releasing the handle after shutdown began must not resurrect the application
            if (_state == BackendFxApplicationState.SingleUserMode)
            {
                Transition(BackendFxApplicationState.MultiUserMode);
            }
        }
    }

    private sealed class SingleUserModeHandle(ApplicationLifecycle lifecycle) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                lifecycle.LeaveSingleUserMode();
            }
        }
    }

    #endregion

    #region shutdown

    public void Shutdown()
    {
        if (!TryBeginShutdown())
        {
            return;
        }

        _bootEnded.Task.GetAwaiter().GetResult();

        foreach (var feature in Features.Reverse())
        {
            Isolated(
                () =>
                {
                    switch (feature)
                    {
                        // ReSharper disable SuspiciousTypeConversion.Global
                        case IDisposable disposable:
                            disposable.Dispose();
                            break;
                        case IAsyncDisposable asyncDisposable:
                            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                            break;
                        // ReSharper restore SuspiciousTypeConversion.Global
                    }
                },
                "Error disposing feature {FeatureType}",
                feature.GetType().Name
            );
        }

        Isolated(
            () => _application.CompositionRoot.Dispose(),
            "Error disposing {CompositionRootType}",
            _application.CompositionRoot.GetType().Name
        );

        EndShutdown();
    }

    public async ValueTask ShutdownAsync()
    {
        if (!TryBeginShutdown())
        {
            return;
        }

        await _bootEnded.Task.ConfigureAwait(false);

        foreach (var feature in Features.Reverse())
        {
            await IsolatedAsync(
                    async () =>
                    {
                        switch (feature)
                        {
                            // ReSharper disable SuspiciousTypeConversion.Global
                            case IAsyncDisposable asyncDisposable:
                                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                                break;
                            case IDisposable disposable:
                                disposable.Dispose();
                                break;
                            // ReSharper restore SuspiciousTypeConversion.Global
                        }
                    },
                    "Error disposing feature {FeatureType}",
                    feature.GetType().Name
                )
                .ConfigureAwait(false);
        }

        await IsolatedAsync(
                () => _application.CompositionRoot.DisposeAsync().AsTask(),
                "Error disposing {CompositionRootType}",
                _application.CompositionRoot.GetType().Name
            )
            .ConfigureAwait(false);

        EndShutdown();
    }

    /// <returns>false, when shutdown already began</returns>
    private bool TryBeginShutdown()
    {
        lock (_sync)
        {
            if (
                _state
                is BackendFxApplicationState.ShuttingDown
                    or BackendFxApplicationState.Disposed
            )
            {
                return false;
            }

            _logger.LogInformation("Application shut down initialized");
            Transition(BackendFxApplicationState.ShuttingDown);

            if (!_bootStarted)
            {
                // nobody will ever boot: release those waiting for it
                _bootEnded.TrySetResult(true);
            }
        }

        Isolated(
            () => _shutdownSource.Cancel(),
            "Error in a callback of {Token}",
            nameof(ShutdownRequested)
        );
        return true;
    }

    private void EndShutdown()
    {
        lock (_sync)
        {
            Transition(BackendFxApplicationState.Disposed);
        }

        _shutdownSource.Dispose();
    }

    private void Isolated(Action action, string errorMessage, string subject)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, errorMessage, subject);
        }
    }

    private async Task IsolatedAsync(Func<Task> action, string errorMessage, string subject)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, errorMessage, subject);
        }
    }

    #endregion

    /// <remarks>Must be called while holding <see cref="_sync"/>.</remarks>
    private void Transition(BackendFxApplicationState newState)
    {
        _logger.LogInformation(
            "Application state switches from {OldState} to {NewState}",
            _state,
            newState
        );
        _state = newState;

        var stateChanged = _stateChanged;
        _stateChanged = NewSignal();
        stateChanged.TrySetResult(true);
    }

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitAsync(Task task, CancellationToken cancellation)
    {
        if (task.IsCompleted || !cancellation.CanBeCanceled)
        {
            await task.ConfigureAwait(false);
            return;
        }

        var canceled = NewSignal();
        using (cancellation.Register(() => canceled.TrySetResult(true)))
        {
            await Task.WhenAny(task, canceled.Task).ConfigureAwait(false);
        }

        cancellation.ThrowIfCancellationRequested();
    }
}
