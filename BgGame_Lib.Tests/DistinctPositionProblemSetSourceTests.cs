namespace BgGame_Lib.Tests;

using System.Runtime.CompilerServices;
using BgDataTypes_Lib;

public class DistinctPositionProblemSetSourceTests
{
    /// <summary>In-memory source with a fixed item list, re-iterable from the start.</summary>
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
                await Task.Yield();
            }
        }
    }

    private static DecisionId Id(string file) => new XgpDecisionId(file);

    /// <summary>
    /// Synthesized decision: distinct filenames give distinct (file-relative)
    /// ids; a shared xgid makes copies content-equal.
    /// </summary>
    private static BgDecisionData Decision(string file, string xgid) => new()
    {
        Id = new XgpDecisionId(file),
        Xgid = xgid,
        Position = new PositionData
        {
            Mop = new int[26],
            CubeOwner = CubeOwner.Centered,
        },
        Decision = new DecisionData(),
        Descriptive = new DescriptiveData(),
        Outcome = new PlayOutcomeData(),
    };

    private static async Task<List<DecisionId>> IdsAsync(IProblemSetSource source, CancellationToken ct = default)
    {
        var ids = new List<DecisionId>();
        await foreach (var item in source.EnumerateAsync(ct))
            ids.Add(item.Id);
        return ids;
    }

    private static DistinctPositionProblemSetSource Distinct(
        IReadOnlyList<BgDecisionData> items, Func<DecisionId, bool>? survivorPreference = null) =>
        new(new InMemorySource("Test", items), survivorPreference);

    [Fact]
    public async Task DuplicateXgids_AcrossDistinctIds_CollapseToFirstOccurrence()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", "X1"),
            Decision("b.xgp", "X1"),
            Decision("c.xgp", "X2"),
        });

        Assert.Equal(new[] { Id("a.xgp"), Id("c.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task CrossFileDuplicates_CollapseIdentically()
    {
        // The same match streamed under two filenames: every position repeats
        // under a second file-relative identity. One copy of each survives.
        var source = Distinct(new[]
        {
            Decision("match-copy1.xgp", "X1"),
            Decision("match-copy1(2).xgp", "X2"),
            Decision("match-copy2.xgp", "X1"),
            Decision("match-copy2(2).xgp", "X2"),
        });

        Assert.Equal(
            new[] { Id("match-copy1.xgp"), Id("match-copy1(2).xgp") },
            await IdsAsync(source));
    }

    [Fact]
    public async Task Preference_LaterSatisfyingCopy_ReplacesEarlierPlainCopy_InPlace()
    {
        // The stats-bearing copy arrives after a plain copy and a filler; it
        // survives *at the plain copy's position* (rule: first content-
        // occurrence order), not at its own arrival position.
        var source = Distinct(
            new[]
            {
                Decision("plain.xgp", "X1"),
                Decision("filler.xgp", "X0"),
                Decision("stats.xgp", "X1"),
            },
            survivorPreference: id => id == Id("stats.xgp"));

        Assert.Equal(new[] { Id("stats.xgp"), Id("filler.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task Preference_BothSatisfy_FirstOccurrenceWins()
    {
        var source = Distinct(
            new[] { Decision("a.xgp", "X1"), Decision("b.xgp", "X1") },
            survivorPreference: _ => true);

        Assert.Equal(new[] { Id("a.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task Preference_NeitherSatisfies_FirstOccurrenceWins()
    {
        var source = Distinct(
            new[] { Decision("a.xgp", "X1"), Decision("b.xgp", "X1") },
            survivorPreference: _ => false);

        Assert.Equal(new[] { Id("a.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task Preference_IncumbentSatisfies_NewcomerDoesNot_IncumbentSurvives()
    {
        // The fourth cell of the contest matrix: an inverted replacement
        // condition would evict the stats-bearing incumbent here.
        var source = Distinct(
            new[] { Decision("stats.xgp", "X1"), Decision("plain.xgp", "X1") },
            survivorPreference: id => id == Id("stats.xgp"));

        Assert.Equal(new[] { Id("stats.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task Preference_ThreeCopies_FirstSatisfyingOccurrenceSurvives_AtFirstCopysPosition()
    {
        // plain, stats, stats: the *first* stats-bearing copy survives (later
        // satisfying copies tie and lose to it), placed where the plain copy
        // first occurred.
        var source = Distinct(
            new[]
            {
                Decision("plain.xgp", "X1"),
                Decision("filler.xgp", "X0"),
                Decision("stats1.xgp", "X1"),
                Decision("stats2.xgp", "X1"),
            },
            survivorPreference: id => id == Id("stats1.xgp") || id == Id("stats2.xgp"));

        Assert.Equal(new[] { Id("stats1.xgp"), Id("filler.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task NullPreference_FirstOccurrenceWins()
    {
        var source = Distinct(
            new[] { Decision("a.xgp", "X1"), Decision("b.xgp", "X1") },
            survivorPreference: null);

        Assert.Equal(new[] { Id("a.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task NoPreferenceConstructor_FirstOccurrenceWins()
    {
        var inner = new InMemorySource("Test", new[]
        {
            Decision("a.xgp", "X1"),
            Decision("b.xgp", "X1"),
        });
        var source = new DistinctPositionProblemSetSource(inner);

        Assert.Equal(new[] { Id("a.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task MissingXgid_PassesThrough_EvenWhenIdentical()
    {
        // Fail-open guard: absent digests carry no content claim, so items
        // sharing a null / empty / whitespace Xgid must never collapse.
        var source = Distinct(new[]
        {
            Decision("a.xgp", ""),
            Decision("b.xgp", ""),
            Decision("c.xgp", "   "),
            Decision("d.xgp", "   "),
            Decision("e.xgp", null!),  // defensive: e.g. deserialized wire data
        });

        Assert.Equal(
            new[] { Id("a.xgp"), Id("b.xgp"), Id("c.xgp"), Id("d.xgp"), Id("e.xgp") },
            await IdsAsync(source));
    }

    [Fact]
    public async Task EmptySource_YieldsEmpty()
    {
        var source = Distinct(Array.Empty<BgDecisionData>());

        Assert.Empty(await IdsAsync(source));
    }

    [Fact]
    public async Task SingleItem_PassesThrough()
    {
        var source = Distinct(new[] { Decision("a.xgp", "X1") });

        Assert.Equal(new[] { Id("a.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task AllDuplicates_CollapseToOne()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", "X1"),
            Decision("b.xgp", "X1"),
            Decision("c.xgp", "X1"),
            Decision("d.xgp", "X1"),
        });

        Assert.Equal(new[] { Id("a.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task AllMissingXgid_AllPassThrough_InOrder()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", ""),
            Decision("b.xgp", ""),
            Decision("c.xgp", ""),
        });

        Assert.Equal(new[] { Id("a.xgp"), Id("b.xgp"), Id("c.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task Output_PreservesFirstOccurrenceOrder_AcrossUniqueDuplicateAndMissing()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", "X1"),
            Decision("noid1.xgp", ""),
            Decision("b.xgp", "X2"),
            Decision("dup.xgp", "X1"),
            Decision("noid2.xgp", ""),
            Decision("c.xgp", "X3"),
        });

        Assert.Equal(
            new[] { Id("a.xgp"), Id("noid1.xgp"), Id("b.xgp"), Id("noid2.xgp"), Id("c.xgp") },
            await IdsAsync(source));
    }

    [Fact]
    public async Task Reiteration_YieldsSameResult()
    {
        var source = Distinct(
            new[] { Decision("a.xgp", "X1"), Decision("b.xgp", "X1"), Decision("c.xgp", "X2") },
            survivorPreference: id => id == Id("b.xgp"));

        Assert.Equal(await IdsAsync(source), await IdsAsync(source));
    }

    [Fact]
    public async Task Reiteration_ReevaluatesPreferenceFresh()
    {
        // Restart semantics: stats recorded between enumerations change the
        // survivor on the next pass — no survivor list is cached.
        var preferredId = default(DecisionId);
        var source = Distinct(
            new[] { Decision("a.xgp", "X1"), Decision("b.xgp", "X1") },
            survivorPreference: id => id == preferredId);

        Assert.Equal(new[] { Id("a.xgp") }, await IdsAsync(source));

        preferredId = Id("b.xgp");
        Assert.Equal(new[] { Id("b.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public void Name_PassesThroughFromInner()
    {
        var inner = new InMemorySource("My Set", new[] { Decision("a.xgp", "X1") });
        var source = new DistinctPositionProblemSetSource(inner);

        Assert.Equal("My Set", source.Name);
    }

    [Fact]
    public void Count_IsNull_EvenWhenInnerCountIsKnown()
    {
        // How many positions collapse is unknowable before enumeration, and a
        // non-null Count must equal the yielded item count — so always null.
        var inner = new InMemorySource("Test", new[]
        {
            Decision("a.xgp", "X1"),
            Decision("b.xgp", "X1"),
        });
        var source = new DistinctPositionProblemSetSource(inner);

        Assert.Equal(2, inner.Count);
        Assert.Null(source.Count);
    }

    [Fact]
    public async Task EnumerateAsync_HonoursCancellation()
    {
        var source = Distinct(new[] { Decision("a.xgp", "X1"), Decision("b.xgp", "X2") });
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
        Assert.Throws<ArgumentNullException>(() => new DistinctPositionProblemSetSource(null!));
        Assert.Throws<ArgumentNullException>(
            () => new DistinctPositionProblemSetSource(null!, survivorPreference: _ => true));
    }
}
