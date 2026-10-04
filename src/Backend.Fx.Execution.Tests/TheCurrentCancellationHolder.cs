using System.Threading;
using Backend.Fx.Execution.Pipeline;
using Backend.Fx.Util;
using Xunit;

namespace Backend.Fx.Execution.Tests;

public class TheCurrentCancellationHolder
{
    [Fact]
    public void DefaultsToNoneWhenNotReplaced()
    {
        var sut = new CurrentCancellationHolder();
        Assert.Equal(CancellationToken.None, sut.Current);
    }

    [Fact]
    public void HoldsTheReplacedToken()
    {
        using var cts = new CancellationTokenSource();
        ICurrentTHolder<CancellationToken> sut = new CurrentCancellationHolder();

        sut.ReplaceCurrent(cts.Token);

        Assert.Equal(cts.Token, sut.Current);
    }

    [Fact]
    public void ReflectsCancellationOfTheHeldToken()
    {
        using var cts = new CancellationTokenSource();
        ICurrentTHolder<CancellationToken> sut = new CurrentCancellationHolder();
        sut.ReplaceCurrent(cts.Token);

        cts.Cancel();

        Assert.True(sut.Current.IsCancellationRequested);
    }
}
