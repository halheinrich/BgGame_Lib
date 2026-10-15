namespace BgGame_Lib.Tests;

using System.Runtime.CompilerServices;
using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

/// <summary>
/// The producer half of SPEC-quiz-history.md §8, "Releasing never
/// misreports": an inner stream's release never replaces a draw's outcome,
/// in any decorator, on any path. A draw that faulted throws its own fault,
/// and a stream that ran out reports its end; what the inner stream's
/// release failed with comes from the decorator's own <c>DisposeAsync</c>,
/// through any depth of nesting. The release itself still happens where it
/// always did, once. The internal <see cref="InnerReleases"/> is the rule's
/// one definition; its own edges, which no decorator reaches, close the
/// class.
/// </summary>
public class InnerReleasesTests
{
    /// <summary>Each way a decorator draws from its inner stream.</summary>
    public enum DecoratorPath
    {
        /// <summary>The position-distinct decorator, which materializes.</summary>
        Distinct,

        /// <summary>The plain order as read, which streams.</summary>
        OrderedAsRead,

        /// <summary>The plain order under any other order, which materializes.</summary>
        OrderedMaterializing,

        /// <summary>The composer's blank-mix delegation as read, which streams.</summary>
        MixedBlankAsRead,

        /// <summary>The composer's blank-mix delegation under any other order, which materializes.</summary>
        MixedBlankMaterializing,

        /// <summary>The composer's composition, which materializes.</summary>
        MixedActive,
    }

    /// <summary>The faults a draw can end with; cancellation is one like any other.</summary>
    public enum DrawFault
    {
        /// <summary>The inner stream's draw fails.</summary>
        Failure,

        /// <summary>The consumer's token is cancelled during a draw, and the inner stream observes it.</summary>
        Cancellation,
    }

    /// <summary>How a consumer's draws from a stream end.</summary>
    public enum DrawEnd
    {
        /// <summary>The stream runs out.</summary>
        RanOut,

        /// <summary>A draw faults.</summary>
        Faulted,

        /// <summary>The consumer releases the stream after its first item.</summary>
        ReleasedEarly,
    }

    /// <summary>
    /// An inner source scripted to end its draws where asked — at the end of
    /// its items, or with a fault once <see cref="FaultAfter"/> of them are
    /// out — and, given a <see cref="ReleaseFailure"/>, to fail every release
    /// with it. Its enumeration is one object, enumerable and enumerator
    /// alike, as a compiler-generated iterator's is; the source keeps only a
    /// weak reference to it, and counts its releases.
    /// </summary>
    private sealed class ScriptedSource(IReadOnlyList<BgDecisionData> items) : IProblemSetSource
    {
        private readonly IReadOnlyList<BgDecisionData> _items = items;

        public string Name => "Scripted";

        public int? Count => null;

        /// <summary>How many items are drawn before a draw faults; null for none.</summary>
        public int? FaultAfter { get; init; }

        public DrawFault Fault { get; init; }

        /// <summary>The consumer's token source, which a <see cref="DrawFault.Cancellation"/> cancels.</summary>
        public CancellationTokenSource? Cancellation { get; init; }

        public Exception? ReleaseFailure { get; init; }

        /// <summary>What the faulting draw threw, once it has.</summary>
        public Exception? Thrown { get; private set; }

        /// <summary>How many times an enumeration of this source was released.</summary>
        public int Releases { get; private set; }

        /// <summary>The last enumeration handed out, weakly held.</summary>
        public WeakReference? Enumeration { get; private set; }

        public IAsyncEnumerable<BgDecisionData> EnumerateAsync(CancellationToken cancellationToken = default)
        {
            var enumeration = new ScriptedEnumeration(this, cancellationToken);
            Enumeration = new WeakReference(enumeration);
            return enumeration;
        }

