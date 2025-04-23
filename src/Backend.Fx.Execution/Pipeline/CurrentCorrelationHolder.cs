using Backend.Fx.Util;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline;

[PublicAPI]
public sealed class CurrentCorrelationHolder : CurrentTHolder<Correlation>
{
    public override Correlation ProvideInstance()
    {
        return new Correlation();
    }

    protected override string Describe(Correlation instance)
    {
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract - let's be safe
        return $"Correlation: {instance?.Id.ToString() ?? "NULL"}";
    }
}