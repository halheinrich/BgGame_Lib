namespace BgGame_Lib.Tests;

public class CooperativeYielderTests
{
    /// <summary>
    /// Deterministic monotonic clock: <see cref="TimeProvider.GetTimestamp"/>
    /// returns a manually advanced counter. <see cref="TimestampFrequency"/> is
    /// pinned to <see cref="TimeSpan.TicksPerSecond"/> so one advanced tick maps
    /// to one <see cref="TimeSpan"/> tick and <see cref="TimeProvider.GetElapsedTime(long)"/>
    /// reports exactly what was advanced.
    /// </summary>
    private sealed class FakeClock : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan by) => _timestamp += by.Ticks;
    }

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(50);

    // A within-budget YieldIfDueAsync completes synchronously (ValueTask.CompletedTask,
    // IsCompleted == true); a due one returns the Task.Yield() path, which never
    // completes synchronously (IsCompleted == false). That distinction is how these
    // tests observe whether a yield happened.

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

        var pending = yielder.YieldIfDueAsync();
        Assert.False(pending.IsCompleted);
        await pending;
    }

    [Fact]
    public async Task BudgetResets_AfterAYield()
    {
        var clock = new FakeClock();
        var yielder = new CooperativeYielder(clock, Budget);

        // First window elapses → yields, resetting the window to "now".
        clock.Advance(Budget);
        var first = yielder.YieldIfDueAsync();
        Assert.False(first.IsCompleted);
        await first;

        // No further time has passed since the reset → not due again.
        var second = yielder.YieldIfDueAsync();
        Assert.True(second.IsCompleted);
        await second;

        // A fresh full budget from the reset point → due once more.
        clock.Advance(Budget);
        var third = yielder.YieldIfDueAsync();
        Assert.False(third.IsCompleted);
        await third;
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
        var due = yielder.YieldIfDueAsync();
        Assert.False(due.IsCompleted);
        await due;
    }
}
