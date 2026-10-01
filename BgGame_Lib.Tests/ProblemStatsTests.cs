namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

public class ProblemStatsTests
{
    // Canonical key fixtures: the ratified wire grammar (board/score/cube[/dice]),
    // standard starting board. A cube key is a play key without the dice field —
    // the kind discriminant — so CubeKey and Key name the same position's two
    // distinct decision kinds.
    private const string Board = "0,-2,0,0,0,0,5,0,3,0,0,0,-5,5,0,0,0,-3,0,-5,0,0,0,0,2,0";

    private static readonly ProblemKey Key = ProblemKey.Parse($"{Board}/7a7/1c/31");
    private static readonly ProblemKey OtherKey = ProblemKey.Parse($"{Board}/5a3/2o/65");
    private static readonly ProblemKey CubeKey = ProblemKey.Parse($"{Board}/7a7/1c");

    private static readonly DateTimeOffset T1 = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    // Scored as production scores it; correct exactly when the error counts as
    // zero. A submission's key is its record's, so each helper builds the
    // problem whose key it folds under: Play's spells Key, OtherPlay's
    // OtherKey, Cube's CubeKey, OtherCube's another cube key.
    private static SubmittedPlay Play(double equityLoss) =>
        TestSubmissions.Play(equityLoss);

    private static SubmittedPlay OtherPlay(double equityLoss) =>
        TestSubmissions.Play(
            equityLoss,
            TestRecords.Position(
                cubeSize: 2,
                cubeOwner: CubeOwner.OnRoll,
                session: TestRecords.MatchSession(onRollNeeds: 5, opponentNeeds: 3)),
            dice: [6, 5]);

    /// <summary>
    /// <paramref name="answer"/> scored at the standard start, 7-away/7-away,
    /// the cube centred on 1 — the problem <see cref="CubeKey"/> spells —
    /// where gammons are possible, at a decision with the given equities.
    /// </summary>
    private static SubmittedCubeAnswer Cube(CubeAnswer answer, double noDoubleEquity, double doubleTakeEquity) =>
        TestSubmissions.Cube(answer, noDoubleEquity, doubleTakeEquity, TestRecords.Position());

    /// <summary><paramref name="answer"/> at another cube problem: the standard start at 5-away/3-away.</summary>
    private static SubmittedCubeAnswer OtherCube(CubeAnswer answer) =>
        TestSubmissions.Cube(
            answer, 0.30, 0.60,
            TestRecords.Position(session: TestRecords.MatchSession(onRollNeeds: 5, opponentNeeds: 3)));

    [Fact]
    public void FromPlay_Correct_OneSubmittedOneCorrectNoLoss()
    {
        var s = ProblemStats.From(Play(equityLoss: 0.0), T1);

        Assert.Equal(Key, s.Key);
        Assert.Equal(new ScoreSegment(Submitted: 1, Correct: 1, TotalEquityLoss: 0.0), s.Tally);
        Assert.Equal(0, s.Wrong);
        Assert.Equal(T1, s.LastQuizzed);
    }

    [Fact]
    public void FromPlay_Incorrect_OneSubmittedZeroCorrectWithLoss()
    {
        var s = ProblemStats.From(Play(equityLoss: 0.042), T1);

        Assert.Equal(1, s.Tally.Submitted);
        Assert.Equal(0, s.Tally.Correct);
        Assert.Equal(0.042, s.Tally.TotalEquityLoss, precision: 9);
        Assert.Equal(1, s.Wrong);
    }

    // ---------------------------------------------------------------------
    //  A cube answer adds two decisions (SPEC-scoring §3, 2026-10-01; v3 kept)
    // ---------------------------------------------------------------------

    [Fact]
    public void Fixtures_EachCubeAnswerCarriesTheKeyItsProblemSpells()
    {
        // So a key assertion below can never pass on a mis-built fixture.
        Assert.Equal(CubeKey, Cube(CubeAnswer.DoubleTake, 0.30, 0.60).ProblemKey);
        Assert.NotEqual(CubeKey, OtherCube(CubeAnswer.DoubleTake).ProblemKey);
    }

    /// <summary>
    /// Every answer at a Double / Take truth (N +0.30, T +0.60), gammons
    /// possible, adds two decisions: the doubling half and the take half, each
    /// correct when its part counts as zero, and the whole cost as the loss.
    /// The parts: No double (0.30, 0); Double / Take (0, 0); Double / Pass
    /// (0, 0.40); Too good (0.40, 0.40).
    /// </summary>
    [Theory]
    [InlineData(CubeAnswer.NoDouble, 1, 0.30)]
    [InlineData(CubeAnswer.DoubleTake, 2, 0.0)]
    [InlineData(CubeAnswer.DoublePass, 1, 0.40)]
    [InlineData(CubeAnswer.NoDoublePass, 0, 0.80)]
    public void FromCube_EveryAnswer_AddsTwoDecisions_EachHalfJudgedOnItsPart(
        CubeAnswer answer, int correct, double loss)
    {
        var cube = Cube(answer, 0.30, 0.60);

        var s = ProblemStats.From(cube, T1);

        Assert.Equal(CubeKey, s.Key);
        Assert.Equal(2, s.Tally.Submitted);
        Assert.Equal(correct, s.Tally.Correct);
        Assert.Equal((cube.IsDoublingPartCorrect ? 1 : 0) + (cube.IsTakePartCorrect ? 1 : 0), s.Tally.Correct);
        Assert.Equal(cube.Cost.Total, s.Tally.TotalEquityLoss);
        Assert.Equal(loss, s.Tally.TotalEquityLoss, precision: 12);
        Assert.Equal(T1, s.LastQuizzed);
    }

