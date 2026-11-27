using System;
using System.Security.Principal;
using JetBrains.Annotations;

namespace Backend.Fx.Execution;

[PublicAPI]
public static class BackendFxApplicationSyncInvokeExtension
{
    extension(IBackendFxApplication application)
    {
        /// <summary>
        ///     Invokes a synchronous action
        /// </summary>
        [Obsolete("Prefer async overload")]
        public void Do(Action<IServiceProvider> action, IIdentity? identity = null) =>
            application.Invoker.InvokeAction(action, identity);

        /// <summary>
        ///     Invokes a synchronous function that returns a result
        /// </summary>
        [Obsolete("Prefer async overload")]
        public TResult Do<TResult>(
            Func<IServiceProvider, TResult> function,
            IIdentity? identity = null
        ) => application.Invoker.InvokeFunction(function, identity);
    }
}
