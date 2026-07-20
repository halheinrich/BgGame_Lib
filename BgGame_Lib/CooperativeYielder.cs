namespace BgGame_Lib;

/// <summary>
/// A time-budgeted cooperative-yield gate for long synchronous loops running on
/// a single-threaded scheduler (e.g. Blazor WebAssembly's event loop).
///
/// <para>
/// <b>The problem it single-sources.</b> An <c>await Task.Yield()</c> after every
/// item of a large materialization pays one event-loop round-trip <i>per item</i>
/// — tens of thousands of them for a big corpus — which dominates the cost of a
/// quiz Start without letting the browser do anything useful in between.
/// <see cref="YieldIfDueAsync"/> instead yields only once a time budget has
/// elapsed since the last yield (<see cref="DefaultBudget"/>, ~50&#160;ms, by
/// default): often enough that the browser can repaint a busy cursor during a
/// long materialization, rare enough that the yields are not themselves the
/// bottleneck. Within the budget the call completes synchronously and allocates
/// nothing (<see cref="ValueTask.CompletedTask"/>).
/// </para>
///
/// <para>
/// <b>Why a class, not a struct.</b> The gate owns the <c>await Task.Yield()</c>
/// itself, so the yield mechanism — not merely the "is it due?" decision — is
/// single-sourced across every caller. An <see langword="async"/> method on a
/// value type mutates a copy of <c>this</c> and would silently lose the
/// budget-reset across the await, so owning the await requires a reference type.
/// </para>
///
/// <para>
/// <b>Clock seam.</b> Timing runs through a <see cref="TimeProvider"/>'s
/// monotonic <see cref="TimeProvider.GetTimestamp"/> /
/// <see cref="TimeProvider.GetElapsedTime(long)"/> (the testable analog of
/// <see cref="System.Diagnostics.Stopwatch"/>), so the yield policy is
/// deterministically unit-testable with a fake clock. Production callers pass
/// <see cref="TimeProvider.System"/>. The timing is a pure pacing concern: it
/// never affects which items flow through a loop or in what order, so it is
/// deliberately kept separate from any wall-clock <see cref="TimeProvider"/> a
/// caller may already hold for <i>semantic</i> reasons.
/// </para>
///
/// One instance per loop (the budget window starts at construction). Not
/// thread-safe — intended for a single sequential enumeration, matching the
/// single-threaded scheduler it exists to cooperate with.
/// </summary>
public sealed class CooperativeYielder
{
    /// <summary>
    /// The default minimum wall-clock interval between yields (~50&#160;ms):
    /// long enough that yields are not the bottleneck, short enough that the
    /// scheduler can repaint between them.
    /// </summary>
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromMilliseconds(50);

    private readonly TimeProvider _clock;
    private readonly TimeSpan _budget;
    private long _lastYieldTimestamp;

    /// <summary>
    /// Create a yielder pacing to <paramref name="budget"/>, measuring elapsed
    /// time through <paramref name="clock"/>. The budget window starts now, so
    /// the first yield is due only once <paramref name="budget"/> has elapsed.
    /// </summary>
    /// <param name="clock">
    /// The monotonic time source (its <see cref="TimeProvider.GetTimestamp"/> /
    /// <see cref="TimeProvider.GetElapsedTime(long)"/>). Production callers pass
    /// <see cref="TimeProvider.System"/>.
    /// </param>
    /// <param name="budget">
    /// The minimum interval between yields; must be strictly positive.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="budget"/> is zero or negative.
    /// </exception>
    public CooperativeYielder(TimeProvider clock, TimeSpan budget)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(budget, TimeSpan.Zero);
        _clock = clock;
        _budget = budget;
        _lastYieldTimestamp = clock.GetTimestamp();
    }

    /// <summary>
    /// Create a yielder pacing to <see cref="DefaultBudget"/>.
    /// </summary>
    /// <param name="clock">
    /// The monotonic time source. Production callers pass
    /// <see cref="TimeProvider.System"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
    public CooperativeYielder(TimeProvider clock)
        : this(clock, DefaultBudget)
    {
    }

    /// <summary>
    /// Yield control to the scheduler (via <see cref="Task.Yield"/>) if at least
    /// the budget has elapsed since the last yield, resetting the budget window;
    /// otherwise complete synchronously without yielding. The returned
    /// <see cref="ValueTask"/> is already completed on the no-yield path, so a
    /// caller can cheaply <c>await</c> it every iteration.
    /// </summary>
    public ValueTask YieldIfDueAsync()
    {
        if (_clock.GetElapsedTime(_lastYieldTimestamp) < _budget)
            return ValueTask.CompletedTask;

        return YieldCoreAsync();
    }

    private async ValueTask YieldCoreAsync()
    {
        await Task.Yield();
        _lastYieldTimestamp = _clock.GetTimestamp();
    }
}
