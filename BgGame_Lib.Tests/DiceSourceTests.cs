namespace BgGame_Lib.Tests;

public class DiceSourceTests
{
    // ── SeededDiceSource ──────────────────────────────────────────

    [Fact]
    public void Seeded_SameSeed_YieldsSameSequence()
    {
        var a = new SeededDiceSource(seed: 42);
        var b = new SeededDiceSource(seed: 42);

        for (int i = 0; i < 200; i++)
            Assert.Equal(a.Roll(), b.Roll());
    }

    [Fact]
    public void Seeded_DifferentSeeds_DivergeSomewhere()
    {
        var a = new SeededDiceSource(seed: 1);
        var b = new SeededDiceSource(seed: 2);

        bool diverged = false;
        for (int i = 0; i < 200 && !diverged; i++)
            diverged = a.Roll() != b.Roll();

        Assert.True(diverged);
    }

    [Fact]
    public void Seeded_AllRollsInRange()
    {
        var source = new SeededDiceSource(seed: 7);

        for (int i = 0; i < 1000; i++)
        {
            var (die1, die2) = source.Roll();
            Assert.InRange(die1, 1, 6);
            Assert.InRange(die2, 1, 6);
        }
    }

    [Fact]
    public void Seeded_EveryFaceAppears()
    {
        // With 1000 rolls, absence of any face would indicate a broken range
        // mapping (e.g., an off-by-one excluding 6).
        var source = new SeededDiceSource(seed: 123);
        var seen = new HashSet<int>();

        for (int i = 0; i < 1000; i++)
        {
            var (die1, die2) = source.Roll();
            seen.Add(die1);
            seen.Add(die2);
        }

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, seen.Order());
    }

    // ── RecordedDiceSource ────────────────────────────────────────

    [Fact]
    public void Recorded_ReplaysSequenceInOrder()
    {
        var rolls = new (int, int)[] { (3, 1), (6, 6), (2, 5) };
        var source = new RecordedDiceSource(rolls);

        Assert.Equal((3, 1), source.Roll());
        Assert.Equal((6, 6), source.Roll());
        Assert.Equal((2, 5), source.Roll());
    }

    [Fact]
    public void Recorded_Exhausted_Throws()
    {
        var source = new RecordedDiceSource([(4, 2)]);
        source.Roll();

        Assert.Throws<InvalidOperationException>(() => source.Roll());
    }

    [Fact]
    public void Recorded_EmptySequence_ThrowsOnFirstRoll()
    {
        var source = new RecordedDiceSource([]);

        Assert.Throws<InvalidOperationException>(() => source.Roll());
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    [InlineData(7, 3)]
    [InlineData(3, 7)]
    [InlineData(-1, 3)]
    public void Recorded_OutOfRangeDie_ThrowsAtConstruction(int die1, int die2)
    {
        Assert.Throws<ArgumentException>(() => new RecordedDiceSource([(die1, die2)]));
    }

    [Fact]
    public void Recorded_NullSequence_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RecordedDiceSource(null!));
    }

    [Fact]
    public void Recorded_CopiesSequence_LaterMutationHasNoEffect()
    {
        var rolls = new List<(int, int)> { (1, 2), (3, 4) };
        var source = new RecordedDiceSource(rolls);

        rolls[1] = (6, 6);

        source.Roll();
        Assert.Equal((3, 4), source.Roll());
    }

    [Fact]
    public void Recorded_Remaining_CountsDown()
    {
        var source = new RecordedDiceSource([(1, 1), (2, 2)]);

        Assert.Equal(2, source.Remaining);
        source.Roll();
        Assert.Equal(1, source.Remaining);
        source.Roll();
        Assert.Equal(0, source.Remaining);
    }
}
