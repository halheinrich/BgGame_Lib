namespace BgGame_Lib;

using System.Text.Json;

/// <summary>
/// Thrown by <see cref="ProblemStatsDocumentJsonConverter"/> when a stats
/// document read recognises a genuine document in the one schema version
/// that <b>folds</b> into the current document rather than retiring:
/// version <see cref="ProblemStatsDocument.FoldableSchemaVersion"/> (4), the
/// interim format that wrapped each per-problem record in its answer kind
/// and never reached production (halheinrich/backgammon#86 leg 2, superseded
/// by halheinrich/backgammon#187). The exact sibling of
/// <see cref="RetiredStatsSchemaException"/>: same base, same
/// <see cref="SchemaVersion"/> carrier, same catch-before-the-general-
/// <see cref="JsonException"/> discipline — and a different name because
/// the disposition is different. A retired file is set aside unread; a
/// foldable file is read with
/// <see cref="ProblemStatsDocument.ReadFoldable(string)"/>, merged into the
/// current document with <see cref="ProblemStatsDocument.Merge"/>, and only
/// then renamed aside as merged (SPEC-stats-identity.md §3, 2026-09-02
/// amendment). Naming it "retired" would tell the consumer to throw its
/// tallies away, which is exactly what the amendment rules out: under the
/// amended Too Good predicate v4's tallies are comparable with v3's
/// (SPEC-scoring.md §3, amendment consequence (iv)).
///
/// <para>
/// Recognition is shallow and shape-based, as it is for the retired signal:
/// the read path checks the version and the body container and throws
/// without parsing the records. Deep validation of a v4 body — the answer-
/// kind layer, the records, trailing content — is
/// <see cref="ProblemStatsDocument.ReadFoldable(string)"/>'s, which throws
/// plain <see cref="JsonException"/> for anything but a well-formed v4. So
/// a v4 file that turns out corrupt inside surfaces as corrupt at the fold,
/// not as a false "current" read and not as retired.
/// </para>
///
/// <para>
/// Deriving from <see cref="JsonException"/> is deliberate: a consumer that
/// only knows the general fail-loud contract still fails loud. The file
/// dance — which file is the base, what the merged file is renamed to — is
/// the consumer's; this library owns the document algebra only.
/// </para>
/// </summary>
public sealed class FoldableStatsSchemaException : JsonException
{
    /// <summary>
    /// The foldable schema version the document declared — always
    /// <see cref="ProblemStatsDocument.FoldableSchemaVersion"/> today, carried
    /// rather than assumed so the consumer's rename-aside name derives from
    /// the document, exactly as <see cref="RetiredStatsSchemaException.SchemaVersion"/>.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>Construct for a recognised document of foldable <paramref name="schemaVersion"/>.</summary>
    /// <param name="schemaVersion">The foldable schema version the document declared.</param>
    /// <param name="message">The human-readable diagnostic.</param>
    public FoldableStatsSchemaException(int schemaVersion, string message)
        : base(message)
    {
        SchemaVersion = schemaVersion;
    }
}
