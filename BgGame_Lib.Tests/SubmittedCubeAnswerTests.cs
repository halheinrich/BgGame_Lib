namespace BgGame_Lib.Tests;

using System.Reflection;
using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

/// <summary>
/// A submitted cube answer is scored at its record, in one place
/// (<see cref="SubmittedCubeAnswer.Score"/>): the key, the truth and the cost
/// are that one record's, and nothing can pair facts from two decisions
/// (SPEC-scoring §3, amended on halheinrich/backgammon#326;
/// halheinrich/backgammon#285's cube items).
/// </summary>
public class SubmittedCubeAnswerTests
{
    // -----------------------------------------------------------------------
    //  Fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// One decision per truth, by its no-double equity (N) and double/take
    /// equity (T), off every boundary but the last: the halheinrich/backgammon#293
    /// tie, N exactly 1 with a pass, whose truth is the fourth answer by the
    /// producer's tie-breaks. The truth is the equities' alone, so each row
    /// holds whether gammons are possible or not.
    /// </summary>
    private static readonly (double N, double T, CubeAnswer Truth)[] Truths =
    [
        (0.20, 0.10, CubeAnswer.NoDouble),
        (0.30, 0.60, CubeAnswer.DoubleTake),
        (0.50, 1.20, CubeAnswer.DoublePass),
        (1.30, 1.50, CubeAnswer.NoDoublePass),
        (1.00, 1.20, CubeAnswer.NoDoublePass),
    ];

    /// <summary>The position for a gammon state: the default race where they are possible, <see cref="TestSubmissions.NoGammons"/> where not.</summary>
    private static PositionData? PositionFor(bool gammonsPossible) =>
        gammonsPossible ? null : TestSubmissions.NoGammons;

    /// <summary>Every answer, at a decision of every truth, under both gammon states.</summary>
    public static TheoryData<double, double, CubeAnswer, bool, CubeAnswer> EveryAnswerAtEveryTruth
    {
        get
        {
            var data = new TheoryData<double, double, CubeAnswer, bool, CubeAnswer>();
            foreach (var (n, t, truth) in Truths)
                foreach (bool gammonsPossible in new[] { true, false })
                    foreach (var answer in Enum.GetValues<CubeAnswer>())
                        data.Add(n, t, truth, gammonsPossible, answer);
            return data;
        }
    }

