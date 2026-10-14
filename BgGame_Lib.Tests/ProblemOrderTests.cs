namespace BgGame_Lib.Tests;

using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

/// <summary>
/// The four orders as a type (SPEC-filtering.md §7): the default, the labels,
/// and the persisted token — each member's declared name, string-exact, on
/// the reflection path and through <see cref="BgGameJsonContext"/> alike.
/// How each order arranges a source is pinned in
/// <see cref="OrderedProblemSetSourceTests"/> and
/// <see cref="MixedProblemSetSourceTests"/>.
/// </summary>
public class ProblemOrderTests
{
    [Fact]
    public void AsRead_IsTheDefault_AndEachOrderHasItsLabel()
    {
        Assert.Equal(ProblemOrder.AsRead, default);
        Assert.Equal(
            [
                ProblemOrder.AsRead,
                ProblemOrder.Shuffled,
                ProblemOrder.ByErrorLargestFirst,
                ProblemOrder.ByMoveNumberAscending,
            ],
            Enum.GetValues<ProblemOrder>());
        Assert.Equal("As read", Label(ProblemOrder.AsRead));
        Assert.Equal("Shuffled", Label(ProblemOrder.Shuffled));
        Assert.Equal("By error, largest first", Label(ProblemOrder.ByErrorLargestFirst));
        Assert.Equal("By move number, ascending", Label(ProblemOrder.ByMoveNumberAscending));

        static string? Label(ProblemOrder order) =>
            typeof(ProblemOrder).GetField(order.ToString())!.GetCustomAttribute<DescriptionAttribute>()?.Description;
    }

    /// <summary>The tokens a consumer persists. Stable: renaming a member breaks every stored choice.</summary>
    public static TheoryData<ProblemOrder, string> Tokens => new()
    {
        { ProblemOrder.AsRead, "\"AsRead\"" },
        { ProblemOrder.Shuffled, "\"Shuffled\"" },
        { ProblemOrder.ByErrorLargestFirst, "\"ByErrorLargestFirst\"" },
        { ProblemOrder.ByMoveNumberAscending, "\"ByMoveNumberAscending\"" },
    };

    [Theory]
    [MemberData(nameof(Tokens))]
    public void EachOrder_IsWrittenAndReadByItsName_OnBothPaths(ProblemOrder order, string token)
    {
        Assert.Equal(token, JsonSerializer.Serialize(order));
        Assert.Equal(token, JsonSerializer.Serialize(order, BgGameJsonContext.Default.ProblemOrder));
        Assert.Equal(order, JsonSerializer.Deserialize<ProblemOrder>(token));
        Assert.Equal(order, JsonSerializer.Deserialize(token, BgGameJsonContext.Default.ProblemOrder));
    }

    [Theory]
    [MemberData(nameof(EveryOrdinal))]
    public void AnOrdinal_IsRefused_OnBothPaths(string ordinal)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProblemOrder>(ordinal));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(ordinal, BgGameJsonContext.Default.ProblemOrder));
    }

    /// <summary>Every member's own ordinal, and one no member has.</summary>
    public static TheoryData<string> EveryOrdinal
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var order in Enum.GetValues<ProblemOrder>())
                data.Add(((int)order).ToString(CultureInfo.InvariantCulture));
            data.Add("99");
            return data;
        }
    }

    [Fact]
    public void AnUnknownName_IsRefused() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProblemOrder>("\"ByError\""));
}
