namespace BgGame_Lib.Tests;

using System.Globalization;
using System.Runtime.CompilerServices;
using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

public class ShuffledProblemSetSourceTests
{
    /// <summary>In-memory source with a fixed item list, re-iterable from the start.</summary>
    private sealed class InMemorySource : IProblemSetSource
    {
        private readonly IReadOnlyList<BgDecisionData> _items;
        public InMemorySource(string name, IReadOnlyList<BgDecisionData> items)
            : this(name, items, items.Count)
        {
        }

        public InMemorySource(string name, IReadOnlyList<BgDecisionData> items, int? count)
        {
            Name = name;
            _items = items;
            Count = count;
        }

        public string Name { get; }
        public int? Count { get; }

        public async IAsyncEnumerable<BgDecisionData> EnumerateAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var item in _items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
    }

    private static IReadOnlyList<BgDecisionData> Decisions(int count) =>
        Enumerable.Range(0, count).Select(Decision).ToList();

    private static BgDecisionData Decision(int seed) =>
        TestRecords.CheckerPlay(id: new XgpDecisionId($"test-{seed}.xgp"));

    /// <summary>The seed a decision was built for, read back off its file name.</summary>
    private static int SeedOf(BgDecisionData item) =>
        int.Parse(item.SourceFile["test-".Length..^".xgp".Length], CultureInfo.InvariantCulture);

    private static async Task<List<int>> IdsAsync(IProblemSetSource source, CancellationToken ct = default)
    {
        var ids = new List<int>();
        await foreach (var item in source.EnumerateAsync(ct))
            ids.Add(SeedOf(item));
        return ids;
    }

    [Fact]
    public async Task SameSeed_YieldsSameOrder()
    {
        var items = Decisions(30);
        var a = new ShuffledProblemSetSource(new InMemorySource("Test", items), seed: 42);
        var b = new ShuffledProblemSetSource(new InMemorySource("Test", items), seed: 42);

        Assert.Equal(await IdsAsync(a), await IdsAsync(b));
    }

    [Fact]
    public async Task DifferentSeeds_YieldDifferentOrder()
    {
        var items = Decisions(30);
        var a = new ShuffledProblemSetSource(new InMemorySource("Test", items), seed: 1);
        var b = new ShuffledProblemSetSource(new InMemorySource("Test", items), seed: 2);

        Assert.NotEqual(await IdsAsync(a), await IdsAsync(b));
    }

    [Fact]
    public async Task Shuffle_IsPermutationOfOriginal()
    {
        var items = Decisions(20);
        var source = new ShuffledProblemSetSource(new InMemorySource("Test", items), seed: 7);

        var shuffled = await IdsAsync(source);

        Assert.Equal(items.Select(SeedOf).Order(), shuffled.Order());
        Assert.NotEqual(items.Select(SeedOf), shuffled);
    }

    [Fact]
    public async Task Restart_ReshufflesRatherThanRepeating()
    {
        var items = Decisions(30);
        var source = new ShuffledProblemSetSource(new InMemorySource("Test", items), seed: 7);

        var first = await IdsAsync(source);
        var second = await IdsAsync(source);

        Assert.NotEqual(first, second);
        Assert.Equal(first.Order(), second.Order());
    }

    [Fact]
    public async Task NoShuffle_UnwrappedSourceStillYieldsFileOrder()
    {
        var items = Decisions(20);
        var inner = new InMemorySource("Test", items);

        // The decorator is additive: using the inner source directly (without
        // wrapping it) still yields strict file order, unaffected by
        // ShuffledProblemSetSource existing at all.
        var ids = await IdsAsync(inner);

        Assert.Equal(items.Select(SeedOf), ids);
    }

    [Fact]
    public void Name_PassesThroughFromInner()
    {
        var inner = new InMemorySource("My Set", Decisions(3));
        var source = new ShuffledProblemSetSource(inner, seed: 1);

        Assert.Equal("My Set", source.Name);
    }

    [Fact]
    public void Count_PassesThroughFromInner()
    {
        var inner = new InMemorySource("Test", Decisions(9));
        var source = new ShuffledProblemSetSource(inner, seed: 1);

        Assert.Equal(9, source.Count);
    }

    [Fact]
    public void Count_PassesThroughNull_WhenInnerIsStreaming()
    {
        var streamingInner = new InMemorySource("Test", Decisions(4), count: null);
        var source = new ShuffledProblemSetSource(streamingInner, seed: 1);

        Assert.Null(source.Count);
    }

    [Fact]
    public async Task EnumerateAsync_HonoursCancellation()
    {
        var inner = new InMemorySource("Test", Decisions(5));
        var source = new ShuffledProblemSetSource(inner, seed: 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in source.EnumerateAsync(cts.Token)) { }
        });
    }

    [Fact]
    public void Constructor_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ShuffledProblemSetSource(null!, seed: 1));
        Assert.Throws<ArgumentNullException>(() => new ShuffledProblemSetSource(null!));
    }
}
