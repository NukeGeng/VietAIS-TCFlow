using System.Globalization;
using System.Text.Json;
using VietAIS.TCFlow.Modules.Projects.Domain;

namespace VietAIS.TCFlow.Tools.Migration;

internal static class ProjectMigrationMapper
{
    public const string MigrationActor = "migration";
    public const string MigrationSource = "migration.goal2.projects";

    public static ProjectCreated ToProjectCreated(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "Project");
        var name = RequiredString(record.Payload, "name");
        var ownerId = RequiredStringAny(record.Payload, "ownerId", "primaryOwnerId", "createdBy");
        var occurredAt = RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "occurredAtUtc");
        var projectId = operation.TargetId;
        var correlationId = CorrelationId(operation);

        return new ProjectCreated(
            projectId,
            name,
            ownerId,
            MigrationActor,
            correlationId,
            occurredAt);
    }

    public static ProjectLifecycleReconciled ToLifecycleReconciled(
        MigrationOperation operation,
        LegacyRecord record)
    {
        EnsureKind(operation, record, "ProjectState");
        var status = RequiredString(record.Payload, "status");
        var isSuspended = status switch
        {
            "Active" => false,
            "Suspended" => true,
            _ => throw new InvalidOperationException(
                $"Legacy ProjectState '{operation.SourceId}' has unsupported status '{status}'.")
        };
        var occurredAt = RequiredDateTime(record.Payload, "updatedAtUtc", "updatedAt", "occurredAtUtc");

        return new ProjectLifecycleReconciled(
            operation.TargetId,
            isSuspended,
            MigrationActor,
            CorrelationId(operation),
            occurredAt);
    }

    public static string CorrelationId(MigrationOperation operation) =>
        $"migration:{operation.SourceReference}";

    private static void EnsureKind(
        MigrationOperation operation,
        LegacyRecord record,
        string expectedKind)
    {
        if (!string.Equals(operation.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.SourceId, operation.SourceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Migration record '{operation.SourceReference}' does not match its planned kind or source id.");
        }

        if (!string.Equals(record.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Migration record '{operation.SourceReference}' payload hash does not match its plan.");
        }
    }

    private static string RequiredString(JsonElement payload, string propertyName)
    {
        if (!TryGetProperty(payload, propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException(
                $"Migration payload is missing required string property '{propertyName}'.");
        }

        return value.GetString()!.Trim();
    }

    private static string RequiredStringAny(JsonElement payload, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(payload, propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString()!.Trim();
            }
        }

        throw new InvalidOperationException(
            $"Migration payload is missing one of the required owner properties: {string.Join(", ", propertyNames)}.");
    }

    private static DateTimeOffset RequiredDateTime(JsonElement payload, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(payload, propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(
                    value.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        throw new InvalidOperationException(
            $"Migration payload is missing a valid timestamp; expected one of: {string.Join(", ", propertyNames)}.");
    }

    private static bool TryGetProperty(
        JsonElement payload,
        string propertyName,
        out JsonElement value)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        var property = payload.EnumerateObject()
            .FirstOrDefault(item => string.Equals(
                item.Name,
                propertyName,
                StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(property.Name))
        {
            value = default;
            return false;
        }

        value = property.Value;
        return true;
    }
}
