namespace BgGame_Lib;

using System.Text.Json;
using BgDataTypes_Lib;

/// <summary>
/// Thrown by <see cref="ProblemStatsDocumentJsonConverter"/> when a stats
/// document read recognises a genuine document in a <b>retired</b> schema
/// version — <i>every</i> recognised version below
/// <see cref="ProblemStatsDocument.CurrentSchemaVersion"/>, not one pinned
/// version: today version 1 (the <c>DecisionId</c>-keyed format, retired by
/// the clean break to <see cref="ProblemKey"/> keying —
/// halheinrich/backgammon#95), version 2 (the <see cref="ProblemKey"/>-keyed
/// format from before the Jacoby rule entered money keys —
/// halheinrich/backgammon#120), and version 3 (the
/// <see cref="ProblemKey"/>-keyed format from before answer kinds entered the
/// per-problem records — halheinrich/backgammon#86). Each throws carrying its
/// <b>own</b>
/// <see cref="SchemaVersion"/>, which is what lets a consumer name the file it
/// sets aside per version (SPEC-stats-identity.md §3).
///
/// <para>
/// This is the deliberate recognition signal that separates "your stats file
/// is from a retired format" from "your stats file is corrupt or foreign":
/// a retired document must not surface as a generic hard load error, which
/// would strand existing users on a dead stats file with no honest notice.
/// The consumer catches this type <i>before</i> the general
/// <see cref="JsonException"/> and retires the file (rename aside — under a
/// name derived from <see cref="SchemaVersion"/>, so two retired formats
/// cannot overwrite one another — seed a fresh current-version document, tell
/// the user stats restarted). Recognition is shallow and shape-based — the
/// retired content itself is never parsed; there is no migration.
/// </para>
///
/// <para>
/// Deriving from <see cref="JsonException"/> is deliberate: a consumer that
/// only knows the general fail-loud contract still fails loud. Versions
/// <b>newer</b> than <see cref="ProblemStatsDocument.CurrentSchemaVersion"/>
/// are not retired and keep the plain fail-loud posture.
/// </para>
/// </summary>
public sealed class RetiredStatsSchemaException : JsonException
{
    /// <summary>
    /// The retired schema version the document declared — the specific
    /// version, never a placeholder: the consumer's set-aside name is derived
    /// from it.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>Construct for a recognised document of retired <paramref name="schemaVersion"/>.</summary>
    /// <param name="schemaVersion">The retired schema version the document declared.</param>
    /// <param name="message">The human-readable diagnostic.</param>
    public RetiredStatsSchemaException(int schemaVersion, string message)
        : base(message)
    {
        SchemaVersion = schemaVersion;
    }
}
