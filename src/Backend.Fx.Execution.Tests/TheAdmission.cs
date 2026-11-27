using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.DependencyInjection;
using Backend.Fx.Execution.Pipeline;
using FakeItEasy;
using Xunit;

namespace Backend.Fx.Execution.Tests;

/// <summary>
/// Which identity may run an invocation in which state. Exercised directly on the internal lifecycle module,
/// since an invocation through the full pipeline would need a real injection container.
/// </summary>
public class TheAdmission
{
    private readonly List<string> _journal = [];
    private readonly ICompositionRoot _compositionRoot = A.Fake<ICompositionRoot>();
    private readonly ApplicationLifecycle _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IIdentity _user = new GenericIdentity("alice");
    private readonly IIdentity _system = new SystemIdentity();

    public TheAdmission()
    {
        var application = A.Fake<IBackendFxApplication>();
        A.CallTo(() => application.CompositionRoot).Returns(_compositionRoot);
        _sut = new ApplicationLifecycle(application);
        _sut.AddFeature(new SubdomainFeature(typeof(TheAdmission).Assembly));
    }

    [Fact]
    public async Task AdmitsEveryoneInMultiUserMode()
    {
        await _sut.BootAsync(_ct);

        await _sut.AdmitAsync(_user, _ct);
        await _sut.AdmitAsync(_system, _ct);
    }

    [Fact]
    public async Task LetsUsersWaitWhileHaltedUntilBooted()
    {
        var admission = _sut.AdmitAsync(_user, _ct);
        await Task.Yield();
        Assert.False(admission.IsCompleted);

        await _sut.BootAsync(_ct);

        await admission;
    }

    [Fact]
    public async Task LetsSystemWaitWhileHalted()
    {
        var admission = _sut.AdmitAsync(_system, _ct);
        await Task.Yield();
        Assert.False(admission.IsCompleted);

        await _sut.BootAsync(_ct);

        await admission;
    }

    [Fact]
    public async Task AdmitsOnlySystemWhileBooting()
    {
        var gated = new GatedBootFeature(_journal);
        _sut.AddFeature(gated);
        var boot = _sut.BootAsync(_ct);
        await gated.Entered;

        await _sut.AdmitAsync(_system, _ct);
        var userAdmission = _sut.AdmitAsync(_user, _ct);
        await Task.Yield();
        Assert.False(userAdmission.IsCompleted);

        gated.Release();
        await boot;
        await userAdmission;
    }

    [Fact]
    public async Task AdmitsOnlySystemInSingleUserMode()
    {
        await _sut.BootAsync(_ct);
        var handle = _sut.UseSingleUserMode();

        await _sut.AdmitAsync(_system, _ct);
        var userAdmission = _sut.AdmitAsync(_user, _ct);
        await Task.Yield();
        Assert.False(userAdmission.IsCompleted);

        handle.Dispose();
        await userAdmission;
    }

    [Fact]
    public async Task WaitingHonorsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var admission = _sut.AdmitAsync(_user, cts.Token);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => admission);
    }

    [Fact]
    public async Task RejectsEveryoneWhenCrashed()
    {
        A.CallTo(() => _compositionRoot.Verify()).Throws(new InvalidOperationException("bad"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.BootAsync(_ct));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.AdmitAsync(_user, _ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.AdmitAsync(_system, _ct));
    }

    [Fact]
    public async Task FailsWaitingUsersWhenBootCrashes()
    {
        var gated = new GatedBootFeature(_journal);
        _sut.AddFeature(gated);
        using var bootCancellation = new CancellationTokenSource();
        var boot = _sut.BootAsync(bootCancellation.Token);
        await gated.Entered;
        var userAdmission = _sut.AdmitAsync(_user, _ct);

        await bootCancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => boot);

        await Assert.ThrowsAsync<InvalidOperationException>(() => userAdmission);
    }

    [Fact]
    public async Task RejectsEveryoneOnceShutdownBegan()
    {
        await _sut.BootAsync(_ct);

        await _sut.ShutdownAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _sut.AdmitAsync(_user, _ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _sut.AdmitAsync(_system, _ct)
        );
    }

    [Fact]
    public async Task FailsWaitingUsersFastWhenShutdownBegins()
    {
        await _sut.BootAsync(_ct);
        var handle = _sut.UseSingleUserMode();
        var userAdmission = _sut.AdmitAsync(_user, _ct);

        await _sut.ShutdownAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => userAdmission);
        handle.Dispose();
    }
}
