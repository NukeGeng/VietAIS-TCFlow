using System.Globalization;
using System.Text.Json;
using TaskStatus = VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries.TaskStatus;
using VietAIS.TCFlow.Modules.TaskFlow.Domain;

namespace VietAIS.TCFlow.Tools.Migration;

/// <summary>
/// Maps a legacy EngineeringTask snapshot to a proposed task plus an explicit
/// lifecycle reconciliation event. Synthetic accept/start/review transitions
/// would invent actors and business history, so the snapshot event is used.
/// </summary>
internal static class TaskFlowMigrationMapper
{
    public const string MigrationActor = "migration";
    public const string MigrationSource = "migration.goal2.task-flow";

    public static IReadOnlyList<object> ToEvents(
        MigrationOperation operation,
        LegacyRecord record)
    {
        EnsureKind(operation, record);
        var projectSourceId = operation.ProjectSourceId
            ?? throw new InvalidOperationException(
                $"Task migration '{operation.SourceReference}' must identify its project source id.");
        var status = ParseStatus(record.Payload);
        var occurredAt = RequiredDateTime(
            record.Payload,
            "updatedAtUtc",
            "updatedAt",
            "createdAtUtc",
            "createdAt",
            "occurredAtUtc");
        var correlationId = CorrelationId(operation);
        var taskId = operation.TargetId;
        return
        [
            new TaskProposed(
                taskId,
                Goal2MigrationPlanner.CreateDeterministicId("Project", projectSourceId),
                RequiredText(record.Payload, 2, 240, "title", "name"),
                OptionalText(record.Payload, 2000, "description", "details"),
                OptionalSourceChangeKey(record.Payload),
                MigrationActor,
                correlationId,
                occurredAt),
            new TaskLifecycleReconciled(
                taskId,
                status,
                OptionalText(record.Payload, 200, "assigneeId", "assignedTo"),
                ParseAiVerification(record.Payload),
                OptionalBoolean(record.Payload, false, "humanReviewRequested", "reviewRequested"),
                ParseHumanApproval(record.Payload),
                MigrationActor,
                correlationId,
                occurredAt)
        ];
    }

    public static TaskVersionImported ToVersionEvent(
        MigrationOperation operation,
        LegacyRecord record)
    {
        EnsureKind(operation, record, "TaskVersion");
        var version = RequiredPositiveInt(record.Payload, "version", "versionNumber");
        var changedAt = RequiredDateTime(
            record.Payload,
            "changedAtUtc",
            "changedAt",
            "updatedAtUtc",
            "updatedAt",
            "createdAtUtc",
            "createdAt");
        return new TaskVersionImported(
            operation.TargetId,
            Goal2MigrationPlanner.CreateDeterministicId("TaskVersion", operation.SourceId),
            version,
            RequiredRawJson(record.Payload, "snapshot"),
            OptionalRawJson(record.Payload, "assignment"),
            OptionalRawJson(record.Payload, "review"),
            OptionalRawJson(record.Payload, "evidence"),
            OptionalString(record.Payload, "changedBy", "actorId") ?? MigrationActor,
            OptionalString(record.Payload, "changedByType", "actorType") ?? "Legacy",
            OptionalString(record.Payload, "changeReason", "reason") ?? "legacy task version import",
            MigrationActor,
            CorrelationId(operation),
            changedAt);
    }

    public static TaskEvidenceImported ToEvidenceEvent(
        MigrationOperation operation,
        LegacyRecord record)
    {
        EnsureKind(operation, record, "TaskEvidence");
        var confidence = OptionalDecimal(record.Payload, "confidence");
        if (confidence is < 0 or > 1)
        {
            throw new InvalidOperationException("Legacy task evidence confidence must be between 0 and 1.");
        }

        return new TaskEvidenceImported(
            operation.TargetId,
            Goal2MigrationPlanner.CreateDeterministicId("TaskEvidence", operation.SourceId),
            RequiredString(record.Payload, "evidenceKind", "kind"),
            RequiredText(record.Payload, 2, 2000, "summary", "claim", "description"),
            OptionalText(record.Payload, 2000, "location", "sourcePath"),
            OptionalScalarString(record.Payload, "sourceChangeKey", "sourceChangeId"),
            OptionalScalarString(record.Payload, "artifactKey", "artifactId"),
            OptionalScalarString(record.Payload, "impactKey", "impactId"),
            confidence,
            OptionalString(record.Payload, "createdBy", "actorId") ?? MigrationActor,
            OptionalString(record.Payload, "createdByType", "actorType") ?? "Legacy",
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(
                record.Payload,
                "createdAtUtc",
                "createdAt",
                "occurredAtUtc",
                "occurredAt"));
    }

    private static TaskStatus ParseStatus(JsonElement payload)
    {
        var value = RequiredString(payload, "status");
        if (!Enum.TryParse<TaskStatus>(value, ignoreCase: true, out var status))
        {
            throw new InvalidOperationException($"Legacy task contains unsupported status '{value}'.");
        }

        return status;
    }

