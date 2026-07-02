namespace BgGame_Lib;

/// <summary>
/// Stable identity of one side of a match, independent of whose turn it is.
///
/// All live substrate state (board, scores, cube ownership) is
/// on-roll-relative and flips at every turn boundary; seats are the fixed
/// labels a driver uses to attribute results. Results are keyed by seat
/// rather than by participant reference so a mirror match — the same agent
/// instances occupying both seats, the core self-play scenario — stays
/// unambiguous.
/// </summary>
public enum MatchSeat
{
    /// <summary>The first seat. Receives <c>Die1</c> of each opening roll.</summary>
    One = 1,

    /// <summary>The second seat. Receives <c>Die2</c> of each opening roll.</summary>
    Two = 2,
}

/// <summary>Convenience operations on <see cref="MatchSeat"/>.</summary>
public static class MatchSeatExtensions
{
    /// <summary>The opposing seat.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seat"/> is not a defined <see cref="MatchSeat"/>.</exception>
    public static MatchSeat Other(this MatchSeat seat) => seat switch
    {
        MatchSeat.One => MatchSeat.Two,
        MatchSeat.Two => MatchSeat.One,
        _ => throw new ArgumentOutOfRangeException(nameof(seat), seat, "Unknown MatchSeat value."),
    };
}
