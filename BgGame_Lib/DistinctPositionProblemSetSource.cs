namespace BgGame_Lib;

using System.Runtime.CompilerServices;
using BgDataTypes_Lib;

/// <summary>
/// <see cref="IProblemSetSource"/> decorator that presents each distinct
/// position exactly once: a quiz drawn through it never asks the same
/// position twice, wherever the copies came from.
///
/// <para>
/// <b>Content identity, not record identity.</b> The dedupe key is the
/// non-empty <see cref="BgDecisionData.Xgid"/> digest — the producer-stamped
/// content identity of the whole decision context (board, cube, score, dice,
/// on-roll: exactly what renders). Copies collapse across matches and files,
/// not just file-duplicate copies: <see cref="DecisionId"/> is file-relative,
/// so the same match under two filenames — or the same early position reached
/// in different matches — carries distinct ids yet renders the same problem.
/// This is distinct from <see cref="MixedProblemSetSource"/>'s internal
/// id-dedupe (the same record drawn twice); this type is about the same
/// position appearing under different identities.
/// </para>
///
/// <para>
/// <b>Fail-open on a missing digest.</b> Items whose <c>Xgid</c> is null,
/// empty, or whitespace pass through undeduped, even when several such items
/// are otherwise identical: a missing digest carries no content claim, and
/// treating absence as equality would collapse unrelated problems.
/// </para>
///
/// <para>
/// <b>Survivor rule.</b> With no preference predicate, the first occurrence
/// of each position survives. First-wins alone, however, can drop the copy
/// carrying the user's lifetime record (e.g. the id with GotWrong history),
/// silently emptying that mix pool — so the optional
/// <c>survivorPreference</c> predicate marks ids whose copy should survive:
/// among content-equal copies, a satisfying copy beats a non-satisfying one,
/// and ties (both satisfy, or neither) fall back to first occurrence. The
/// predicate is the whole stats seam — this library stays stats-agnostic;
/// consumers close over their own stats lookup. It is consulted only to
/// arbitrate content-equal copies, never for unique positions.
/// </para>
///
/// <para>
/// <b>Ordering and materialization cost.</b> Output order is the order of
/// first content-occurrence; a later-preferred copy takes the earlier copy's
/// position rather than its own. That placement rule needs the full item set
/// up front, so each <see cref="EnumerateAsync"/> call drains the inner
/// source into a list before yielding anything — the
/// <see cref="ShuffledProblemSetSource"/> materialization tradeoff, deliberate
/// here for the same reason.
/// </para>
///
/// <para>
/// <b>Name / Count.</b> <see cref="Name"/> passes through — dedupe doesn't
/// rename the set. <see cref="Count"/> is always null: the
/// <see cref="IProblemSetSource"/> contract requires a non-null count to
/// equal the number of items yielded, and how many positions collapse is
/// unknowable before enumeration.
/// </para>
///
/// <para>
/// <b>Re-iterability and Restart.</b> Every call re-drains the inner source
/// (itself required to be re-iterable) and re-evaluates the preference
/// predicate fresh, so a quiz controller's Restart after the consumer's
/// stats changed re-picks survivors against current state. No survivor list
/// is cached between calls.
/// </para>
///
/// <para>
/// <b>Intended layering.</b> Composes beneath shuffle / mix: the consumer
/// wires this decorator closest to the raw source, so every downstream layer
/// sees an already position-distinct stream. Nothing in this library depends
/// on that ordering; it is the consumer's wiring convention.
/// </para>
///
/// Holds no per-instance mutable state; safety of concurrent enumeration is
/// bounded by the inner source's and the supplied predicate's own
/// thread-safety.
/// </summary>
public sealed class DistinctPositionProblemSetSource : IProblemSetSource
{
    private readonly IProblemSetSource _inner;
    private readonly Func<DecisionId, bool>? _survivorPreference;

    /// <summary>
    /// Construct a position-distinct view over <paramref name="inner"/> with
    /// no survivor preference: the first occurrence of each position survives.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    public DistinctPositionProblemSetSource(IProblemSetSource inner)
        : this(inner, survivorPreference: null)
    {
    }

    /// <summary>
    /// Construct a position-distinct view over <paramref name="inner"/> where
    /// <paramref name="survivorPreference"/> arbitrates content-equal copies.
    /// </summary>
    /// <param name="inner">The source whose positions are deduped.</param>
    /// <param name="survivorPreference">
    /// Marks ids whose copy should survive dedupe (e.g. "this id has a
    /// lifetime-stats record"). Among content-equal copies a satisfying copy
    /// beats a non-satisfying one; ties fall back to first occurrence.
    /// <see langword="null"/> means no preference — first occurrence wins —
    /// so consumers may pass through whatever they resolved.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    public DistinctPositionProblemSetSource(
        IProblemSetSource inner,
        Func<DecisionId, bool>? survivorPreference)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _survivorPreference = survivorPreference;
    }

    /// <inheritdoc/>
    public string Name => _inner.Name;

    /// <inheritdoc/>
    public int? Count => null;

    /// <inheritdoc/>
    public async IAsyncEnumerable<BgDecisionData> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Survivor slots in first-content-occurrence order. Preferred caches
        // the predicate's verdict for the slot's current occupant; it stays
        // null until a duplicate actually contests the slot, so the predicate
        // runs at most once per item and not at all in a duplicate-free set.
        var survivors = new List<(BgDecisionData Item, bool? Preferred)>();
        var indexByXgid = new Dictionary<string, int>(StringComparer.Ordinal);

        await foreach (var item in _inner.EnumerateAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(item.Xgid))
            {
                survivors.Add((item, null));
                continue;
            }

            if (!indexByXgid.TryGetValue(item.Xgid, out int index))
            {
                indexByXgid.Add(item.Xgid, survivors.Count);
                survivors.Add((item, null));
                continue;
            }

            if (_survivorPreference is null)
                continue;

            var (incumbent, preferred) = survivors[index];
            preferred ??= _survivorPreference(incumbent.Id);
            survivors[index] = preferred.Value || !_survivorPreference(item.Id)
                ? (incumbent, preferred)
                : (item, true);
        }

        // Cooperative yielding is time-budgeted, not per-item: a per-item
        // Task.Yield() over a large materialized set pays tens of thousands of
        // scheduler round-trips on Blazor WASM's single event loop. See
        // CooperativeYielder.
        var yielder = new CooperativeYielder(TimeProvider.System);
        foreach (var (item, _) in survivors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await yielder.YieldIfDueAsync();
        }
    }
}
