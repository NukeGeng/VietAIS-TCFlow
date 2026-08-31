using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VietAIS.TCFlow.Tools.Migration;

internal static class Goal2MigrationPlanner
{
    public const int CurrentToolVersion = 1;
    public const int SupportedInputSchemaVersion = 1;

    private static readonly Dictionary<string, (MigrationDisposition Disposition, string Stream, string EventType)> Mappings =
        new Dictionary<string, (MigrationDisposition, string, string)>(StringComparer.Ordinal)
        {
            ["Project"] = (MigrationDisposition.EventStream, "Projects", "ProjectCreated"),
            ["ProjectState"] = (MigrationDisposition.EventStream, "Projects", "ProjectLifecycleReconciled"),
            ["ProjectRole"] = (MigrationDisposition.EventStream, "AccessControl", "ProjectRoleImported"),
            ["ProjectMembership"] = (MigrationDisposition.EventStream, "AccessControl", "ProjectMembershipImported"),
            ["Plan"] = (MigrationDisposition.EventStream, "Planning", "PlanImported"),
            ["Requirement"] = (MigrationDisposition.EventStream, "Planning", "RequirementImported"),
            ["Milestone"] = (MigrationDisposition.EventStream, "Planning", "MilestoneImported"),
            ["EngineeringTask"] = (MigrationDisposition.EventStream, "TaskFlow", "TaskImported"),
            ["TaskVersion"] = (MigrationDisposition.EventStream, "TaskFlow", "TaskVersionImported"),
            ["TaskEvidence"] = (MigrationDisposition.EventStream, "TaskFlow", "TaskEvidenceImported"),
            ["AnalysisRun"] = (MigrationDisposition.EventStream, "RepositoryIntelligence", "AnalysisRunImported"),
            ["SourceArtifact"] = (MigrationDisposition.EventStream, "RepositoryIntelligence", "SourceArtifactImported"),
            ["SourceImpact"] = (MigrationDisposition.EventStream, "RepositoryIntelligence", "SourceImpactImported"),
            ["GitHubCredential"] = (MigrationDisposition.OperationalDocument, "Operational", "GitHubCredentialImported"),
            ["GitHubDelivery"] = (MigrationDisposition.OperationalDocument, "Operational", "GitHubDeliveryImported")
        };

    public static MigrationPlan Plan(
        LegacyExport export,
        IReadOnlySet<string>? appliedSourceReferences = null)
    {
        ArgumentNullException.ThrowIfNull(export);
        if (export.SchemaVersion != SupportedInputSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported legacy export schema version '{export.SchemaVersion}'. Expected '{SupportedInputSchemaVersion}'.");
        }

        ArgumentNullException.ThrowIfNull(export.Records);
        var applied = appliedSourceReferences ?? new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(applied, StringComparer.Ordinal);
        var operations = new List<MigrationOperation>(export.Records.Count);

        foreach (var record in export.Records)
        {
            ArgumentNullException.ThrowIfNull(record);
            var kind = Normalize(record.Kind, nameof(record.Kind));
            var sourceId = Normalize(record.SourceId, nameof(record.SourceId));
            var payloadHash = Normalize(record.PayloadHash, nameof(record.PayloadHash));
            if (record.Payload.ValueKind == JsonValueKind.Undefined)
            {
                throw new InvalidOperationException(
                    $"Legacy record '{kind}:{sourceId}' is missing its payload.");
            }

            var projectSourceId = record.ProjectSourceId is null
                ? null
                : Normalize(record.ProjectSourceId, nameof(record.ProjectSourceId));
            if (!Mappings.TryGetValue(kind, out var mapping))
            {
                throw new InvalidOperationException($"No GOAL2 migration mapping exists for legacy kind '{kind}'.");
            }

            var sourceReference = BuildSourceReference(kind, sourceId);
            var isNew = seen.Add(sourceReference);
            operations.Add(new MigrationOperation(
                kind,
                sourceId,
                projectSourceId,
                payloadHash,
                sourceReference,
                CreateDeterministicId(kind, sourceId),
                mapping.Stream,
                mapping.EventType,
                mapping.Disposition,
                isNew ? MigrationAction.Append : MigrationAction.Skip,
                GetSkipReason(isNew, applied, sourceReference)));
        }

        return new MigrationPlan(
            CurrentToolVersion,
            export.SchemaVersion,
            operations
                .OrderBy(operation => operation.Kind, StringComparer.Ordinal)
                .ThenBy(operation => operation.SourceId, StringComparer.Ordinal)
                .ToArray());
    }

    public static string BuildSourceReference(string kind, string sourceId)
    {
        return $"v0.1:{Normalize(kind, nameof(kind))}:{Normalize(sourceId, nameof(sourceId))}";
    }

    public static Guid CreateDeterministicId(string kind, string sourceId)
    {
        var source = BuildSourceReference(kind, sourceId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"tcflow:goal2:{source}"));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash.AsSpan(0, 16));
    }

    private static string? GetSkipReason(
        bool isNew,
        IReadOnlySet<string> applied,
        string sourceReference)
    {
        if (isNew)
        {
            return null;
        }

        return applied.Contains(sourceReference) ? "already-applied" : "duplicate-in-export";
    }

    private static string Normalize(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }

        return value.Trim();
    }
}
