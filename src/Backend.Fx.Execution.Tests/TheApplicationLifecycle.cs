using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.DependencyInjection;
using Backend.Fx.Execution.Features;
using Backend.Fx.Logging;
using FakeItEasy;
using Xunit;

namespace Backend.Fx.Execution.Tests;

/// <summary>
/// States, feature phases and shutdown, exercised through the public <see cref="IBackendFxApplication"/> interface.
/// </summary>
public class TheApplicationLifecycle : IAsyncLifetime
{
    private readonly List<string> _journal = [];
    private readonly ICompositionRoot _compositionRoot = A.Fake<ICompositionRoot>();
    private readonly BackendFxApplication _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public TheApplicationLifecycle()
    {
        A.CallTo(() => _compositionRoot.Verify()).Invokes(() => _journal.Add("root.Verify"));
        A.CallTo(() => _compositionRoot.Dispose()).Invokes(() => _journal.Add("root.Dispose"));
        A.CallTo(() => _compositionRoot.DisposeAsync())
            .Invokes(() => _journal.Add("root.DisposeAsync"))
            .Returns(default);
        _sut = new BackendFxApplication(_compositionRoot, A.Fake<IExceptionLogger>());
        _sut.AddFeature(new SubdomainFeature(typeof(TheApplicationLifecycle).Assembly));
    }

    public ValueTask InitializeAsync() => default;

    public ValueTask DisposeAsync() => _sut.DisposeAsync();

    #region boot

    [Fact]
    public async Task BootsFromHaltedToMultiUserMode()
    {
        Assert.Equal(BackendFxApplicationState.Halted, _sut.State);

        await _sut.BootAsync(_ct);

        Assert.Equal(BackendFxApplicationState.MultiUserMode, _sut.State);
    }

    [Fact]
    public async Task EnablesAllFeaturesOnceThenVerifiesThenBoots()
    {
        _sut.AddFeature(new RecordingFeature("a", _journal));
        _sut.AddFeature(new RecordingFeature("b", _journal));

        await _sut.BootAsync(_ct);

        Assert.Equal(["a.Enable", "b.Enable", "root.Verify", "a.Boot", "b.Boot"], _journal);
    }

    [Fact]
    public async Task CrashesWhenNoFeatureBringsAssemblies()
    {
        var sut = new BackendFxApplication(_compositionRoot, A.Fake<IExceptionLogger>());
        sut.AddFeature(new RecordingFeature("a", _journal));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.BootAsync(_ct));

        Assert.Equal(BackendFxApplicationState.Crashed, sut.State);
        Assert.Empty(_journal);
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task RejectsSecondBoot()
    {
        await _sut.BootAsync(_ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.BootAsync(_ct));
        Assert.Equal(BackendFxApplicationState.MultiUserMode, _sut.State);
    }

    [Fact]
    public async Task RejectsFeaturesAfterBoot()
    {
        await _sut.BootAsync(_ct);

        Assert.Throws<InvalidOperationException>(() =>
            _sut.AddFeature(new RecordingFeature("late", _journal))
        );
    }

    [Fact]
    public async Task CrashesWhenEnablingAFeatureFails()
    {
        _sut.AddFeature(new ThrowingEnableFeature(_journal));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.BootAsync(_ct));

