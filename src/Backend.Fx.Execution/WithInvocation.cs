using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.Pipeline;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Fx.Execution;

[PublicAPI]
public class WithInvocation<TService>
    where TService : class
{
    private readonly IBackendFxApplicationInvoker _invoker;

    public WithInvocation(IBackendFxApplicationInvoker invoker)
    {
        _invoker = invoker;
    }

    /// <summary>
    ///     Invokes an async action on <see cref="TService" />
    /// </summary>
    public Task DoAsync(Func<TService, Task> asyncAction, IIdentity? identity = null) =>
        _invoker.InvokeActionAsync((sp, _) => asyncAction(Resolve(sp)), identity, default);

    /// <summary>
    ///     Invokes an async cancelable action on <see cref="TService" />
    /// </summary>
    public Task DoAsync(
        Func<TService, CancellationToken, Task> asyncAction,
        IIdentity? identity = null,
        CancellationToken cancellation = default
    ) =>
        _invoker.InvokeActionAsync(
            (sp, ct) => asyncAction(Resolve(sp), ct),
            identity,
            cancellation
        );

    /// <summary>
    ///     Invokes an async function that returns <see cref="TResult" /> on <see cref="TService" />
    /// </summary>
    public Task<TResult> DoAsync<TResult>(
        Func<TService, Task<TResult>> func,
        IIdentity? identity = null
    ) => _invoker.InvokeFunctionAsync((sp, _) => func(Resolve(sp)), identity, default);

    /// <summary>
    ///     Invokes an async cancelable function that returns <see cref="TResult" /> on <see cref="TService" />
    /// </summary>
    public Task<TResult> DoAsync<TResult>(
        Func<TService, CancellationToken, Task<TResult>> func,
        IIdentity? identity = null,
        CancellationToken cancellation = default
    ) => _invoker.InvokeFunctionAsync((sp, ct) => func(Resolve(sp), ct), identity, cancellation);

    #region obsolete sync

    /// <summary>
    ///     Invokes a synchronous action on <see cref="TService" />
    /// </summary>
    [Obsolete("Prefer async overload")]
    public void Do(Action<TService> action, IIdentity? identity = null) =>
        _invoker.InvokeAction(sp => action(Resolve(sp)), identity);

    /// <summary>
    ///     Invokes a synchronous function that returns <see cref="TResult" />on <see cref="TService" />
    /// </summary>
    [Obsolete("Prefer async overload")]
    public TResult Do<TResult>(Func<TService, TResult> func, IIdentity? identity = null) =>
        _invoker.InvokeFunction(sp => func(Resolve(sp)), identity);

    #endregion

    private static TService Resolve(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<TService>();
}
