namespace BgGame_Lib;

using BgDataTypes_Lib;

/// <summary>
/// One content-equivalence class <see cref="DistinctProblems"/> observed: a
/// <see cref="ProblemKey"/> that more than one enumerated record rendered,
/// with the file-relative identities of every copy. Produced only for genuine
/// duplicates (<see cref="Members"/> always holds at least two ids —
/// singleton classes would just re-list the corpus) via
/// <see cref="DistinctProblems.DuplicateClasses"/>.
///
/// <para>
/// This is the producer-side telemetry seam ruled to ride the halheinrich/backgammon#95 identity
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
/// order — the surviving first occurrence leads. Always at least two. Copied
/// on construction into an immutable list.
/// </param>
public sealed record DuplicatePositionClass(
    ProblemKey Key,
    IReadOnlyList<DecisionId> Members)
{
    private readonly IReadOnlyList<DecisionId> _members = ImmutableCopy.Of(Members, nameof(Members));

    /// <summary>
    /// The <see cref="DecisionId"/> of every copy, survivor first; an immutable
    /// copy, so neither the caller's list nor a cast can change it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on init when the value is <see langword="null"/>.</exception>
    public IReadOnlyList<DecisionId> Members
    {
        get => _members;
        init => _members = ImmutableCopy.Of(value, nameof(Members));
    }
}
