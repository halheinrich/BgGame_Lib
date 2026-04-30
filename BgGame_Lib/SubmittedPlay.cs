namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// One play submitted by a quiz user, scored against the position's candidate
/// plays.
/// </summary>
/// <param name="UserPlay">The play the user chose.</param>
/// <param name="MatchedCandidateIndex">
/// Index into the position's <c>Plays</c> list of the candidate the user's
/// play matched. Null if the user's play did not match any listed candidate
/// (rare — would mean an analysis omission in the source data).
/// </param>
/// <param name="EquityLoss">Equity loss vs. the best candidate (0 if best).</param>
/// <param name="IsCorrect">True iff the user matched the best candidate (i.e., zero equity loss).</param>
public sealed record SubmittedPlay(
    Play UserPlay,
    int? MatchedCandidateIndex,
    double EquityLoss,
    bool IsCorrect);
