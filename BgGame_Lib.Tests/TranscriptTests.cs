namespace BgGame_Lib.Tests;

using BgDataTypes_Lib;

public class TranscriptTests
{
    [Fact]
    public void NewTranscript_IsEmpty()
    {
        var t = new Transcript();
        Assert.Empty(t.Entries);
    }

    [Fact]
    public void Append_NullEntry_Throws()
    {
        var t = new Transcript();
        Assert.Throws<ArgumentNullException>(() => t.Append(null!));
    }

    [Fact]
    public void Append_AddsEntriesInOrder()
    {
        var t = new Transcript();
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);

        var snap1 = game.Snapshot();
        var snap2 = game.Snapshot();

        var play = new Play();
        play.Add(new Move(13, 7));

        t.Append(new PlayTranscriptEntry(snap1, 6, 4, play));
        t.Append(new CubeTranscriptEntry(snap2, CubeAction.Double));

        Assert.Equal(2, t.Entries.Count);
        Assert.IsType<PlayTranscriptEntry>(t.Entries[0]);
        Assert.IsType<CubeTranscriptEntry>(t.Entries[1]);
    }

    [Fact]
    public void Entries_PatternMatchAcrossSubtypes()
    {
        var t = new Transcript();
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);
        var snap = game.Snapshot();

        var play = new Play();
        play.Add(new Move(8, 5));

        t.Append(new PlayTranscriptEntry(snap, 3, 1, play));
        t.Append(new CubeTranscriptEntry(snap, CubeAction.NoDouble));
        t.Append(new GameEndedTranscriptEntry(
            snap, new GameResult(GameResultKind.WinGammon, OnRollWon: true, CubeSize: 1)));

        int playCount = 0, cubeCount = 0, endCount = 0;
        foreach (var e in t.Entries)
        {
            switch (e)
            {
                case PlayTranscriptEntry: playCount++; break;
                case CubeTranscriptEntry: cubeCount++; break;
                case GameEndedTranscriptEntry: endCount++; break;
            }
        }

        Assert.Equal(1, playCount);
        Assert.Equal(1, cubeCount);
        Assert.Equal(1, endCount);
    }

    [Fact]
    public void EntrySnapshot_DecoupledFromLiveStateMutations()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);

        var snapAtStart = game.Snapshot();
        var entry = new CubeTranscriptEntry(snapAtStart, CubeAction.Double);

        // Mutate the live state.
        game.DoubleCube();
        match.AwardGame(new GameResult(GameResultKind.WinSingle, OnRollWon: true, CubeSize: 1));

        // The transcript entry retains the original snapshot data.
        Assert.Equal(1, entry.State.CubeSize);
        Assert.Equal(0, entry.State.Match.OnRollScore);
    }

    [Fact]
    public void Entries_IsReadOnlyView()
    {
        var t = new Transcript();
        Assert.IsAssignableFrom<IReadOnlyList<TranscriptEntry>>(t.Entries);
    }

    [Fact]
    public void TranscriptEntry_RecordEqualityHoldsByValue()
    {
        var match = MatchState.NewMatch(7);
        var game = GameState.NewGame(match);

        // Use the same MatchSnapshot/board reference so record equality compares cleanly.
        var matchSnap = match.Snapshot();
        int[] board = new int[26];
        Array.Copy(game.Board.Points, board, 26);
        var snap = new GameSnapshot(board, 1, CubeOwner.Centered, matchSnap);

        var a = new CubeTranscriptEntry(snap, CubeAction.Double);
        var b = new CubeTranscriptEntry(snap, CubeAction.Double);

        Assert.Equal(a, b);
    }
}
