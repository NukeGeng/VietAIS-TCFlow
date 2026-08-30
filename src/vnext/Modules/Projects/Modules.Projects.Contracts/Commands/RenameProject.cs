namespace VietAIS.TCFlow.Modules.Projects.Contracts.Commands;

public sealed record RenameProject(
    Guid ProjectId,
    string Name,
    long ExpectedVersion,
    string CorrelationId);
