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

        Play play = [new(13, 7)];

        t.Append(new PlayTranscriptEntry(snap1, MatchSeat.One, 6, 4, play));
        t.Append(new CubeTranscriptEntry(snap2, MatchSeat.Two, CubeAction.Double));

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

        Play play = [new(8, 5)];

        t.Append(new PlayTranscriptEntry(snap, MatchSeat.One, 3, 1, play));
        t.Append(new CubeTranscriptEntry(snap, MatchSeat.Two, CubeAction.NoDouble));
        t.Append(new GameEndedTranscriptEntry(
            snap, MatchSeat.One, new GameResult(GameResultKind.WinGammon, OnRollWon: true, CubeSize: 1)));

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
        var entry = new CubeTranscriptEntry(snapAtStart, MatchSeat.One, CubeAction.Double);

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
        var game = GameState.NewGame(MatchState.NewMatch(7));

        // Two separate captures of one state: the snapshot's board is a
        // value, so the entries compare by content, not by reference.
        var a = new CubeTranscriptEntry(game.Snapshot(), MatchSeat.One, CubeAction.Double);
        var b = new CubeTranscriptEntry(game.Snapshot(), MatchSeat.One, CubeAction.Double);

        Assert.NotSame(a.State, b.State);
        Assert.Equal(a, b);
    }

    // ── PlayTranscriptEntry equality: the exact encoding ──
    //
    // An entry records the one encoding its turn applied, so its equality
    // compares that encoding (Play.IsSameEncoding) and the rest by value —
    // never the play's own equality, which throws (halheinrich/backgammon#273).

    /// <summary>
    /// A 5-4 with two checkers able to hit the opponent's blot on the 3-point:
    /// 8/3* 7/3, whichever checker carries the mark.
    /// </summary>
    private static GameSnapshot BlotOnTheThree()
    {
        var mop = new int[26];
        mop[24] = 2; mop[13] = 4; mop[8] = 2; mop[7] = 2; mop[6] = 5;       // on roll: 15
        mop[19] = -5; mop[17] = -3; mop[12] = -5; mop[3] = -1; mop[1] = -1; // opponent: 15
        return GameState.FromPosition(
            MatchState.NewMatch(7), BoardState.FromMop(mop), cubeSize: 1, cubeOwner: CubeOwner.Centered).Snapshot();
    }

    private static PlayTranscriptEntry PlayEntry(Play play, int die1 = 5, int die2 = 4, MatchSeat seat = MatchSeat.One) =>
        new(BlotOnTheThree(), seat, die1, die2, play);

    [Fact]
    public void PlayEntry_TheSameEncoding_IsEqual_AndHashesAlike()
    {
        var a = PlayEntry([new(8, -3), new(7, 3)]);
        var b = PlayEntry([new(8, -3), new(7, 3)]);

        Assert.NotSame(a.State, b.State);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.True(((TranscriptEntry)a).Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Single(new HashSet<TranscriptEntry> { a, b });
    }

    [Fact]
    public void PlayEntry_AnotherEncodingOfTheSamePlay_IsADifferentRecord()
    {
        // 8/3* 7/3 and 8/3 7/3* are one play from this position — but an
        // entry records the encoding applied, and these are two encodings.
        Play markOnTheEight = [new(8, -3), new(7, 3)];
        Play markOnTheSeven = [new(8, 3), new(7, -3)];
        Play otherOrder = [new(7, 3), new(8, -3)];
        var from = new BoardState(BlotOnTheThree().Board);
        Assert.True(from.IsSamePlay(markOnTheEight, markOnTheSeven));
        Assert.True(from.IsSamePlay(markOnTheEight, otherOrder));

        Assert.NotEqual(PlayEntry(markOnTheEight), PlayEntry(markOnTheSeven));
        Assert.NotEqual(PlayEntry(markOnTheEight), PlayEntry(otherOrder));
        Assert.Equal(3, new HashSet<TranscriptEntry>
        {
            PlayEntry(markOnTheEight), PlayEntry(markOnTheSeven), PlayEntry(otherOrder),
        }.Count);
    }

    [Fact]
    public void PlayEntry_TheDiceSeatAndStateParticipate()
    {
        Play play = [new(8, -3), new(7, 3)];
        var entry = PlayEntry(play);

        Assert.NotEqual(entry, PlayEntry(play, die1: 4, die2: 5));
        Assert.NotEqual(entry, PlayEntry(play, seat: MatchSeat.Two));
        Assert.NotEqual(entry, entry with { State = entry.State with { CubeSize = 2 } });
    }

    [Fact]
    public void PlayEntry_ASlotLeftBehindByRemoveLast_IsNotPartOfTheEncoding()
    {
        Play trimmed = [new(8, -3), new(7, 3), new(13, 9)];
        trimmed.RemoveLast();

        var a = PlayEntry(trimmed);
        var b = PlayEntry([new(8, -3), new(7, 3)]);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void PlayEntry_DanceTurns_CompareEqual()
    {
        Assert.Equal(PlayEntry([]), PlayEntry([]));
        Assert.NotEqual(PlayEntry([]), PlayEntry([new(8, -3), new(7, 3)]));
    }

    // ── Derived attribution (single-sourced on the entry types) ──
    //
    // OnRollSeat is the frame seat; the actor/winner rules live on the
    // subtypes. Offer-side cube actions (NoDouble, Double) belong to the
    // on-roll offerer; response-side actions (Take, Pass) to the other seat.

    [Theory]
    [InlineData(CubeAction.NoDouble, MatchSeat.One, MatchSeat.One)]
    [InlineData(CubeAction.Double, MatchSeat.One, MatchSeat.One)]
    [InlineData(CubeAction.Take, MatchSeat.One, MatchSeat.Two)]
    [InlineData(CubeAction.Pass, MatchSeat.One, MatchSeat.Two)]
    [InlineData(CubeAction.NoDouble, MatchSeat.Two, MatchSeat.Two)]
    [InlineData(CubeAction.Double, MatchSeat.Two, MatchSeat.Two)]
    [InlineData(CubeAction.Take, MatchSeat.Two, MatchSeat.One)]
    [InlineData(CubeAction.Pass, MatchSeat.Two, MatchSeat.One)]
    public void CubeEntry_ActingSeat_OfferAtOnRoll_ResponseAtOther(
        CubeAction action, MatchSeat onRollSeat, MatchSeat expectedActor)
    {
        var snap = GameState.NewGame(MatchState.NewMatch(7)).Snapshot();
        var entry = new CubeTranscriptEntry(snap, onRollSeat, action);

        Assert.Equal(expectedActor, entry.ActingSeat);
    }

    [Theory]
    [InlineData(true, MatchSeat.One, MatchSeat.One)]
    [InlineData(false, MatchSeat.One, MatchSeat.Two)]
    [InlineData(true, MatchSeat.Two, MatchSeat.Two)]
    [InlineData(false, MatchSeat.Two, MatchSeat.One)]
    public void GameEndedEntry_Winner_ResolvesOnRollWonAgainstFrameSeat(
        bool onRollWon, MatchSeat onRollSeat, MatchSeat expectedWinner)
    {
        var snap = GameState.NewGame(MatchState.NewMatch(7)).Snapshot();
        var entry = new GameEndedTranscriptEntry(
            snap, onRollSeat, new GameResult(GameResultKind.WinSingle, onRollWon, CubeSize: 1));

        Assert.Equal(expectedWinner, entry.Winner);
    }
}
