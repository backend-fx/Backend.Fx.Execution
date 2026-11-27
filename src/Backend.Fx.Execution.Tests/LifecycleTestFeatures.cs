using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Backend.Fx.Execution.Features;

namespace Backend.Fx.Execution.Tests;

/// <summary>Records every lifecycle hook it receives into a shared journal.</summary>
public class RecordingFeature(string name, List<string> journal) : IFeature, IBootableFeature
{
    protected string Name => name;

    protected List<string> Journal => journal;

    public IEnumerable<Assembly> Assemblies => [];

    public virtual void Enable(IBackendFxApplication application) => journal.Add($"{name}.Enable");

    public virtual Task BootAsync(
        IBackendFxApplication application,
        CancellationToken cancellation = default
    )
    {
        journal.Add($"{name}.Boot");
        return Task.CompletedTask;
    }
}

public class DisposableFeature(string name, List<string> journal)
    : RecordingFeature(name, journal),
        IDisposable
{
    public void Dispose() => Journal.Add($"{Name}.Dispose");
}

public class AsyncDisposableFeature(string name, List<string> journal)
    : RecordingFeature(name, journal),
        IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        Journal.Add($"{Name}.DisposeAsync");
        return default;
    }
}

public class ThrowingDisposableFeature(string name, List<string> journal)
    : RecordingFeature(name, journal),
        IDisposable
{
    public void Dispose() => throw new InvalidOperationException("boom");
}

public class ThrowingEnableFeature(List<string> journal) : RecordingFeature("throwing", journal)
{
    public override void Enable(IBackendFxApplication application) =>
        throw new InvalidOperationException("enable failed");
}

/// <summary>Holds the boot until <see cref="Release"/> is called, observing cancellation.</summary>
public class GatedBootFeature(List<string> journal)
    : RecordingFeature("gated", journal),
        IDisposable
{
    private readonly TaskCompletionSource<bool> _gate = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly TaskCompletionSource<bool> _entered = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    public bool IgnoreCancellation { get; init; }

    public Task Entered => _entered.Task;

    public void Release() => _gate.TrySetResult(true);

    public override async Task BootAsync(
        IBackendFxApplication application,
        CancellationToken cancellation = default
    )
    {
        _entered.TrySetResult(true);
        if (IgnoreCancellation)
        {
            await _gate.Task;
        }
        else
        {
            await Task.WhenAny(_gate.Task, Task.Delay(Timeout.Infinite, cancellation));
            cancellation.ThrowIfCancellationRequested();
        }

        Journal.Add("gated.BootCompleted");
    }

    public void Dispose() => Journal.Add("gated.Dispose");
}

/// <summary>A subdomain of a test application: brings its assemblies, needs no further setup.</summary>
public sealed class SubdomainFeature(params Assembly[] assemblies) : IFeature
{
    public IEnumerable<Assembly> Assemblies => assemblies;

    public void Enable(IBackendFxApplication application) { }
}
