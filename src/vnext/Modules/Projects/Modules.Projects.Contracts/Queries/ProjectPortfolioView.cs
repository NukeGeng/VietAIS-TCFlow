namespace VietAIS.TCFlow.Modules.Projects.Contracts.Queries;

public sealed record ProjectPortfolioView(
    Guid ProjectId,
    string Name,
    bool IsSuspended,
    long Version,
    DateTimeOffset LastChangedAtUtc);
