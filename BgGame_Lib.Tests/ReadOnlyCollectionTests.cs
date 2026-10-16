namespace BgGame_Lib.Tests;

using System.Collections;
using System.Runtime.CompilerServices;
using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

/// <summary>
/// No public member hands out a live mutable collection or array behind a
/// read-only interface (halheinrich/backgammon#273's collection rider): each
/// such member hands out nothing a cast can make writable, and a record
/// holds a copy of what its caller passed, never the caller's list.
/// </summary>
public class ReadOnlyCollectionTests
{
    /// <summary>
    /// Every way a cast could write <paramref name="list"/> fails: it is no
    /// array and no <see cref="List{T}"/>, and through <see cref="IList{T}"/>
    /// and <see cref="IList"/> it is read-only — an element set included,
    /// which an array seen as <see cref="IList{T}"/> would allow.
    /// </summary>
    private static void AssertNotWritable<T>(IReadOnlyList<T> list)
    {
        Assert.NotEmpty(list);   // so the element set below is exercised
        Assert.False(list is T[], "an array's elements are writable through a cast");
        Assert.False(list is List<T>, "a List is writable through a cast");

        var generic = Assert.IsAssignableFrom<IList<T>>(list);
        Assert.True(generic.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => generic[0] = generic[0]);
        Assert.Throws<NotSupportedException>(() => generic.Add(generic[0]));
        Assert.Throws<NotSupportedException>(() => generic.Clear());

        var nonGeneric = Assert.IsAssignableFrom<IList>(list);
        Assert.True(nonGeneric.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => nonGeneric[0] = nonGeneric[0]);
    }

    private static GameRecord Game(MatchSeat winner) =>
        new(winner, new GameResult(GameResultKind.WinSingle, OnRollWon: true, CubeSize: 1), new Transcript());

    // -----------------------------------------------------------------------
    //  Transcript.Entries — a read-only view of the transcript's own list
    // -----------------------------------------------------------------------

    [Fact]
    public void TranscriptEntries_CannotBeWrittenThroughACast()
    {
        var transcript = new Transcript();
        transcript.Append(new CubeTranscriptEntry(
            GameState.NewGame(MatchState.NewMatch(7)).Snapshot(), MatchSeat.One, CubeAction.Double));

        AssertNotWritable(transcript.Entries);
        Assert.Single(transcript.Entries);
    }

    [Fact]
    public void TranscriptEntries_IsALiveView_ShowingLaterAppends()
    {
        var transcript = new Transcript();
        var entries = transcript.Entries;
        var entry = new CubeTranscriptEntry(
            GameState.NewGame(MatchState.NewMatch(7)).Snapshot(), MatchSeat.One, CubeAction.Double);

        transcript.Append(entry);

        Assert.Same(entry, Assert.Single(entries));
    }

    // -----------------------------------------------------------------------
    //  MatchResult.Games
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MatchResultGames_FromTheRunner_CannotBeWrittenThroughACast()
    {
        var runner = new MatchRunner(new SeededDiceSource(3));
        var bot = new MatchParticipant(new FirstPlayAgent(), CubeAgents.Never());

        var result = await runner.RunMatchAsync(bot, bot, matchLength: 0, maxGames: 2);

        AssertNotWritable(result.Games);
    }

    [Fact]
    public void MatchResultGames_IsACopyOfTheCallersList()
    {
        var games = new List<GameRecord> { Game(MatchSeat.One) };

        var result = new MatchResult(MatchSeat.One, 1, 0, games);
        var rebuilt = result with { Games = games };
        games.Add(Game(MatchSeat.Two));

        Assert.Single(result.Games);
        Assert.Single(rebuilt.Games);
        AssertNotWritable(result.Games);
        AssertNotWritable(rebuilt.Games);
    }

    [Fact]
    public void MatchResultGames_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MatchResult(null, 0, 0, null!));
        var result = new MatchResult(null, 0, 0, []);
        Assert.Throws<ArgumentNullException>(() => result with { Games = null! });
    }

    // -----------------------------------------------------------------------
    //  DuplicatePositionClass.Members and the source's LastDuplicateClasses
    // -----------------------------------------------------------------------

    [Fact]
    public void DuplicatePositionClassMembers_IsACopyOfTheCallersList()
    {
        var key = ProblemKey.From(TestRecords.CheckerPlay());
        var members = new List<DecisionId> { new XgpDecisionId("a.xgp"), new XgpDecisionId("b.xgp") };

        var cls = new DuplicatePositionClass(key, members);
        var rebuilt = cls with { Members = members };
        members.Add(new XgpDecisionId("c.xgp"));

        Assert.Equal(2, cls.Members.Count);
        Assert.Equal(2, rebuilt.Members.Count);
        AssertNotWritable(cls.Members);
        AssertNotWritable(rebuilt.Members);
        Assert.Throws<ArgumentNullException>(() => new DuplicatePositionClass(key, null!));
    }

    [Fact]
    public async Task DistinctProblems_ItsClassesAndTheirMembers_CannotBeWrittenThroughACast()
    {
        var distinct = await DistinctProblems.OfAsync(
        [
            TestRecords.CheckerPlay(id: new XgpDecisionId("a.xgp")),
            TestRecords.CheckerPlay(id: new XgpDecisionId("b.xgp")),
        ], new CooperativeYielder(TimeProvider.System));

        AssertNotWritable(distinct.Problems);
        AssertNotWritable(distinct.DuplicateClasses);
        AssertNotWritable(distinct.DuplicateClasses[0].Members);
    }

    // -----------------------------------------------------------------------
    //  MixComposition.Entries
    // -----------------------------------------------------------------------

    [Fact]
    public void MixCompositionEntries_IsACopyOfTheCallersArray()
    {
        MixCompositionEntry[] entries =
            [new(QuizCategory.NeverSeen, 100, PoolSize: 3, Requested: 2, Drawn: 2)];

        var composition = new MixComposition(2, HasRequestedLength: true, 2, entries);
        entries[0] = entries[0] with { Drawn = 0 };

        Assert.Equal(2, composition.Entries[0].Drawn);
        AssertNotWritable(composition.Entries);
        Assert.Throws<ArgumentNullException>(() => new MixComposition(0, false, 0, null!));
    }

    [Fact]
    public async Task ProblemSequence_ItsProblemsAndCompositionEntries_CannotBeWrittenThroughACast()
    {
        var mix = new QuizMix([new QuizMixEntry(QuizCategory.NeverSeen, 100)]);
        var sequence = await ProblemSequence.BuildAsync(
            [TestRecords.CheckerPlay()], mix, ProblemOrder.Shuffled, PlayRanking.Equity,
            ProblemStatsDocument.Empty, DateTimeOffset.UnixEpoch, new Random(1), new CooperativeYielder(TimeProvider.System));

        AssertNotWritable(sequence.Problems);
        AssertNotWritable(sequence.Composition!.Entries);
    }
}
