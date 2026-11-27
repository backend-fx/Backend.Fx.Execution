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
    extension(IBackendFxApplication application)
    {
        /// <summary>
        ///     Invokes an async action
        /// </summary>
        public Task DoAsync(Func<IServiceProvider, Task> asyncAction, IIdentity? identity = null) =>
            application.Invoker.InvokeActionAsync((sp, _) => asyncAction(sp), identity, default);

        /// <summary>
        ///     Invokes an async cancelable action
        /// </summary>
        public Task DoAsync(
            Func<IServiceProvider, CancellationToken, Task> asyncAction,
            IIdentity? identity = null,
            CancellationToken cancellation = default
        ) => application.Invoker.InvokeActionAsync(asyncAction, identity, cancellation);

        /// <summary>
        ///     Invokes an async function that returns <see cref="TResult" />
        /// </summary>
        public Task<TResult> DoAsync<TResult>(
            Func<IServiceProvider, Task<TResult>> asyncFunction,
            IIdentity? identity = null
        ) =>
            application.Invoker.InvokeFunctionAsync(
                (sp, _) => asyncFunction(sp),
                identity,
                default
            );

        /// <summary>
        ///     Invokes an async cancelable function that returns <see cref="TResult" />
        /// </summary>
        public Task<TResult> DoAsync<TResult>(
            Func<IServiceProvider, CancellationToken, Task<TResult>> asyncFunction,
            IIdentity? identity = null,
            CancellationToken cancellation = default
        ) => application.Invoker.InvokeFunctionAsync(asyncFunction, identity, cancellation);
    }

    public static WithInvocation<TService> With<TService>(this IBackendFxApplicationInvoker invoker)
        where TService : class
    {
        return new WithInvocation<TService>(invoker);
    }

    public static WithAppInvocation<TService> With<TService>(this IBackendFxApplication application)
        where TService : class
    {
        return new WithAppInvocation<TService>(application);
    }
}
