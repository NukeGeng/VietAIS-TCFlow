using System.Globalization;
using System.Text.Json;
using VietAIS.TCFlow.Modules.Architecture.Domain;

namespace VietAIS.TCFlow.Tools.Migration;

/// <summary>
/// Maps legacy living-architecture records to the ArchitectureModel event
/// stream. All relationship endpoints are resolved from explicit source ids.
/// </summary>
internal static class ArchitectureMigrationMapper
{
    public const string MigrationActor = "migration";
    public const string MigrationSource = "migration.goal2.architecture";

    public static ArchitectureModelCreated ToModelCreated(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "ArchitectureModel");
        var projectSourceId = operation.ProjectSourceId ?? throw new InvalidOperationException(
            $"Architecture migration '{operation.SourceReference}' must identify its project source id.");
        return new(
            operation.TargetId,
            Goal2MigrationPlanner.CreateDeterministicId("Project", projectSourceId),
            RequiredText(record.Payload, 2, 200, "name", "title"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "updatedAtUtc", "occurredAtUtc"));
    }

    public static ArchitectureModuleAdded ToModuleAdded(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "ArchitectureModule");
        return new(
            operation.TargetId,
            Goal2MigrationPlanner.CreateDeterministicId("ArchitectureModule", operation.SourceId),
            RequiredText(record.Payload, 2, 200, "name", "title"),
            OptionalText(record.Payload, 2000, "description", "details"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "updatedAtUtc", "occurredAtUtc"));
    }

    public static ArchitectureModulesConnected ToModulesConnected(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "ArchitectureModuleRelationship");
        return new(
            operation.TargetId,
            ArchitectureId(record.Payload, "fromModuleSourceId", "fromModuleId", "from"),
            ArchitectureId(record.Payload, "toModuleSourceId", "toModuleId", "to"),
            RequiredText(record.Payload, 2, 120, "relationship", "type"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "updatedAtUtc", "occurredAtUtc"));
    }

    public static ArchitectureEntityAdded ToEntityAdded(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "ArchitectureEntity");
        return new(
            operation.TargetId,
            Goal2MigrationPlanner.CreateDeterministicId("ArchitectureEntity", operation.SourceId),
            RequiredText(record.Payload, 2, 200, "name", "title", "entityName"),
            OptionalText(record.Payload, 2000, "description", "details"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "updatedAtUtc", "occurredAtUtc"));
    }

    public static ArchitectureDataRelationshipAdded ToDataRelationshipAdded(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "ArchitectureDataRelationship");
        return new(
            operation.TargetId,
            ArchitectureId(record.Payload, "fromEntitySourceId", "fromEntityId", "from"),
            ArchitectureId(record.Payload, "toEntitySourceId", "toEntityId", "to"),
            RequiredText(record.Payload, 2, 120, "relationship", "type"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "updatedAtUtc", "occurredAtUtc"));
    }

    public static ArchitectureDriftRecorded ToDriftRecorded(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "ArchitectureDrift");
        return new(
            operation.TargetId,
            RequiredText(record.Payload, 2, 300, "driftKey", "key", "id"),
            RequiredText(record.Payload, 2, 1000, "summary", "title"),
            RequiredText(record.Payload, 2, 4000, "evidence", "details", "description"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "detectedAtUtc", "createdAtUtc", "createdAt", "updatedAtUtc", "occurredAtUtc"));
    }

    private static Guid ArchitectureId(JsonElement payload, params string[] names) =>
        Goal2MigrationPlanner.CreateDeterministicId(
            names.Any(name => name.Contains("Module", StringComparison.OrdinalIgnoreCase))
                ? "ArchitectureModule"
                : "ArchitectureEntity",
            RequiredText(payload, 1, 300, names));

    private static string CorrelationId(MigrationOperation operation) => $"migration:{operation.SourceReference}";

    private static void EnsureKind(MigrationOperation operation, LegacyRecord record, string expectedKind)
    {
        if (!string.Equals(operation.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.SourceId, operation.SourceId, StringComparison.Ordinal) ||
            !string.Equals(record.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Migration record '{operation.SourceReference}' does not match its Architecture operation.");
        }
    }

    private static string RequiredText(JsonElement payload, int min, int max, params string[] names)
    {
        var value = OptionalText(payload, max, names);
        if (value is null || value.Length < min) throw new InvalidOperationException($"Migration payload is missing a valid text property: {string.Join(", ", names)}.");
        return value;
    }

    private static string? OptionalText(JsonElement payload, int max, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names) || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = text.Trim();
        if (normalized.Length > max) throw new InvalidOperationException($"Migration text property cannot exceed {max} characters: {string.Join(", ", names)}.");
        return normalized;
    }

    private static DateTimeOffset RequiredDateTime(JsonElement payload, params string[] names)
    {
        foreach (var name in names)
        {
            var value = OptionalText(payload, 100, name);
            if (value is not null && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)) return parsed.ToUniversalTime();
        }

        throw new InvalidOperationException($"Migration payload is missing a valid timestamp; expected one of: {string.Join(", ", names)}.");
    }

    private static bool TryGetProperty(JsonElement payload, out JsonElement value, params string[] names)
    {
        if (payload.ValueKind == JsonValueKind.Object)
        {
            var property = payload.EnumerateObject().FirstOrDefault(item => names.Any(name => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));
            if (property.Value.ValueKind != JsonValueKind.Undefined)
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
