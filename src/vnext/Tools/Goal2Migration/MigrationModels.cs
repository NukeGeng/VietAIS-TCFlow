using System.Text.Json;

namespace VietAIS.TCFlow.Tools.Migration;

internal enum MigrationDisposition
{
    EventStream,
    OperationalDocument
}

internal enum MigrationAction
{
    Append,
    Skip
}

internal sealed record LegacyExport(
    int SchemaVersion,
    IReadOnlyList<LegacyRecord> Records);

internal sealed record LegacyRecord(
    string Kind,
    string SourceId,
    string? ProjectSourceId,
    string PayloadHash,
    JsonElement Payload);

internal sealed record MigrationOperation(
    string Kind,
    string SourceId,
    string? ProjectSourceId,
    string? AggregateSourceId,
    string PayloadHash,
    string SourceReference,
    Guid TargetId,
    string TargetStream,
    string TargetEventType,
    MigrationDisposition Disposition,
    MigrationAction Action,
    string? SkipReason);

internal sealed record MigrationPlan(
    int ToolVersion,
    int InputSchemaVersion,
    IReadOnlyList<MigrationOperation> Operations);

/// <summary>
/// Operational migration state. This is deliberately not an event-sourced
/// aggregate: it is a small idempotency ledger used to make a cutover
/// repeatable while the model-level writers are introduced per bounded
/// context.
/// </summary>
internal sealed record MigrationLedger(
    int ToolVersion,
    int InputSchemaVersion,
    IReadOnlyList<MigrationLedgerEntry> Entries)
{
    public static MigrationLedger Empty(int toolVersion, int inputSchemaVersion) =>
        new(toolVersion, inputSchemaVersion, []);
}

internal sealed record MigrationLedgerEntry(
    string SourceReference,
    string Kind,
    string SourceId,
    string PayloadHash,
    Guid TargetId,
    string TargetStream,
    string TargetEventType,
    MigrationDisposition Disposition,
    DateTimeOffset AppliedAtUtc);

internal sealed record MigrationApplyReport(
    int PlannedCount,
    int AppendCount,
    int SkipCount,
    int LedgerEntriesBefore,
    int LedgerEntriesAfter,
    bool Idempotent,
    IReadOnlyList<string> Conflicts);

internal sealed record MigrationBusinessApplyReport(
    int PlannedEventOperations,
    int AppendedEventCount,
    int SkippedEventCount,
    int StreamsTouched,
    IReadOnlyList<string> Conflicts);

internal sealed record MigrationOperationalApplyReport(
    int PlannedOperations,
    int UpsertedDocumentCount,
    int SkippedDocumentCount,
    IReadOnlyList<string> Conflicts);

internal sealed record MigrationApplyOutput(
    MigrationPlan Plan,
    MigrationApplyReport Report,
    MigrationBusinessApplyReport? BusinessEvents = null,
    MigrationOperationalApplyReport? OperationalDocuments = null);

/// <summary>
/// Read-only reconciliation result for an exported migration plan. The
/// reconciler checks source markers and hashes in the target Event Store; it
/// never changes business state.
/// </summary>
internal sealed record MigrationReconciliationReport(
    int PlannedOperations,
    int EventStreamOperations,
    int OperationalDocumentOperations,
    int ExpectedSourceMarkers,
    int FoundSourceMarkers,
    int ExpectedOperationalDocuments,
    int FoundOperationalDocuments,
    int DuplicateSourceMarkers,
    int MissingStreams,
    IReadOnlyList<string> MissingSourceReferences,
    IReadOnlyList<string> MissingOperationalReferences,
    IReadOnlyList<string> HashMismatches,
    IReadOnlyList<string> DuplicateSourceReferences,
    IReadOnlyDictionary<string, int> ExpectedEventOperationsByKind,
    IReadOnlyDictionary<string, int> FoundEventOperationsByKind,
    IReadOnlyDictionary<string, int> ExpectedOperationalDocumentsByKind,
    IReadOnlyDictionary<string, int> FoundOperationalDocumentsByKind,
    IReadOnlyList<string> CountMismatches,
    IReadOnlyList<string> Issues,
    bool Reconciled);

internal sealed record MigrationReconciliationOutput(
    MigrationPlan Plan,
    MigrationReconciliationReport Report);
