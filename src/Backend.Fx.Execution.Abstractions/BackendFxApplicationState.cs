namespace Backend.Fx.Execution;

/// <summary>
/// The lifecycle states of an <see cref="IBackendFxApplication"/>. Legal transitions:
/// <code>
/// Halted ──BootAsync──▶ Booting ──▶ MultiUserMode ◀──▶ SingleUserMode
///                          └──────▶ Crashed
/// (any) ──Dispose──▶ ShuttingDown ──▶ Disposed
/// </code>
/// Any other transition is rejected with an <see cref="System.InvalidOperationException"/>.
/// </summary>
public enum BackendFxApplicationState
{
    /// <summary>Constructed, features may be added. Nobody is admitted yet.</summary>
    Halted,

    /// <summary>Features are enabled, the composition root is verified and features boot. Only the
    /// <see cref="Pipeline.SystemIdentity"/> is admitted.</summary>
    Booting,

    /// <summary>Maintenance window entered via <see cref="IBackendFxApplication.UseSingleUserMode"/>. Only the
    /// <see cref="Pipeline.SystemIdentity"/> is admitted, all other identities wait.</summary>
    SingleUserMode,

    /// <summary>Booted. Every identity is admitted.</summary>
    MultiUserMode,

    /// <summary>Boot failed or was canceled. Terminal; every invocation is rejected.</summary>
    Crashed,

    /// <summary>Disposal has begun. Every invocation is rejected.</summary>
    ShuttingDown,

    /// <summary>Disposal completed. Terminal.</summary>
    Disposed,
}
