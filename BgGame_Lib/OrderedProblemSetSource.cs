namespace BgGame_Lib;

using System.Runtime.CompilerServices;
using BgDataTypes_Lib;

/// <summary>
/// <see cref="IProblemSetSource"/> decorator that presents an inner source's
/// decisions in a <see cref="ProblemOrder"/> (SPEC-filtering.md §7) — the
/// order's plain application, with no mix and no stats.
///
/// <para>
/// <b>As read is inert.</b> <see cref="ProblemOrder.AsRead"/> streams the
/// inner source through unchanged: no materialization, no randomness, no
/// dedupe.
/// </para>
///
/// <para>
/// <b>Every other order materializes.</b> Sorting or shuffling needs the
/// whole item set up front, so each <see cref="EnumerateAsync"/> call drains
/// the inner source into a list before yielding anything. For a lazily
/// streamed, uncounted inner source (e.g. <c>WasmUploadedProblemSetSource</c>,
/// whose <c>Count</c> is null), this trades that source's streaming for one
/// full materialization per enumeration — the cost of an order, not a free
/// wrapper. Each decision's key is then read once, in a pass that yields on
/// the cooperative time budget and honours cancellation, as the presentation
/// does; the sort itself is synchronous. Once the decisions are arranged,
/// the keys and the source-ordered list are released: the enumeration holds
/// the ordered decisions and nothing else per decision.
/// </para>
///
/// <para>
/// <b>Name / Count passthrough.</b> An order changes presentation only; it
/// neither changes cardinality nor knows more or less about the inner
/// source's size than the inner source itself. Both properties pass through
/// unchanged (including <c>Count</c> being null when the inner source doesn't
/// know its size up front).
/// </para>
///
/// <para>
/// <b>Re-iterability and Restart.</b> Every <see cref="EnumerateAsync"/> call
/// re-applies the order from the inner source's current state (itself
/// required to be re-iterable); nothing is cached between calls. So a quiz
/// controller's Restart under <see cref="ProblemOrder.Shuffled"/> draws a new
/// permutation from this instance's shared <see cref="Random"/> rather than
/// replaying the last one — a fresh random pass, which is what "restart a
/// shuffled quiz" means to a user — and the deterministic orders re-sort what
/// the inner source yields now.
/// </para>
///
/// <para>
/// <b>Reproducibility scope.</b> Mirrors <see cref="SeededDiceSource"/>: an
/// explicit seed makes a single instance's sequence of shuffles reproducible
/// within a given .NET runtime version (seeded <see cref="Random"/> uses the
/// framework's compatibility algorithm — stable in practice, not
/// contractually guaranteed across major .NET versions). That is sufficient
/// for deterministic tests; it is not an audit-grade guarantee. Only
/// <see cref="ProblemOrder.Shuffled"/> consumes the generator.
/// </para>
///
/// Not thread-safe — <see cref="Random"/> is not, and this wrapper does not
/// add its own synchronization.
/// </summary>
public sealed class OrderedProblemSetSource : IProblemSetSource
{
    private readonly IProblemSetSource _inner;
    private readonly ProblemOrder _order;
    private readonly PlayRanking _ranking;
    private readonly Random _random;

    /// <summary>
    /// Construct an ordered view over <paramref name="inner"/> whose shuffles
    /// are seeded from a non-deterministic source — for production use, where
    /// reproducibility isn't required.
    /// </summary>
    /// <param name="inner">The source whose decisions are ordered.</param>
    /// <param name="order">The order to present them in.</param>
    /// <param name="ranking">
    /// The run's ranking: the by-error order reads XG's error under it. Read
    /// by no other order, but always a defined ranking.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="order"/> or <paramref name="ranking"/> is not a defined member.
    /// </exception>
    public OrderedProblemSetSource(IProblemSetSource inner, ProblemOrder order, PlayRanking ranking)
        : this(inner, order, ranking, new Random())
    {
    }

    /// <summary>
    /// Construct an ordered view over <paramref name="inner"/> whose shuffle
    /// sequence is determined by <paramref name="seed"/> — for deterministic
    /// tests and replayable sessions.
    /// </summary>
    /// <param name="inner">The source whose decisions are ordered.</param>
    /// <param name="order">The order to present them in.</param>
    /// <param name="ranking">The run's ranking; the by-error order reads XG's error under it.</param>
    /// <param name="seed">Fixes the shuffle sequence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="order"/> or <paramref name="ranking"/> is not a defined member.
    /// </exception>
    public OrderedProblemSetSource(IProblemSetSource inner, ProblemOrder order, PlayRanking ranking, int seed)
        : this(inner, order, ranking, new Random(seed))
    {
    }

    /// <summary>
    /// The construction both public forms share, and the composer's door: a
    /// blank-mix <see cref="MixedProblemSetSource"/> hands in its own
    /// generator, so its seeded form reproduces the plain order too.
    /// </summary>
    internal OrderedProblemSetSource(IProblemSetSource inner, ProblemOrder order, PlayRanking ranking, Random random)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ProblemOrdering.RequireDefined(order, ranking);
        _inner = inner;
        _order = order;
        _ranking = ranking;
        _random = random;
    }

    /// <inheritdoc/>
    public string Name => _inner.Name;

    /// <inheritdoc/>
    public int? Count => _inner.Count;

    /// <inheritdoc/>
    public async IAsyncEnumerable<BgDecisionData> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_order == ProblemOrder.AsRead)
        {
            await foreach (var item in _inner.EnumerateAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
            yield break;
        }

        // Cooperative yielding is time-budgeted, not per-item: a per-item
        // Task.Yield() over a large materialized set pays tens of thousands of
        // scheduler round-trips on Blazor WASM's single event loop. One
        // yielder paces the whole path — the key pass and the presentation.
        // See CooperativeYielder.
        var yielder = new CooperativeYielder(TimeProvider.System);
        var ordered = await OrderAsync(yielder, cancellationToken);

        foreach (var item in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await yielder.YieldIfDueAsync();
        }
    }

    /// <summary>
    /// One enumeration's ordering: drain the inner source, read every
    /// decision's key once (paced and cancellable — see
    /// <see cref="ProblemOrdering"/>), and arrange. A method of its own so
    /// that what it builds — the source-ordered list, the positions, the keys
    /// — is released when it returns: from then on the enumeration holds the
    /// ordered decisions and nothing else per decision.
    /// </summary>
    private async ValueTask<List<BgDecisionData>> OrderAsync(
        CooperativeYielder yielder, CancellationToken cancellationToken)
    {
        var items = new List<BgDecisionData>();
        await foreach (var item in _inner.EnumerateAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(item);
        }

        var positions = Enumerable.Range(0, items.Count).ToList();
        var ordering = await ProblemOrdering.OverAsync(
            items, positions, _order, _ranking, records: null, _random, yielder, cancellationToken);
        ordering.Arrange(positions);
        return positions.ConvertAll(position => items[position]);
    }
}
