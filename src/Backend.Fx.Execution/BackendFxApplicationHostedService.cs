using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Logging;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution
{
    [PublicAPI]
    public interface IBackendFxApplicationHostedService<out TApplication> : IHostedService
        where TApplication : IBackendFxApplication
    {
        TApplication Application { get; }
    }

    public abstract class
        BackendFxApplicationHostedService<TApplication> : IBackendFxApplicationHostedService<TApplication>
        where TApplication : IBackendFxApplication
    {
        private static readonly ILogger Logger = Log.Create<BackendFxApplicationHostedService<TApplication>>();

        public abstract TApplication Application { get; }

        public virtual async Task StartAsync(CancellationToken ct)
        {
            using (Logger.LogInformationDuration("Application starting..."))
            {
                try
                {
                    await Application.BootAsync(ct);
                }
                catch (Exception ex)
                {
                    Logger.LogCritical(ex, "Application could not be started");
                    throw;
                }
            }
        }

        public virtual Task StopAsync(CancellationToken cancellationToken)
        {
            using (Logger.LogInformationDuration("Application stopping..."))
            {
                Application.Dispose();
                return Task.CompletedTask;
            }
        }
    }
}