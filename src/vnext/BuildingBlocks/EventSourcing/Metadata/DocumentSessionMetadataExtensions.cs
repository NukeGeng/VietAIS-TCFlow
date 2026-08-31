using Marten;

namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;

public static class DocumentSessionMetadataExtensions
{
    public static void ApplyEventMetadata(this IDocumentSession session, EventMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(metadata);

        var normalized = metadata.Normalize();
        session.LastModifiedBy = normalized.ActorId;
        session.CorrelationId = normalized.CorrelationId;
        session.CausationId = normalized.CausationId;
        session.SetHeader(EventMetadataHeaders.ActorId, normalized.ActorId);
        session.SetHeader(EventMetadataHeaders.Source, normalized.Source);

        if (normalized.ProjectId is { } projectId)
        {
            session.SetHeader(EventMetadataHeaders.ProjectId, projectId.ToString("D"));
        }

        if (normalized.TenantId is { } tenantId)
        {
            session.SetHeader(EventMetadataHeaders.TenantId, tenantId);
        }
    }
}
