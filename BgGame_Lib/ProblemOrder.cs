namespace BgGame_Lib;

using System.ComponentModel;
using System.Text.Json.Serialization;
using BgDataTypes_Lib;

/// <summary>
/// The order a run presents its problems in — the one definition of the four
/// orders (SPEC-filtering.md §7). A consumer holds the user's choice and
/// passes it in; this library applies it, exactly once, and decides which
/// application it gets: <see cref="ProblemSequence.BuildAsync"/> takes the order
/// with the mix and applies it by the mixed key when the mix is active, and
/// by the plain key when the mix is blank.
/// </summary>
/// <remarks>
/// <para>
/// <b>Equal keys fall in a random order, fresh on every Start and Restart</b>
/// (SPEC-filtering.md §7, ruled 2026-10-15, halheinrich/backgammon#418).
/// <see cref="ByErrorLargestFirst"/> and <see cref="ByMoveNumberAscending"/>
/// break a tie by a uniform random rank drawn when the run is prepared, so a
/// Restart never repeats the previous run's order among equal keys, and a
/// mix row's quota taken from a tie group is a uniform random choice from it.
/// <see cref="AsRead"/> alone presents source order.
/// </para>
/// <para>
/// <b>By error has one key, in two contexts.</b> Without a mix it is the
/// error XG recorded for the move actually played, under the run's
/// <see cref="PlayRanking"/>, exactly as the filter's error facet reads it
/// (<see cref="PlayerResult.TryGetError"/> on
/// <see cref="BgDecisionData.ViewFor"/>): a correct decision's is 0, and an
/// unstated move's is the analyser's. With an active mix it is the player's
/// lifetime loss per answer where the decision's record holds submissions —
/// the record the composition classified against, a cube's two halves summed
/// (SPEC-scoring.md, amended 2026-10-14) — and XG's error otherwise. A
/// decision with no error from either source counts as 0.0.
/// </para>
/// <para>
/// <b>With an active mix, the order works twice in one application.</b> Each
/// row's eligible set is put in the order and the row's quota is filled from
/// its front; the whole selection is then sorted again in the same order. So
/// <see cref="Shuffled"/> draws each row uniformly and shuffles the
/// presentation, and <see cref="AsRead"/> draws and presents in source order.
/// </para>
/// <para>
/// <b>The names are the wire tokens, and stable.</b> A consumer persists the
/// user's choice, so each member's declared name is its token, string-exact
/// in both directions (<see cref="StrictJsonStringEnumConverter{TEnum}"/>,
/// the convention of every enum on a wire here), and the numbering is not
/// contractual. <see cref="AsRead"/> is the default and deliberately the
/// zero value. Every member carries a <see cref="DescriptionAttribute"/> —
/// the UI-facing label, which belongs to the type owner.
/// </para>
/// </remarks>
[JsonConverter(typeof(StrictJsonStringEnumConverter<ProblemOrder>))]
public enum ProblemOrder
{
    /// <summary>
    /// Source order: files in the sample's order, decisions in game-and-move
    /// order within a file. The default.
    /// </summary>
    [Description("As read")]
    AsRead = 0,

    /// <summary>A fresh uniform random order on every enumeration.</summary>
    [Description("Shuffled")]
    Shuffled,

    /// <summary>
    /// By the decision's error, largest first (see the type remarks for the
    /// key); equal errors fall in a fresh random order.
    /// </summary>
    [Description("By error, largest first")]
    ByErrorLargestFirst,

    /// <summary>
    /// By move number alone, ascending, across games and files; a
    /// non-standard start's number is used as recorded. A decision with no
    /// move number — a position file's — goes last, in a fresh random order
    /// among such decisions; equal numbers fall in a fresh random order.
    /// </summary>
    [Description("By move number, ascending")]
    ByMoveNumberAscending,
}