    private static string CorrelationId(MigrationOperation operation) =>
        $"migration:{operation.SourceReference}";

    private static bool ParseAiVerification(JsonElement payload)
    {
        if (TryGetProperty(payload, out var value, "aiVerificationPassed"))
        {
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return value.GetBoolean();
            }

            throw new InvalidOperationException("Legacy task contains an invalid aiVerificationPassed value.");
        }

        var status = OptionalString(payload, "aiVerification", "aiVerificationStatus");
        return string.Equals(status, "Passed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ParseHumanApproval(JsonElement payload)
    {
        if (TryGetProperty(payload, out var value, "humanReviewApproved"))
        {
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return value.GetBoolean();
            }

            throw new InvalidOperationException("Legacy task contains an invalid humanReviewApproved value.");
        }

        var status = OptionalString(payload, "humanApproval", "humanApprovalStatus");
        return string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);
    }

    private static string? OptionalSourceChangeKey(JsonElement payload)
    {
        var direct = OptionalString(payload, "sourceChangeKey", "sourceChangeId");
        if (direct is not null)
        {
            return direct;
        }

        if (TryGetProperty(payload, out var trace, "sourceTrace") && trace.ValueKind == JsonValueKind.Object)
        {
            return OptionalString(trace, "sourceChangeKey", "sourceChangeId");
        }

        return null;
    }

    private static void EnsureKind(MigrationOperation operation, LegacyRecord record)
        => EnsureKind(operation, record, "EngineeringTask");

    private static void EnsureKind(
        MigrationOperation operation,
        LegacyRecord record,
        string expectedKind)
    {
        if (!string.Equals(operation.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.SourceId, operation.SourceId, StringComparison.Ordinal) ||
            !string.Equals(record.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Migration record '{operation.SourceReference}' does not match its planned TaskFlow operation.");
        }
    }

    private static string RequiredText(
        JsonElement payload,
        int min,
        int max,
        params string[] names)
    {
        var value = RequiredString(payload, names);
        if (value.Length < min || value.Length > max)
        {
            throw new InvalidOperationException(
                $"Migration task text must contain between {min} and {max} characters.");
        }

        return value;
    }

    private static string? OptionalText(JsonElement payload, int max, params string[] names)
    {
        var value = OptionalString(payload, names);
        if (value is not null && value.Length > max)
        {
            throw new InvalidOperationException(
                $"Migration task text cannot exceed {max} characters.");
        }

        return value;
    }

    private static string RequiredString(JsonElement payload, params string[] names)
    {
        var value = OptionalString(payload, names);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Migration payload is missing one of the required string properties: {string.Join(", ", names)}.");
        }

        return value;
    }

    private static int RequiredPositiveInt(JsonElement payload, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names))
        {
            throw new InvalidOperationException(
                $"Migration payload is missing one of the required integer properties: {string.Join(", ", names)}.");
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed) || parsed <= 0)
        {
            throw new InvalidOperationException(
                $"Migration payload contains an invalid positive integer property: {string.Join(", ", names)}.");
        }

        return parsed;
    }

    private static string RequiredRawJson(JsonElement payload, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names) || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Migration payload is missing an object property: {string.Join(", ", names)}.");
        }

        return value.GetRawText();
    }

    private static string? OptionalRawJson(JsonElement payload, params string[] names)
    {
        return TryGetProperty(payload, out var value, names) &&
            (value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            ? value.GetRawText()
            : null;
    }

    private static string? OptionalScalarString(JsonElement payload, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString()!.Trim(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static decimal? OptionalDecimal(JsonElement payload, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Migration payload contains an invalid decimal property: {string.Join(", ", names)}.");
    }

    private static string? OptionalString(JsonElement payload, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static bool OptionalBoolean(JsonElement payload, bool defaultValue, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names))
        {
            return defaultValue;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        throw new InvalidOperationException(
            $"Migration payload contains an invalid boolean property: {string.Join(", ", names)}.");
    }

    private static DateTimeOffset RequiredDateTime(JsonElement payload, params string[] names)
    {
        foreach (var name in names)
        {
            var value = OptionalString(payload, name);
            if (value is not null && DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        throw new InvalidOperationException(
            $"Migration payload is missing a valid timestamp; expected one of: {string.Join(", ", names)}.");
    }

    private static bool TryGetProperty(JsonElement payload, out JsonElement value, params string[] names)
    {
        if (payload.ValueKind == JsonValueKind.Object)
        {
            var propertyValue = payload.EnumerateObject()
                .Where(item => names.Any(name =>
                    string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
                .Select(item => item.Value)
                .FirstOrDefault();
            if (propertyValue.ValueKind != JsonValueKind.Undefined)
            {
                value = propertyValue;
                return true;
            }
        }

        value = default;
        return false;
    }
}
