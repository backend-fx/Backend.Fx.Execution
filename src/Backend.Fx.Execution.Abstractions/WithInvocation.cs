using System;
using System.Security.Principal;
using System.Threading.Tasks;
using Backend.Fx.Execution.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Fx.Execution;

public class WithInvocation<TService> where TService : class
{
    private readonly IBackendFxApplication _application;

    public WithInvocation(IBackendFxApplication application)
    {
        _application = application;
    }

    /// <summary>
    ///     Invokes an async action on <see cref="TService" />
    /// </summary>
    public async Task DoAsync(
        Func<TService, Task> asyncAction,
        IIdentity identity = null
    )
    {
        identity ??= new AnonymousIdentity();
        await _application.Invoker.InvokeAsync(sp => asyncAction(sp.GetRequiredService<TService>()), identity);
    }

    /// <summary>
    ///     Invokes a synchronous action on <see cref="TService" />
    /// </summary>
    public async Task DoAsync(
        Action<TService> action,
        IIdentity identity = null
    )
    {
        identity ??= new SystemIdentity();
        await _application.Invoker.InvokeAsync(
            sp =>
            {
                action(sp.GetRequiredService<TService>());
                return Task.CompletedTask;
            }, identity);
    }

    /// <summary>
    ///     Invokes an async function that returns <see cref="TResult" />on <see cref="TService" />
    /// </summary>
    public async Task<TResult> DoAsync<TResult>(
        Func<TService, Task<TResult>> func,
        IIdentity identity = null
    )
    {
        identity ??= new SystemIdentity();
        TResult result = default!;
        await _application.Invoker.InvokeAsync(
            async sp => result = await func(sp.GetRequiredService<TService>()),
            identity);
        return result;
    }

    /// <summary>
    ///     Invokes a synchronous function that returns <see cref="TResult" />on <see cref="TService" />
    /// </summary>
    public async Task<TResult> DoAsync<TResult>(
        Func<TService, TResult> func,
        IIdentity identity = null
    )
    {
        identity ??= new SystemIdentity();
        TResult result = default!;
        await _application.Invoker.InvokeAsync(
            sp =>
            {
                result = func(sp.GetRequiredService<TService>());
                return Task.CompletedTask;
            }, identity);
        return result;
    }
}