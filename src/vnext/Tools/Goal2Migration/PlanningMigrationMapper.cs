using System.Globalization;
using System.Text.Json;
using VietAIS.TCFlow.Modules.Planning.Domain;

namespace VietAIS.TCFlow.Tools.Migration;

/// <summary>
/// Maps the legacy planning documents to the Plan aggregate event language.
/// Child records must identify their owning plan explicitly; they are never
/// guessed from a project id.
/// </summary>
internal static class PlanningMigrationMapper
{
    public const string MigrationActor = "migration";
    public const string MigrationSource = "migration.goal2.planning";

    public static IReadOnlyList<object> ToEvents(
        MigrationOperation operation,
        LegacyRecord record)
    {
        return operation.Kind switch
        {
            "Plan" => [ToPlanCreated(operation, record)],
            "Requirement" => [ToRequirementAdded(operation, record)],
            "Milestone" => [ToMilestoneAdded(operation, record)],
            _ => throw new InvalidOperationException(
                $"Planning mapper does not support migration kind '{operation.Kind}'.")
        };
    }

    private static PlanCreated ToPlanCreated(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "Plan");
        var projectSourceId = operation.ProjectSourceId
            ?? throw new InvalidOperationException(
                $"Plan migration '{operation.SourceReference}' must identify its project source id.");
        return new PlanCreated(
            operation.TargetId,
            Goal2MigrationPlanner.CreateDeterministicId("Project", projectSourceId),
            RequiredString(record.Payload, "name", "title"),
            OptionalString(record.Payload, "purpose", "description"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "updatedAtUtc", "occurredAtUtc"));
    }

    private static RequirementAdded ToRequirementAdded(
        MigrationOperation operation,
        LegacyRecord record)
    {
        EnsureKind(operation, record, "Requirement");
        return new RequirementAdded(
            operation.TargetId,
            Goal2MigrationPlanner.CreateDeterministicId("Requirement", operation.SourceId),
            RequiredString(record.Payload, "title", "name"),
            OptionalString(record.Payload, "description", "details"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "updatedAtUtc", "occurredAtUtc"));
    }

    private static MilestoneAdded ToMilestoneAdded(
        MigrationOperation operation,
        LegacyRecord record)
    {
        EnsureKind(operation, record, "Milestone");
        var targetDate = OptionalDateOnly(record.Payload, "targetDate", "dueDate", "plannedFor");
        return new MilestoneAdded(
            operation.TargetId,
            Goal2MigrationPlanner.CreateDeterministicId("Milestone", operation.SourceId),
            RequiredString(record.Payload, "name", "title"),
            targetDate,
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "updatedAtUtc", "occurredAtUtc"));
    }

    private static void EnsureKind(MigrationOperation operation, LegacyRecord record, string expectedKind)
    {
        if (!string.Equals(operation.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.SourceId, operation.SourceId, StringComparison.Ordinal) ||
            !string.Equals(record.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Migration record '{operation.SourceReference}' does not match its planned Planning operation.");
        }
    }

    private static string CorrelationId(MigrationOperation operation) =>
        $"migration:{operation.SourceReference}";

    private static string RequiredString(JsonElement payload, params string[] names)
    {
        var value = OptionalString(payload, names);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Migration payload is missing one of the required string properties: {string.Join(", ", names)}.");
        }

        return value.Trim();
    }

    private static string? OptionalString(JsonElement payload, params string[] names)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var value = payload.EnumerateObject()
            .Where(property => names.Any(name =>
                string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
            .Select(property => property.Value)
            .FirstOrDefault();
        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static DateTimeOffset RequiredDateTime(JsonElement payload, params string[] names)
    {
        foreach (var name in names)
        {
            var text = OptionalString(payload, name);
            if (text is not null && DateTimeOffset.TryParse(
                    text,
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

    private static DateOnly? OptionalDateOnly(JsonElement payload, params string[] names)
    {
        var text = OptionalString(payload, names);
        if (text is null)
        {
            return null;
        }

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Migration payload contains an invalid date; expected one of: {string.Join(", ", names)}.");
    }
}
