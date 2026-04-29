namespace BgGame_Lib;

/// <summary>
/// Append-only ordered record of <see cref="TranscriptEntry"/>s through a
/// game (or, by composition, a match — one transcript per game).
///
/// Mutability: internal mutable list, public read API exposes only
/// <see cref="IReadOnlyList{T}"/>. Append is O(1); reads are random-access.
/// Matches the BgMoveGen "mutable where it earns its keep" idiom.
///
/// Each entry's <see cref="TranscriptEntry.State"/> is an immutable snapshot,
/// so subsequent mutations of the live <see cref="GameState"/> do not affect
/// previously appended entries.
/// </summary>
public sealed class Transcript
{
    private readonly List<TranscriptEntry> _entries = [];

    /// <summary>Read-only view of all entries in append order.</summary>
    public IReadOnlyList<TranscriptEntry> Entries => _entries;

    /// <summary>Append an entry. Linear time per call is O(1) amortised.</summary>
    public void Append(TranscriptEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }
}
