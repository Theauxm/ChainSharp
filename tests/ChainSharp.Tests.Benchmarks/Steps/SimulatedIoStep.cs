using ChainSharp.Step;

namespace ChainSharp.Tests.Benchmarks.Steps;

public class SimulatedIoStep : Step<int, int>
{
    public override async Task<int> Run(int input)
    {
        await Task.Yield();
        return input + 1;
    }
}
