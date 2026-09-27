namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

/// <summary>
/// A submitted play is scored under a ranking, in one place
/// (<see cref="PlaySubmission.Score"/>), and its facts cannot disagree
/// (halheinrich/backgammon#273; SPEC-scoring §2 and §2a).
/// </summary>
public class PlaySubmissionTests
{
    // -----------------------------------------------------------------------
    //  Fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// The opening 3-1's position at 5-away/3-away rather than the default
    /// 7-away/7-away: the same board and roll, another problem.
    /// </summary>
    private static PositionData AtFiveAwayThreeAway =>
        TestRecords.Position(session: TestRecords.MatchSession(onRollNeeds: 5, opponentNeeds: 3));

    /// <summary>
    /// The opening 3-1 (<see cref="TestRecords.CheckerPlay"/>'s default):
    /// three candidates at one depth, 8/5 6/5 the best.
    /// </summary>
    private static CheckerPlayDecision Opening => TestRecords.CheckerPlay();

    /// <summary>The opening 3-1 as another problem (<see cref="AtFiveAwayThreeAway"/>).</summary>
    private static CheckerPlayDecision OpeningElsewhere => TestRecords.CheckerPlay(position: AtFiveAwayThreeAway);

    private static Play EightFiveSixFive => [new(8, 5), new(6, 5)];
    private static Play ThirteenTenSixFive => [new(13, 10), new(6, 5)];
    private static Play TwentyFourTwentyThreeThirteenTen => [new(24, 23), new(13, 10)];

    /// <summary>
    /// The opening 3-1 analysed at two depths: 8/5 6/5 evaluated at 3-ply and
    /// rated highest; 13/10 6/5 and 24/23 13/10 rolled out, lower. Under
    /// equity 8/5 6/5 is best; under depth first 13/10 6/5 is, and 8/5 6/5 —
    /// analysed less deeply, rated higher — is not scored (SPEC-scoring §2a).
    /// </summary>
    private static CheckerPlayDecision TwoDepths => TwoDepthsFrom(position: null);

    /// <summary><see cref="TwoDepths"/> from <paramref name="position"/> (by default the opening's).</summary>
    private static CheckerPlayDecision TwoDepthsFrom(PositionData? position) => TestRecords.CheckerPlay(
        position: position,
        decision: TestRecords.CheckerPlayData(plays:
        [
            TestRecords.Candidate(play: EightFiveSixFive, equity: 0.20),
            TestRecords.Candidate(
                play: ThirteenTenSixFive, analysisMode: AnalysisMode.Rollout, equity: 0.10),
            TestRecords.Candidate(
                play: TwentyFourTwentyThreeThirteenTen, analysisMode: AnalysisMode.Rollout, equity: 0.05),
        ]));

    /// <summary>
    /// A 5-4 with two checkers able to hit the opponent's blot on the 3-point:
    /// 8/3* 7/3, whichever checker carries the mark. Candidate 0 carries it on
    /// the checker from the 8; candidate 1 is 13/8 13/9.
    /// </summary>
    private static CheckerPlayDecision BlotOnTheThree
    {
        get
        {
            var mop = new int[26];
            mop[24] = 2; mop[13] = 4; mop[8] = 2; mop[7] = 2; mop[6] = 5;     // on roll: 15
            mop[19] = -5; mop[17] = -3; mop[12] = -5; mop[3] = -1; mop[1] = -1; // opponent: 15
            return TestRecords.CheckerPlay(
                position: TestRecords.Position(mop: new BoardPosition(mop)),
                decision: TestRecords.CheckerPlayData(
                    dice: [5, 4],
                    plays:
                    [
                        TestRecords.Candidate(play: [new(8, -3), new(7, 3)], equity: 0.30),
                        TestRecords.Candidate(play: [new(13, 8), new(13, 9)], equity: -0.10),
                    ]));
        }
    }

    private static SubmittedPlay Scored(PlaySubmission submission)
    {
        Assert.Equal(PlaySubmissionKind.Scored, submission.Kind);
        Assert.True(submission.TryGetScored(out var scored));
        return scored;
    }