    [Theory]
    [InlineData(0.30, 0.60)]   // Double / Take is right: the implied take is right
    [InlineData(0.50, 1.20)]   // Double / Pass is right: the implied take would be wrong
    public void FromCube_NoDouble_StillAddsTwo_ItsTakeHalfACorrectZero(double noDoubleEquity, double doubleTakeEquity)
    {
        // No double's implied take is never charged, so its take part is 0
        // and its take half a correct zero, whatever the take would have
        // cost — unlike the session's Take row, which leaves it out.
        var noDouble = Cube(CubeAnswer.NoDouble, noDoubleEquity, doubleTakeEquity);

        var s = ProblemStats.From(noDouble, T1);

        Assert.Equal(0.0, noDouble.Cost.TakePart);
        Assert.True(noDouble.IsTakePartCorrect);
        Assert.False(noDouble.IsDoublingPartCorrect);
        Assert.Equal(new ScoreSegment(2, 1, noDouble.Cost.DoublingPart), s.Tally);
        Assert.Equal(ScoreSegment.Empty, QuizScore.Empty.Plus(noDouble).TakeDecisions);
    }

    [Fact]
    public void PlusCube_OntoAnExistingRecord_KeepsEverythingItHeld_AndAddsTwoHalves()
    {
        // A record holding history folded under earlier rules: the fold adds
        // two decisions to it and touches nothing it held.
        var existing = new ProblemStats(CubeKey, new ScoreSegment(6, 3, 0.75), T1);
        var doublePass = Cube(CubeAnswer.DoublePass, 0.30, 0.60);

        var s = existing.Plus(doublePass, T2);

        Assert.Equal(new ScoreSegment(8, 4, 0.75 + doublePass.Cost.Total), s.Tally);
        Assert.Equal(T2, s.LastQuizzed);
        Assert.Equal(new ScoreSegment(6, 3, 0.75), existing.Tally);
    }

    [Fact]
    public void Plus_RepeatedFolds_AccumulateAndAdvanceLastQuizzed()
    {
        var s = ProblemStats.From(Play(equityLoss: 0.025), T1)
            .Plus(Play(equityLoss: 0.0), T2);

        Assert.Equal(2, s.Tally.Submitted);
        Assert.Equal(1, s.Tally.Correct);
        Assert.Equal(0.025, s.Tally.TotalEquityLoss, precision: 9);
        Assert.Equal(1, s.Wrong);
        Assert.Equal(T2, s.LastQuizzed);
    }

    [Fact]
    public void Plus_ReturnsNewInstance_OriginalUnchanged()
    {
        var original = ProblemStats.From(Play(equityLoss: 0.0), T1);
        var updated = original.Plus(Play(equityLoss: 0.05), T2);

        Assert.NotSame(original, updated);
        Assert.Equal(1, original.Tally.Submitted);
        Assert.Equal(T1, original.LastQuizzed);
        Assert.Equal(2, updated.Tally.Submitted);
    }

    [Fact]
    public void Fixtures_EachPlayCarriesTheKeyItsProblemSpells()
    {
        // So a key assertion below can never pass on a mis-built fixture.
        Assert.Equal(Key, Play(equityLoss: 0.0).ProblemKey);
        Assert.Equal(OtherKey, OtherPlay(equityLoss: 0.0).ProblemKey);
    }

    [Fact]
    public void PlusPlay_MismatchedKey_Throws()
    {
        var s = ProblemStats.From(Play(equityLoss: 0.0), T1);

        Assert.Throws<ArgumentException>(
            () => s.Plus(OtherPlay(equityLoss: 0.0), T2));
    }

    [Fact]
    public void PlusCube_IntoPlayKeyRecord_Throws_KindsNeverMix()
    {
        // The same position's play key and cube key are distinct by grammar
        // (the dice field), so a mixed-kind fold is a key mismatch — kind
        // purity is structural, which keeps per-kind tally derivations
        // (Sightings' divide-by-two for cubes) exact.
        var s = ProblemStats.From(Play(equityLoss: 0.0), T1);

        Assert.Throws<ArgumentException>(() => s.Plus(Cube(CubeAnswer.DoubleTake, 0.30, 0.60), T2));
    }

