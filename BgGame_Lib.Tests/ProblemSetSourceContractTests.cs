namespace BgGame_Lib.Tests;

using System.Runtime.CompilerServices;
using BgDataTypes_Lib;

public class ProblemSetSourceContractTests
{
    /// <summary>
    /// In-memory fake source that re-iterates from the start on each call.
    /// Mirrors the contract that real implementations must honor.
    /// </summary>
    private sealed class InMemorySource : IProblemSetSource
    {
        private readonly IReadOnlyList<BgDecisionData> _items;
        public InMemorySource(string name, IReadOnlyList<BgDecisionData> items)
        {
            Name = name;
            _items = items;
        }

        public string Name { get; }
        public int? Count => _items.Count;

        public async IAsyncEnumerable<BgDecisionData> EnumerateAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var item in _items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();  // allow async semantics to be exercised
            }
        }
    }

    /// <summary>Fake source that does not pre-compute Count (streaming case).</summary>
    private sealed class StreamingSource : IProblemSetSource
    {
        private readonly IReadOnlyList<BgDecisionData> _items;
        public StreamingSource(IReadOnlyList<BgDecisionData> items) => _items = items;

        public string Name => "Streaming";
        public int? Count => null;

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

    [Fact]
    public async Task EnumerateAsync_YieldsItems()
    {
        var src = new InMemorySource("Test", new[] { Decision(1), Decision(2), Decision(3) });
        var collected = new List<BgDecisionData>();
        await foreach (var item in src.EnumerateAsync())
            collected.Add(item);

        Assert.Equal(3, collected.Count);
    }

    [Fact]
    public async Task EnumerateAsync_IsReIterable()
    {
        var src = new InMemorySource("Test", new[] { Decision(1), Decision(2) });

        int firstPass = 0, secondPass = 0;
        await foreach (var _ in src.EnumerateAsync()) firstPass++;
        await foreach (var _ in src.EnumerateAsync()) secondPass++;

        Assert.Equal(2, firstPass);
        Assert.Equal(2, secondPass);
    }

    [Fact]
    public async Task Count_WhenNonNull_MatchesEnumeratedItemCount()
    {
        var items = new[] { Decision(1), Decision(2), Decision(3), Decision(4) };
        var src = new InMemorySource("Test", items);

        int enumerated = 0;
        await foreach (var _ in src.EnumerateAsync()) enumerated++;

        Assert.Equal(src.Count, enumerated);
    }

    [Fact]
    public async Task Count_NullForStreaming_StillEnumerates()
    {
        var src = new StreamingSource(new[] { Decision(1), Decision(2) });

        Assert.Null(src.Count);

        int enumerated = 0;
        await foreach (var _ in src.EnumerateAsync()) enumerated++;
        Assert.Equal(2, enumerated);
    }

    [Fact]
    public async Task EnumerateAsync_HonoursCancellation()
    {
        var src = new InMemorySource("Test", new[] { Decision(1), Decision(2), Decision(3) });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in src.EnumerateAsync(cts.Token)) { }
        });
    }

    private static BgDecisionData Decision(int seed) => new()
    {
        Position = new PositionData
        {
            Mop = new int[26],
            OnRollNeeds = seed,
            OpponentNeeds = seed,
            CubeOwner = CubeOwner.Centered,
        },
        Decision = new DecisionData(),
        Descriptive = new DescriptiveData(),
        Outcome = new PlayOutcomeData(),
    };
}
