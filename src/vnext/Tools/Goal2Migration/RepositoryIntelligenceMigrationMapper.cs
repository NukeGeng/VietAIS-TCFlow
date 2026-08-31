using System.Globalization;
using System.Text.Json;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Domain;

namespace VietAIS.TCFlow.Tools.Migration;

/// <summary>
/// Maps legacy repository-analysis records to the RepositoryIntelligence event
/// stream. Analysis facts stay deterministic and typed; semantic inference is
/// deliberately not performed during migration.
/// </summary>
internal static class RepositoryIntelligenceMigrationMapper
{
    public const string MigrationActor = "migration";
    public const string MigrationSource = "migration.goal2.repository-intelligence";

    public static AnalysisStarted ToAnalysisStarted(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "AnalysisRun");
        var projectSourceId = operation.ProjectSourceId ?? throw new InvalidOperationException(
            $"Analysis migration '{operation.SourceReference}' must identify its project source id.");
        return new AnalysisStarted(
            operation.TargetId,
            Goal2MigrationPlanner.CreateDeterministicId("Project", projectSourceId),
            RequiredText(record.Payload, 1, 300, "repositoryId", "repository", "repositoryName"),
            RequiredText(record.Payload, 1, 200, "commitSha", "commit", "revision"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "startedAtUtc", "startedAt", "createdAtUtc", "createdAt", "occurredAtUtc"));
    }

    public static ArtifactObserved ToArtifactObserved(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "SourceArtifact");
        return new ArtifactObserved(
            operation.TargetId,
            RequiredText(record.Payload, 1, 1000, "path", "filePath"),
            ParseFactKind(record.Payload),
            RequiredText(record.Payload, 1, 300, "symbol", "name", "typeName"),
            OptionalText(record.Payload, 2000, "details", "description"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "observedAtUtc", "observedAt", "createdAtUtc", "createdAt", "occurredAtUtc"));
    }

    public static ImpactRecorded ToImpactRecorded(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "SourceImpact");
        var confidence = RequiredDecimal(record.Payload, "confidence");
        if (confidence is < 0 or > 1)
        {
            throw new InvalidOperationException("Legacy source impact confidence must be between 0 and 1.");
        }

        return new ImpactRecorded(
            operation.TargetId,
            RequiredText(record.Payload, 2, 300, "impactKey", "id"),
            RequiredText(record.Payload, 2, 300, "changeKey", "sourceChangeKey", "sourceChangeId"),
            RequiredText(record.Payload, 1, 300, "affectedArtifactKey", "affectedArtifactId", "artifactId"),
            RequiredText(record.Payload, 2, 80, "severity"),
            RequiredText(record.Payload, 2, 2000, "reason", "summary"),
            confidence,
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "observedAtUtc", "observedAt", "createdAtUtc", "createdAt", "occurredAtUtc"));
    }

    private static SourceFactKind ParseFactKind(JsonElement payload)
    {
        var value = RequiredText(payload, 1, 80, "kind", "type", "factKind");
        if (!Enum.TryParse<SourceFactKind>(value, ignoreCase: true, out var result))
        {
            throw new InvalidOperationException($"Legacy source artifact contains unsupported fact kind '{value}'.");
        }

        return result;
    }

    private static string CorrelationId(MigrationOperation operation) => $"migration:{operation.SourceReference}";

    private static void EnsureKind(MigrationOperation operation, LegacyRecord record, string expectedKind)
    {
        if (!string.Equals(operation.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.SourceId, operation.SourceId, StringComparison.Ordinal) ||
            !string.Equals(record.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Migration record '{operation.SourceReference}' does not match its RepositoryIntelligence operation.");
        }
    }

    private static string RequiredText(JsonElement payload, int min, int max, params string[] names)
    {
        var value = OptionalText(payload, max, names);
        if (value is null || value.Length < min)
        {
            throw new InvalidOperationException(
                $"Migration payload is missing a valid text property: {string.Join(", ", names)}.");
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
            throw new InvalidOperationException(
                $"Migration text property cannot exceed {max} characters: {string.Join(", ", names)}.");
        }

        return normalized;
    }

    private static decimal RequiredDecimal(JsonElement payload, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names))
        {
            throw new InvalidOperationException(
                $"Migration payload is missing a decimal property: {string.Join(", ", names)}.");
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

    private static DateTimeOffset RequiredDateTime(JsonElement payload, params string[] names)
    {
        foreach (var name in names)
        {
            var value = OptionalText(payload, 100, name);
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
                .Where(item => names.Any(name => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
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
