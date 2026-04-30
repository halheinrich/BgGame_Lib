namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

public class QuizScoreTests
{
    [Fact]
    public void Empty_ZeroSubmittedZeroCorrectZeroLoss()
    {
        var s = QuizScore.Empty;
        Assert.Equal(0, s.Submitted);
        Assert.Equal(0, s.Correct);
        Assert.Equal(0.0, s.TotalEquityLoss);
        Assert.Equal(0.0, s.AverageEquityLoss);
    }

    [Fact]
    public void Plus_CorrectPlay_IncrementsSubmittedAndCorrect()
    {
        var s = QuizScore.Empty
            .Plus(new SubmittedPlay(new Play(), MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true));

        Assert.Equal(1, s.Submitted);
        Assert.Equal(1, s.Correct);
        Assert.Equal(0.0, s.TotalEquityLoss);
    }

    [Fact]
    public void Plus_IncorrectPlay_IncrementsSubmittedNotCorrect_AccumulatesEquityLoss()
    {
        var s = QuizScore.Empty
            .Plus(new SubmittedPlay(new Play(), MatchedCandidateIndex: 1, EquityLoss: 0.025, IsCorrect: false))
            .Plus(new SubmittedPlay(new Play(), MatchedCandidateIndex: 2, EquityLoss: 0.075, IsCorrect: false));

        Assert.Equal(2, s.Submitted);
        Assert.Equal(0, s.Correct);
        Assert.Equal(0.10, s.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void AverageEquityLoss_DividesTotalBySubmitted()
    {
        var s = QuizScore.Empty
            .Plus(new SubmittedPlay(new Play(), MatchedCandidateIndex: 0, EquityLoss: 0.0, IsCorrect: true))
            .Plus(new SubmittedPlay(new Play(), MatchedCandidateIndex: 1, EquityLoss: 0.04, IsCorrect: false));

        Assert.Equal(0.02, s.AverageEquityLoss, precision: 9);
    }

    [Fact]
    public void Plus_ReturnsNewInstance_OriginalUnchanged()
    {
        var original = QuizScore.Empty;
        var updated = original.Plus(
            new SubmittedPlay(new Play(), MatchedCandidateIndex: 0, EquityLoss: 0.05, IsCorrect: false));

        Assert.Equal(0, original.Submitted);
        Assert.Equal(1, updated.Submitted);
        Assert.NotSame(original, updated);
    }

    [Fact]
    public void Plus_NullPlay_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => QuizScore.Empty.Plus(null!));
    }
}
