using BenchmarkDotNet.Attributes;

namespace Benchmarks1;

[MemoryDiagnoser]
public class FirstName
{
    private static readonly string Name = "John Smith";

    [Benchmark]
    public string FirstName1() => Name.Split(" ", 2)[0];

    [Benchmark]
    public string FirstName2() => Name[..Name.IndexOf(" ")];

    [Benchmark]
    public ReadOnlySpan<char> FirstName3() => Name.AsSpan()[..Name.IndexOf(" ")];
}