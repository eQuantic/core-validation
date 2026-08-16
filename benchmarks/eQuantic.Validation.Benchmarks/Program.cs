using BenchmarkDotNet.Running;

namespace eQuantic.Validation.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<ValidationBenchmarks>(args: args);
    }
}
