namespace BgGame_Lib.Tests;

public class CooperativeYielderTests
{
    /// <summary>
    /// Deterministic monotonic clock: <see cref="TimeProvider.GetTimestamp"/>
    /// returns a manually advanced counter. <see cref="TimestampFrequency"/> is
    /// pinned to <see cref="TimeSpan.TicksPerSecond"/> so one advanced tick maps
    /// to one <see cref="TimeSpan"/> tick and <see cref="TimeProvider.GetElapsedTime(long)"/>
    /// reports exactly what was advanced.
    ///
    /// <para>
    /// It also counts reads, which is how these tests observe whether a yield
    /// happened — see <see cref="YieldedAsync"/>.
    /// </para>
    /// </summary>
    private sealed class FakeClock : TimeProvider
    {
        private long _timestamp;

        /// <summary>How many times the yielder has read this clock.</summary>
        public int TimestampReads { get; private set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp()
        {
            TimestampReads++;
            return _timestamp;
        }
        public void Advance(TimeSpan by) => _timestamp += by.Ticks;
    }

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(50);

    // Observing a yield.
    //
    // A within-budget YieldIfDueAsync completes synchronously — it returns
    // ValueTask.CompletedTask, so IsCompleted is true immediately and stays
    // true. Asserting that is race-free, and the not-due tests below still do.
    //
    // The reverse — reading IsCompleted == false to prove a yield DID happen —
    // is not sound, and was the flake behind umbrella issue #40. The yield path
    // is an `async ValueTask` whose Task.Yield() continuation is posted to the
    // ambient scheduler; it can therefore run on another thread and complete the
    // ValueTask between the call returning and the test reading the flag. The
    // more test collections run in parallel, the more often that wins the race,
    // so the suite went red once ~18 unrelated tests were added elsewhere in the
    // project.
    //
    // What IS deterministic is the budget reset the yield path performs after
    // resuming: it reads the clock a second time (the first read computed the
    // elapsed time). Counting reads across an AWAITED call is race-free — the
    // await is what guarantees the continuation has already run.

    /// <summary>
    /// Await one gate call and report whether it yielded, observed race-free
    /// through the clock reads it performed rather than through
    /// <see cref="ValueTask.IsCompleted"/>.
    /// </summary>
    private static async Task<bool> YieldedAsync(CooperativeYielder yielder, FakeClock clock)
    {
        int readsBefore = clock.TimestampReads;
        await yielder.YieldIfDueAsync();
        int reads = clock.TimestampReads - readsBefore;

        // 1 = the due-check read alone (no yield); 2 = due-check plus the
        // post-resume budget reset. Pinned here so that if GetElapsedTime ever
        // stops reading the clock exactly once, these tests fail saying so
        // rather than silently mis-reporting every yield.
        Assert.InRange(reads, 1, 2);
        return reads == 2;
    }

    [Fact]
    public async Task WithinBudget_DoesNotYield()
    {
        var clock = new FakeClock();
        var yielder = new CooperativeYielder(clock, Budget);

        clock.Advance(Budget - TimeSpan.FromMilliseconds(1));

        var pending = yielder.YieldIfDueAsync();
        Assert.True(pending.IsCompleted);
        await pending;
    }

    [Fact]
    public async Task OnceBudgetElapses_Yields()
    {
        var clock = new FakeClock();
        var yielder = new CooperativeYielder(clock, Budget);

        clock.Advance(Budget);

        Assert.True(await YieldedAsync(yielder, clock));
    }

    [Fact]
    public async Task BudgetResets_AfterAYield()
    {
        var clock = new FakeClock();
        var yielder = new CooperativeYielder(clock, Budget);

        // First window elapses → yields, resetting the window to "now".
        clock.Advance(Budget);
        Assert.True(await YieldedAsync(yielder, clock));

        // No further time has passed since the reset → not due again.
        var second = yielder.YieldIfDueAsync();
        Assert.True(second.IsCompleted);
        await second;

        // A fresh full budget from the reset point → due once more.
        clock.Advance(Budget);
        Assert.True(await YieldedAsync(yielder, clock));
    }

    [Fact]
    public async Task NoYield_BeforeTheFirstBudgetWindowElapses()
    {
        // The window starts at construction, so an immediate call never yields.
        var clock = new FakeClock();
        var yielder = new CooperativeYielder(clock, Budget);

        var pending = yielder.YieldIfDueAsync();
        Assert.True(pending.IsCompleted);
        await pending;
    }

    [Fact]
    public void Constructor_NullClock_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CooperativeYielder(null!));
        Assert.Throws<ArgumentNullException>(() => new CooperativeYielder(null!, Budget));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveBudget_Throws(int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CooperativeYielder(new FakeClock(), TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public async Task DefaultBudget_IsUsed_ByTheSingleArgumentConstructor()
    {
        var clock = new FakeClock();
        var yielder = new CooperativeYielder(clock);

        // Just under the documented default → not due; at the default → due.
        clock.Advance(CooperativeYielder.DefaultBudget - TimeSpan.FromTicks(1));
        var notDue = yielder.YieldIfDueAsync();
        Assert.True(notDue.IsCompleted);
        await notDue;

        clock.Advance(TimeSpan.FromTicks(1));
        Assert.True(await YieldedAsync(yielder, clock));
    }
}
