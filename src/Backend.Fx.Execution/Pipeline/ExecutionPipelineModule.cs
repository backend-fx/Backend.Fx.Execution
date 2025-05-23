using System.Security.Principal;
using Backend.Fx.Execution.DependencyInjection;
using Backend.Fx.Util;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Backend.Fx.Execution.Pipeline;

internal class ExecutionPipelineModule : IModule
{
    private readonly bool _withFrozenClockDuringExecution;

    public ExecutionPipelineModule(bool withFrozenClockDuringExecution = true)
    {
        _withFrozenClockDuringExecution = withFrozenClockDuringExecution;
    }

    public void Register(ICompositionRoot compositionRoot)
    {
        compositionRoot.Register(ServiceDescriptor.Singleton<IClock>(_ => SystemClock.Instance));

        if (_withFrozenClockDuringExecution)
        {
            compositionRoot.RegisterDecorator(ServiceDescriptor.Scoped<IClock, FrozenClock>());
        }

        compositionRoot.Register(ServiceDescriptor.Singleton<Counter, Counter>());
        compositionRoot.Register(ServiceDescriptor.Scoped<IOperation, Operation>());
        compositionRoot.Register(ServiceDescriptor.Scoped<ICurrentTHolder<IIdentity>, CurrentIdentityHolder>());
        compositionRoot.Register(ServiceDescriptor.Scoped<ICurrentTHolder<Correlation>, CurrentCorrelationHolder>());
    }
}