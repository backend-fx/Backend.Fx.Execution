using System.Threading.Tasks;
using Backend.Fx.Execution.Pipeline;
using NodaTime;
using Xunit;

namespace Backend.Fx.Execution.Tests;

public class TheFrozenClock 
{
    [Fact]
    public async Task IsFrozen()
    {
        var sut = new FrozenClock(SystemClock.Instance);
        await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.True(
            sut.GetCurrentInstant() <= SystemClock.Instance.GetCurrentInstant().Plus(-Duration.FromMilliseconds(9)));
    }
}