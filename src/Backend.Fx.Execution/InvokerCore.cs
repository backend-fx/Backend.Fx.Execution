using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.Pipeline;

namespace Backend.Fx.Execution;

/// <summary>
/// The single place where the convenience overloads (<see cref="WithInvocation{TService}"/>,
/// <see cref="BackendFxApplicationInvokeExtension"/>, <see cref="BackendFxApplicationSyncInvokeExtension"/>)
/// meet the <see cref="IBackendFxApplicationInvoker"/>: identity defaulting, result capturing and
/// sync-over-async live here and nowhere else.
/// </summary>
internal static class InvokerCore
{
    public static Task InvokeActionAsync(
        this IBackendFxApplicationInvoker invoker,
        Func<IServiceProvider, CancellationToken, Task> asyncAction,
        IIdentity? identity,
        CancellationToken cancellation
    )
    {
        return invoker.InvokeAsync(asyncAction, identity ?? new AnonymousIdentity(), cancellation);
    }

    public static async Task<TResult> InvokeFunctionAsync<TResult>(
        this IBackendFxApplicationInvoker invoker,
        Func<IServiceProvider, CancellationToken, Task<TResult>> asyncFunction,
        IIdentity? identity,
        CancellationToken cancellation
    )
    {
        TResult result = default!;
        await invoker
            .InvokeActionAsync(
                async (sp, ct) => result = await asyncFunction(sp, ct).ConfigureAwait(false),
                identity,
                cancellation
            )
            .ConfigureAwait(false);
        return result;
    }

    /// <remarks>Blocks; exceptions surface wrapped in an <see cref="AggregateException"/>.</remarks>
    public static void InvokeAction(
        this IBackendFxApplicationInvoker invoker,
        Action<IServiceProvider> action,
        IIdentity? identity
    )
    {
        invoker
            .InvokeActionAsync(
                (sp, _) =>
                {
                    action(sp);
                    return Task.CompletedTask;
                },
                identity,
                CancellationToken.None
            )
            .Wait();
    }

    /// <remarks>Blocks; exceptions surface wrapped in an <see cref="AggregateException"/>.</remarks>
    public static TResult InvokeFunction<TResult>(
        this IBackendFxApplicationInvoker invoker,
        Func<IServiceProvider, TResult> function,
        IIdentity? identity
    )
    {
        return invoker
            .InvokeFunctionAsync(
                (sp, _) => Task.FromResult(function(sp)),
                identity,
                CancellationToken.None
            )
            .Result;
    }
}
