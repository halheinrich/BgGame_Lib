namespace BgGame_Lib.Tests;

using System.Globalization;
using BgDataTypes_Lib;

public class DecisionStatsTests
{
    private static readonly DecisionId Id =
        DecisionId.Parse("problem.xgp", CultureInfo.InvariantCulture);
    private static readonly DecisionId OtherId =
        DecisionId.Parse("other.xgp", CultureInfo.InvariantCulture);

    private static readonly DateTimeOffset T1 = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    private static SubmittedPlay Play(DecisionId id, double equityLoss, bool correct) =>
        new(id, new Play(), MatchedCandidateIndex: correct ? 0 : 1,
            EquityLoss: equityLoss, IsCorrect: correct);

    private static SubmittedCubeAction Cube(
        DecisionId id, double doublerLoss, bool doublerCorrect, double takerLoss, bool takerCorrect) =>
        new(id, new CubeDecisionPair(CubeAction.Double, CubeAction.Take),
            DoublerEquityLoss: doublerLoss, TakerEquityLoss: takerLoss,
            DoublerCorrect: doublerCorrect, TakerCorrect: takerCorrect);

    [Fact]
    public void FromPlay_Correct_OneSubmittedOneCorrectNoLoss()
    {
        var s = DecisionStats.From(Play(Id, equityLoss: 0.0, correct: true), T1);

        Assert.Equal(Id, s.Id);
        Assert.Equal(new ScoreSegment(Submitted: 1, Correct: 1, TotalEquityLoss: 0.0), s.Tally);
        Assert.Equal(0, s.Wrong);
        Assert.Equal(T1, s.LastQuizzed);
    }

    [Fact]
    public void FromPlay_Incorrect_OneSubmittedZeroCorrectWithLoss()
    {
        var s = DecisionStats.From(Play(Id, equityLoss: 0.042, correct: false), T1);

        Assert.Equal(1, s.Tally.Submitted);
        Assert.Equal(0, s.Tally.Correct);
        Assert.Equal(0.042, s.Tally.TotalEquityLoss, precision: 9);
        Assert.Equal(1, s.Wrong);
    }

    [Fact]
    public void FromCube_BothHalvesCorrect_CountsTwoCorrectDecisions()
    {
        var s = DecisionStats.From(
            Cube(Id, doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.0, takerCorrect: true), T1);

        Assert.Equal(new ScoreSegment(Submitted: 2, Correct: 2, TotalEquityLoss: 0.0), s.Tally);
    }

    [Theory]
    [InlineData(0.05, false, 0.00, true)]   // doubler half wrong
    [InlineData(0.00, true, 0.08, false)]   // taker half wrong
    public void FromCube_OneHalfWrong_CountsOneOfTwo_LossSumsBothHalves(
        double doublerLoss, bool doublerCorrect, double takerLoss, bool takerCorrect)
    {
        var s = DecisionStats.From(
            Cube(Id, doublerLoss, doublerCorrect, takerLoss, takerCorrect), T1);

        Assert.Equal(2, s.Tally.Submitted);
        Assert.Equal(1, s.Tally.Correct);
        Assert.Equal(1, s.Wrong);
        Assert.Equal(doublerLoss + takerLoss, s.Tally.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void FromCube_BothHalvesWrong_CountsZeroOfTwo_LossSumsBothHalves()
    {
        var s = DecisionStats.From(
            Cube(Id, doublerLoss: 0.05, doublerCorrect: false, takerLoss: 0.08, takerCorrect: false), T1);

        Assert.Equal(2, s.Tally.Submitted);
        Assert.Equal(0, s.Tally.Correct);
        Assert.Equal(2, s.Wrong);
        Assert.Equal(0.13, s.Tally.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void Plus_RepeatedFolds_AccumulateAndAdvanceLastQuizzed()
    {
        var s = DecisionStats.From(Play(Id, equityLoss: 0.025, correct: false), T1)
            .Plus(Play(Id, equityLoss: 0.0, correct: true), T2);

        Assert.Equal(2, s.Tally.Submitted);
        Assert.Equal(1, s.Tally.Correct);
        Assert.Equal(0.025, s.Tally.TotalEquityLoss, precision: 9);
        Assert.Equal(1, s.Wrong);
        Assert.Equal(T2, s.LastQuizzed);
    }

    [Fact]
    public void Plus_MixedPlayAndCubeFolds_AccumulateIntoOneTally()
    {
        var s = DecisionStats.From(Play(Id, equityLoss: 0.02, correct: false), T1)
            .Plus(Cube(Id, doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.0, takerCorrect: true), T2);

        Assert.Equal(3, s.Tally.Submitted);   // one play + a cube's two halves
        Assert.Equal(2, s.Tally.Correct);
        Assert.Equal(0.02, s.Tally.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void Plus_ReturnsNewInstance_OriginalUnchanged()
    {
        var original = DecisionStats.From(Play(Id, equityLoss: 0.0, correct: true), T1);
        var updated = original.Plus(Play(Id, equityLoss: 0.05, correct: false), T2);

        Assert.NotSame(original, updated);
        Assert.Equal(1, original.Tally.Submitted);
        Assert.Equal(T1, original.LastQuizzed);
        Assert.Equal(2, updated.Tally.Submitted);
    }

    [Fact]
    public void PlusPlay_MismatchedDecisionId_Throws()
    {
        var s = DecisionStats.From(Play(Id, equityLoss: 0.0, correct: true), T1);

        Assert.Throws<ArgumentException>(
            () => s.Plus(Play(OtherId, equityLoss: 0.0, correct: true), T2));
    }

    [Fact]
    public void PlusCube_MismatchedDecisionId_Throws()
    {
        var s = DecisionStats.From(Play(Id, equityLoss: 0.0, correct: true), T1);

        Assert.Throws<ArgumentException>(() => s.Plus(
            Cube(OtherId, doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.0, takerCorrect: true), T2));
    }

    [Fact]
    public void FromAndPlus_NullSubmissions_Throw()
    {
        var s = DecisionStats.From(Play(Id, equityLoss: 0.0, correct: true), T1);

        Assert.Throws<ArgumentNullException>(() => DecisionStats.From((SubmittedPlay)null!, T1));
        Assert.Throws<ArgumentNullException>(() => DecisionStats.From((SubmittedCubeAction)null!, T1));
        Assert.Throws<ArgumentNullException>(() => s.Plus((SubmittedPlay)null!, T2));
        Assert.Throws<ArgumentNullException>(() => s.Plus((SubmittedCubeAction)null!, T2));
    }

    [Fact]
    public void RecordEquality_HoldsByValue()
    {
        var a = new DecisionStats(Id, new ScoreSegment(3, 2, 0.125), T1);
        var b = new DecisionStats(Id, new ScoreSegment(3, 2, 0.125), T1);

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { LastQuizzed = T2 });
        Assert.NotEqual(a, a with { Id = OtherId });
    }
}
