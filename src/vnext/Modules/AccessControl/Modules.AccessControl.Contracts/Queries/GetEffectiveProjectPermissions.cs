using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;

namespace VietAIS.TCFlow.Modules.AccessControl.Contracts.Queries;

public sealed record GetEffectiveProjectPermissions(
    Guid ProjectId,
    string UserId,
    string? RepositoryId = null,
    ProjectComponentScope? Component = null) : IQuery;
