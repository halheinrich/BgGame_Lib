namespace BgGame_Lib;

using System.ComponentModel;
using System.Text.Json.Serialization;
using BgDataTypes_Lib;

/// <summary>
/// The order a run presents its problems in — the one definition of the four
/// orders (SPEC-filtering.md §7). A consumer holds the user's choice and
/// passes it in; this library applies it
/// (<see cref="OrderedProblemSetSource"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Equal keys keep source order.</b> Every order but
/// <see cref="Shuffled"/> is a stable order over the inner stream: two
/// decisions with the same key are presented in the order the source yielded
/// them.
/// </para>
/// <para>
/// <b>The by-error key.</b> The error XG recorded for the move actually
/// played, under the run's <see cref="PlayRanking"/>, exactly as the
/// filter's error facet reads it (<see cref="PlayerResult.TryGetError"/> on
/// <see cref="BgDecisionData.ViewFor"/>): a correct decision's is 0, and an
/// unstated move's is the analyser's. A decision with no error counts as
/// 0.0.
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
    /// key); equal errors keep source order.
    /// </summary>
    [Description("By error, largest first")]
    ByErrorLargestFirst,

    /// <summary>
    /// By move number alone, ascending, across games and files; a
    /// non-standard start's number is used as recorded. A decision with no
    /// move number — a position file's — goes last, in source order; equal
    /// numbers keep source order.
    /// </summary>
    [Description("By move number, ascending")]
    ByMoveNumberAscending,
}
