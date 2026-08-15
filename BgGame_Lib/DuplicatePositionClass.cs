namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// One content-equivalence class observed by a
/// <see cref="DistinctPositionProblemSetSource"/> enumeration: a
/// <see cref="ProblemKey"/> that more than one enumerated record rendered,
/// with the file-relative identities of every copy. Produced only for genuine
/// duplicates (<see cref="Members"/> always holds at least two ids —
/// singleton classes would just re-list the corpus) via
/// <see cref="DistinctPositionProblemSetSource.LastDuplicateClasses"/>.
///
/// <para>
/// This is the producer-side telemetry seam ruled to ride the #95 identity
/// arc (SPEC-stats-identity.md §4): the dedupe enumeration already holds the
/// equivalence classes in hand, and exposing them here is the SSOT-correct
/// base for a future "which files hold the same position" report
/// (halheinrich/backgammon#104 related work). No report UI exists yet
/// anywhere; this type is deliberately just the data.
/// </para>
///
/// <para>
/// A record for value-shaped reading, but — like <see cref="MixComposition"/>
/// — it wraps a list, so record equality is effectively reference equality;
/// compare contents if needed.
/// </para>
/// </summary>
/// <param name="Key">The shared content identity.</param>
/// <param name="Members">
/// The <see cref="DecisionId"/> of every copy, in enumeration-occurrence
/// order — the surviving first occurrence leads. Always at least two.
/// </param>
public sealed record DuplicatePositionClass(
    ProblemKey Key,
    IReadOnlyList<DecisionId> Members);
