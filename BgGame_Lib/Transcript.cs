namespace BgGame_Lib;

using System.Collections.ObjectModel;

/// <summary>
/// Append-only ordered record of <see cref="TranscriptEntry"/>s through a
/// game (or, by composition, a match — one transcript per game).
///
/// Mutability: an internal list that only <see cref="Append"/> writes.
/// <see cref="Entries"/> hands out a read-only view of it — live, so it shows
/// later appends, but nothing a caller can write through, a cast included.
/// Append is O(1) amortized; reads are random-access. Matches the BgMoveGen
/// "mutable where it earns its keep" idiom.
///
/// Each entry's <see cref="TranscriptEntry.State"/> is an immutable snapshot,
/// so subsequent mutations of the live <see cref="GameState"/> do not affect
/// previously appended entries.
/// </summary>
public sealed class Transcript
{
    private readonly List<TranscriptEntry> _entries = [];

    /// <summary>The one view <see cref="Entries"/> hands out, over <see cref="_entries"/>.</summary>
    private readonly ReadOnlyCollection<TranscriptEntry> _view;

    /// <summary>Creates an empty transcript.</summary>
    public Transcript() => _view = _entries.AsReadOnly();

    /// <summary>
    /// All entries in append order: a read-only view of the transcript's own
    /// list, which shows later appends and cannot be written through.
    /// </summary>
    public IReadOnlyList<TranscriptEntry> Entries => _view;

    /// <summary>Append an entry. O(1) amortized.</summary>
    public void Append(TranscriptEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }
}
