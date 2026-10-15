namespace BgGame_Lib;

using System.Runtime.ExceptionServices;

/// <summary>
/// The one definition of how an <see cref="IProblemSetSource"/> decorator
/// releases the inner streams it draws from (SPEC-quiz-history.md §8,
/// "Releasing never misreports"): an inner stream's release never replaces a
/// draw's outcome, and what the release failed with is reported where the
/// decorator's own stream is released.
///
/// <para>
/// <b>Why the decorators need it.</b> <c>await foreach</c> releases its
/// enumerator in a <c>finally</c>, so a release that throws replaces whatever
/// the loop was ending with, and the decorator's own <c>MoveNextAsync</c>
/// throws it: a draw that faulted loses its fault, and a stream that ran out
/// throws instead of reporting its end, so its consumer never learns that it
/// ended. A compiler-generated iterator has no way to defer that to its own
/// <c>DisposeAsync</c>: once it has run to its end, its release does nothing.
/// </para>
///
/// <para>
/// <b>What is deferred is the report, never the release.</b> A decorator
/// still draws from each inner stream with <c>await foreach</c>, over
/// <see cref="Draw"/>, so the loop releases the inner enumerator where it
/// always did — when the stream runs out, at a fault, or when the decorator's
/// own stream is released early — and exactly once; a materializing decorator
/// still releases its inner stream before it presents anything. What changes
/// is where a failure of that release goes. It is kept here, the exception
/// alone: the inner enumerator is dropped as it is released, and its release
/// is never retried. The loop then ends as it would have without it.
/// </para>
///
/// <para>
/// <b>The report.</b> <see cref="ReportedOnRelease"/> builds the decorator's
/// own stream. Each enumeration of it has an instance of its own, and the
/// enumeration's <c>DisposeAsync</c> releases the decorator's iterator and
/// then throws what the instance kept: one failure as itself, its stack trace
/// kept, and several as an <see cref="AggregateException"/> in the order they
/// were kept. A failure of the iterator's own release is kept with them, last.
/// The report is made once; a second release throws nothing. A decorator over
/// another decorator draws from that one's stream like any other, so the inner
/// decorator's report is a failure of a release it keeps in turn: a failure at
/// any depth reaches the outermost consumer's release.
/// </para>
///
/// <para>
/// <b>What it leaves to the consumer.</b> A plain <c>await foreach</c> over a
/// decorator releases in its own <c>finally</c>, where the report replaces the
/// loop's outcome as an inner release's once did inside the decorator. A
/// consumer that needs the draw's outcome to win guards its release
/// explicitly (SPEC-quiz-history.md §8).
/// </para>
///
/// Not thread-safe: an instance belongs to one enumeration, which runs
/// sequentially.
/// </summary>
internal sealed class InnerReleases
{
    /// <summary>The release failures kept so far, in the order they happened; null while there are none.</summary>
    private List<ExceptionDispatchInfo>? _failures;

    private InnerReleases()
    {
    }

    /// <summary>
    /// A decorator's own stream. Each enumeration runs
    /// <paramref name="enumerate"/> with an instance of its own, and releasing
    /// the enumeration reports what that instance kept.
    /// </summary>
    /// <param name="enumerate">
    /// The decorator's iterator, which draws from every inner stream through
    /// <see cref="Draw"/>. It is given <paramref name="cancellationToken"/>,
    /// and opened with the token given when the stream is opened, so its
    /// <c>[EnumeratorCancellation]</c> parameter combines the two exactly as
    /// it would if it were the stream itself.
    /// </param>
    /// <param name="cancellationToken">The token given to the decorator's <c>EnumerateAsync</c>.</param>
    internal static IAsyncEnumerable<T> ReportedOnRelease<T>(
        Func<InnerReleases, CancellationToken, IAsyncEnumerable<T>> enumerate,
        CancellationToken cancellationToken) =>
        new ReportingStream<T>(enumerate, cancellationToken);

    /// <summary>
    /// <paramref name="inner"/>, to draw from with <c>await foreach</c>. The
    /// loop releases it where it always does, once; a failure of that release
    /// is kept here rather than thrown.
    /// </summary>
    internal IAsyncEnumerable<T> Draw<T>(IAsyncEnumerable<T> inner) => new DrawnStream<T>(this, inner);

    private void Keep(Exception failure) => (_failures ??= []).Add(ExceptionDispatchInfo.Capture(failure));

    /// <summary>Throws what was kept, once: one failure as itself, several as an aggregate.</summary>
    private void Report()
    {
        var failures = _failures;
        _failures = null;
        if (failures is null)
            return;
        if (failures.Count == 1)
            failures[0].Throw();
        throw new AggregateException(
            "Releasing the stream failed more than once.",
            failures.Select(failure => failure.SourceException));
    }

    /// <summary>A decorator's own stream: an instance of <see cref="InnerReleases"/> per enumeration.</summary>
    private sealed class ReportingStream<T>(
        Func<InnerReleases, CancellationToken, IAsyncEnumerable<T>> enumerate,
        CancellationToken enumerateToken) : IAsyncEnumerable<T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            var releases = new InnerReleases();
            var iterator = enumerate(releases, enumerateToken).GetAsyncEnumerator(cancellationToken);
            return new ReportingEnumerator<T>(iterator, releases);
        }
    }

    /// <summary>
    /// One enumeration of a decorator's stream: its draws are the iterator's,
    /// and its release reports what the iterator's draws kept.
    /// </summary>
    private sealed class ReportingEnumerator<T>(IAsyncEnumerator<T> iterator, InnerReleases releases)
        : IAsyncEnumerator<T>
    {
        public T Current => iterator.Current;

        public ValueTask<bool> MoveNextAsync() => iterator.MoveNextAsync();

        public async ValueTask DisposeAsync()
        {
            try
            {
                await iterator.DisposeAsync();
            }
            catch (Exception failure)
            {
                releases.Keep(failure);
            }
            releases.Report();
        }
    }

    /// <summary>An inner stream a decorator draws from: each enumeration's release keeps its failure.</summary>
    private sealed class DrawnStream<T>(InnerReleases releases, IAsyncEnumerable<T> inner) : IAsyncEnumerable<T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new DrawnEnumerator<T>(releases, inner.GetAsyncEnumerator(cancellationToken));
    }

    /// <summary>
    /// One draw from an inner stream. Its release releases the inner
    /// enumerator once and drops it, keeping a failure rather than throwing it.
    /// </summary>
    private sealed class DrawnEnumerator<T>(InnerReleases releases, IAsyncEnumerator<T> inner)
        : IAsyncEnumerator<T>
    {
        /// <summary>The inner enumerator, until it is released.</summary>
        private IAsyncEnumerator<T>? _inner = inner;

        public T Current
        {
            get
            {
                ObjectDisposedException.ThrowIf(_inner is null, this);
                return _inner.Current;
            }
        }

        public ValueTask<bool> MoveNextAsync()
        {
            ObjectDisposedException.ThrowIf(_inner is null, this);
            return _inner.MoveNextAsync();
        }

        public async ValueTask DisposeAsync()
        {
            var released = _inner;
            if (released is null)
                return;
            _inner = null;
            try
            {
                await released.DisposeAsync();
            }
            catch (Exception failure)
            {
                releases.Keep(failure);
            }
        }
    }
}
