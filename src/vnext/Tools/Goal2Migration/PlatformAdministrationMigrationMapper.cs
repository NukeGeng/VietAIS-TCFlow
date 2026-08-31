using System.Globalization;
using System.Text.Json;
using VietAIS.TCFlow.Modules.PlatformAdministration.Domain;

namespace VietAIS.TCFlow.Tools.Migration;

/// <summary>
/// Maps system-scoped v0.1 configuration documents to typed platform streams.
/// The mapper never places platform records in a project stream and fails
/// closed when a required setting cannot be represented safely.
/// </summary>
internal static class PlatformAdministrationMigrationMapper
{
    public const string MigrationSource = "migration.goal2.platform-administration";
    private const string MigrationActor = "migration";

    public static GlobalAiProviderImported ToAiProviderImported(
        MigrationOperation operation,
        LegacyRecord record)
    {
        EnsureKind(operation, record, "GlobalAiProviderConfiguration");
        return new(
            operation.TargetId,
            RequiredInt(record.Payload, "kind", "providerKind"),
            RequiredText(record.Payload, 2, 200, "displayName", "name"),
            RequiredBool(record.Payload, "isEnabled", "enabled"),
            RequiredActor(record.Payload),
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "updatedAt", "updatedAtUtc", "createdAtUtc", "occurredAtUtc"));
    }

    public static GlobalSystemSettingsImported ToGlobalSettingsImported(
        MigrationOperation operation,
        LegacyRecord record)
    {
        EnsureKind(operation, record, "GlobalSystemSettings");
        var supportUrl = OptionalText(record.Payload, 2000, "supportUrl");
        Uri? parsedUrl = null;
        if (supportUrl is not null && (!Uri.TryCreate(supportUrl, UriKind.Absolute, out parsedUrl) || parsedUrl is null))
        {
            throw new InvalidOperationException("Legacy global settings supportUrl must be an absolute URI.");
        }

        return new(
            operation.TargetId,
            RequiredText(record.Payload, 1, 200, "platformName", "name"),
            RequiredText(record.Payload, 1, 120, "defaultTimeZone", "timeZone"),
            supportUrl is null ? null : parsedUrl,
            RequiredActor(record.Payload),
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "updatedAt", "updatedAtUtc", "createdAtUtc", "occurredAtUtc"));
    }

    public static IReadOnlyList<object> ToPlatformPolicyEvents(
        MigrationOperation operation,
        LegacyRecord record)
    {
        EnsureKind(operation, record, "PlatformPolicy");
        var occurredAt = RequiredDateTime(
            record.Payload,
            "updatedAt",
            "updatedAtUtc",
            "createdAtUtc",
            "occurredAtUtc");
        var actor = RequiredActor(record.Payload);
        var correlation = CorrelationId(operation);
        var maximumRepositories = RequiredInt(record.Payload, "maximumRepositoriesPerProject", "maxRepositoriesPerProject");
        if (maximumRepositories is < 0 or > 100_000)
        {
            throw new InvalidOperationException("Legacy platform policy maximumRepositoriesPerProject must be between 0 and 100000.");
        }
        return
        [
            new PlatformPolicyCreated(operation.TargetId, actor, correlation, occurredAt),
            new PlatformPolicyImported(
                operation.TargetId,
                RequiredBool(record.Payload, "projectCreationEnabled", "allowProjectCreation"),
                RequiredBool(record.Payload, "repositoryConnectionsEnabled", "allowRepositoryConnections"),
                maximumRepositories,
                actor,
                correlation,
                occurredAt)
        ];
    }

    private static string RequiredActor(JsonElement payload)
    {
        var value = OptionalText(payload, 200, "updatedBy", "updatedById", "actorId", "createdBy");
        return value ?? MigrationActor;
    }

    private static bool RequiredBool(JsonElement payload, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names))
        {
            throw new InvalidOperationException($"Migration payload is missing a boolean property: {string.Join(", ", names)}.");
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        if (value.ValueKind == JsonValueKind.String &&
            bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Migration payload contains an invalid boolean property: {string.Join(", ", names)}.");
    }

    private static int RequiredInt(JsonElement payload, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names))
        {
            throw new InvalidOperationException($"Migration payload is missing an integer property: {string.Join(", ", names)}.");
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Migration payload contains an invalid integer property: {string.Join(", ", names)}.");
    }

    private static string RequiredText(JsonElement payload, int min, int max, params string[] names)
    {
        var value = OptionalText(payload, max, names);
        if (value is null || value.Length < min)
        {
            throw new InvalidOperationException($"Migration payload is missing a valid text property: {string.Join(", ", names)}.");
        }

        return value;
    }

    private static string? OptionalText(JsonElement payload, int max, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text.Trim();
        if (normalized.Length > max)
        {
            throw new InvalidOperationException($"Migration text property cannot exceed {max} characters: {string.Join(", ", names)}.");
        }

        return normalized;
    }

    private static DateTimeOffset RequiredDateTime(JsonElement payload, params string[] names)
    {
        foreach (var name in names)
        {
            var value = OptionalText(payload, 100, name);
            if (value is not null && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        throw new InvalidOperationException($"Migration payload is missing a valid timestamp; expected one of: {string.Join(", ", names)}.");
    }

    private static bool TryGetProperty(JsonElement payload, out JsonElement value, params string[] names)
    {
        if (payload.ValueKind == JsonValueKind.Object)
        {
            var property = payload.EnumerateObject()
                .FirstOrDefault(item => names.Any(name => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));
            if (property.Value.ValueKind != JsonValueKind.Undefined)
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string CorrelationId(MigrationOperation operation) => $"migration:{operation.SourceReference}";

    private static void EnsureKind(MigrationOperation operation, LegacyRecord record, string expectedKind)
    {
        if (!string.Equals(operation.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.SourceId, operation.SourceId, StringComparison.Ordinal) ||
            !string.Equals(record.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Migration record '{operation.SourceReference}' does not match its PlatformAdministration operation.");
        }
    }
}