    [Fact]
    public void TwoDepthsFixture_RanksAsItsDocumentationSays()
    {
        // So a scoring failure below can never be a mis-built fixture.
        var decision = TwoDepths.Decision;
        Assert.Equal(0, decision.RankedBy(PlayRanking.Equity).Best.Index);
        Assert.Equal(1, decision.RankedBy(PlayRanking.DepthFirst).Best.Index);
        Assert.False(decision.RankedBy(PlayRanking.DepthFirst).ForCandidate(0).IsScored);
    }

    // -----------------------------------------------------------------------
    //  The facts come from the ranking, together
    // -----------------------------------------------------------------------

    [Fact]
    public void Score_TheBestPlay_IsCorrectAtZero()
    {
        var submitted = Scored(PlaySubmission.Score(EightFiveSixFive, Opening, PlayRanking.Equity));

        Assert.Equal(0, submitted.MatchedCandidateIndex);
        Assert.Equal(0.0, submitted.EquityLoss);
        Assert.True(submitted.IsCorrect);
    }

    [Fact]
    public void Score_AnotherCandidate_CarriesTheRankingsErrorAndIsIncorrect()
    {
        var decision = Opening;

        var submitted = Scored(PlaySubmission.Score(ThirteenTenSixFive, decision, PlayRanking.Equity));

        Assert.Equal(1, submitted.MatchedCandidateIndex);
        Assert.Equal(decision.Decision.RankedBy(PlayRanking.Equity).ForCandidate(1).Error, submitted.EquityLoss);
        Assert.True(submitted.EquityLoss > 0.0);
        Assert.False(submitted.IsCorrect);
    }

    [Fact]
    public void Score_TheBestIsTheRankingsFirst_NotTheStoredFirst()
    {
        // The producer's stored order need not be any ranking's: here the
        // first stored candidate is the worse play. Its error is measured
        // against the ranking's best, and the stored-second play is correct.
        var decision = TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(plays:
        [
            TestRecords.Candidate(play: ThirteenTenSixFive, equity: 0.0),
            TestRecords.Candidate(play: EightFiveSixFive, equity: 0.2),
        ]));

        var storedFirst = Scored(PlaySubmission.Score(ThirteenTenSixFive, decision, PlayRanking.Equity));
        var rankingsBest = Scored(PlaySubmission.Score(EightFiveSixFive, decision, PlayRanking.Equity));

