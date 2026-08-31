namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Projections;

public sealed record ProjectionStatus(
    string ProjectionName,
    string? TenantId,
    long Sequence,
    long HighWaterMark,
    long Lag,
    string? AgentStatus,
    DateTimeOffset? LastHeartbeat);