        /// <summary>
        /// One enumeration. It observes the token it was given at
        /// <see cref="EnumerateAsync"/>, as a real source does; the
        /// decorators open it with no token of their own.
        /// </summary>
        private sealed class ScriptedEnumeration(ScriptedSource source, CancellationToken token)
            : IAsyncEnumerable<BgDecisionData>, IAsyncEnumerator<BgDecisionData>
        {
            private int _drawn;

            public BgDecisionData Current { get; private set; } = null!;

            public IAsyncEnumerator<BgDecisionData> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
                this;

            public async ValueTask<bool> MoveNextAsync()
            {
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                if (_drawn == source.FaultAfter)
                    await FaultAsync();
                if (_drawn == source._items.Count)
                    return false;
                Current = source._items[_drawn++];
                return true;
            }

            public async ValueTask DisposeAsync()
            {
                await Task.Yield();
                source.Releases++;
                if (source.ReleaseFailure is { } failure)
                    throw failure;
            }

            private async Task FaultAsync()
            {
                if (source.Fault == DrawFault.Cancellation)
                {
                    await source.Cancellation!.CancelAsync();
                    try
                    {
                        token.ThrowIfCancellationRequested();
                    }
                    catch (OperationCanceledException cancelled)
                    {
                        source.Thrown = cancelled;
                        throw;
                    }
                    throw new InvalidOperationException("The consumer's token did not reach the inner stream.");
                }

                var failure = new InvalidOperationException("The draw failed.");
                source.Thrown = failure;
                throw failure;
            }
        }
    }

    /// <summary>Deterministic <see cref="TimeProvider"/> for the composer's classification timestamp.</summary>
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// Five content-distinct decisions, so the position-distinct decorator
    /// keeps them all, and with no move number, so every order here leaves
    /// them in source order.
    /// </summary>
    private static readonly IReadOnlyList<BgDecisionData> Items =
        [.. Enumerable.Range(1, 5).Select(n => TestRecords.CheckerPlay(
            id: new XgpDecisionId($"d{n}.xgp"),
            position: TestRecords.Position(
                session: TestRecords.MatchSession(length: n, onRollNeeds: n, opponentNeeds: n))))];

    /// <summary>How many items a faulting script draws before its fault.</summary>
    private const int DrawnBeforeFault = 3;

    /// <summary>A mix that draws every decision: the residual pool alone, at 100%.</summary>
    private static readonly QuizMix Everything = new([new QuizMixEntry(QuizCategory.EverythingElse, 100)]);

    /// <summary>The decorator drawing from <paramref name="inner"/> by <paramref name="path"/>.</summary>
    private static IProblemSetSource Over(DecoratorPath path, IProblemSetSource inner) => path switch
    {
        DecoratorPath.Distinct => new DistinctPositionProblemSetSource(inner),
        DecoratorPath.OrderedAsRead =>
            new OrderedProblemSetSource(inner, ProblemOrder.AsRead, PlayRanking.Equity),
        DecoratorPath.OrderedMaterializing =>
            new OrderedProblemSetSource(inner, ProblemOrder.ByMoveNumberAscending, PlayRanking.Equity),
        DecoratorPath.MixedBlankAsRead => new MixedProblemSetSource(
            inner, ProblemOrder.AsRead, PlayRanking.Equity, QuizMix.Empty, statsProvider: null, new FixedClock()),
        DecoratorPath.MixedBlankMaterializing => new MixedProblemSetSource(
            inner, ProblemOrder.ByMoveNumberAscending, PlayRanking.Equity, QuizMix.Empty, statsProvider: null,
            new FixedClock()),
        DecoratorPath.MixedActive => new MixedProblemSetSource(
            inner, ProblemOrder.AsRead, PlayRanking.Equity, Everything, () => ProblemStatsDocument.Empty,
            new FixedClock()),
        _ => throw new ArgumentOutOfRangeException(nameof(path)),
    };

    /// <summary>
    /// Whether <paramref name="path"/> streams its inner source, holding it
    /// open to the end; every other path materializes it, releasing it before
    /// the first item.
    /// </summary>
    private static bool Streams(DecoratorPath path) =>
        path is DecoratorPath.OrderedAsRead or DecoratorPath.MixedBlankAsRead;