        Assert.Equal(0, storedFirst.MatchedCandidateIndex);
        Assert.Equal(0.2, storedFirst.EquityLoss, precision: 12);
        Assert.False(storedFirst.IsCorrect);
        Assert.Equal(1, rankingsBest.MatchedCandidateIndex);
        Assert.True(rankingsBest.IsCorrect);
    }

    [Fact]
    public void Score_ATinyNonzeroError_IsIncorrect()
    {
        // Correct is an error of exactly 0 (SPEC-scoring §2a); the threshold
        // question of halheinrich/backgammon#202 is pending and changes
        // SubmittedPlay.IsCorrect alone when it lands.
        var decision = TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(plays:
        [
            TestRecords.Candidate(play: EightFiveSixFive, equity: 0.1),
            TestRecords.Candidate(play: ThirteenTenSixFive, equity: 0.1 - 1e-9),
        ]));

        var submitted = Scored(PlaySubmission.Score(ThirteenTenSixFive, decision, PlayRanking.Equity));

        Assert.True(submitted.EquityLoss > 0.0);
        Assert.False(submitted.IsCorrect);
    }

    [Fact]
    public void Score_TheErrorIsMeasuredAgainstTheRankingsBest_NotTheEquityBest()
    {
        // 24/23 13/10 at 0.05: under equity the best is 8/5 6/5 at 0.20
        // (error 0.15); under depth first the best is 13/10 6/5 at 0.10
        // (error 0.05). The ranking decides which play the error is against.
        var decision = TwoDepths;

        var underEquity = Scored(PlaySubmission.Score(
            TwentyFourTwentyThreeThirteenTen, decision, PlayRanking.Equity));
        var underDepth = Scored(PlaySubmission.Score(
            TwentyFourTwentyThreeThirteenTen, decision, PlayRanking.DepthFirst));

        Assert.Equal(0.20 - 0.05, underEquity.EquityLoss, precision: 12);
        Assert.Equal(0.10 - 0.05, underDepth.EquityLoss, precision: 12);
        Assert.Equal(PlayRanking.Equity, underEquity.Ranking);
        Assert.Equal(PlayRanking.DepthFirst, underDepth.Ranking);
    }

    [Fact]
    public void Score_TheRankingsBest_IsCorrect_UnderEitherRanking()
    {
        var decision = TwoDepths;

        Assert.True(Scored(PlaySubmission.Score(EightFiveSixFive, decision, PlayRanking.Equity)).IsCorrect);
        Assert.True(Scored(PlaySubmission.Score(ThirteenTenSixFive, decision, PlayRanking.DepthFirst)).IsCorrect);
        Assert.False(Scored(PlaySubmission.Score(ThirteenTenSixFive, decision, PlayRanking.Equity)).IsCorrect);
    }

    [Theory]
    [InlineData(PlayRanking.Equity)]
    [InlineData(PlayRanking.DepthFirst)]
    public void Score_EveryCandidate_AgreesWithTheRankingAndCannotDisagreeWithItself(PlayRanking ranking)
    {
        // For every candidate: a scored one carries the ranking's error, and
        // is correct exactly when that error is 0; a not-scored one has no
        // error under the ranking.
        var decision = TwoDepths;
        var ranked = decision.Decision.RankedBy(ranking);

        for (int i = 0; i < decision.Decision.Plays.Count; i++)
        {
            var submission = PlaySubmission.Score(decision.Decision.Plays[i].Play, decision, ranking);
            var rankedPlay = ranked.ForCandidate(i);
            if (rankedPlay.Error is double error)
            {
                var submitted = Scored(submission);
                Assert.Equal(i, submitted.MatchedCandidateIndex);
                Assert.Equal(error, submitted.EquityLoss);
                Assert.Equal(error == 0.0, submitted.IsCorrect);
            }
            else
            {
                Assert.Equal(PlaySubmissionKind.NotScored, submission.Kind);
            }
        }
    }

    [Fact]
    public void Score_CarriesTheRecordsOwnKeyAndTheUsersOwnEncoding()
    {
        // The key is derived from the record scored, never taken from a
        // caller: the same play of the opening at another score is another
        // problem, and carries that record's key.
        Play reversed = [new(6, 5), new(8, 5)];
        var elsewhere = OpeningElsewhere;

        var submitted = Scored(PlaySubmission.Score(reversed, Opening, PlayRanking.Equity));
        var submittedElsewhere = Scored(PlaySubmission.Score(reversed, elsewhere, PlayRanking.Equity));

        Assert.Equal(ProblemKey.From(Opening), submitted.ProblemKey);
        Assert.Equal(ProblemKey.From(elsewhere), submittedElsewhere.ProblemKey);
        Assert.NotEqual(submitted.ProblemKey, submittedElsewhere.ProblemKey);
        Assert.True(submitted.UserPlay.IsSameEncoding(reversed));
        Assert.False(submitted.UserPlay.IsSameEncoding(EightFiveSixFive));
    }

    [Fact]
    public void Score_CarriesThePositionThePlayReaches()
    {
        // The play's identity is the board it leaves: the matched candidate's
        // after-board, which is the board applying the user's own encoding
        // leaves. 13/10 6/5 is neither the stored first nor the best, so
        // neither can stand in for it.
        var decision = Opening;
        Play reversed = [new(6, 5), new(13, 10)];
        var applied = new BoardState(decision.Board);
        applied.ApplyPlay(reversed);

        var submitted = Scored(PlaySubmission.Score(reversed, decision, PlayRanking.Equity));

        Assert.Equal(1, submitted.MatchedCandidateIndex);
        Assert.Equal(decision.AfterBoardOf(1), submitted.AfterBoard);
        Assert.Equal(applied.ToPosition(), submitted.AfterBoard);
        Assert.NotEqual(decision.AfterBoardOf(0), submitted.AfterBoard);
    }

    // -----------------------------------------------------------------------
    //  Matched by position, not by encoding
    // -----------------------------------------------------------------------

    public static TheoryData<string, Play> EncodingsOfEightFiveSixFive => new()
    {
        { "as stored", [new(8, 5), new(6, 5)] },
        { "the moves in the other order", [new(6, 5), new(8, 5)] },
    };

    [Theory]
    [MemberData(nameof(EncodingsOfEightFiveSixFive))]
    public void Score_AnyEncodingOfACandidate_MatchesIt(string label, Play play)
    {
        _ = label;
        Assert.Equal(0, Scored(PlaySubmission.Score(play, Opening, PlayRanking.Equity)).MatchedCandidateIndex);
    }

    [Fact]
    public void Score_ADecomposedTrajectory_MatchesTheCombinedCandidate()
    {
        // 13/9 with a 3-1 is 13/10 10/9 hop by hop: a stored single-move
        // candidate is found from its two hops.
        var decision = TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(plays:
        [
            TestRecords.Candidate(play: [new(13, 9)], equity: 0.0),
            TestRecords.Candidate(play: EightFiveSixFive, equity: 0.2),
        ]));

        var submitted = Scored(PlaySubmission.Score([new(13, 10), new(10, 9)], decision, PlayRanking.Equity));

        Assert.Equal(0, submitted.MatchedCandidateIndex);
    }

    [Fact]
    public void Score_TheHitMarkOnTheOtherChecker_MatchesTheSameCandidate()
    {
        // 8/3* 7/3 and 8/3 7/3* are one play: the candidate stores the mark
        // on the checker from the 8, the user put it on the one from the 7.
        Play markOnTheSeven = [new(8, 3), new(7, -3)];

        var submitted = Scored(PlaySubmission.Score(markOnTheSeven, BlotOnTheThree, PlayRanking.Equity));

        Assert.Equal(0, submitted.MatchedCandidateIndex);
        Assert.True(submitted.IsCorrect);
        Assert.True(submitted.UserPlay.IsSameEncoding(markOnTheSeven));
    }

    // -----------------------------------------------------------------------
    //  The two skips of record: no scored submission, and which one
    // -----------------------------------------------------------------------

    [Fact]
    public void Score_APlayThatIsNoCandidate_IsOffList()
    {
        Play offList = [new(24, 21), new(24, 23)];

        var submission = PlaySubmission.Score(offList, Opening, PlayRanking.Equity);

        Assert.Equal(PlaySubmissionKind.OffList, submission.Kind);
        Assert.False(submission.TryGetScored(out var scored));
        Assert.Null(scored);
    }

    public static TheoryData<string, Play> InvalidFromTheBlotPosition => new()
    {
        // Ruling 2: a hit mark that disagrees with the board makes the play
        // invalid from that position — here, no mark on the blot landed on.
        { "the blot landed on unmarked", [new(8, 3), new(7, 3)] },
        // And a mark where no blot stands.
        { "a mark on an empty point", [new(13, -8), new(13, 9)] },
    };

    [Theory]
    [MemberData(nameof(InvalidFromTheBlotPosition))]
    public void Score_APlayInvalidFromThePosition_IsOffList_EvenBesideAMatchingEncoding(string label, Play play)
    {
        _ = label;
        var submission = PlaySubmission.Score(play, BlotOnTheThree, PlayRanking.Equity);

        Assert.Equal(PlaySubmissionKind.OffList, submission.Kind);
        Assert.False(submission.TryGetScored(out _));
    }

    [Fact]
    public void Score_ACandidateTheRankingDoesNotScore_IsNotScored_NamingTheCandidate()
    {
        // Under depth first 8/5 6/5 was analysed less deeply than the best
        // and rated higher: treated as an off-list play, with no error
        // (SPEC-scoring §2a) — and the caller still learns which candidate.
        var submission = PlaySubmission.Score(EightFiveSixFive, TwoDepths, PlayRanking.DepthFirst);

        Assert.Equal(PlaySubmissionKind.NotScored, submission.Kind);
        Assert.False(submission.TryGetScored(out _));
        Assert.Equal(0, submission.Match(
            scored: static _ => -1, notScored: static index => index, offList: static () => -2));
    }

    [Fact]
    public void Score_TheSamePlay_IsScoredUnderEquity_WhereDepthFirstDoesNotScoreIt()
    {
        var submission = PlaySubmission.Score(EightFiveSixFive, TwoDepths, PlayRanking.Equity);

        Assert.True(Scored(submission).IsCorrect);
    }

    [Fact]
    public void Match_HandsEachCaseExactlyWhatItHas()
    {
        var scored = PlaySubmission.Score(ThirteenTenSixFive, Opening, PlayRanking.Equity);
        var offList = PlaySubmission.Score([new(24, 21), new(24, 23)], Opening, PlayRanking.Equity);

        Assert.Equal("scored 1", scored.Match(
            scored: static s => $"scored {s.MatchedCandidateIndex}",
            notScored: static i => $"not scored {i}",
            offList: static () => "off list"));
        Assert.Equal("off list", offList.Match(
            scored: static s => $"scored {s.MatchedCandidateIndex}",
            notScored: static i => $"not scored {i}",
            offList: static () => "off list"));
    }

    [Fact]
    public void Match_NullBranch_Throws()
    {
        var submission = PlaySubmission.Score(EightFiveSixFive, Opening, PlayRanking.Equity);

        Assert.Throws<ArgumentNullException>(() => submission.Match<int>(null!, static _ => 0, static () => 0));
        Assert.Throws<ArgumentNullException>(() => submission.Match(static _ => 0, null!, static () => 0));
        Assert.Throws<ArgumentNullException>(() => submission.Match(static _ => 0, static _ => 0, null!));
    }

    // -----------------------------------------------------------------------
    //  Guards
    // -----------------------------------------------------------------------

    [Fact]
    public void Score_NullDecision_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PlaySubmission.Score(EightFiveSixFive, null!, PlayRanking.Equity));
    }

    [Fact]
    public void Score_AnUndefinedRanking_Throws_WhateverThePlay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlaySubmission.Score(EightFiveSixFive, Opening, (PlayRanking)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlaySubmission.Score([new(24, 21), new(24, 23)], Opening, (PlayRanking)99));
    }

    // -----------------------------------------------------------------------
    //  SubmittedPlay identity: the same problem and the same play (Hal's
    //  ruling of 2026-09-27 on halheinrich/backgammon#273). The index, the
    //  encoding, the ranking, the error and the verdict are facts, outside it.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The opening 3-1 — the default problem, so every such record has one
    /// key — analysed as <paramref name="candidates"/>, in that stored order,
    /// all at one depth, so both rankings score every candidate.
    /// </summary>
    private static CheckerPlayDecision OpeningWith(params (Play Play, double Equity)[] candidates) =>
        TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(
            plays: [.. candidates.Select(c => TestRecords.Candidate(play: c.Play, equity: c.Equity))]));

    /// <summary>Equal through every door: both Equals, both operators, the hash, and a set.</summary>
    private static void AssertSameSubmittedPlay(SubmittedPlay a, SubmittedPlay b)
    {
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Single(new HashSet<SubmittedPlay> { a, b });
    }

    /// <summary>Unequal through every door: both Equals, both operators, and a set.</summary>
    private static void AssertDifferentSubmittedPlays(SubmittedPlay a, SubmittedPlay b)
    {
        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
        Assert.False(a.Equals((object)b));
        Assert.Equal(2, new HashSet<SubmittedPlay> { a, b }.Count);
    }

    [Fact]
    public void SubmittedPlay_TwoEncodingsOfOnePlay_AreTheSameSubmittedPlay()
    {
        // A play has no equality — Equals throws — so this passes only if the
        // submission's equality never reaches its play; and the encodings
        // differ, so it fails if equality compares them.
        var a = Scored(PlaySubmission.Score([new(8, 5), new(6, 5)], Opening, PlayRanking.Equity));
        var b = Scored(PlaySubmission.Score([new(6, 5), new(8, 5)], Opening, PlayRanking.Equity));

        Assert.False(a.UserPlay.IsSameEncoding(b.UserPlay));
        AssertSameSubmittedPlay(a, b);
    }

    [Fact]
    public void SubmittedPlay_ContentEqualRecordsListingTheCandidatesInAnotherOrder_AreTheSameSubmittedPlay()
    {
        // One analysis, stored in two orders: the same play matches another
        // row of each, and that row is all that differs.
        var stored = OpeningWith(
            (EightFiveSixFive, 0.20), (ThirteenTenSixFive, 0.10), (TwentyFourTwentyThreeThirteenTen, 0.05));
        var reordered = OpeningWith(
            (TwentyFourTwentyThreeThirteenTen, 0.05), (ThirteenTenSixFive, 0.10), (EightFiveSixFive, 0.20));

        var a = Scored(PlaySubmission.Score(EightFiveSixFive, stored, PlayRanking.Equity));
        var b = Scored(PlaySubmission.Score(EightFiveSixFive, reordered, PlayRanking.Equity));

        Assert.Equal(0, a.MatchedCandidateIndex);
        Assert.Equal(2, b.MatchedCandidateIndex);
        Assert.Equal(a.EquityLoss, b.EquityLoss);
        AssertSameSubmittedPlay(a, b);
    }

    [Fact]
    public void SubmittedPlay_UnderEitherRanking_IsTheSameSubmittedPlay()
    {
        // The opening's candidates share one depth, so both rankings agree on
        // every error: the best play is 0 under each. Only the ranking differs.
        var equity = Scored(PlaySubmission.Score(EightFiveSixFive, Opening, PlayRanking.Equity));
        var depth = Scored(PlaySubmission.Score(EightFiveSixFive, Opening, PlayRanking.DepthFirst));

        Assert.NotEqual(equity.Ranking, depth.Ranking);
        Assert.Equal(equity.MatchedCandidateIndex, depth.MatchedCandidateIndex);
        Assert.Equal(equity.EquityLoss, depth.EquityLoss);
        AssertSameSubmittedPlay(equity, depth);
    }

    [Fact]
    public void SubmittedPlay_UnderAnotherAnalysis_IsTheSameSubmittedPlay()
    {
        // One problem, one play, one ranking, one row; three analyses give it
        // three results. The first two differ in the error alone, the first
        // and the third in the verdict too.
        var slightlyWorse = OpeningWith((EightFiveSixFive, 0.2), (ThirteenTenSixFive, 0.1));
        var muchWorse = OpeningWith((EightFiveSixFive, 0.2), (ThirteenTenSixFive, 0.0));
        var best = OpeningWith((EightFiveSixFive, 0.2), (ThirteenTenSixFive, 0.3));

        var a = Scored(PlaySubmission.Score(ThirteenTenSixFive, slightlyWorse, PlayRanking.Equity));
        var b = Scored(PlaySubmission.Score(ThirteenTenSixFive, muchWorse, PlayRanking.Equity));
        var c = Scored(PlaySubmission.Score(ThirteenTenSixFive, best, PlayRanking.Equity));

        Assert.NotEqual(a.EquityLoss, b.EquityLoss);
        Assert.Equal(a.IsCorrect, b.IsCorrect);
        Assert.NotEqual(a.IsCorrect, c.IsCorrect);
        AssertSameSubmittedPlay(a, b);
        AssertSameSubmittedPlay(a, c);
    }

    [Fact]
    public void SubmittedPlay_EveryFactButTheProblemAndThePlayDiffers_StillTheSameSubmittedPlay()
    {
        // Same problem, same play = same submitted play. Here the index, the
        // encoding, the ranking, the error and the verdict all differ at once.
        var first = OpeningWith((EightFiveSixFive, 0.2), (ThirteenTenSixFive, 0.1));
        var second = OpeningWith((ThirteenTenSixFive, 0.3), (EightFiveSixFive, 0.2));

        var a = Scored(PlaySubmission.Score(ThirteenTenSixFive, first, PlayRanking.Equity));
        var b = Scored(PlaySubmission.Score([new(6, 5), new(13, 10)], second, PlayRanking.DepthFirst));

        Assert.NotEqual(a.MatchedCandidateIndex, b.MatchedCandidateIndex);
        Assert.False(a.UserPlay.IsSameEncoding(b.UserPlay));
        Assert.NotEqual(a.Ranking, b.Ranking);
        Assert.NotEqual(a.EquityLoss, b.EquityLoss);
        Assert.NotEqual(a.IsCorrect, b.IsCorrect);
        AssertSameSubmittedPlay(a, b);
    }

    [Fact]
    public void SubmittedPlay_ADifferentPlayOfTheSameProblem_IsADifferentSubmittedPlay()
    {
        // Two analyses of one problem, each with its best play first: every
        // fact but the play is alike — problem, row, ranking, error, verdict —
        // so only the play tells them apart. (The equality this replaced
        // compared the row, the ranking and the error, and called them equal.)
        var first = OpeningWith((EightFiveSixFive, 0.1), (ThirteenTenSixFive, 0.0));
        var second = OpeningWith((ThirteenTenSixFive, 0.1), (EightFiveSixFive, 0.0));

        var a = Scored(PlaySubmission.Score(EightFiveSixFive, first, PlayRanking.Equity));
        var b = Scored(PlaySubmission.Score(ThirteenTenSixFive, second, PlayRanking.Equity));

        Assert.Equal(a.ProblemKey, b.ProblemKey);
        Assert.Equal(a.MatchedCandidateIndex, b.MatchedCandidateIndex);
        Assert.Equal(a.Ranking, b.Ranking);
        Assert.Equal(a.EquityLoss, b.EquityLoss);
        Assert.NotEqual(a.AfterBoard, b.AfterBoard);
        AssertDifferentSubmittedPlays(a, b);
    }

    [Fact]
    public void SubmittedPlay_TheSamePlayOfAnotherProblem_IsADifferentSubmittedPlay()
    {
        // The same board and roll at another score: the play reaches the same
        // position with the same row and error, so only the problem tells
        // them apart.
        var a = Scored(PlaySubmission.Score(EightFiveSixFive, Opening, PlayRanking.Equity));
        var b = Scored(PlaySubmission.Score(EightFiveSixFive, OpeningElsewhere, PlayRanking.Equity));

        Assert.NotEqual(a.ProblemKey, b.ProblemKey);
        Assert.Equal(a.AfterBoard, b.AfterBoard);
        Assert.Equal(a.MatchedCandidateIndex, b.MatchedCandidateIndex);
        Assert.Equal(a.EquityLoss, b.EquityLoss);
        AssertDifferentSubmittedPlays(a, b);
    }

    [Fact]
    public void SubmittedPlay_NullComparisons()
    {
        var a = Scored(PlaySubmission.Score(EightFiveSixFive, Opening, PlayRanking.Equity));
        SubmittedPlay? none = null;

        Assert.False(a.Equals(none));
        Assert.False(a == none);
        Assert.True(a != none);
        Assert.True(none == null);
    }

    // -----------------------------------------------------------------------
    //  PlaySubmission equality: the same outcome
    // -----------------------------------------------------------------------

    [Fact]
    public void PlaySubmission_TheSameOutcome_IsEqual()
    {
        Assert.Equal(
            PlaySubmission.Score(EightFiveSixFive, Opening, PlayRanking.Equity),
            PlaySubmission.Score([new(6, 5), new(8, 5)], Opening, PlayRanking.Equity));
        // A scored outcome compares its submitted play's identity, so the same
        // play of one problem is the same outcome whichever row it matched.
        Assert.Equal(
            PlaySubmission.Score(
                EightFiveSixFive, OpeningWith((EightFiveSixFive, 0.2), (ThirteenTenSixFive, 0.1)),
                PlayRanking.Equity),
            PlaySubmission.Score(
                EightFiveSixFive, OpeningWith((ThirteenTenSixFive, 0.1), (EightFiveSixFive, 0.2)),
                PlayRanking.Equity));
        // The two skips carry no problem: a not-scored outcome is its case and
        // candidate index, an off-list one its case alone.
        Assert.Equal(
            PlaySubmission.Score(EightFiveSixFive, TwoDepths, PlayRanking.DepthFirst),
            PlaySubmission.Score(
                [new(6, 5), new(8, 5)], TwoDepthsFrom(AtFiveAwayThreeAway), PlayRanking.DepthFirst));
        Assert.Equal(
            PlaySubmission.Score([new(24, 21), new(24, 23)], Opening, PlayRanking.Equity),
            PlaySubmission.Score(
                [new(24, 22), new(24, 21)], TwoDepthsFrom(AtFiveAwayThreeAway), PlayRanking.DepthFirst));
    }

    [Fact]
    public void PlaySubmission_DifferentOutcomes_AreUnequal()
    {
        var scored = PlaySubmission.Score(EightFiveSixFive, TwoDepths, PlayRanking.Equity);
        var notScored = PlaySubmission.Score(EightFiveSixFive, TwoDepths, PlayRanking.DepthFirst);
        var offList = PlaySubmission.Score([new(24, 21), new(24, 23)], TwoDepths, PlayRanking.Equity);

        Assert.NotEqual(scored, notScored);
        Assert.NotEqual(notScored, offList);
        Assert.NotEqual(scored, offList);
        Assert.True(scored != notScored);
        Assert.NotEqual(
            scored,
            PlaySubmission.Score(EightFiveSixFive, TwoDepthsFrom(AtFiveAwayThreeAway), PlayRanking.Equity));
    }
}
