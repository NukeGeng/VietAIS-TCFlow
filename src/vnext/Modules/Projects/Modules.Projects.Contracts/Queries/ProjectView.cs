namespace VietAIS.TCFlow.Modules.Projects.Contracts.Queries;

public sealed record ProjectView(
    Guid ProjectId,
    string Name,
    string OwnerId,
    bool IsSuspended,
    long Version,
    DateTimeOffset LastChangedAtUtc);
