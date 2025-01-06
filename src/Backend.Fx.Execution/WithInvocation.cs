using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.Pipeline;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Fx.Execution;

[PublicAPI]
public class WithInvocation<TService> where TService : class
{
    private readonly IBackendFxApplicationInvoker _invoker;

    public WithInvocation(IBackendFxApplicationInvoker invoker)
    {
        _invoker = invoker;
    }
    
    /// <summary>
    ///     Invokes an async action on <see cref="TService" />
    /// </summary>
    public Task DoAsync(
        Func<TService, Task> asyncAction, 
        IIdentity? identity = null)
    {
        identity ??= new AnonymousIdentity();
        return _invoker.InvokeAsync(
            (sp, _) => asyncAction(sp.GetRequiredService<TService>()),
            identity);
    }
    

    /// <summary>
    ///     Invokes an async cancelable action on <see cref="TService" />
    /// </summary>
    public Task DoAsync(
        Func<TService, CancellationToken, Task> asyncAction, 
        IIdentity? identity = null,
        CancellationToken cancellationToken = default)
    {
        identity ??= new AnonymousIdentity();
        return _invoker.InvokeAsync(
            (sp, ct) => asyncAction(sp.GetRequiredService<TService>(), ct),
            identity, cancellationToken);
    }
    
    /// <summary>
    ///     Invokes an async function that returns <see cref="TResult" /> on <see cref="TService" />
    /// </summary>
    public async Task<TResult> DoAsync<TResult>(
        Func<TService, Task<TResult>> func,
        IIdentity? identity = null)
    {
        identity ??= new AnonymousIdentity();
        TResult result = default!;
        await _invoker.InvokeAsync(
            async (sp, _) => result = await func(sp.GetRequiredService<TService>()),
            identity);
        return result;
    }
    
    /// <summary>
    ///     Invokes an async cancelable function that returns <see cref="TResult" /> on <see cref="TService" />
    /// </summary>
    public async Task<TResult> DoAsync<TResult>(
        Func<TService, CancellationToken, Task<TResult>> func,
        IIdentity? identity = null,
        CancellationToken cancellationToken = default)
    {
        identity ??= new AnonymousIdentity();
        TResult result = default!;
        await _invoker.InvokeAsync(
            async (sp, ct) => result = await func(sp.GetRequiredService<TService>(), ct),
            identity, 
            cancellationToken);
        return result;
    }

    #region obsolete sync

    /// <summary>
    ///     Invokes a synchronous action on <see cref="TService" />
    /// </summary>
    [Obsolete("Prefer async overload")]
    public void Do(Action<TService> action, IIdentity? identity = null)
    {
        identity ??= new AnonymousIdentity();
        _invoker.InvokeAsync(
            (sp, _) =>
            {
                action(sp.GetRequiredService<TService>());
                return Task.CompletedTask;
            }, identity).Wait();
    }

    /// <summary>
    ///     Invokes a synchronous function that returns <see cref="TResult" />on <see cref="TService" />
    /// </summary>
    public TResult Do<TResult>(
        Func<TService, TResult> func,
        IIdentity? identity = null
    )
    {
        identity ??= new AnonymousIdentity();
        TResult result = default!;
        _invoker.InvokeAsync(
            (sp, _) =>
            {
                result = func(sp.GetRequiredService<TService>());
                return Task.CompletedTask;
            }, identity).Wait();
        return result;
    }

    #endregion
}