---
marp: true
class: invert
style: |
    .hljs-keyword  { color: #569cd6; }
    .hljs-built_in { color: #569cd6; }
    .hljs-number   { color: #fff; }
    .hljs-string   { color: #d69d85; }
    .hljs-title    { color: #4ec9b0; }
    .logo {
        position: absolute;
        height: 80%;
        left: 23%;
        top: 0;
        opacity: 10%;
    }
    .author {
        margin-top: 10vh;
        text-align: right;
    }
---

<img src="./images/logo.svg" class="logo">

# <!--fit--> Zero allocations in .NET

<span class="author">
Andrey Shchekin<br>
10 March 2022
</span>

---

# Why?

* Optimize bottleneck performance
* Optimize library performance
* Understand memory better

---

# Do *you* need to aim for lower allocations?

* Sometimes, if you have a good reason
* However, prefer clarity over micro-optimizations
* There is always a cost; understand it
* Developer time is more expensive than extra hardware

---

# Do *you* need to aim for zero allocations?

* Absolutely not, unless you are have a very convincing reason

---

![bg left:60% width: 100%](./images/pepe-silvia.png)

5 hours into implementing zero allocations, 5 lines of code written.

---

# Simple case of lowering allocations

---

# Example 1: Large file

```csharp
var results = new List<string>();
foreach (var line in File.ReadAllLines(input)) {
    results.Add(Transform(line));
}
Response.Write(results.Join("\r\n"));
```

* Active memory ≥ 3 times file size
    * 1: result of ReadAllLines
    * 1: results list
    * 1: joined string
    * ?: when writing response
* Risky approach if expecting large files
* Security: allows DoS attacks through large files

---

# Example 1: Large file

```csharp
foreach (var line in File.ReadLines(path)) {
    var result = Transform(line);
    Response.WriteLine(result);
}
```

* Active memory ≈ 3 times max line size
* Cost: Cannot validate the whole file in advance
* Cost: Errors will be intermixed with the output

---

# Let's increase complexity

---

# Theory

---

# Memory

* Stack
* Heap
  * Actually, many heaps: SOH, LOH, POH
* Unmanaged

---

# Stack

* Limited
* Relatively cheap
* Freed when existing scope
* Data generally does not move
* Stores
  * Method calls (e.g. at M1() at M2() at M3())
  * Method arguments (value types or pointers)
  * Local variables (value types or pointers)
  * More (to be revealed)

---

# Heap

* Large (limited only by the process memory)
* Relatively expensive
* Freed by GC
  * Pause, mark, sweep
  * Multiple generations
* Data can be moved by GC
* Stores
  * Instances of reference types
  * Boxed values of value types

---

# Unmanaged memory

* Large (limited only by the process memory)
* Allocated manually by operating system or native libraries, or code that needs to interact with those
* Freed manually
* Data generally does not move
* Stores
  * Anything, but most common scenarios are interacting with network OS APIs and libraries

---

# Zero *Heap* Allocations

---

# Heap allocations

* Explicit new instances, e.g. `new Client()`
* Copying, e.g.
  * `string.Substring(3, 5)`
  * `array.ToList()`
* Params, e.g.
  * `void Print(params object[] args); Print(5)`
* Boxing
* Lambdas, e.g. `() => c.Name == name` 

---

# What heap allocations you can see in this code?

```csharp
clients
    .Where(c => c.FullName.Contains(search))
    .ToArray();
```

```csharp
class Client { string FullName => FirstName + ' ' + LastName; }
```

---

# Heap allocations

```csharp
clients
    .Where(c => c.FullName.Contains(search))
    .ToArray();
```

```csharp
class Client { string FullName => FirstName + ' ' + LastName; }
```

1) `FullName` -- 1 new string for each client checked
2) `c => c.FullName.Contains(search)` -- 1 new lambda instance
3) `ToArray()`
    * Number of arrays depending on number of clients
    * One last array to make sure the size is exact

---

# Let's optimize a bit?

```csharp
clients
    .Where(c => c.FirstName.Contains(search) || c.LastName.Contains(search))
    .ToList();
```
* ⚠️ Regression: now "First L" no longer matches

---

# Simpler examples

---

# Example 2: Case-insensitive compare

```csharp
var matches1 = code.ToLower().Contains(search.ToLower());

var matches2 = code.Contains(search, StringComparison.OrdinalIgnoreCase);
```

```
|   Method |     Mean |    Error |   StdDev |  Gen 0 | Allocated |
|--------- |---------:|---------:|---------:|-------:|----------:|
| Matches1 | 75.36 ns | 0.675 ns | 0.598 ns | 0.0229 |      72 B |
| Matches2 | 50.16 ns | 0.676 ns | 0.632 ns |      - |         - |
```

---

# Example 3: Convert to `IReadOnlyList`

```csharp
// IList<object> values

var readOnlyList1 = values.ToList();

var readOnlyList2 = (values as IReadOnlyList<object>) ?? values.ToList();
```

```
|  Method |        Mean |     Error |    StdDev |      Median |  Gen 0 | Allocated |
|-------- |------------:|----------:|----------:|------------:|-------:|----------:|
| ToList1 | 108.5690 ns | 1.9495 ns | 1.8236 ns | 108.9779 ns | 0.2728 |     856 B |
| ToList2 |   0.0273 ns | 0.0273 ns | 0.0228 ns |   0.0166 ns |      - |         - |
```

---

# More complexity, more theory

---

# `Span<T>` and `ReadOnlySpan<T>`

* Added at around 2018?
* Represents a contiguous set of values
* Can point to heap, stack and unmanaged memory 
* Restricted and clear lifetime

---

# `Memory<T>` and `ReadOnlyMemory<T>`

* Pretty much same as Span
* However it can't point at stack
* Not as restricted

---

# Example 4: Extract first name

```csharp
var firstName1 = name.Split(" ", 2)[0]; // string

var firstName2 = name[..name.IndexOf(" ")]; // string

var firstName3 = name.AsSpan()[..name.IndexOf(" ")]; // ReadOnlySpan<char>
```

```
|     Method |     Mean |    Error |   StdDev |  Gen 0 | Allocated |
|----------- |---------:|---------:|---------:|-------:|----------:|
| FirstName1 | 71.11 ns | 1.248 ns | 1.167 ns | 0.0331 |     104 B |
| FirstName2 | 70.74 ns | 0.883 ns | 0.782 ns | 0.0101 |      32 B |
| FirstName3 | 58.48 ns | 0.446 ns | 0.395 ns |      - |         - |
```

---

# Span restrictions

```csharp
public readonly ref struct SpanFullName
{
    public SpanFullName(ReadOnlySpan<char> first, ReadOnlySpan<char> last)
    {
        First = first;
        Last = last;
    }

    public ReadOnlySpan<char> First { get; }
    public ReadOnlySpan<char> Last { get; }
}
```

---

# What if you do need a copy?

* `System.Buffers.ArrayPool`
    * Cost: Easy memory leaks

* `Microsoft.IO.RecyclableMemoryStream`
    * Cost: Easy memory leaks

* `stackalloc`
    * Cost: Easy StackOverflowException

---

# Example 5: Convert number to UTF8 bytes

```csharp
var bytes1 = Encoding.UTF8.GetBytes(number.ToString());
```

```csharp
var allBytes2 = ArrayPool<byte>.Shared.GetBytes(100);
try {
    Utf8Formatter.TryFormat(number, allBytes2, out var count);
    var bytes2 = allBytes2.AsSpan()[..count];
}
finally {
    ArrayPool<byte>.Shared.Return(allBytes2);
}
```

```csharp
var allBytes3 = (Span<byte>)stackalloc byte[100];
Utf8Formatter.TryFormat(number, allBytes3, out var count);
var bytes3 = allBytes2.AsSpan()[..count];
```

---

# Example 5: Convert number to UTF8 bytes

```
|  Method |     Mean |    Error |   StdDev |  Gen 0 | Allocated |
|-------- |---------:|---------:|---------:|-------:|----------:|
| Format1 | 40.40 ns | 0.873 ns | 0.934 ns | 0.0204 |      64 B |
| Format2 | 33.90 ns | 0.442 ns | 0.391 ns |      - |         - |
| Format3 | 11.64 ns | 0.145 ns | 0.129 ns |      - |         - |
```

---

# Example 5: Optimize further?

```csharp
private static readonly IReadOnlyList<byte[]> CachedNumbers = Enumerable
    .Range(0, 1000)
    .Select(x => Format(number))
    .ToList();

var bytes4 = number < 1000 ? CachedBytes[number] : Format(number);
```

```
|  Method |      Mean |     Error |    StdDev |  Gen 0 | Allocated |
|-------- |----------:|----------:|----------:|-------:|----------:|
| Format1 | 38.804 ns | 0.1894 ns | 0.1679 ns | 0.0204 |      64 B |
| Format2 | 31.637 ns | 0.1554 ns | 0.1454 ns |      - |         - |
| Format3 | 10.091 ns | 0.1495 ns | 0.1398 ns |      - |         - |
| Format4 |  2.528 ns | 0.0343 ns | 0.0321 ns |      - |         - |
```

---

# Actual Span use case: Network

---

![bg contain](./images/network-old.png?1)

---

![bg contain](./images/network-new.png?)

---

# Further reading

* https://github.com/Cysharp/ZString - Zero-allocation StringBuilder
* https://github.com/Cysharp/ZLogger - Zero-allocation Logger
* [System.IO.Pipelines: High performance IO in .NET](https://devblogs.microsoft.com/dotnet/system-io-pipelines-high-performance-io-in-net/)