    /// <summary>What the stream's next draw threw, or null when it threw nothing.</summary>
    private static Task<Exception?> DrawAsync(IAsyncEnumerator<BgDecisionData> stream) =>
        Record.ExceptionAsync(() => stream.MoveNextAsync().AsTask());

    /// <summary>What releasing the stream threw, or null when it threw nothing.</summary>
    private static Task<Exception?> ReleaseAsync(IAsyncEnumerator<BgDecisionData> stream) =>
        Record.ExceptionAsync(() => stream.DisposeAsync().AsTask());

    /// <summary>
    /// Whether <paramref name="weak"/>'s target survives collection. Each try
    /// yields first, so that the draws just made have unwound: a stream's
    /// last draw can resume its consumer inline, on top of the inner stream's
    /// own completing method, whose frame holds the inner stream until it
    /// returns. A reference that something keeps survives every try.
    /// </summary>
    private static async Task<bool> SurvivesCollectionAsync(WeakReference weak)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            await Task.Yield();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            if (!weak.IsAlive)
                return false;
        }
        return true;
    }

    public static TheoryData<DecoratorPath> EveryPath => new(Enum.GetValues<DecoratorPath>());

    public static TheoryData<DecoratorPath, DrawFault> EveryPathAndFault
    {
        get
        {
            var data = new TheoryData<DecoratorPath, DrawFault>();
            foreach (var path in Enum.GetValues<DecoratorPath>())
            {
                foreach (var fault in Enum.GetValues<DrawFault>())
                    data.Add(path, fault);
            }
            return data;
        }
    }

    public static TheoryData<DecoratorPath, DrawEnd> EveryPathAndEnd
    {
        get
        {
            var data = new TheoryData<DecoratorPath, DrawEnd>();
            foreach (var path in Enum.GetValues<DecoratorPath>())
            {
                foreach (var end in Enum.GetValues<DrawEnd>())
                    data.Add(path, end);
            }
            return data;
        }
    }

    /// <summary>The composer's three paths, each over the position-distinct decorator, by every end.</summary>
    public static TheoryData<DecoratorPath, DrawEnd> EveryComposerPathAndEnd
    {
        get
        {
            var data = new TheoryData<DecoratorPath, DrawEnd>();
            DecoratorPath[] composer =
                [DecoratorPath.MixedBlankAsRead, DecoratorPath.MixedBlankMaterializing, DecoratorPath.MixedActive];
            foreach (var path in composer)
            {
                foreach (var end in Enum.GetValues<DrawEnd>())
                    data.Add(path, end);
            }
            return data;
        }
    }

    // -----------------------------------------------------------------------
    //  Through each decorator: the draw's outcome is the draw's, and the
    //  release's failure comes from the decorator's own release.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryPathAndFault))]
    public async Task AFaultedDraw_ThrowsItsOwnFault_AndTheReleaseFailureComesFromDisposeAsync(
        DecoratorPath path, DrawFault fault)
    {
        using var cancellation = new CancellationTokenSource();
        var releaseFailure = new InvalidOperationException("The release failed.");
        var inner = new ScriptedSource(Items)
        {
            FaultAfter = DrawnBeforeFault, Fault = fault, Cancellation = cancellation, ReleaseFailure = releaseFailure,
        };
        var stream = Over(path, inner).EnumerateAsync(cancellation.Token).GetAsyncEnumerator();

        // A streaming path hands over what was drawn before the fault; a
        // materializing one faults before its first item.
        for (int i = 0; i < (Streams(path) ? DrawnBeforeFault : 0); i++)
            Assert.True(await stream.MoveNextAsync());

        var thrown = await DrawAsync(stream);
        Assert.NotNull(inner.Thrown);
        Assert.Same(inner.Thrown, thrown);
        Assert.Equal(1, inner.Releases);

        Assert.Same(releaseFailure, await ReleaseAsync(stream));
        Assert.Equal(1, inner.Releases);
    }

    [Theory]
    [MemberData(nameof(EveryPath))]
    public async Task AStreamThatRanOut_ReportsItsEnd_AndTheReleaseFailureComesFromDisposeAsync(DecoratorPath path)
    {
        var releaseFailure = new InvalidOperationException("The release failed.");
        var inner = new ScriptedSource(Items) { ReleaseFailure = releaseFailure };
        var stream = Over(path, inner).EnumerateAsync().GetAsyncEnumerator();

        var handed = new List<BgDecisionData>();
        while (await stream.MoveNextAsync())
            handed.Add(stream.Current);

        Assert.Equal(Items, handed);
        Assert.Equal(1, inner.Releases);

        Assert.Same(releaseFailure, await ReleaseAsync(stream));
        Assert.Equal(1, inner.Releases);
    }

    [Theory]
    [MemberData(nameof(EveryPath))]
    public async Task AnEarlyRelease_ThrowsTheInnerReleaseFailure(DecoratorPath path)
    {
        var releaseFailure = new InvalidOperationException("The release failed.");
        var inner = new ScriptedSource(Items) { ReleaseFailure = releaseFailure };
        var stream = Over(path, inner).EnumerateAsync().GetAsyncEnumerator();

        Assert.True(await stream.MoveNextAsync());
        Assert.Same(Items[0], stream.Current);
        Assert.Equal(Streams(path) ? 0 : 1, inner.Releases);

        Assert.Same(releaseFailure, await ReleaseAsync(stream));
        Assert.Equal(1, inner.Releases);
    }

    [Theory]
    [MemberData(nameof(EveryComposerPathAndEnd))]
    public async Task ANestedStack_CarriesTheInnermostReleaseFailure_ToTheOutermostRelease(
        DecoratorPath composer, DrawEnd end)
    {
        var releaseFailure = new InvalidOperationException("The release failed.");
        var inner = new ScriptedSource(Items)
        {
            FaultAfter = end == DrawEnd.Faulted ? DrawnBeforeFault : null, ReleaseFailure = releaseFailure,
        };
        var stream = Over(composer, new DistinctPositionProblemSetSource(inner)).EnumerateAsync().GetAsyncEnumerator();

        // The position-distinct decorator materializes, so whatever the
        // composer's path, the innermost stream is released before the
        // first item, and a fault surfaces at the first draw.
        switch (end)
        {
            case DrawEnd.RanOut:
                var handed = new List<BgDecisionData>();
                while (await stream.MoveNextAsync())
                    handed.Add(stream.Current);
                Assert.Equal(Items, handed);
                break;
            case DrawEnd.Faulted:
                var thrown = await DrawAsync(stream);
                Assert.NotNull(inner.Thrown);
                Assert.Same(inner.Thrown, thrown);
                break;
            case DrawEnd.ReleasedEarly:
                Assert.True(await stream.MoveNextAsync());
                Assert.Same(Items[0], stream.Current);
                break;
        }
        Assert.Equal(1, inner.Releases);

        Assert.Same(releaseFailure, await ReleaseAsync(stream));
        Assert.Equal(1, inner.Releases);
    }

    // -----------------------------------------------------------------------
    //  What is deferred is the report, never the release: the inner stream
    //  is released where it always was, once, and only the failure is kept.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryPathAndEnd))]
    public async Task TheInnerStream_IsReleasedOnce_WhereItAlwaysWas(DecoratorPath path, DrawEnd end)
    {
        var inner = new ScriptedSource(Items) { FaultAfter = end == DrawEnd.Faulted ? DrawnBeforeFault : null };
        var stream = Over(path, inner).EnumerateAsync().GetAsyncEnumerator();

        // A materializing path releases its inner stream before its first
        // item; a streaming one holds it open until it runs out or faults.
        int openWhileHanding = Streams(path) ? 0 : 1;
        switch (end)
        {
            case DrawEnd.RanOut:
                for (int i = 0; i < Items.Count; i++)
                {
                    Assert.True(await stream.MoveNextAsync());
                    Assert.Equal(openWhileHanding, inner.Releases);
                }
                Assert.False(await stream.MoveNextAsync());
                Assert.Equal(1, inner.Releases);
                break;
            case DrawEnd.Faulted:
                for (int i = 0; i < (Streams(path) ? DrawnBeforeFault : 0); i++)
                {
                    Assert.True(await stream.MoveNextAsync());
                    Assert.Equal(0, inner.Releases);
                }
                var thrown = await DrawAsync(stream);
                Assert.NotNull(inner.Thrown);
                Assert.Same(inner.Thrown, thrown);
                Assert.Equal(1, inner.Releases);
                break;
            case DrawEnd.ReleasedEarly:
                Assert.True(await stream.MoveNextAsync());
                Assert.Equal(openWhileHanding, inner.Releases);
                break;
        }

        Assert.Null(await ReleaseAsync(stream));
        Assert.Equal(1, inner.Releases);
    }

    [Theory]
    [MemberData(nameof(EveryPath))]
    public async Task AFailedRelease_KeepsItsFailure_NotTheInnerStream(DecoratorPath path)
    {
        var releaseFailure = new InvalidOperationException("The release failed.");
        var inner = new ScriptedSource(Items) { ReleaseFailure = releaseFailure };
        var stream = Over(path, inner).EnumerateAsync().GetAsyncEnumerator();

        // Draw to where the inner stream has been released, with the
        // decorator's own stream still open: the first item when the path
        // materializes, the end when it streams.
        if (Streams(path))
        {
            while (await stream.MoveNextAsync()) { }
        }
        else
        {
            Assert.True(await stream.MoveNextAsync());
        }
        Assert.Equal(1, inner.Releases);

        Assert.False(await SurvivesCollectionAsync(inner.Enumeration!));
        Assert.Same(releaseFailure, await ReleaseAsync(stream));
    }

    [Theory]
    [MemberData(nameof(EveryPath))]
    public async Task TheReleaseFailure_IsReportedOnce(DecoratorPath path)
    {
        var releaseFailure = new InvalidOperationException("The release failed.");
        var inner = new ScriptedSource(Items) { ReleaseFailure = releaseFailure };
        var stream = Over(path, inner).EnumerateAsync().GetAsyncEnumerator();
        while (await stream.MoveNextAsync()) { }

        Assert.Same(releaseFailure, await ReleaseAsync(stream));
        Assert.Null(await ReleaseAsync(stream));
        Assert.Equal(1, inner.Releases);
    }

    [Theory]
    [MemberData(nameof(EveryPath))]
    public async Task AReleaseBeforeAnyDraw_OpensNothing_AndReportsNothing(DecoratorPath path)
    {
        var inner = new ScriptedSource(Items) { ReleaseFailure = new InvalidOperationException("The release failed.") };
        var stream = Over(path, inner).EnumerateAsync().GetAsyncEnumerator();

        Assert.Null(await ReleaseAsync(stream));
        Assert.Null(inner.Enumeration);
        Assert.Equal(0, inner.Releases);
    }

    // -----------------------------------------------------------------------
    //  Nothing else changes: a token given when the stream is opened is
    //  honoured beside one given to EnumerateAsync.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryPath))]
    public async Task ATokenGivenWhenTheStreamIsOpened_IsHonoured(DecoratorPath path)
    {
        using var atEnumerate = new CancellationTokenSource();
        using var atOpen = new CancellationTokenSource();
        await atOpen.CancelAsync();
        var source = Over(path, new ScriptedSource(Items));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in source.EnumerateAsync(atEnumerate.Token).WithCancellation(atOpen.Token)) { }
        });
    }

    [Theory]
    [MemberData(nameof(EveryPath))]
    public async Task ATokenGivenToEnumerateAsync_IsHonouredBesideOneGivenWhenTheStreamIsOpened(DecoratorPath path)
    {
        using var atEnumerate = new CancellationTokenSource();
        using var atOpen = new CancellationTokenSource();
        await atEnumerate.CancelAsync();
        var source = Over(path, new ScriptedSource(Items));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in source.EnumerateAsync(atEnumerate.Token).WithCancellation(atOpen.Token)) { }
        });
    }

    // -----------------------------------------------------------------------
    //  The helper's own edges, which no decorator reaches: an iterator that
    //  draws from more than one inner stream, or whose own release fails, and
    //  a drawn stream used after its release.
    // -----------------------------------------------------------------------

    /// <summary>Draws from each of <paramref name="sources"/> in turn, through <paramref name="releases"/>.</summary>
    private static async IAsyncEnumerable<BgDecisionData> EachInTurnAsync(
        InnerReleases releases, IReadOnlyList<IProblemSetSource> sources,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var source in sources)
        {
            await foreach (var item in releases.Draw(source.EnumerateAsync(cancellationToken)))
                yield return item;
        }
    }

    /// <summary>
    /// Draws from <paramref name="source"/> through <paramref name="releases"/>;
    /// its own release then fails with <paramref name="ownFailure"/>.
    /// </summary>
    private static async IAsyncEnumerable<BgDecisionData> FailingItsOwnReleaseAsync(
        InnerReleases releases, IProblemSetSource source, Exception ownFailure,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in releases.Draw(source.EnumerateAsync(cancellationToken)))
                yield return item;
        }
        finally
        {
            throw ownFailure;
        }
    }

    [Fact]
    public async Task SeveralReleaseFailures_AreReportedTogether_InTheOrderTheyHappened()
    {
        var first = new InvalidOperationException("The first release failed.");
        var second = new InvalidOperationException("The second release failed.");
        IProblemSetSource[] sources =
        [
            new ScriptedSource(Items) { ReleaseFailure = first },
            new ScriptedSource(Items) { ReleaseFailure = second },
        ];
        var stream = InnerReleases.ReportedOnRelease(
            (releases, cancellationToken) => EachInTurnAsync(releases, sources, cancellationToken),
            CancellationToken.None).GetAsyncEnumerator();

        var handed = new List<BgDecisionData>();
        while (await stream.MoveNextAsync())
            handed.Add(stream.Current);
        Assert.Equal([.. Items, .. Items], handed);

        var reported = Assert.IsType<AggregateException>(await ReleaseAsync(stream));
        Assert.Collection(
            reported.InnerExceptions,
            failure => Assert.Same(first, failure),
            failure => Assert.Same(second, failure));
    }

    [Fact]
    public async Task TheIteratorsOwnReleaseFailure_IsReportedAfterTheInnerOnes()
    {
        var innerFailure = new InvalidOperationException("The inner release failed.");
        var ownFailure = new InvalidOperationException("The iterator's own release failed.");
        var inner = new ScriptedSource(Items) { ReleaseFailure = innerFailure };
        var stream = InnerReleases.ReportedOnRelease(
            (releases, cancellationToken) => FailingItsOwnReleaseAsync(releases, inner, ownFailure, cancellationToken),
            CancellationToken.None).GetAsyncEnumerator();

        Assert.True(await stream.MoveNextAsync());

        var reported = Assert.IsType<AggregateException>(await ReleaseAsync(stream));
        Assert.Collection(
            reported.InnerExceptions,
            failure => Assert.Same(innerFailure, failure),
            failure => Assert.Same(ownFailure, failure));
        Assert.Equal(1, inner.Releases);
    }

    [Fact]
    public async Task ADrawnStream_IsReleasedOnce_AndCannotBeDrawnFromAfter()
    {
        InnerReleases? captured = null;
        var stream = InnerReleases.ReportedOnRelease(
            (releases, cancellationToken) =>
            {
                captured = releases;
                return EachInTurnAsync(releases, [], cancellationToken);
            },
            CancellationToken.None).GetAsyncEnumerator();
        Assert.NotNull(captured);
        var releaseFailure = new InvalidOperationException("The release failed.");
        var inner = new ScriptedSource(Items) { ReleaseFailure = releaseFailure };
        var drawn = captured.Draw(inner.EnumerateAsync()).GetAsyncEnumerator();
        Assert.True(await drawn.MoveNextAsync());

        await drawn.DisposeAsync();
        await drawn.DisposeAsync();
        Assert.Equal(1, inner.Releases);
        Assert.Throws<ObjectDisposedException>(() => drawn.Current);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => drawn.MoveNextAsync().AsTask());

        Assert.Same(releaseFailure, await ReleaseAsync(stream));
    }
}
