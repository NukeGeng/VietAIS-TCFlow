using System.Globalization;
using System.Text.Json;
using VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;
using VietAIS.TCFlow.Modules.EventStorming.Domain;

namespace VietAIS.TCFlow.Tools.Migration;

/// <summary>
/// Maps legacy Event Storming board records to typed board-stream events.
/// Parent board and node identities are explicit in the export; migration
/// never guesses a relationship target from a project id.
/// </summary>
internal static class EventStormingMigrationMapper
{
    public const string MigrationActor = "migration";
    public const string MigrationSource = "migration.goal2.event-storming";

    public static BoardCreated ToBoardCreated(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "StormingBoard");
        var projectSourceId = operation.ProjectSourceId ?? throw new InvalidOperationException(
            $"Event Storming migration '{operation.SourceReference}' must identify its project source id.");
        return new(
            operation.TargetId,
            Goal2MigrationPlanner.CreateDeterministicId("Project", projectSourceId),
            RequiredText(record.Payload, 2, 200, "name", "title"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "updatedAtUtc", "occurredAtUtc"));
    }

    public static StormingNodeAdded ToNodeAdded(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "StormingNode");
        return new(
            operation.TargetId,
            Goal2MigrationPlanner.CreateDeterministicId("StormingNode", operation.SourceId),
            ParseNodeType(record.Payload),
            RequiredText(record.Payload, 2, 240, "label", "name", "title"),
            OptionalText(record.Payload, 2000, "description", "details"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "observedAtUtc", "occurredAtUtc"));
    }

    public static StormingNodesConnected ToNodesConnected(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "StormingConnection");
        return new(
            operation.TargetId,
            NodeId(record.Payload, "fromNodeSourceId", "fromNodeId", "from"),
            NodeId(record.Payload, "toNodeSourceId", "toNodeId", "to"),
            RequiredText(record.Payload, 2, 120, "relationship", "type"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "createdAtUtc", "createdAt", "observedAtUtc", "occurredAtUtc"));
    }

    public static StormingHotspotMarked ToHotspotMarked(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "StormingHotspot");
        return new(
            operation.TargetId,
            NodeId(record.Payload, "nodeSourceId", "nodeId", "node"),
            RequiredText(record.Payload, 2, 1000, "reason", "summary"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "markedAtUtc", "createdAtUtc", "createdAt", "occurredAtUtc"));
    }

    public static StormingNodeReordered ToNodeReordered(MigrationOperation operation, LegacyRecord record)
    {
        EnsureKind(operation, record, "StormingNodeOrder");
        return new(
            operation.TargetId,
            NodeId(record.Payload, "nodeSourceId", "nodeId", "node"),
            RequiredInt32(record.Payload, "position", "order"),
            MigrationActor,
            CorrelationId(operation),
            RequiredDateTime(record.Payload, "reorderedAtUtc", "updatedAtUtc", "createdAtUtc", "occurredAtUtc"));
    }

    private static Guid NodeId(JsonElement payload, params string[] names) =>
        Goal2MigrationPlanner.CreateDeterministicId("StormingNode", RequiredText(payload, 1, 300, names));

    private static StormingNodeType ParseNodeType(JsonElement payload)
    {
        var value = RequiredText(payload, 1, 80, "nodeType", "type", "kind");
        if (!Enum.TryParse<StormingNodeType>(value, true, out var result))
        {
            throw new InvalidOperationException($"Legacy Storming node contains unsupported node type '{value}'.");
        }

        return result;
    }

    private static int RequiredInt32(JsonElement payload, params string[] names)
    {
        if (!TryGetProperty(payload, out var value, names))
        {
            throw new InvalidOperationException($"Migration payload is missing an integer property: {string.Join(", ", names)}.");
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return ValidatePosition(number);
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return ValidatePosition(parsed);
        throw new InvalidOperationException($"Migration payload contains an invalid integer property: {string.Join(", ", names)}.");
    }

    private static int ValidatePosition(int value)
    {
        if (value < 0) throw new InvalidOperationException("Legacy Storming node position cannot be negative.");
        return value;
    }

    private static string CorrelationId(MigrationOperation operation) => $"migration:{operation.SourceReference}";

    private static void EnsureKind(MigrationOperation operation, LegacyRecord record, string expectedKind)
    {
        if (!string.Equals(operation.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.Kind, expectedKind, StringComparison.Ordinal) ||
            !string.Equals(record.SourceId, operation.SourceId, StringComparison.Ordinal) ||
            !string.Equals(record.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Migration record '{operation.SourceReference}' does not match its EventStorming operation.");
        }
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
