using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.Pipeline;
using JetBrains.Annotations;

namespace Backend.Fx.Execution;

[PublicAPI]
public static class BackendFxApplicationInvokeExtension
{
    /// <summary>
    ///     Invokes an async action
    /// </summary>
    public static Task DoAsync(
        this IBackendFxApplication application,
        Func<IServiceProvider, Task> asyncAction,
        IIdentity identity = null)
    {
        return application.Invoker.InvokeAsync((sp, _) => asyncAction(sp), identity ?? new AnonymousIdentity());
    }
    
    /// <summary>
    ///     Invokes an async cancelable action
    /// </summary>
    public static Task DoAsync(
        this IBackendFxApplication application,
        Func<IServiceProvider, CancellationToken, Task> asyncAction,
        IIdentity identity = null,
        CancellationToken cancellationToken = default)
    {
        return application.Invoker.InvokeAsync(asyncAction, identity ?? new AnonymousIdentity(), cancellationToken);
    }

    /// <summary>
    ///     Invokes an async function that returns <see cref="TResult" />
    /// </summary>
    public static async Task<TResult> DoAsync<TResult>(
        this IBackendFxApplication application,
        Func<IServiceProvider, Task<TResult>> asyncFunction,
        IIdentity identity = null)
    {
        TResult result = default!;
        await application.Invoker.InvokeAsync(
            async (sp, _) => result = await asyncFunction(sp),
            identity ?? new AnonymousIdentity());
        return result;
    }
    
    /// <summary>
    ///     Invokes an async cancelable function that returns <see cref="TResult" />
    /// </summary>
    public static async Task<TResult> DoAsync<TResult>(
        this IBackendFxApplication application,
        Func<IServiceProvider, CancellationToken, Task<TResult>> asyncFunction,
        IIdentity identity = null,
        CancellationToken cancellationToken = default)
    {
        TResult result = default!;
        await application.Invoker.InvokeAsync(
            async (sp, ct) => result = await asyncFunction(sp, ct),
            identity ?? new AnonymousIdentity(),
            cancellationToken);
        return result;
    }

    public static WithInvocation<TService> With<TService>(this IBackendFxApplication application)
        where TService : class
    {
        return new WithInvocation<TService>(application);
    }
}