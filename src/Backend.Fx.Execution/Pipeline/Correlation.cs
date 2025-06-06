using System;
using Backend.Fx.Logging;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution.Pipeline;

/// <summary>
/// A guid that is unique for an invocation. By calling <code>Resume(Guid correlationId)</code> a distributed process
/// that spans multiple invocations can be identified as correlating.
/// </summary>
[PublicAPI]
public sealed class Correlation
{
    private readonly ILogger _logger = Log.Create<Correlation>();

    public Guid Id { get; private set; } = Guid.NewGuid();

    public void Resume(Guid correlationId)
    {
        Id = correlationId;
        _logger.LogInformation("Resuming correlation {Correlation}", Id);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Correlation);
    }

    private bool Equals(Correlation? other)
    {
        return Id.Equals(other?.Id);
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return Id.GetHashCode();
    }
}