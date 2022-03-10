using System.Buffers;
using System.Buffers.Text;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace Benchmarks1;

[MemoryDiagnoser]
public class FormatNumber
{
    private static readonly IReadOnlyList<byte[]> CachedNumbers =
        Enumerable.Range(0, 1000).Select(x => Encoding.UTF8.GetBytes(x.ToString())).ToList();

    private static readonly int Number = 999;

    [Benchmark]
    public byte[] Format1() => Encoding.UTF8.GetBytes(Number.ToString());

    [Benchmark]
    public int Format2()
    {
        var allBytes = ArrayPool<byte>.Shared.Rent(100);
        Utf8Formatter.TryFormat(Number, allBytes, out var count);
        try
        {
            return allBytes.AsSpan()[..count].Length;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(allBytes);
        }
    }

    [Benchmark]
    public int Format3()
    {
        var allBytes = (Span<byte>)stackalloc byte[100];
        Utf8Formatter.TryFormat(Number, allBytes, out var count);
        return allBytes[..count].Length;
    }

    [Benchmark]
    public int Format4()
    {
        return Number < 1000 ? CachedNumbers[Number].Length : Format3();
    }
}