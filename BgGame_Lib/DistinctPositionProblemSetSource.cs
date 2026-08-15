namespace BgGame_Lib;

using System.Runtime.CompilerServices;
using BgDataTypes_Lib;

/// <summary>
/// <see cref="IProblemSetSource"/> decorator that presents each distinct
/// problem exactly once: a quiz drawn through it never asks the same
/// problem twice, wherever the copies came from.
///
/// <para>
/// <b>Content identity, not record identity.</b> The dedupe key is the
/// derived <see cref="ProblemKey"/> — the decomposed content identity of the
/// whole decision problem (SPEC-stats-identity.md §4;
/// halheinrich/backgammon#95). Copies collapse across matches and files, not
/// just file-duplicate copies: <see cref="DecisionId"/> is file-relative, so
/// the same match under two filenames — or the same early position reached
/// in different matches — carries distinct ids yet renders the same problem.
/// The key collapses strictly more than the raw XGID string did (the
/// pre-#95 dedupe key): same-away positions from different match lengths
/// unify, and mirror-turn duplicates unify — the ratified consequence, not a
/// defect. The XGID string is display/provenance only, an identity nowhere.
/// This layer is distinct from <see cref="MixedProblemSetSource"/>'s internal
/// id-dedupe (the same record drawn twice); this type is about the same
/// problem appearing under different identities.
/// </para>
///
/// <para>
/// <b>Fail-open on an underivable key.</b> Items with no derivable
/// <see cref="ProblemKey"/> (the no-key rung: malformed or degenerate facts —
/// see <see cref="ProblemKey.TryDerive"/>) pass through undeduped, even when
/// several such items are otherwise identical: derivation that would guess is
/// forbidden, and treating underivability as equality would collapse
/// unrelated problems.
/// </para>
///
/// <para>
/// <b>First occurrence survives.</b> Among content-equal copies the first
/// occurrence is yielded and later copies are dropped — for display and
/// provenance only, since nothing downstream keys on the surviving copy's
/// identity: lifetime stats are content-keyed, so every copy folds into and
/// reads the same record. (The pre-#95 survivor-preference seam existed
/// solely to keep id-keyed stats reachable and is deleted, not bypassed.)
/// </para>
///
/// <para>
/// <b>Duplicate-class telemetry.</b> <see cref="LastDuplicateClasses"/>
/// carries the most recent enumeration's content-equivalence classes — one
/// <see cref="DuplicatePositionClass"/> per key that had two or more copies —
/// assigned before the first item is yielded. The producer-side seam for a
/// future duplicate-file report; see the record type.
/// </para>
///
/// <para>
/// <b>Ordering and materialization cost.</b> Output order is the order of
/// first content-occurrence. Determining which later copies collapse — and
/// the full class inventory — needs the whole item set up front, so each
/// <see cref="EnumerateAsync"/> call drains the inner source into a list
/// before yielding anything: the <see cref="ShuffledProblemSetSource"/>
/// materialization tradeoff, deliberate here for the same reason.
/// </para>
///
/// <para>
/// <b>Name / Count.</b> <see cref="Name"/> passes through — dedupe doesn't
/// rename the set. <see cref="Count"/> is always null: the
/// <see cref="IProblemSetSource"/> contract requires a non-null count to
/// equal the number of items yielded, and how many problems collapse is
/// unknowable before enumeration.
/// </para>
///
/// <para>
/// <b>Re-iterability.</b> Every call re-drains the inner source (itself
/// required to be re-iterable) and recomputes survivors and duplicate
/// classes fresh; nothing is cached between calls.
/// </para>
///
/// <para>
/// <b>Intended layering.</b> Composes beneath shuffle / mix: the consumer
/// wires this decorator closest to the raw source, so every downstream layer
/// sees an already problem-distinct stream. Nothing in this library depends
/// on that ordering; it is the consumer's wiring convention.
/// </para>
///
/// Not thread-safe: <see cref="LastDuplicateClasses"/> is per-instance state
/// written by each enumeration (and safety of concurrent enumeration is
/// additionally bounded by the inner source's own thread-safety).
/// </summary>
public sealed class DistinctPositionProblemSetSource : IProblemSetSource
{
    private readonly IProblemSetSource _inner;

    /// <summary>
    /// Construct a problem-distinct view over <paramref name="inner"/>: the
    /// first occurrence of each problem survives.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    public DistinctPositionProblemSetSource(IProblemSetSource inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc/>
    public string Name => _inner.Name;

    /// <inheritdoc/>
    public int? Count => null;

    /// <summary>
    /// Telemetry from the most recent enumeration: the content-equivalence
    /// classes with two or more copies, in first-occurrence order (empty when
    /// nothing collapsed). Null before the first enumeration. Assigned before
    /// the first item is yielded. Items with no derivable key never form a
    /// class. Per-instance state — see the thread-safety note in the type
    /// remarks.
    /// </summary>
    public IReadOnlyList<DuplicatePositionClass>? LastDuplicateClasses { get; private set; }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BgDecisionData> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Survivors in first-content-occurrence order. Duplicate classes are
        // materialized lazily, keyed by survivor slot: a class exists only
        // once a second copy actually contests the slot, so a duplicate-free
        // pass allocates no per-item member lists.
        var survivors = new List<BgDecisionData>();
        var indexByKey = new Dictionary<ProblemKey, int>();
        var classesBySlot = new Dictionary<int, (ProblemKey Key, List<DecisionId> Members)>();

        await foreach (var item in _inner.EnumerateAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ProblemKey.TryDerive(item, out var key))
            {
                survivors.Add(item);   // no-key rung: fail-open, never merged
                continue;
            }

            if (!indexByKey.TryGetValue(key, out int index))
            {
                indexByKey.Add(key, survivors.Count);
                survivors.Add(item);
                continue;
            }

            if (!classesBySlot.TryGetValue(index, out var cls))
                classesBySlot[index] = cls = (key, [survivors[index].Id]);
            cls.Members.Add(item.Id);
        }

        LastDuplicateClasses = classesBySlot
            .OrderBy(entry => entry.Key)
            .Select(entry => new DuplicatePositionClass(entry.Value.Key, entry.Value.Members))
            .ToList();

        // Cooperative yielding is time-budgeted, not per-item: a per-item
        // Task.Yield() over a large materialized set pays tens of thousands of
        // scheduler round-trips on Blazor WASM's single event loop. See
        // CooperativeYielder.
        var yielder = new CooperativeYielder(TimeProvider.System);
        foreach (var item in survivors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await yielder.YieldIfDueAsync();
        }
    }
}
