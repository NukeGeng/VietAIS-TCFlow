using System.Text.Json;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

internal static class AuditRecordFactory
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static AuditRecord Create(
        Guid projectId,
        Guid actorId,
        string actorType,
        string action,
        string targetType,
        string targetId,
        object? before,
        object? after,
        TimeProvider timeProvider) =>
        new(
            Guid.NewGuid(),
            projectId,
            actorId,
            actorType,
            action,
            timeProvider.GetUtcNow(),
            targetType,
            targetId,
            Serialize(before),
            Serialize(after));

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, Options);
}
