namespace BgGame_Lib;

using System.Runtime.CompilerServices;
using BgDataTypes_Lib;

/// <summary>
/// <see cref="IProblemSetSource"/> decorator that presents an inner source's
/// decisions in a randomized order via a Fisher-Yates shuffle.
///
/// <para>
/// <b>Materialization cost.</b> Shuffling requires the full item set up front,
/// so each <see cref="EnumerateAsync"/> call drains the inner source into a
/// list before yielding anything. For a lazily streamed, uncounted inner
/// source (e.g. <c>WasmUploadedProblemSetSource</c>, whose <c>Count</c> is
/// null), this trades that source's streaming/lazy-count behavior for one
/// full materialization per enumeration — a deliberate tradeoff for
/// shuffling, not a free wrapper.
/// </para>
///
/// <para>
/// <b>Name / Count passthrough.</b> Shuffling only changes presentation
/// order; it neither changes cardinality nor knows more or less about the
/// inner source's size than the inner source itself. Both properties pass
/// through unchanged (including <c>Count</c> being null when the inner
/// source doesn't know its size up front).
/// </para>
///
/// <para>
/// <b>Re-iterability and Restart.</b> Every <see cref="EnumerateAsync"/> call
/// re-materializes and re-shuffles from the inner source's current state
/// (itself required to be re-iterable) using this instance's shared
/// <see cref="Random"/>, so successive calls — e.g. a quiz controller's
/// Restart — draw a new permutation each time rather than replaying the same
/// shuffled order. This matches what "restart a shuffled quiz" means to a
/// user (a fresh random pass), and keeps the decorator stateless beyond the
/// RNG itself: no shuffled list is cached between calls.
/// </para>
///
/// <para>
/// <b>Reproducibility scope.</b> Mirrors <see cref="SeededDiceSource"/>: an
/// explicit seed makes a single instance's sequence of shuffles reproducible
/// within a given .NET runtime version (seeded <see cref="Random"/> uses the
/// framework's compatibility algorithm — stable in practice, not
/// contractually guaranteed across major .NET versions). That is sufficient
/// for deterministic tests; it is not an audit-grade guarantee.
/// </para>
///
/// Not thread-safe — <see cref="Random"/> is not, and this wrapper does not
/// add its own synchronization.
/// </summary>
public sealed class ShuffledProblemSetSource : IProblemSetSource
{
    private readonly IProblemSetSource _inner;
    private readonly Random _random;

    /// <summary>
    /// Construct a shuffled view over <paramref name="inner"/> whose shuffle
    /// sequence is determined by <paramref name="seed"/> — for deterministic
    /// tests and replayable sessions.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    public ShuffledProblemSetSource(IProblemSetSource inner, int seed)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _random = new Random(seed);
    }

    /// <summary>
    /// Construct a shuffled view over <paramref name="inner"/> seeded from a
    /// non-deterministic source — for production use where reproducibility
    /// isn't required.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    public ShuffledProblemSetSource(IProblemSetSource inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _random = new Random();
    }

    /// <inheritdoc/>
    public string Name => _inner.Name;

    /// <inheritdoc/>
    public int? Count => _inner.Count;

    /// <inheritdoc/>
    public async IAsyncEnumerable<BgDecisionData> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var items = new List<BgDecisionData>();
        await foreach (var item in _inner.EnumerateAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(item);
        }

        // Fisher-Yates: for each index from the end, swap with a uniformly
        // chosen earlier-or-equal index.
        for (int i = items.Count - 1; i > 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        // Cooperative yielding is time-budgeted, not per-item: a per-item
        // Task.Yield() over a large materialized set pays tens of thousands of
        // scheduler round-trips on Blazor WASM's single event loop. See
        // CooperativeYielder.
        var yielder = new CooperativeYielder(TimeProvider.System);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await yielder.YieldIfDueAsync();
        }
    }
}