    /// <summary>Every truth, under both gammon states.</summary>
    public static TheoryData<double, double, CubeAnswer, bool> EveryTruth
    {
        get
        {
            var data = new TheoryData<double, double, CubeAnswer, bool>();
            foreach (var (n, t, truth) in Truths)
                foreach (bool gammonsPossible in new[] { true, false })
                    data.Add(n, t, truth, gammonsPossible);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EveryTruth))]
    public void Fixtures_DeriveTheTruthAndTheGammonStateTheyAreListedWith(
        double n, double t, CubeAnswer truth, bool gammonsPossible)
    {
        // So a scoring failure below can never be a mis-built fixture.
        var decision = TestSubmissions.CubeRecord(n, t, PositionFor(gammonsPossible));

        Assert.Equal(truth, decision.Decision.BestAnswer);
        Assert.Equal(gammonsPossible, decision.GammonsPossible);
    }

    // -----------------------------------------------------------------------
    //  Every fact from the one record
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryAnswerAtEveryTruth))]
    public void Score_EveryAnswerAtEveryTruth_ReadsEveryFactOffTheOneRecord(
        double n, double t, CubeAnswer truth, bool gammonsPossible, CubeAnswer answer)
    {
        var decision = TestSubmissions.CubeRecord(n, t, PositionFor(gammonsPossible));

        var submitted = SubmittedCubeAnswer.Score(answer, decision);

        Assert.Equal(answer, submitted.Answer);
        Assert.Equal(truth, submitted.BestAnswer);
        Assert.Equal(decision.CostOf(answer), submitted.Cost);
        Assert.Equal(ProblemKey.From(decision), submitted.ProblemKey);
    }

    [Theory]
    [MemberData(nameof(EveryAnswerAtEveryTruth))]
    public void Score_EveryAnswerAtEveryTruth_IsCorrectExactlyWhereItsCostCountsAsZero(
        double n, double t, CubeAnswer truth, bool gammonsPossible, CubeAnswer answer)
    {
        // Each verdict is its own loss's, by the zero rule — the whole on the
        // whole cost, each part on its own — and never "equals the truth".
        _ = truth;
        var submitted = TestSubmissions.Cube(answer, n, t, PositionFor(gammonsPossible));

        Assert.Equal(EquityLoss.CountsAsZero(submitted.Cost.Total), submitted.IsCorrect);
        Assert.Equal(EquityLoss.CountsAsZero(submitted.Cost.DoublingPart), submitted.IsDoublingPartCorrect);
        Assert.Equal(EquityLoss.CountsAsZero(submitted.Cost.TakePart), submitted.IsTakePartCorrect);
    }

    [Theory]
    [MemberData(nameof(EveryTruth))]
    public void Score_TheTruth_IsCorrectWholeAndInEachPart(
        double n, double t, CubeAnswer truth, bool gammonsPossible)
    {
        var submitted = TestSubmissions.Cube(truth, n, t, PositionFor(gammonsPossible));

        Assert.Equal(0.0, submitted.Cost.Total);
        Assert.True(submitted.IsCorrect);
        Assert.True(submitted.IsDoublingPartCorrect);
        Assert.True(submitted.IsTakePartCorrect);
    }

    // -----------------------------------------------------------------------
    //  The positions ruled on halheinrich/backgammon#326
    // -----------------------------------------------------------------------

    [Fact]
    public void Score_TooGoodWhenTheydTake_CostsTwiceTheTakeItMisreads()
    {
        // TooGoodAndTake.xgp's equities, gammons possible: No double / Take is
        // the truth, and the fourth answer reads Too good. It costs 2(1 − T),
        // half in each part; No double costs nothing.
        var decision = TestSubmissions.CubeRecord(noDoubleEquity: 1.1711, doubleTakeEquity: 0.6004);
        Assert.Equal(CubeAnswer.NoDouble, decision.Decision.BestAnswer);
        Assert.Equal(CubeClaim.TooGood, decision.ClaimOf(CubeAnswer.NoDoublePass));

        var tooGood = SubmittedCubeAnswer.Score(CubeAnswer.NoDoublePass, decision);
        var noDouble = SubmittedCubeAnswer.Score(CubeAnswer.NoDouble, decision);

        Assert.Equal(0.7992, tooGood.Cost.Total, precision: 12);
        Assert.Equal(tooGood.Cost.DoublingPart, tooGood.Cost.TakePart);
        Assert.False(tooGood.IsCorrect);
        Assert.Equal(0.0, noDouble.Cost.Total);
        Assert.True(noDouble.IsCorrect);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Score_AtTheTie_MoreThanOneAnswerIsCorrect(bool gammonsPossible)
    {
        // The Shimodaira tie of halheinrich/backgammon#293: N exactly 1, T
        // +2.0267 as XG stores it. No double, Double / Pass and the fourth
        // answer each cost 0 and are each correct; Double / Take costs T − 1
        // = 1.0267, computed from the stored equity, and is wrong. The truth
        // is the fourth answer, the representative, and is not what makes the
        // other two correct.
        var decision = TestSubmissions.CubeRecord(1.0, 2.0267, PositionFor(gammonsPossible));
        Assert.Equal(CubeAnswer.NoDoublePass, decision.Decision.BestAnswer);

        CubeAnswer[] correct = [CubeAnswer.NoDouble, CubeAnswer.DoublePass, CubeAnswer.NoDoublePass];
        foreach (var answer in correct)
        {
            var submitted = SubmittedCubeAnswer.Score(answer, decision);
            Assert.Equal(0.0, submitted.Cost.Total);
            Assert.True(submitted.IsCorrect, $"{answer} costs nothing at the tie.");
        }

        var doubleTake = SubmittedCubeAnswer.Score(CubeAnswer.DoubleTake, decision);
        Assert.Equal(1.0267, doubleTake.Cost.Total, precision: 12);
        Assert.False(doubleTake.IsCorrect);
    }

    [Fact]
    public void Score_AStoredCostBelowTheDisplay_IsCorrect_AndStaysExact()
    {
        // Gammons not possible, the no-double equity stored at +1.0000015:
        // No double and the fourth answer cost nothing, and Double / Pass
        // keeps its raw cost of 0.0000015 — exact, not rounded — which the
        // zero rule judges correct (halheinrich/backgammon#202).
        var decision = TestSubmissions.CubeRecord(1.0000015, 1.2, TestSubmissions.NoGammons);

        var doublePass = SubmittedCubeAnswer.Score(CubeAnswer.DoublePass, decision);

        Assert.Equal(1.0000015 - 1.0, doublePass.Cost.Total);
        Assert.True(doublePass.Cost.Total > 0.0);
        Assert.True(doublePass.IsCorrect);
        Assert.True(SubmittedCubeAnswer.Score(CubeAnswer.NoDouble, decision).IsCorrect);
        Assert.True(SubmittedCubeAnswer.Score(CubeAnswer.NoDoublePass, decision).IsCorrect);
    }

    [Fact]
    public void Score_TheGammonFactIsTheRecords_SoTheSameEquitiesCostDifferentlyInAnotherPosition()
    {
        // The same equities as above where gammons are possible: the fourth
        // answer reads Too good there, and No double, where Too good is right,
        // carries the N − 0.6 convention. Only the record knows which applies,
        // which is why the answer is scored at the record and not its category.
        var possible = SubmittedCubeAnswer.Score(
            CubeAnswer.NoDouble, TestSubmissions.CubeRecord(1.0000015, 1.2));
        var notPossible = SubmittedCubeAnswer.Score(
            CubeAnswer.NoDouble, TestSubmissions.CubeRecord(1.0000015, 1.2, TestSubmissions.NoGammons));

        Assert.Equal(1.0000015 - 0.6, possible.Cost.Total, precision: 12);
        Assert.False(possible.IsCorrect);
        Assert.Equal(0.0, notPossible.Cost.Total);
        Assert.True(notPossible.IsCorrect);
    }

    // -----------------------------------------------------------------------
    //  The key is the record's
    // -----------------------------------------------------------------------

    [Fact]
    public void Score_CarriesTheRecordsOwnKey_NeverACallers()
    {
        // The same equities at two scores are two problems: each submission
        // carries its own record's key, and a cube key at that.
        var here = TestSubmissions.CubeRecord(0.30, 0.60, TestRecords.Position());
        var elsewhere = TestSubmissions.CubeRecord(
            0.30, 0.60, TestRecords.Position(session: TestRecords.MatchSession(onRollNeeds: 5, opponentNeeds: 3)));

        var a = SubmittedCubeAnswer.Score(CubeAnswer.DoubleTake, here);
        var b = SubmittedCubeAnswer.Score(CubeAnswer.DoubleTake, elsewhere);

        Assert.Equal(ProblemKey.From(here), a.ProblemKey);
        Assert.Equal(ProblemKey.From(elsewhere), b.ProblemKey);
        Assert.NotEqual(a.ProblemKey, b.ProblemKey);
        Assert.True(a.ProblemKey.IsCubeDecision);
    }

    // -----------------------------------------------------------------------
    //  No public construction path
    // -----------------------------------------------------------------------

    [Fact]
    public void SubmittedCubeAnswer_HasNoPublicConstructor()
    {
        Assert.Empty(typeof(SubmittedCubeAnswer).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void SubmittedCubeAnswer_IsBuiltByScoreAlone()
    {
        // The one public member that hands out a new submission.
        var factories = typeof(SubmittedCubeAnswer)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(SubmittedCubeAnswer))
            .Select(m => m.Name);

        Assert.Equal(new[] { "Score" }, factories);
    }

    [Fact]
    public void SubmittedCubeAnswer_NoMemberCanBeSet_AndNoWithCanSetOneApart()
    {
        // No setter and no init accessor on any public property, and not a
        // record, whose `with` would copy the submission with one fact
        // replaced.
        var properties = typeof(SubmittedCubeAnswer).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.NotEmpty(properties);
        Assert.All(properties, p => Assert.Null(p.SetMethod));
        Assert.Null(typeof(SubmittedCubeAnswer).GetMethod("<Clone>$"));
    }

    [Fact]
    public void Score_NullDecision_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SubmittedCubeAnswer.Score(CubeAnswer.NoDouble, null!));
    }

    [Fact]
    public void Score_AnUndefinedAnswer_Throws()
    {
        var thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => SubmittedCubeAnswer.Score((CubeAnswer)4, TestRecords.Cube()));

        Assert.Equal("answer", thrown.ParamName);
    }

    // -----------------------------------------------------------------------
    //  Identity: the same problem and the same answer
    // -----------------------------------------------------------------------

    /// <summary>Equal through every door: both Equals, both operators, and the hash.</summary>
    private static void AssertSameSubmittedAnswer(SubmittedCubeAnswer a, SubmittedCubeAnswer b)
    {
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Single(new HashSet<SubmittedCubeAnswer> { a, b });
    }

    /// <summary>Unequal through every door: both Equals, both operators, and a set.</summary>
    private static void AssertDifferentSubmittedAnswers(SubmittedCubeAnswer a, SubmittedCubeAnswer b)
    {
        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
        Assert.False(a.Equals((object)b));
        Assert.Equal(2, new HashSet<SubmittedCubeAnswer> { a, b }.Count);
    }

    [Fact]
    public void SubmittedCubeAnswer_TheSameAnswerToTheSameProblemUnderAnotherAnalysis_IsTheSameSubmittedAnswer()
    {
        // One problem, one answer, two analyses: the truth, the cost and the
        // verdict all differ, and none is identity.
        var a = TestSubmissions.Cube(CubeAnswer.DoubleTake, noDoubleEquity: 0.30, doubleTakeEquity: 0.60);
        var b = TestSubmissions.Cube(CubeAnswer.DoubleTake, noDoubleEquity: 0.20, doubleTakeEquity: 0.10);

        Assert.Equal(a.ProblemKey, b.ProblemKey);
        Assert.NotEqual(a.BestAnswer, b.BestAnswer);
        Assert.NotEqual(a.Cost, b.Cost);
        Assert.NotEqual(a.IsCorrect, b.IsCorrect);
        AssertSameSubmittedAnswer(a, b);
    }

    [Fact]
    public void SubmittedCubeAnswer_AnotherAnswerToTheSameProblem_IsADifferentSubmittedAnswer()
    {
        // At the tie, two answers alike in everything but the answer: same
        // problem, same truth, both costing nothing and both correct.
        var decision = TestSubmissions.CubeRecord(1.0, 1.2);
        var a = SubmittedCubeAnswer.Score(CubeAnswer.NoDouble, decision);
        var b = SubmittedCubeAnswer.Score(CubeAnswer.DoublePass, decision);

        Assert.Equal(a.BestAnswer, b.BestAnswer);
        Assert.Equal(a.Cost.Total, b.Cost.Total);
        Assert.Equal(a.IsCorrect, b.IsCorrect);
        AssertDifferentSubmittedAnswers(a, b);
    }

    [Fact]
    public void SubmittedCubeAnswer_TheSameAnswerToAnotherProblem_IsADifferentSubmittedAnswer()
    {
        var a = TestSubmissions.Cube(CubeAnswer.DoubleTake, 0.30, 0.60);
        var b = TestSubmissions.Cube(
            CubeAnswer.DoubleTake, 0.30, 0.60,
            TestRecords.Position(session: TestRecords.MatchSession(onRollNeeds: 5, opponentNeeds: 3)));

        Assert.Equal(a.Cost, b.Cost);
        AssertDifferentSubmittedAnswers(a, b);
    }

    [Fact]
    public void SubmittedCubeAnswer_EqualityWithNull()
    {
        var a = TestSubmissions.Cube(CubeAnswer.NoDouble, 0.20, 0.10);

        Assert.False(a.Equals(null));
        Assert.False(a.Equals((object?)null));
        Assert.False(a == null);
        Assert.False(null == a);
        Assert.True((SubmittedCubeAnswer?)null == null);
    }
}
