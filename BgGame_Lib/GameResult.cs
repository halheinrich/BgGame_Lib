namespace BgGame_Lib;

/// <summary>
/// How a game ended. Multiplied with cube size to give points awarded.
/// </summary>
public enum GameResultKind
{
    /// <summary>Loser borne off at least one checker.</summary>
    WinSingle = 1,

    /// <summary>Loser borne off zero checkers; no checker in winner's home or on bar.</summary>
    WinGammon = 2,

    /// <summary>Loser borne off zero checkers and has a checker in winner's home or on the bar.</summary>
    WinBackgammon = 3,
}

/// <summary>
/// Outcome of a single game.
///
/// <see cref="OnRollWon"/> is from the perspective of the <see cref="GameState"/>
/// the result was computed against — true when the on-roll player won, false
/// when the opponent (typically the player who just moved, after
/// <see cref="Referee.ApplyPlay"/> has flipped perspective) won.
/// </summary>
/// <param name="Kind">Win type (single / gammon / backgammon).</param>
/// <param name="OnRollWon">True iff the current on-roll player is the winner.</param>
/// <param name="CubeSize">Cube size at the moment the game ended (1, 2, 4, ...).</param>
public sealed record GameResult(GameResultKind Kind, bool OnRollWon, int CubeSize)
{
    /// <summary>Points awarded to the winner: cube size × win-type multiplier.</summary>
    public int Points => CubeSize * (int)Kind;
}
