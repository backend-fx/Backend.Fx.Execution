using System.Security.Principal;
using Backend.Fx.Execution.Pipeline;
using Xunit;

namespace Backend.Fx.Execution.Tests;

public class TheSystemIdentity
{
    [Fact]
    public void HasCorrectName()
    {
        var sut = new SystemIdentity();
        Assert.Equal("SYSTEM", sut.Name);
    }

    [Fact]
    public void IsAuthenticated()
    {
        var sut = new SystemIdentity();
        Assert.True(sut.IsAuthenticated);
    }

    [Fact]
    public void IsDetectedAsSystemIdentity()
    {
        IIdentity sut = new SystemIdentity();
        Assert.True(sut.IsSystem());
        Assert.False(sut.IsAnonymous());
    }

    [Fact]
    public void HasInternalAuthenticationType()
    {
        var sut = new SystemIdentity();
        Assert.Equal("Internal", sut.AuthenticationType);
    }

    [Fact]
    public void EqualsOtherSystemIdentity()
    {
        var sut = new SystemIdentity();
        var other = new SystemIdentity();
        Assert.True(sut.Equals(other));
        Assert.True(Equals(sut, other));
        Assert.Equal(sut.GetHashCode(), other.GetHashCode());
    }

    [Fact]
    public void DoesNotEqualOtherIdentity()
    {
        var sut = new SystemIdentity();
        var other = new AnonymousIdentity();
        Assert.False(sut.Equals(other));
        Assert.False(Equals(sut, other));
        Assert.NotEqual(sut.GetHashCode(), other.GetHashCode());
    }
}