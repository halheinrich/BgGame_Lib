namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// One entry in a <see cref="Transcript"/> — an immutable record of a single
/// decision moment plus the state at which it occurred.
///
/// Three concrete subtypes:
///   • <see cref="PlayTranscriptEntry"/> — a play decision with dice and chosen Play.
///   • <see cref="CubeTranscriptEntry"/> — a cube offer or response.
///   • <see cref="GameEndedTranscriptEntry"/> — the terminating event of a game.
///
/// Pattern-match consumers use <c>is X</c> to reach the payload — the standard
/// C# discriminated-union pattern. A single entry record with nullable fields
/// would be worse because legal-payload-by-discriminator would have to be
/// enforced at runtime.
/// </summary>
public abstract record TranscriptEntry(GameSnapshot State);

/// <summary>A play decision: the dice rolled and the play chosen.</summary>
public sealed record PlayTranscriptEntry(
    GameSnapshot State,
    int Die1,
    int Die2,
    Play ChosenPlay) : TranscriptEntry(State);

/// <summary>A cube decision: an offer side action or a response side action.</summary>
public sealed record CubeTranscriptEntry(
    GameSnapshot State,
    CubeAction Action) : TranscriptEntry(State);

/// <summary>The terminating event of a game — bear-off completion or cube pass.</summary>
public sealed record GameEndedTranscriptEntry(
    GameSnapshot State,
    GameResult Result) : TranscriptEntry(State);
