using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.Pipeline;
using FakeItEasy;
using Xunit;

namespace Backend.Fx.Execution.Tests;

public class TheInvocationOverloads
{
    private readonly RecordingInvoker _invoker = new();
    private readonly IBackendFxApplication _application = A.Fake<IBackendFxApplication>();
    private readonly CancellationToken _cancellation = TestContext.Current.CancellationToken;

    public TheInvocationOverloads()
    {
        A.CallTo(() => _application.Invoker).Returns(_invoker);
    }

    [Fact]
    public async Task ReturnResultFromServiceViaInvokerWith()
    {
        var result = await _invoker.With<Greeter>().DoAsync(g => Task.FromResult(g.Greet()));

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task ReturnResultFromServiceViaApplicationWith()
    {
        var result = await _application
            .With<Greeter>()
            .DoAsync((g, _) => Task.FromResult(g.Greet()), cancellation: _cancellation);

        Assert.Equal("hello", result);
        Assert.Equal(_cancellation, _invoker.LastCancellation);
    }

    [Fact]
    public async Task ReturnResultFromServiceProviderViaApplication()
    {
        var result = await _application.DoAsync(sp =>
            Task.FromResult(((Greeter)sp.GetService(typeof(Greeter))!).Greet())
        );

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task DefaultToAnonymousIdentity()
    {
        await _invoker.With<Greeter>().DoAsync(_ => Task.CompletedTask);
        Assert.IsType<AnonymousIdentity>(_invoker.LastIdentity);

        await _application.DoAsync((_, _) => Task.CompletedTask, cancellation: _cancellation);
        Assert.IsType<AnonymousIdentity>(_invoker.LastIdentity);
    }

    [Fact]
    public async Task ForwardIdentityAndCancellation()
    {
        var identity = new GenericIdentity("alice");

        await _application
            .With<Greeter>()
            .DoAsync((_, ct) => Task.CompletedTask, identity, _cancellation);

        Assert.Same(identity, _invoker.LastIdentity);
        Assert.Equal(_cancellation, _invoker.LastCancellation);
    }

    [Fact]
    public async Task PassInvocationCancellationToTheAction()
    {
        CancellationToken seen = default;

        await _invoker
            .With<Greeter>()
            .DoAsync(
                (_, ct) =>
                {
                    seen = ct;
                    return Task.CompletedTask;
                },
                cancellation: _cancellation
            );

        Assert.Equal(_invoker.InvocationCancellation, seen);
    }

#pragma warning disable CS0618 // sync overloads are obsolete, but still supported
    [Fact]
    public void ReturnResultSynchronously()
    {
        Assert.Equal("hello", _invoker.With<Greeter>().Do(g => g.Greet()));
        Assert.Equal("hello", _application.With<Greeter>().Do(g => g.Greet()));
        Assert.Equal(
            "hello",
            _application.Do(sp => ((Greeter)sp.GetService(typeof(Greeter))!).Greet())
        );
    }

    [Fact]
    public void WrapExceptionsOfSyncOverloadsInAggregateException()
    {
        Assert.Throws<AggregateException>(() =>
            _application.Do(_ => throw new DivideByZeroException())
        );
    }
#pragma warning restore CS0618

    public class Greeter
    {
        public string Greet() => "hello";
    }

    private sealed class RecordingInvoker : IBackendFxApplicationInvoker, IServiceProvider
    {
        private readonly CancellationTokenSource _invocationCancellationSource = new();

        public IIdentity? LastIdentity { get; private set; }

        public CancellationToken LastCancellation { get; private set; }

        public CancellationToken InvocationCancellation => _invocationCancellationSource.Token;

        public async Task InvokeAsync(
            Func<IServiceProvider, CancellationToken, Task> awaitableAsyncAction,
            IIdentity? identity = null,
            CancellationToken cancellation = default
        )
        {
            LastIdentity = identity;
            LastCancellation = cancellation;
            await awaitableAsyncAction(this, InvocationCancellation);
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(Greeter) ? new Greeter() : null;
    }
}
