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
            ["ProjectRole"] = (MigrationDisposition.EventStream, "AccessControl", "ProjectRoleReconciled"),
            ["ProjectMembership"] = (MigrationDisposition.EventStream, "AccessControl", "ProjectMembershipReconciled"),
            ["Plan"] = (MigrationDisposition.EventStream, "Planning", "PlanImported"),
            ["Requirement"] = (MigrationDisposition.EventStream, "Planning", "RequirementImported"),
            ["Milestone"] = (MigrationDisposition.EventStream, "Planning", "MilestoneImported"),
            ["EngineeringTask"] = (MigrationDisposition.EventStream, "TaskFlow", "TaskImported"),
            ["TaskVersion"] = (MigrationDisposition.EventStream, "TaskFlow", "TaskVersionImported"),
            ["TaskEvidence"] = (MigrationDisposition.EventStream, "TaskFlow", "TaskEvidenceImported"),
            ["AnalysisRun"] = (MigrationDisposition.EventStream, "RepositoryIntelligence", "AnalysisStarted"),
            ["SourceArtifact"] = (MigrationDisposition.EventStream, "RepositoryIntelligence", "ArtifactObserved"),
            ["SourceImpact"] = (MigrationDisposition.EventStream, "RepositoryIntelligence", "ImpactRecorded"),
            ["StormingBoard"] = (MigrationDisposition.EventStream, "EventStorming", "BoardCreated"),
            ["StormingNode"] = (MigrationDisposition.EventStream, "EventStorming", "StormingNodeAdded"),
            ["StormingConnection"] = (MigrationDisposition.EventStream, "EventStorming", "StormingNodesConnected"),
            ["StormingHotspot"] = (MigrationDisposition.EventStream, "EventStorming", "StormingHotspotMarked"),
            ["StormingNodeOrder"] = (MigrationDisposition.EventStream, "EventStorming", "StormingNodeReordered"),
            ["ArchitectureModel"] = (MigrationDisposition.EventStream, "Architecture", "ArchitectureModelCreated"),
            ["ArchitectureModule"] = (MigrationDisposition.EventStream, "Architecture", "ArchitectureModuleAdded"),
            ["ArchitectureModuleRelationship"] = (MigrationDisposition.EventStream, "Architecture", "ArchitectureModulesConnected"),
            ["ArchitectureEntity"] = (MigrationDisposition.EventStream, "Architecture", "ArchitectureEntityAdded"),
            ["ArchitectureDataRelationship"] = (MigrationDisposition.EventStream, "Architecture", "ArchitectureDataRelationshipAdded"),
            ["ArchitectureDrift"] = (MigrationDisposition.EventStream, "Architecture", "ArchitectureDriftRecorded"),
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
        var payloadHashes = new Dictionary<string, string>(StringComparer.Ordinal);
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
            if ((kind is "ProjectState" or "ProjectRole" or "ProjectMembership" or "Plan" or
                "EngineeringTask" or "TaskVersion" or "TaskEvidence" or "AnalysisRun" or
                "SourceArtifact" or "SourceImpact" or "StormingBoard" or "StormingNode" or
                "StormingConnection" or "StormingHotspot" or "StormingNodeOrder" or
                "ArchitectureModel" or "ArchitectureModule" or "ArchitectureModuleRelationship" or
                "ArchitectureEntity" or "ArchitectureDataRelationship" or "ArchitectureDrift" or
                "GitHubCredential" or "GitHubDelivery") && projectSourceId is null)
            {
                throw new InvalidOperationException(
                    $"Legacy record '{kind}:{sourceId}' must identify its Project source record.");
            }
            var aggregateSourceId = ResolveAggregateSourceId(kind, sourceId, record.Payload, projectSourceId);
            if (!Mappings.TryGetValue(kind, out var mapping))
            {
                throw new InvalidOperationException($"No GOAL2 migration mapping exists for legacy kind '{kind}'.");
            }

            var sourceReference = BuildSourceReference(kind, sourceId);
            if (payloadHashes.TryGetValue(sourceReference, out var previousHash) &&
                !string.Equals(previousHash, payloadHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Conflicting payload hashes were supplied for source reference '{sourceReference}'.");
            }

            payloadHashes[sourceReference] = payloadHash;
            var isNew = seen.Add(sourceReference);
            operations.Add(new MigrationOperation(
                kind,
                sourceId,
                projectSourceId,
                aggregateSourceId,
                payloadHash,
                sourceReference,
                CreateTargetId(kind, sourceId, projectSourceId, aggregateSourceId),
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

    public static Guid CreateTargetId(
        string kind,
        string sourceId,
        string? projectSourceId = null,
        string? aggregateSourceId = null)
    {
        var normalizedKind = Normalize(kind, nameof(kind));
        var normalizedSourceId = Normalize(sourceId, nameof(sourceId));
        if (normalizedKind is "ProjectState" or "ProjectRole" or "ProjectMembership")
        {
            var projectId = CreateDeterministicId(
                "Project",
                Normalize(projectSourceId, nameof(projectSourceId)));
            return normalizedKind is "ProjectRole" or "ProjectMembership"
                ? AccessControlStreamId(projectId)
                : projectId;
        }

        if (normalizedKind is "Requirement" or "Milestone")
        {
            return CreateDeterministicId(
                "Plan",
                Normalize(aggregateSourceId, nameof(aggregateSourceId)));
        }

        if (normalizedKind is "TaskVersion" or "TaskEvidence")
        {
            return CreateDeterministicId(
                "EngineeringTask",
                Normalize(aggregateSourceId, nameof(aggregateSourceId)));
        }

        if (normalizedKind is "SourceArtifact" or "SourceImpact")
        {
            return CreateDeterministicId(
                "AnalysisRun",
                Normalize(aggregateSourceId, nameof(aggregateSourceId)));
        }

        if (normalizedKind is "StormingNode" or "StormingConnection" or "StormingHotspot" or "StormingNodeOrder")
        {
            return CreateDeterministicId(
                "StormingBoard",
                Normalize(aggregateSourceId, nameof(aggregateSourceId)));
        }

        if (normalizedKind is "ArchitectureModule" or "ArchitectureModuleRelationship" or "ArchitectureEntity" or "ArchitectureDataRelationship" or "ArchitectureDrift")
        {
            return CreateDeterministicId(
                "ArchitectureModel",
                Normalize(aggregateSourceId, nameof(aggregateSourceId)));
        }

        return CreateDeterministicId(normalizedKind, normalizedSourceId);
    }

    public static Guid AccessControlStreamId(Guid projectId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(projectId, Guid.Empty);
        return VietAIS.TCFlow.Modules.AccessControl.Domain.ProjectAccessStreamIdentity.ForProject(projectId);
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

    private static string? ResolveAggregateSourceId(
        string kind,
        string sourceId,
        JsonElement payload,
        string? projectSourceId)
    {
        return kind switch
        {
            "Requirement" or "Milestone" => RequiredPayloadString(
                payload,
                kind,
                "planSourceId",
                "planId"),
            "Plan" or "EngineeringTask" => sourceId,
            "TaskVersion" or "TaskEvidence" => RequiredPayloadString(
                payload,
                kind,
                "taskSourceId",
                "taskId"),
            "AnalysisRun" => sourceId,
            "SourceArtifact" or "SourceImpact" => RequiredPayloadString(
                payload,
                kind,
                "analysisRunSourceId",
                "analysisRunId"),
            "StormingBoard" or "ArchitectureModel" => sourceId,
            "StormingNode" or "StormingConnection" or "StormingHotspot" or "StormingNodeOrder" => RequiredPayloadString(
                payload,
                kind,
                "boardSourceId",
                "boardId"),
            "ArchitectureModule" or "ArchitectureModuleRelationship" or "ArchitectureEntity" or "ArchitectureDataRelationship" or "ArchitectureDrift" => RequiredPayloadString(
                payload,
                kind,
                "modelSourceId",
                "modelId"),
            "ProjectState" or "ProjectRole" or "ProjectMembership" => projectSourceId,
            _ => null
        };
    }

    private static string RequiredPayloadString(
        JsonElement payload,
        string kind,
        params string[] names)
    {
        if (payload.ValueKind == JsonValueKind.Object)
        {
            var value = payload.EnumerateObject()
                .Where(property => names.Any(name =>
                    string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
                .Select(property => property.Value)
                .FirstOrDefault();
            if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString()!.Trim();
            }
        }

        var parent = kind switch
        {
            "Requirement" or "Milestone" => "Plan",
            "TaskVersion" or "TaskEvidence" => "Task",
            "StormingNode" or "StormingConnection" or "StormingHotspot" or "StormingNodeOrder" => "Board",
            "ArchitectureModule" or "ArchitectureModuleRelationship" or "ArchitectureEntity" or "ArchitectureDataRelationship" or "ArchitectureDrift" => "Model",
            _ => "aggregate"
        };
        throw new InvalidOperationException(
            $"Legacy record '{kind}' must identify its {parent} source using one of: {string.Join(", ", names)}.");
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
