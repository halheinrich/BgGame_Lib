namespace BgGame_Lib.Tests;

public class QuizMixTests
{
    private static QuizMixEntry Entry(QuizCategory category, int percent) =>
        new(category, percent);

    // -----------------------------------------------------------------------
    //  QuizMixEntry — per-entry rule at the earliest construction point
    // -----------------------------------------------------------------------

    [Fact]
    public void Entry_RejectsNullCategory() =>
        Assert.Throws<ArgumentNullException>(() => new QuizMixEntry(null!, 50));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Entry_RejectsPercentOutside1To100(int percent) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new QuizMixEntry(QuizCategory.NeverSeen, percent));

    [Fact]
    public void Entry_HasValueEquality()
    {
        Assert.Equal(
            Entry(QuizCategory.SeenFewerThan(3), 40),
            Entry(QuizCategory.SeenFewerThan(3), 40));
        Assert.NotEqual(
            Entry(QuizCategory.SeenFewerThan(3), 40),
            Entry(QuizCategory.SeenFewerThan(3), 60));
        Assert.NotEqual(
            Entry(QuizCategory.SeenFewerThan(3), 40),
            Entry(QuizCategory.SeenFewerThan(5), 40));
    }

    // -----------------------------------------------------------------------
    //  QuizMix — set-level rules
    // -----------------------------------------------------------------------

    [Fact]
    public void Empty_IsInertPassthrough()
    {
        Assert.True(QuizMix.Empty.IsPassthrough);
        Assert.Empty(QuizMix.Empty.Entries);
        Assert.Null(QuizMix.Empty.QuizLength);
        Assert.True(QuizMix.Empty.RandomOrder);
    }

    [Fact]
    public void Constructor_ExposesEntriesInDeclaredOrder()
    {
        var entries = new[]
        {
            Entry(QuizCategory.NeverSeen, 50),
            Entry(QuizCategory.GotWrong, 30),
            Entry(QuizCategory.EverythingElse, 20),
        };

        var mix = new QuizMix(entries, quizLength: 25, randomOrder: false);

        Assert.Equal(entries, mix.Entries);
        Assert.Equal(25, mix.QuizLength);
        Assert.False(mix.RandomOrder);
        Assert.False(mix.IsPassthrough);
    }

    [Fact]
    public void Constructor_DefaultsToNullLengthAndRandomOrder()
    {
        var mix = new QuizMix([Entry(QuizCategory.NeverSeen, 100)]);

        Assert.Null(mix.QuizLength);
        Assert.True(mix.RandomOrder);
    }

    [Fact]
    public void Constructor_RejectsNullEntries() =>
        Assert.Throws<ArgumentNullException>(() => new QuizMix(null!));

    [Fact]
    public void Constructor_RejectsNullEntryElement() =>
        Assert.Throws<ArgumentException>(
            () => new QuizMix([Entry(QuizCategory.NeverSeen, 100), null!]));

    [Theory]
    [InlineData(49)]   // sums to 99
    [InlineData(51)]   // sums to 101
    public void Constructor_RejectsSumOtherThan100(int secondPercent) =>
        Assert.Throws<ArgumentException>(() => new QuizMix(
        [
            Entry(QuizCategory.NeverSeen, 50),
            Entry(QuizCategory.GotWrong, secondPercent),
        ]));

    [Fact]
    public void Constructor_RejectsDuplicateCategory() =>
        Assert.Throws<ArgumentException>(() => new QuizMix(
        [
            Entry(QuizCategory.SeenFewerThan(3), 50),
            Entry(QuizCategory.SeenFewerThan(3), 50),
        ]));

    [Fact]
    public void Constructor_AllowsSameKindWithDifferentParameters()
    {
        var mix = new QuizMix(
        [
            Entry(QuizCategory.SeenFewerThan(3), 50),
            Entry(QuizCategory.SeenFewerThan(5), 50),
        ]);

        Assert.Equal(2, mix.Entries.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveQuizLength(int quizLength) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuizMix(
            [Entry(QuizCategory.NeverSeen, 100)], quizLength));

    [Fact]
    public void Constructor_RejectsQuizLengthWithoutEntries() =>
        // Blank means fully inert; capping without weighting is expressed as
        // a single EverythingElse entry at 100 percent plus a length.
        Assert.Throws<ArgumentException>(() => new QuizMix([], quizLength: 25));

    [Fact]
    public void Constructor_AcceptsSingleEntryAt100()
    {
        var mix = new QuizMix([Entry(QuizCategory.EverythingElse, 100)], quizLength: 25);

        Assert.Single(mix.Entries);
        Assert.Equal(25, mix.QuizLength);
    }
}
