using System.Threading;

namespace Backend.Fx.Execution.Pipeline;

public class Counter
{
    private int _count;
        
    public int Count()
    {
        return Interlocked.Increment(ref _count);
    }
}