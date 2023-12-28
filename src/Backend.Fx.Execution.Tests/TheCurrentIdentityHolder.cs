using System.Security.Claims;
using Backend.Fx.Execution.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace Backend.Fx.Execution.Tests;

public class TheCurrentIdentityHolder
{
    [Fact]
    public void CanBeCreatedWithSystemIdentity()
    {
        var sut = CurrentIdentityHolder.CreateSystem();
        Assert.Equal(sut.Current, new SystemIdentity());
    }

    [Fact]
    public void CanBeCreatedWithArbitraryIdentity()
    {
        var identity = new ClaimsIdentity();
        var sut = CurrentIdentityHolder.Create(identity);
        Assert.Equal(sut.Current, identity);
    }

    [Fact]
    public void DefaultsToAnonymousIdentityWhenCreatedWithNull()
    {
        var sut = CurrentIdentityHolder.Create(null);
        Assert.Equal(sut.Current, new AnonymousIdentity());
    }
}