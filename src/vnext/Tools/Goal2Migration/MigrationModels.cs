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
