namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

public class QuizScoreTests
{
    // QuizScore folds these records by reading their result fields; it never
    // reads ProblemKey — session scoring is deliberately key-independent, so
    // even a no-key (null) submission folds. Null here keeps every folding
    // assertion below unchanged in intent while pinning that independence.
    private static SubmittedPlay Play(double equityLoss, bool correct) =>
        new(ProblemKey: null, UserPlay: [], MatchedCandidateIndex: correct ? 0 : 1,
            EquityLoss: equityLoss, IsCorrect: correct);

    private static SubmittedCubeAction Cube(
        double doublerLoss, bool doublerCorrect, double takerLoss, bool takerCorrect) =>
        new(ProblemKey: null, new CubeDecisionPair(CubeAction.Double, CubeAction.Take),
            DoublerEquityLoss: doublerLoss, TakerEquityLoss: takerLoss,
            DoublerCorrect: doublerCorrect, TakerCorrect: takerCorrect);

    [Fact]
    public void Empty_AllSegmentsAndTotalAreEmpty()
    {
        var s = QuizScore.Empty;
        Assert.Equal(ScoreSegment.Empty, s.PlayDecisions);
        Assert.Equal(ScoreSegment.Empty, s.DoubleDecisions);
        Assert.Equal(ScoreSegment.Empty, s.TakeDecisions);
        Assert.Equal(ScoreSegment.Empty, s.Total);
    }

    [Fact]
    public void PlusPlay_CorrectPlay_IncrementsPlaySubmittedAndCorrect()
    {
        var s = QuizScore.Empty.Plus(Play(equityLoss: 0.0, correct: true));

        Assert.Equal(1, s.PlayDecisions.Submitted);
        Assert.Equal(1, s.PlayDecisions.Correct);
        Assert.Equal(0.0, s.PlayDecisions.TotalEquityLoss);
    }

    [Fact]
    public void PlusPlay_IncorrectPlay_IncrementsSubmittedNotCorrect_AccumulatesEquityLoss()
    {
        var s = QuizScore.Empty
            .Plus(Play(equityLoss: 0.025, correct: false))
            .Plus(Play(equityLoss: 0.075, correct: false));

        Assert.Equal(2, s.PlayDecisions.Submitted);
        Assert.Equal(0, s.PlayDecisions.Correct);
        Assert.Equal(0.10, s.PlayDecisions.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void PlusPlay_LeavesCubeSegmentsUntouched()
    {
        var s = QuizScore.Empty.Plus(Play(equityLoss: 0.05, correct: false));

        Assert.Equal(ScoreSegment.Empty, s.DoubleDecisions);
        Assert.Equal(ScoreSegment.Empty, s.TakeDecisions);
    }

    [Fact]
    public void PlusCube_BumpsDoubleAndTakeByOneEach_PlayUntouched()
    {
        var s = QuizScore.Empty
            .Plus(Cube(doublerLoss: 0.0, doublerCorrect: true, takerLoss: 0.12, takerCorrect: false));

        Assert.Equal(1, s.DoubleDecisions.Submitted);
        Assert.Equal(1, s.DoubleDecisions.Correct);
        Assert.Equal(0.0, s.DoubleDecisions.TotalEquityLoss);

        Assert.Equal(1, s.TakeDecisions.Submitted);
        Assert.Equal(0, s.TakeDecisions.Correct);
        Assert.Equal(0.12, s.TakeDecisions.TotalEquityLoss, precision: 9);

        Assert.Equal(ScoreSegment.Empty, s.PlayDecisions);
    }

    [Fact]
    public void Total_IsTheSumOfAllThreeSegments()
    {
        var s = QuizScore.Empty
            .Plus(Play(equityLoss: 0.02, correct: false))
            .Plus(Cube(doublerLoss: 0.03, doublerCorrect: false, takerLoss: 0.0, takerCorrect: true));

        Assert.Equal(3, s.Total.Submitted);   // 1 play + 1 double + 1 take
        Assert.Equal(1, s.Total.Correct);     // only the taker half was correct
        Assert.Equal(0.05, s.Total.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void Total_IsDerived_ChangingASegmentChangesTotal()
    {
        var before = QuizScore.Empty.Plus(Play(equityLoss: 0.04, correct: false));
        var after = before.Plus(Cube(doublerLoss: 0.06, doublerCorrect: false, takerLoss: 0.0, takerCorrect: true));

        Assert.Equal(1, before.Total.Submitted);
        Assert.Equal(3, after.Total.Submitted);
        Assert.Equal(0.10, after.Total.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void Plus_ReturnsNewInstance_OriginalUnchanged()
    {
        var original = QuizScore.Empty;
        var updated = original.Plus(Play(equityLoss: 0.05, correct: false));

        Assert.Equal(0, original.PlayDecisions.Submitted);
        Assert.Equal(1, updated.PlayDecisions.Submitted);
        Assert.NotSame(original, updated);
    }

    [Fact]
    public void PlusPlay_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => QuizScore.Empty.Plus((SubmittedPlay)null!));
    }

    [Fact]
    public void PlusCube_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => QuizScore.Empty.Plus((SubmittedCubeAction)null!));
    }
}
