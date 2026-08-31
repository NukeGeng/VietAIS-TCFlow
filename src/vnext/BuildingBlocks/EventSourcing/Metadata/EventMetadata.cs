namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;

public sealed record EventMetadata(
    string ActorId,
    string CorrelationId,
    string? CausationId,
    Guid? ProjectId,
    string? TenantId,
    string Source)
{
    public EventMetadata Normalize()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ActorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(CorrelationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Source);

        return this with
        {
            ActorId = ActorId.Trim(),
            CorrelationId = CorrelationId.Trim(),
            CausationId = NormalizeOptional(CausationId),
            TenantId = NormalizeOptional(TenantId),
            Source = Source.Trim()
        };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
