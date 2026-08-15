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

    private static PositionData Position(int n) => new()
    {
        Mop = [0, -2, 0, 0, 0, 0, 5, 0, 3, 0, 0, 0, -5, 5, 0, 0, 0, -3, 0, -5, 0, 0, 0, 0, 2, 0],
        OnRollNeeds = n,
        OpponentNeeds = n,
        CubeOwner = CubeOwner.Centered,
    };

    /// <summary>
    /// Synthesized decision: distinct filenames give distinct (file-relative)
    /// ids; dedupe is by derived <see cref="ProblemKey"/>, so content
    /// identity is the real facts — a standard starting board distinguished
    /// by the away-scores pair <paramref name="position"/>. The XGID string
    /// and match length are deliberately free-floating provenance: identity
    /// participates in neither.
    /// </summary>
    private static BgDecisionData Decision(
        string file, int position, string xgid = "", int matchLength = 0, bool isCube = false) => new()
    {
        Id = new XgpDecisionId(file),
        Xgid = xgid,
        Position = Position(position),
        Decision = isCube
            ? new DecisionData { IsCube = true }
            : new DecisionData { Dice = [3, 1] },
        Descriptive = new DescriptiveData { MatchLength = matchLength },
        Outcome = new PlayOutcomeData(),
    };

    /// <summary>
    /// A decision with real position facts but unstamped dice: no derivable
    /// <see cref="ProblemKey"/> — the no-key rung.
    /// </summary>
    private static BgDecisionData NoKey(string file, int position = 1) => new()
    {
        Id = new XgpDecisionId(file),
        Position = Position(position),
        Decision = new DecisionData(),   // dice default to {0,0}: underivable
        Descriptive = new DescriptiveData(),
        Outcome = new PlayOutcomeData(),
    };

    private static ProblemKey KeyOf(BgDecisionData item)
    {
        Assert.True(ProblemKey.TryDerive(item, out var key));
        return key!;
    }

    private static async Task<List<DecisionId>> IdsAsync(IProblemSetSource source, CancellationToken ct = default)
    {
        var ids = new List<DecisionId>();
        await foreach (var item in source.EnumerateAsync(ct))
            ids.Add(item.Id);
        return ids;
    }

    private static DistinctPositionProblemSetSource Distinct(IReadOnlyList<BgDecisionData> items) =>
        new(new InMemorySource("Test", items));

    // -----------------------------------------------------------------------
    //  Collapse by content identity
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DuplicatePositions_AcrossDistinctIds_CollapseToFirstOccurrence()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 1),
            Decision("c.xgp", position: 2),
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
            Decision("match-copy1.xgp", position: 1),
            Decision("match-copy1(2).xgp", position: 2),
            Decision("match-copy2.xgp", position: 1),
            Decision("match-copy2(2).xgp", position: 2),
        });

        Assert.Equal(
            new[] { Id("match-copy1.xgp"), Id("match-copy1(2).xgp") },
            await IdsAsync(source));
    }

    [Fact]
    public async Task SameAwayScores_AcrossMatchLengths_Collapse()
    {
        // The spec's first new collapse case: away-scores determine identity,
        // not match length — 5-away/5-away is the same problem in a 7-pointer
        // and an 11-pointer. The distinct XGID strings (which encode raw
        // scores) pin that the raw XGID is no longer the dedupe key.
        var source = Distinct(new[]
        {
            Decision("seven.xgp", position: 5, xgid: "XGID=from-7pt", matchLength: 7),
            Decision("eleven.xgp", position: 5, xgid: "XGID=from-11pt", matchLength: 11),
        });

        Assert.Equal(new[] { Id("seven.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task MirrorTurnDuplicates_Collapse()
    {
        // The spec's second new collapse case: the board is on-roll-relative,
        // so the mirrored seating of the same problem — a different XGID,
        // which encodes the turn — carries identical facts and collapses.
        var source = Distinct(new[]
        {
            Decision("as-player1.xgp", position: 3, xgid: "XGID=turn:0"),
            Decision("as-player2.xgp", position: 3, xgid: "XGID=turn:1"),
        });

        Assert.Equal(new[] { Id("as-player1.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task SamePosition_PlayAndCubeDecisions_StayDistinct()
    {
        // The kind discriminant: a play key carries dice, a cube key none, so
        // the same position quizzed both ways is two problems.
        var source = Distinct(new[]
        {
            Decision("play.xgp", position: 1),
            Decision("cube.xgp", position: 1, isCube: true),
        });

        Assert.Equal(new[] { Id("play.xgp"), Id("cube.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task AllDuplicates_CollapseToOne()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 1),
            Decision("c.xgp", position: 1),
            Decision("d.xgp", position: 1),
        });

        Assert.Equal(new[] { Id("a.xgp") }, await IdsAsync(source));
    }

    // -----------------------------------------------------------------------
    //  The no-key rung: fail-open pass-through
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NoDerivableKey_PassesThrough_EvenWhenIdentical()
    {
        // Fail-open guard: an underivable key carries no content claim, so
        // identical no-key items must never collapse.
        var source = Distinct(new[]
        {
            NoKey("a.xgp"),
            NoKey("b.xgp"),
            NoKey("c.xgp"),
        });

        Assert.Equal(
            new[] { Id("a.xgp"), Id("b.xgp"), Id("c.xgp") },
            await IdsAsync(source));
    }

    [Fact]
    public async Task Output_PreservesFirstOccurrenceOrder_AcrossUniqueDuplicateAndNoKey()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", position: 1),
            NoKey("nokey1.xgp"),
            Decision("b.xgp", position: 2),
            Decision("dup.xgp", position: 1),
            NoKey("nokey2.xgp"),
            Decision("c.xgp", position: 3),
        });

        Assert.Equal(
            new[] { Id("a.xgp"), Id("nokey1.xgp"), Id("b.xgp"), Id("nokey2.xgp"), Id("c.xgp") },
            await IdsAsync(source));
    }

    // -----------------------------------------------------------------------
    //  Duplicate-class telemetry
    // -----------------------------------------------------------------------

    [Fact]
    public void LastDuplicateClasses_IsNullBeforeFirstEnumeration()
    {
        var source = Distinct(new[] { Decision("a.xgp", position: 1) });

        Assert.Null(source.LastDuplicateClasses);
    }

    [Fact]
    public async Task LastDuplicateClasses_HoldsOnlyMultiMemberClasses_MembersInOccurrenceOrder()
    {
        // Position 1 has three copies, position 2 has two, position 3 is
        // unique: exactly two classes, in first-occurrence order, survivor
        // leading each member list — singletons never appear.
        var a = Decision("a.xgp", position: 1);
        var source = Distinct(new[]
        {
            a,
            Decision("b.xgp", position: 2),
            Decision("c.xgp", position: 1),
            Decision("d.xgp", position: 3),
            Decision("e.xgp", position: 2),
            Decision("f.xgp", position: 1),
        });

        await IdsAsync(source);

        var classes = source.LastDuplicateClasses!;
        Assert.Equal(2, classes.Count);

        Assert.Equal(KeyOf(a), classes[0].Key);
        Assert.Equal(new[] { Id("a.xgp"), Id("c.xgp"), Id("f.xgp") }, classes[0].Members);
        Assert.Equal(new[] { Id("b.xgp"), Id("e.xgp") }, classes[1].Members);
    }

    [Fact]
    public async Task LastDuplicateClasses_EmptyWhenNothingCollapsed()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 2),
        });

        await IdsAsync(source);

        Assert.NotNull(source.LastDuplicateClasses);
        Assert.Empty(source.LastDuplicateClasses);
    }

    [Fact]
    public async Task NoKeyItems_NeverFormADuplicateClass()
    {
        // The identical no-key items pass through (fail-open) and must not
        // surface as a "duplicate class" either — no key, no content claim.
        var source = Distinct(new[] { NoKey("a.xgp"), NoKey("b.xgp") });

        await IdsAsync(source);

        Assert.Empty(source.LastDuplicateClasses!);
    }

    [Fact]
    public async Task LastDuplicateClasses_IsAvailableAtTheFirstItem()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 1),
        });

        Assert.Null(source.LastDuplicateClasses);

        await foreach (var _ in source.EnumerateAsync())
        {
            Assert.NotNull(source.LastDuplicateClasses);
            break;
        }
    }

    [Fact]
    public async Task LastDuplicateClasses_IsRecomputedPerEnumeration()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 1),
        });

        await IdsAsync(source);
        var first = source.LastDuplicateClasses;
        await IdsAsync(source);
        var second = source.LastDuplicateClasses;

        Assert.NotSame(first, second);
        var cls = Assert.Single(second!);
        Assert.Equal(new[] { Id("a.xgp"), Id("b.xgp") }, cls.Members);
    }

    // -----------------------------------------------------------------------
    //  Contract plumbing
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EmptySource_YieldsEmpty()
    {
        var source = Distinct(Array.Empty<BgDecisionData>());

        Assert.Empty(await IdsAsync(source));
        Assert.Empty(source.LastDuplicateClasses!);
    }

    [Fact]
    public async Task SingleItem_PassesThrough()
    {
        var source = Distinct(new[] { Decision("a.xgp", position: 1) });

        Assert.Equal(new[] { Id("a.xgp") }, await IdsAsync(source));
    }

    [Fact]
    public async Task Reiteration_YieldsSameResult()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 1),
            Decision("c.xgp", position: 2),
        });

        Assert.Equal(await IdsAsync(source), await IdsAsync(source));
    }

    [Fact]
    public void Name_PassesThroughFromInner()
    {
        var inner = new InMemorySource("My Set", new[] { Decision("a.xgp", position: 1) });
        var source = new DistinctPositionProblemSetSource(inner);

        Assert.Equal("My Set", source.Name);
    }

    [Fact]
    public void Count_IsNull_EvenWhenInnerCountIsKnown()
    {
        // How many problems collapse is unknowable before enumeration, and a
        // non-null Count must equal the yielded item count — so always null.
        var inner = new InMemorySource("Test", new[]
        {
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 1),
        });
        var source = new DistinctPositionProblemSetSource(inner);

        Assert.Equal(2, inner.Count);
        Assert.Null(source.Count);
    }

    [Fact]
    public async Task EnumerateAsync_HonoursCancellation()
    {
        var source = Distinct(new[]
        {
            Decision("a.xgp", position: 1),
            Decision("b.xgp", position: 2),
        });
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
    }
}
