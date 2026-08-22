namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record Project(Guid Id, string Name, Guid OwnerId);

public sealed record ProjectRepository(Guid Id, Guid ProjectId, string Name);