        Assert.Equal(BackendFxApplicationState.Crashed, _sut.State);
        Assert.DoesNotContain("root.Verify", _journal);
    }

    [Fact]
    public async Task CrashesWhenVerificationFails()
    {
        A.CallTo(() => _compositionRoot.Verify()).Throws(new InvalidOperationException("bad"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.BootAsync(_ct));

        Assert.Equal(BackendFxApplicationState.Crashed, _sut.State);
    }

    [Fact]
    public async Task CrashesWhenBootIsCanceled()
    {
        var gated = new GatedBootFeature(_journal);
        _sut.AddFeature(gated);
        using var cts = new CancellationTokenSource();

        var boot = _sut.BootAsync(cts.Token);
        await gated.Entered;
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => boot);
        Assert.Equal(BackendFxApplicationState.Crashed, _sut.State);
    }

    [Fact]
    public async Task CannotRecoverFromCrash()
    {
        A.CallTo(() => _compositionRoot.Verify()).Throws(new InvalidOperationException("bad"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.BootAsync(_ct));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.BootAsync(_ct));
        Assert.Throws<InvalidOperationException>(() => _sut.UseSingleUserMode());
    }

    [Fact]
    public async Task WaitForBootReturnsAfterSuccessfulBoot()
    {
        var gated = new GatedBootFeature(_journal);
        _sut.AddFeature(gated);
        var boot = _sut.BootAsync(_ct);
        var waiting = _sut.WaitForBootAsync(_ct);
        await gated.Entered;
        Assert.False(waiting.IsCompleted);

        gated.Release();
        await boot;

        await waiting;
    }

    [Fact]
    public async Task WaitForBootReturnsWithoutThrowingAfterCrash()
    {
        A.CallTo(() => _compositionRoot.Verify()).Throws(new InvalidOperationException("bad"));
        var waiting = _sut.WaitForBootAsync(_ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.BootAsync(_ct));

        await waiting;
        Assert.Equal(BackendFxApplicationState.Crashed, _sut.State);
    }

    [Fact]
    public async Task WaitForBootReturnsWhenShutdownBeginsBeforeBoot()
    {
        var waiting = _sut.WaitForBootAsync(_ct);
        Assert.False(waiting.IsCompleted);

        await _sut.DisposeAsync();

        await waiting;
    }

    [Fact]
    public async Task WaitForBootHonorsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var waiting = _sut.WaitForBootAsync(cts.Token);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    #endregion

    [Fact]
    public void ProvidesAssembliesOfAllFeaturesWithoutDuplicates()
    {
        var thisAssembly = typeof(TheApplicationLifecycle).Assembly;
        var executionAssembly = typeof(BackendFxApplication).Assembly;

        _sut.AddFeature(new SubdomainFeature(thisAssembly));
        _sut.AddFeature(new SubdomainFeature(thisAssembly, executionAssembly));

        Assert.Equal([thisAssembly, executionAssembly], _sut.Assemblies);
    }

    #region single user mode

    [Fact]
    public async Task EntersAndLeavesSingleUserMode()
    {
        await _sut.BootAsync(_ct);

        using (_sut.UseSingleUserMode())
        {
            Assert.Equal(BackendFxApplicationState.SingleUserMode, _sut.State);
        }

        Assert.Equal(BackendFxApplicationState.MultiUserMode, _sut.State);
    }

    [Fact]
    public void RejectsSingleUserModeBeforeBoot()
    {
        Assert.Throws<InvalidOperationException>(() => _sut.UseSingleUserMode());
        Assert.Equal(BackendFxApplicationState.Halted, _sut.State);
    }

    [Fact]
    public async Task RejectsNestedSingleUserMode()
    {
        await _sut.BootAsync(_ct);
        using var _ = _sut.UseSingleUserMode();

        Assert.Throws<InvalidOperationException>(() => _sut.UseSingleUserMode());
    }

    [Fact]
    public async Task StaleHandleDoesNotEndALaterSingleUserMode()
    {
        await _sut.BootAsync(_ct);
        var first = _sut.UseSingleUserMode();
        first.Dispose();
        using var second = _sut.UseSingleUserMode();

        first.Dispose();

        Assert.Equal(BackendFxApplicationState.SingleUserMode, _sut.State);
    }

    [Fact]
    public async Task ReleasingHandleAfterShutdownDoesNothing()
    {
        await _sut.BootAsync(_ct);
        var handle = _sut.UseSingleUserMode();
        await _sut.DisposeAsync();

        handle.Dispose();

        Assert.Equal(BackendFxApplicationState.Disposed, _sut.State);
    }

    #endregion

    #region shutdown

    [Fact]
    public async Task DisposesFeaturesInReverseOrderThenCompositionRoot()
    {
        _sut.AddFeature(new DisposableFeature("a", _journal));
        _sut.AddFeature(new AsyncDisposableFeature("b", _journal));
        _sut.AddFeature(new DisposableFeature("c", _journal));
        await _sut.BootAsync(_ct);
        _journal.Clear();

        await _sut.DisposeAsync();

        Assert.Equal(["c.Dispose", "b.DisposeAsync", "a.Dispose", "root.DisposeAsync"], _journal);
        Assert.Equal(BackendFxApplicationState.Disposed, _sut.State);
        Assert.True(_sut.ShutdownRequested.IsCancellationRequested);
    }

    [Fact]
    public async Task SyncDisposeFollowsTheSameSequence()
    {
        _sut.AddFeature(new DisposableFeature("a", _journal));
        _sut.AddFeature(new AsyncDisposableFeature("b", _journal));
        await _sut.BootAsync(_ct);
        _journal.Clear();

        _sut.Dispose();

        Assert.Equal(["b.DisposeAsync", "a.Dispose", "root.Dispose"], _journal);
        Assert.Equal(BackendFxApplicationState.Disposed, _sut.State);
    }

    [Fact]
    public async Task IsolatesFeatureDisposalErrors()
    {
        _sut.AddFeature(new DisposableFeature("a", _journal));
        _sut.AddFeature(new ThrowingDisposableFeature("b", _journal));
        await _sut.BootAsync(_ct);
        _journal.Clear();

        await _sut.DisposeAsync();

        Assert.Equal(["a.Dispose", "root.DisposeAsync"], _journal);
    }

    [Fact]
    public async Task SecondDisposeDoesNothing()
    {
        _sut.AddFeature(new DisposableFeature("a", _journal));
        await _sut.BootAsync(_ct);
        await _sut.DisposeAsync();
        _journal.Clear();

        await _sut.DisposeAsync();
        _sut.Dispose();

        Assert.Empty(_journal);
    }

    [Fact]
    public async Task DisposingDuringBootCancelsAndAwaitsTheBootBeforeDisposingFeatures()
    {
        var gated = new GatedBootFeature(_journal);
        _sut.AddFeature(gated);
        var boot = _sut.BootAsync(_ct);
        await gated.Entered;

        await _sut.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => boot);
        Assert.Equal(BackendFxApplicationState.Disposed, _sut.State);
        Assert.Equal(
            ["gated.Enable", "root.Verify", "gated.Dispose", "root.DisposeAsync"],
            _journal
        );
    }

    [Fact]
    public async Task IgnoresTheLateResultOfABootThatOutlivedShutdownStart()
    {
        var gated = new GatedBootFeature(_journal) { IgnoreCancellation = true };
        _sut.AddFeature(gated);
        var boot = _sut.BootAsync(_ct);
        await gated.Entered;

        var disposal = _sut.DisposeAsync().AsTask();
        Assert.Equal(BackendFxApplicationState.ShuttingDown, _sut.State);
        Assert.DoesNotContain("gated.Dispose", _journal);

        gated.Release();
        await disposal;

        Assert.Equal(BackendFxApplicationState.Disposed, _sut.State);
        Assert.Equal(
            [
                "gated.Enable",
                "root.Verify",
                "gated.BootCompleted",
                "gated.Dispose",
                "root.DisposeAsync",
            ],
            _journal
        );
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => boot);
    }

    #endregion
}
