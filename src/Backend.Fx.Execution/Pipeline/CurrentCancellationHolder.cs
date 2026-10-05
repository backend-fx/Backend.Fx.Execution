using System.Threading;
using Backend.Fx.Util;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline;

/// <summary>
/// Holds the <see cref="CancellationToken"/> of the current invocation (linked with the application's
/// <see cref="IBackendFxApplication.ShutdownRequested"/> token). Resolve this scoped holder to honor cancellation
/// in services that cannot receive the token as a method argument.
/// </summary>
[PublicAPI]
public sealed class CurrentCancellationHolder : CurrentTHolder<CancellationToken>
{
    public override CancellationToken ProvideInstance()
    {
        return CancellationToken.None;
    }

    protected override string Describe(CancellationToken instance)
    {
        return $"CancellationToken: {(instance.CanBeCanceled ? "cancelable" : "non-cancelable")}, "
            + $"{(instance.IsCancellationRequested ? "cancellation requested" : "not canceled")}";
    }
}
