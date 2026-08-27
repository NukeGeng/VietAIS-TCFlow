using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Paging;
using Marten;
using MediatR;
using VietAIS.TCFlow.Shared.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record SystemProjectSummary(Project Project, ProjectState State);

public sealed record SystemPermissionDefinition(
    string Id,
    string Description,
    PermissionDefinitionScope Scope);

public sealed record SearchSystemProjectsQuery(
    Guid ActorId,
    int PageNumber,
    int PageSize,
    string? Keyword)
    : IRequest<PagedList<SystemProjectSummary>>;

public sealed record UpdateProjectLifecycleStatusCommand(
    Guid ActorId,
    Guid ProjectId,
    ProjectLifecycleStatus Status)
    : IRequest<SystemProjectSummary>;

public sealed record GetSystemPermissionDefinitionsQuery(Guid ActorId)
    : IRequest<IReadOnlyList<SystemPermissionDefinition>>;

public sealed record SearchSystemAuditQuery(
    Guid ActorId,
    int PageNumber,
    int PageSize,
    Guid? ProjectId,
    string? Action)
    : IRequest<PagedList<AuditRecord>>;

public sealed class SearchSystemProjectsHandler(
    IQuerySession session,
    ISystemPermissionEvaluator systemPermissions)
    : IRequestHandler<SearchSystemProjectsQuery, PagedList<SystemProjectSummary>>
{
    public async Task<PagedList<SystemProjectSummary>> Handle(
        SearchSystemProjectsQuery request,
        CancellationToken cancellationToken)
    {
        await systemPermissions.EnsureAuthorizedAsync(
            request.ActorId,
            SystemPermissionCodes.ProjectInspect,
            cancellationToken);
        var (pageNumber, pageSize) = PageRequest.Validate(request.PageNumber, request.PageSize);
        var projects = await session.Query<Project>().ToListAsync(cancellationToken);
        var states = await session.Query<ProjectState>().ToListAsync(cancellationToken);
        var stateByProject = states.ToDictionary(state => state.ProjectId);
        var summaries = projects
            .Where(project => string.IsNullOrWhiteSpace(request.Keyword) ||
                project.Name.Contains(request.Keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .Select(project => new SystemProjectSummary(
                project,
                stateByProject.GetValueOrDefault(project.Id) ?? new ProjectState(
                    project.Id,
                    project.Id,
                    ProjectLifecycleStatus.Active,
                    project.CreatedAt,
                    project.PrimaryOwnerId)))
            .ToArray();

        return new PagedList<SystemProjectSummary>(
            summaries.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(),
            pageNumber,
            pageSize,
            summaries.Length);
    }
}

public sealed class UpdateProjectLifecycleStatusHandler(
    IDocumentSession session,
    ISystemPermissionEvaluator systemPermissions,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateProjectLifecycleStatusCommand, SystemProjectSummary>
{
    public async Task<SystemProjectSummary> Handle(
        UpdateProjectLifecycleStatusCommand request,
        CancellationToken cancellationToken)
    {
        await systemPermissions.EnsureAuthorizedAsync(
            request.ActorId,
            SystemPermissionCodes.ProjectSuspend,
            cancellationToken);
        if (request.Status is not ProjectLifecycleStatus.Active and not ProjectLifecycleStatus.Suspended)
        {
            throw new ProjectManagementValidationException(
                "System administration can only activate or suspend a project.");
        }

        var project = await session.LoadAsync<Project>(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");
        var current = await session.LoadAsync<ProjectState>(request.ProjectId, cancellationToken)
            ?? new ProjectState(
                request.ProjectId,
                request.ProjectId,
                ProjectLifecycleStatus.Active,
                project.CreatedAt,
                project.PrimaryOwnerId);
        if (current.Status == request.Status)
        {
            return new SystemProjectSummary(project, current);
        }

        var updated = current with
        {
            Status = request.Status,
            UpdatedAt = timeProvider.GetUtcNow(),
            UpdatedBy = request.ActorId
        };
        session.Store(updated);
        session.Store(AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "system-admin",
            request.Status == ProjectLifecycleStatus.Suspended
                ? "project.suspend"
                : "project.activate",
            nameof(ProjectState),
            request.ProjectId.ToString(),
            current,
            updated,
            timeProvider));
        await session.SaveChangesAsync(cancellationToken);
        return new SystemProjectSummary(project, updated);
    }
}

public sealed class GetSystemPermissionDefinitionsHandler(
    ISystemPermissionEvaluator systemPermissions)
    : IRequestHandler<GetSystemPermissionDefinitionsQuery, IReadOnlyList<SystemPermissionDefinition>>
{
    public async Task<IReadOnlyList<SystemPermissionDefinition>> Handle(
        GetSystemPermissionDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        await systemPermissions.EnsureAuthorizedAsync(
            request.ActorId,
            SystemPermissionCodes.PermissionDefinitionManage,
            cancellationToken);
        return FshPermissions.All
            .Select(permission => new SystemPermissionDefinition(
                permission.Name,
                permission.Description,
                PermissionDefinitionScope.System))
            .Concat(PermissionCatalog.All.Select(definition => new SystemPermissionDefinition(
                definition.Id,
                definition.Description,
                definition.Scope)))
            .DistinctBy(definition => definition.Id, StringComparer.Ordinal)
            .OrderBy(definition => definition.Scope)
            .ThenBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed class SearchSystemAuditHandler(
    IQuerySession session,
    ISystemPermissionEvaluator systemPermissions)
    : IRequestHandler<SearchSystemAuditQuery, PagedList<AuditRecord>>
{
    public async Task<PagedList<AuditRecord>> Handle(
        SearchSystemAuditQuery request,
        CancellationToken cancellationToken)
    {
        await systemPermissions.EnsureAuthorizedAsync(
            request.ActorId,
            SystemPermissionCodes.SystemAuditView,
            cancellationToken);
        var (pageNumber, pageSize) = PageRequest.Validate(request.PageNumber, request.PageSize);
        var records = await session.Query<AuditRecord>().ToListAsync(cancellationToken);
        var filtered = records
            .Where(record => request.ProjectId is null || record.ProjectId == request.ProjectId)
            .Where(record => string.IsNullOrWhiteSpace(request.Action) ||
                record.Action.Contains(request.Action.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(record => record.OccurredAt)
            .ToArray();

        return new PagedList<AuditRecord>(
            filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(),
            pageNumber,
            pageSize,
            filtered.Length);
    }
}
