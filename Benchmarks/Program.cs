using BenchmarkDotNet.Running;
using Benchmarks1;

BenchmarkRunner.Run(
    typeof(SplitName).Assembly.GetType(typeof(SplitName).Namespace + "." + args[0], true, true)
);