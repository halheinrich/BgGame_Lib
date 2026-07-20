namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

public class QuizCategoryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly DecisionId Id = new XgpDecisionId("test.xgp");

    private static BgDecisionData Decision(bool isCube) => new()
    {
        Id = Id,
        Position = new PositionData
        {
            Mop = new int[26],
            CubeOwner = CubeOwner.Centered,
        },
        Decision = new DecisionData { IsCube = isCube },
        Descriptive = new DescriptiveData(),
        Outcome = new PlayOutcomeData(),
    };

    private static BgDecisionData Play => Decision(isCube: false);
    private static BgDecisionData Cube => Decision(isCube: true);

    private static DecisionStats Stats(
        int submitted, int correct, double loss, DateTimeOffset lastQuizzed) =>
        new(Id, new ScoreSegment(submitted, correct, loss), lastQuizzed);

    private static bool Matches(
        QuizCategory category, BgDecisionData decision, DecisionStats? stats) =>
        category.BuildPredicate().Matches(decision, stats, Now);

    // -----------------------------------------------------------------------
    //  Factory validation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SeenFewerThan_RejectsNonPositive(int times) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => QuizCategory.SeenFewerThan(times));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NotSeenInDays_RejectsNonPositive(int days) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => QuizCategory.NotSeenInDays(days));

    [Theory]
    [InlineData(-0.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AvgEquityLossOver_RejectsNegativeAndNonFinite(double loss) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => QuizCategory.AvgEquityLossOver(loss));

    [Fact]
    public void AvgEquityLossOver_AcceptsZero() =>
        Assert.Equal(0.0, QuizCategory.AvgEquityLossOver(0.0).Value);

    [Theory]
    [InlineData(-0.001)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void WrongRateOver_RejectsOutsideUnitInterval(double rate) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => QuizCategory.WrongRateOver(rate));

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.999)]
    public void WrongRateOver_AcceptsFractions(double rate) =>
        Assert.Equal(rate, QuizCategory.WrongRateOver(rate).Value);

    // -----------------------------------------------------------------------
    //  DTO shape
    // -----------------------------------------------------------------------

    [Fact]
    public void ParameterizedFactories_ExposeKindAndValue()
    {
        var category = QuizCategory.SeenFewerThan(3);
        Assert.Equal(QuizCategoryKind.SeenFewerThan, category.Kind);
        Assert.Equal(3.0, category.Value);

        Assert.Equal(QuizCategoryKind.NotSeenInDays, QuizCategory.NotSeenInDays(30).Kind);
        Assert.Equal(30.0, QuizCategory.NotSeenInDays(30).Value);
    }

    [Fact]
    public void ParameterlessCategories_CarryNullValue()
    {
        Assert.Null(QuizCategory.NeverSeen.Value);
        Assert.Null(QuizCategory.GotWrong.Value);
        Assert.Null(QuizCategory.EverythingElse.Value);
    }

    [Fact]
    public void ParameterlessCategories_AreSingletons()
    {
        Assert.Same(QuizCategory.NeverSeen, QuizCategory.NeverSeen);
        Assert.Same(QuizCategory.GotWrong, QuizCategory.GotWrong);
        Assert.Same(QuizCategory.EverythingElse, QuizCategory.EverythingElse);
    }

    [Fact]
    public void ValueEquality_ComparesKindAndValue()
    {
        Assert.Equal(QuizCategory.SeenFewerThan(3), QuizCategory.SeenFewerThan(3));
        Assert.NotEqual(QuizCategory.SeenFewerThan(3), QuizCategory.SeenFewerThan(5));
        Assert.NotEqual(QuizCategory.SeenFewerThan(3), QuizCategory.NotSeenInDays(3));
        Assert.NotEqual<QuizCategory>(QuizCategory.NeverSeen, QuizCategory.GotWrong);
    }

    [Fact]
    public void IsResidual_TrueOnlyForEverythingElse()
    {
        Assert.True(QuizCategory.EverythingElse.IsResidual);
        Assert.False(QuizCategory.NeverSeen.IsResidual);
        Assert.False(QuizCategory.GotWrong.IsResidual);
        Assert.False(QuizCategory.SeenFewerThan(1).IsResidual);
        Assert.False(QuizCategory.NotSeenInDays(1).IsResidual);
        Assert.False(QuizCategory.AvgEquityLossOver(0.0).IsResidual);
        Assert.False(QuizCategory.WrongRateOver(0.0).IsResidual);
    }

    [Fact]
    public void BuildPredicate_ThrowsForEverythingElse() =>
        Assert.Throws<InvalidOperationException>(
            () => QuizCategory.EverythingElse.BuildPredicate());

    // -----------------------------------------------------------------------
    //  NeverSeen
    // -----------------------------------------------------------------------

    [Fact]
    public void NeverSeen_MatchesMissingRecord() =>
        Assert.True(Matches(QuizCategory.NeverSeen, Play, stats: null));

    [Fact]
    public void NeverSeen_MatchesEmptyTallyDefensively() =>
        Assert.True(Matches(QuizCategory.NeverSeen, Play, Stats(0, 0, 0.0, Now)));

    [Fact]
    public void NeverSeen_DoesNotMatchSeenDecision() =>
        Assert.False(Matches(QuizCategory.NeverSeen, Play, Stats(1, 1, 0.0, Now)));

    // -----------------------------------------------------------------------
    //  GotWrong
    // -----------------------------------------------------------------------

    [Fact]
    public void GotWrong_MatchesAnyWrongSubmission() =>
        Assert.True(Matches(QuizCategory.GotWrong, Play, Stats(2, 1, 0.1, Now)));

    [Fact]
    public void GotWrong_DoesNotMatchAllCorrect() =>
        Assert.False(Matches(QuizCategory.GotWrong, Play, Stats(2, 2, 0.0, Now)));

    [Fact]
    public void GotWrong_DoesNotMatchNeverSeen() =>
        Assert.False(Matches(QuizCategory.GotWrong, Play, stats: null));

    [Fact]
    public void GotWrong_CountsAHalfWrongCube() =>
        // One sighting, half right: 2 submitted, 1 correct — Wrong is 1.
        Assert.True(Matches(QuizCategory.GotWrong, Cube, Stats(2, 1, 0.1, Now)));

    // -----------------------------------------------------------------------
    //  SeenFewerThan — sightings derivation
    // -----------------------------------------------------------------------

    [Fact]
    public void SeenFewerThan_MatchesNeverSeen() =>
        Assert.True(Matches(QuizCategory.SeenFewerThan(1), Play, stats: null));

    [Fact]
    public void SeenFewerThan_ComparesPlaySubmissionsAsSightings()
    {
        var twoSightings = Stats(2, 2, 0.0, Now);
        Assert.True(Matches(QuizCategory.SeenFewerThan(3), Play, twoSightings));
        Assert.False(Matches(QuizCategory.SeenFewerThan(2), Play, twoSightings));
    }

    [Fact]
    public void SeenFewerThan_HalvesCubeSubmissionsIntoSightings()
    {
        // Two sightings of a cube position: 4 submitted (two halves each).
        var twoSightings = Stats(4, 4, 0.0, Now);
        Assert.True(Matches(QuizCategory.SeenFewerThan(3), Cube, twoSightings));
        Assert.False(Matches(QuizCategory.SeenFewerThan(2), Cube, twoSightings));
    }

    // -----------------------------------------------------------------------
    //  NotSeenInDays
    // -----------------------------------------------------------------------

    [Fact]
    public void NotSeenInDays_MatchesNeverSeen() =>
        Assert.True(Matches(QuizCategory.NotSeenInDays(30), Play, stats: null));

    [Fact]
    public void NotSeenInDays_MatchesExactlyOnTheBoundary() =>
        Assert.True(Matches(
            QuizCategory.NotSeenInDays(10), Play,
            Stats(1, 1, 0.0, Now - TimeSpan.FromDays(10))));

    [Fact]
    public void NotSeenInDays_DoesNotMatchRecentlySeen() =>
        Assert.False(Matches(
            QuizCategory.NotSeenInDays(10), Play,
            Stats(1, 1, 0.0, Now - TimeSpan.FromDays(9))));

    // -----------------------------------------------------------------------
    //  AvgEquityLossOver
    // -----------------------------------------------------------------------

    [Fact]
    public void AvgEquityLossOver_ComparesStrictly()
    {
        var stats = Stats(2, 1, 0.3, Now);   // average 0.15
        Assert.True(Matches(QuizCategory.AvgEquityLossOver(0.1), Play, stats));
        Assert.False(Matches(QuizCategory.AvgEquityLossOver(0.15), Play, stats));
    }

    [Fact]
    public void AvgEquityLossOver_DoesNotMatchNeverSeen() =>
        Assert.False(Matches(QuizCategory.AvgEquityLossOver(0.0), Play, stats: null));

    [Fact]
    public void AvgEquityLossOver_NeedsNoCubeAdjustment()
    {
        // One cube sighting: both halves' losses over both half-counts is
        // already the per-half average — the same 0.15 as the play case above,
        // matched against the same threshold.
        var stats = Stats(2, 1, 0.3, Now);
        Assert.True(Matches(QuizCategory.AvgEquityLossOver(0.1), Cube, stats));
        Assert.False(Matches(QuizCategory.AvgEquityLossOver(0.15), Cube, stats));
    }

    // -----------------------------------------------------------------------
    //  WrongRateOver
    // -----------------------------------------------------------------------

    [Fact]
    public void WrongRateOver_ComparesStrictly()
    {
        var stats = Stats(4, 3, 0.2, Now);   // wrong rate 0.25
        Assert.True(Matches(QuizCategory.WrongRateOver(0.2), Play, stats));
        Assert.False(Matches(QuizCategory.WrongRateOver(0.25), Play, stats));
    }

    [Fact]
    public void WrongRateOver_DoesNotMatchNeverSeen() =>
        Assert.False(Matches(QuizCategory.WrongRateOver(0.0), Play, stats: null));

    [Fact]
    public void WrongRateOver_ReadsAHalfRightCubeAsHalfWrong()
    {
        // One sighting, half right: rate 0.5 per the ratified 1-of-2 fold.
        var stats = Stats(2, 1, 0.1, Now);
        Assert.True(Matches(QuizCategory.WrongRateOver(0.49), Cube, stats));
        Assert.False(Matches(QuizCategory.WrongRateOver(0.5), Cube, stats));
    }
}
