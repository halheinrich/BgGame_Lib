namespace BgGame_Lib.Tests;

public class ScoreSegmentTests
{
    [Fact]
    public void Empty_IsZeroAcrossAllFields()
    {
        var s = ScoreSegment.Empty;
        Assert.Equal(0, s.Submitted);
        Assert.Equal(0, s.Correct);
        Assert.Equal(0.0, s.TotalEquityLoss);
        Assert.Equal(0.0, s.AverageEquityLoss);
        Assert.Equal(0.0, s.Accuracy);
    }

    [Fact]
    public void Add_AccumulatesComponentWise()
    {
        var s = ScoreSegment.Empty
            .Add(1, 1, 0.0)
            .Add(1, 0, 0.025)
            .Add(1, 0, 0.075);

        Assert.Equal(3, s.Submitted);
        Assert.Equal(1, s.Correct);
        Assert.Equal(0.10, s.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void OperatorPlus_AddsComponentWise()
    {
        var a = new ScoreSegment(2, 1, 0.05);
        var b = new ScoreSegment(3, 2, 0.15);

        var sum = a + b;

        Assert.Equal(5, sum.Submitted);
        Assert.Equal(3, sum.Correct);
        Assert.Equal(0.20, sum.TotalEquityLoss, precision: 9);
    }

    [Fact]
    public void AverageEquityLoss_DividesTotalBySubmitted()
    {
        var s = new ScoreSegment(2, 1, 0.04);
        Assert.Equal(0.02, s.AverageEquityLoss, precision: 9);
    }

    [Fact]
    public void AverageEquityLoss_ZeroSubmitted_IsZero()
    {
        Assert.Equal(0.0, new ScoreSegment(0, 0, 0.0).AverageEquityLoss);
    }

    [Fact]
    public void Accuracy_IsCorrectOverSubmitted()
    {
        var s = new ScoreSegment(4, 3, 0.0);
        Assert.Equal(0.75, s.Accuracy, precision: 9);
    }

    [Fact]
    public void Accuracy_ZeroSubmitted_IsZero()
    {
        Assert.Equal(0.0, new ScoreSegment(0, 0, 0.0).Accuracy);
    }

    [Fact]
    public void OperatorPlus_NullLeft_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => null! + ScoreSegment.Empty);
    }

    [Fact]
    public void OperatorPlus_NullRight_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ScoreSegment.Empty + null!);
    }
}
