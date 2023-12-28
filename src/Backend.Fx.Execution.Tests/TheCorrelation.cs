using System;
using Backend.Fx.Execution.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace Backend.Fx.Execution.Tests;

public class TheCorrelation
{
    [Fact]
    public void InitializesWithRandomGuid()
    {
        var sut = new Correlation();
        Assert.NotEqual(Guid.Empty, sut.Id);
    }

    [Fact]
    public void CanResume()
    {
        Guid correlationIdToResume = Guid.NewGuid();
        var sut = new Correlation();
        sut.Resume(correlationIdToResume);
        Assert.Equal(correlationIdToResume, sut.Id);
    }
}