using System;
using System.Security.Principal;
using System.Threading.Tasks;
using Backend.Fx.Execution.Pipeline;
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
        public void Do(Action<IServiceProvider> action,
            IIdentity? identity = null)
        {
            application.Invoker.InvokeAsync(
                (sp, _) =>
                {
                    action(sp);
                    return Task.CompletedTask;
                },
                identity ?? new AnonymousIdentity()).Wait();
        }

        /// <summary>
        ///     Invokes a synchronous function that returns a result
        /// </summary>
        [Obsolete("Prefer async overload")]
        public TResult Do<TResult>(Func<IServiceProvider, TResult> function,
            IIdentity? identity = null)
        {
            TResult result = default!;
            application.Invoker.InvokeAsync(
                (sp, _) =>
                {
                    result = function(sp);
                    return Task.CompletedTask;
                },
                identity ?? new AnonymousIdentity()).Wait();
            return result;
        }
    }
}