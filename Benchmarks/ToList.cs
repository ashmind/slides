using BenchmarkDotNet.Attributes;

namespace Benchmarks1;

[MemoryDiagnoser]
public class ToList
{
    private static readonly IList<object> Values = Enumerable.Repeat(new object(), 100).ToList();

    [Benchmark]
    public IReadOnlyList<object> ToList1() => Values.ToList();

    [Benchmark]
    public IReadOnlyList<object> ToList2() => (Values as IReadOnlyList<object>) ?? Values.ToList();
}