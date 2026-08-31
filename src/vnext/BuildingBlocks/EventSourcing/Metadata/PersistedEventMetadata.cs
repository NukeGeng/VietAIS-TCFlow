using JasperFx.Events;

namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;

public sealed record PersistedEventMetadata(
    Guid EventId,
    Guid StreamId,
    long Version,
    long Sequence,
    DateTimeOffset Timestamp,
    string? ActorId,
    string? CorrelationId,
    string? CausationId,
    string TenantId,
    string? ProjectId,
    string? Source)
{
    public static PersistedEventMetadata From(IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return new PersistedEventMetadata(
            @event.Id,
            @event.StreamId,
            @event.Version,
            @event.Sequence,
            @event.Timestamp,
            @event.UserName ?? Header(@event, EventMetadataHeaders.ActorId),
            @event.CorrelationId,
            @event.CausationId,
            @event.TenantId,
            Header(@event, EventMetadataHeaders.ProjectId),
            Header(@event, EventMetadataHeaders.Source));
    }

    private static string? Header(IEvent @event, string key)
    {
        if (@event.Headers is null || !@event.Headers.TryGetValue(key, out var value))
        {
            return null;
        }

        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