    [Fact]
    public void PlusCube_IntoAnotherProblemsRecord_Throws()
    {
        // Every cube answer carries its own record's key now
        // (halheinrich/backgammon#285), but a caller can still hand it to the
        // wrong record: the identity check refuses that, and the record is
        // left as it was.
        var s = ProblemStats.From(Cube(CubeAnswer.DoubleTake, 0.30, 0.60), T1);

        var thrown = Assert.Throws<ArgumentException>(() => s.Plus(OtherCube(CubeAnswer.DoubleTake), T2));

        Assert.Equal("cube", thrown.ParamName);
        Assert.Equal(new ScoreSegment(2, 2, 0.0), s.Tally);
    }

    [Fact]
    public void FromAndPlus_NullSubmissions_Throw()
    {
        var s = ProblemStats.From(Play(equityLoss: 0.0), T1);

        Assert.Throws<ArgumentNullException>(() => ProblemStats.From((SubmittedPlay)null!, T1));
        Assert.Throws<ArgumentNullException>(() => ProblemStats.From((SubmittedCubeAnswer)null!, T1));
        Assert.Throws<ArgumentNullException>(() => s.Plus((SubmittedPlay)null!, T2));
        Assert.Throws<ArgumentNullException>(() => s.Plus((SubmittedCubeAnswer)null!, T2));
    }

    // ---------------------------------------------------------------------
    //  Merge — the per-record half of ProblemStatsDocument.Merge
    //  (halheinrich/backgammon#187)
    // ---------------------------------------------------------------------

    [Fact]
    public void Merge_SumsTalliesFieldByField_AndTakesTheLaterLastQuizzed()
    {
        var mine = new ProblemStats(Key, new ScoreSegment(3, 2, 0.125), T1);
        var theirs = new ProblemStats(Key, new ScoreSegment(2, 0, 0.25), T2);

        Assert.Equal(new ProblemStats(Key, new ScoreSegment(5, 2, 0.375), T2), mine.Merge(theirs));
    }

    [Fact]
    public void Merge_TakesTheLaterLastQuizzed_WhicheverSideCarriesIt()
    {
        var earlier = new ProblemStats(Key, new ScoreSegment(1, 1, 0.0), T1);
        var later = new ProblemStats(Key, new ScoreSegment(1, 0, 0.0), T2);

        Assert.Equal(T2, earlier.Merge(later).LastQuizzed);
        Assert.Equal(T2, later.Merge(earlier).LastQuizzed);
    }

    [Fact]
    public void Merge_IsCommutative()
    {
        var a = new ProblemStats(Key, new ScoreSegment(3, 2, 0.125), T1);
        var b = new ProblemStats(Key, new ScoreSegment(2, 0, 0.25), T2);

        Assert.Equal(a.Merge(b), b.Merge(a));
    }

    [Fact]
    public void Merge_SameInstantInDifferentOffsets_IsSymmetricOnTheOffsetToo()
    {
        // Record equality compares instants, so the tie could hide an
        // order-dependent offset; the tie goes to the larger offset so the
        // serialized date is the same whichever side was the receiver.
        var utc = new ProblemStats(Key, new ScoreSegment(1, 1, 0.0), T1);
        var west = new ProblemStats(Key, new ScoreSegment(1, 0, 0.0), T1.ToOffset(TimeSpan.FromHours(-7)));

        Assert.Equal(utc.Merge(west).LastQuizzed.Offset, west.Merge(utc).LastQuizzed.Offset);
        Assert.Equal(T1, utc.Merge(west).LastQuizzed);
    }

    [Fact]
    public void Merge_LeavesBothInputsUnchanged()
    {
        var mine = new ProblemStats(Key, new ScoreSegment(3, 2, 0.125), T1);
        var theirs = new ProblemStats(Key, new ScoreSegment(2, 0, 0.25), T2);

        var merged = mine.Merge(theirs);

        Assert.NotSame(mine, merged);
        Assert.Equal(new ScoreSegment(3, 2, 0.125), mine.Tally);
        Assert.Equal(T1, mine.LastQuizzed);
        Assert.Equal(new ScoreSegment(2, 0, 0.25), theirs.Tally);
    }

    [Fact]
    public void Merge_MismatchedKey_Throws()
    {
        var s = new ProblemStats(Key, new ScoreSegment(1, 1, 0.0), T1);

        Assert.Throws<ArgumentException>(
            () => s.Merge(new ProblemStats(OtherKey, new ScoreSegment(1, 1, 0.0), T1)));
        Assert.Throws<ArgumentException>(
            () => s.Merge(new ProblemStats(CubeKey, new ScoreSegment(2, 2, 0.0), T1)));
    }

    [Fact]
    public void Merge_Null_Throws()
    {
        var s = new ProblemStats(Key, new ScoreSegment(1, 1, 0.0), T1);

        Assert.Throws<ArgumentNullException>(() => s.Merge(null!));
    }

    [Fact]
    public void RecordEquality_HoldsByValue()
    {
        var a = new ProblemStats(Key, new ScoreSegment(3, 2, 0.125), T1);
        var b = new ProblemStats(Key, new ScoreSegment(3, 2, 0.125), T1);

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { LastQuizzed = T2 });
        Assert.NotEqual(a, a with { Key = OtherKey });
    }
}
