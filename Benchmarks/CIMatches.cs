using BenchmarkDotNet.Attributes;

namespace Benchmarks1;

[MemoryDiagnoser]
public class CIMatches
{
    private static readonly string Code = "ABCDEFGHI";
    private static readonly string Search = "HI";

    [Benchmark]
    public bool Matches1() => Code.ToLower().Contains(Search.ToLower());

    [Benchmark]
    public bool Matches2() => Code.Contains(Search, StringComparison.OrdinalIgnoreCase);
}