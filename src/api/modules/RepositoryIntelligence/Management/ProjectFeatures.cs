using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Paging;
using Marten;
using MediatR;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record CreateProjectCommand(Guid ActorId, string Name)
    : IRequest<CreateProjectResponse>;

public sealed record CreateProjectResponse(
    Project Project,
    ProjectState State,
    ProjectMembership OwnerMembership,
    ProjectRole OwnerRole,
    AuthorityPolicy AuthorityPolicy,
    ConventionProfile ConventionProfile,
    AiPermissionPolicy AiPolicy);

public sealed record GetProjectQuery(Guid ActorId, Guid ProjectId)
    : IRequest<Project>;

public sealed record UpdateProjectCommand(Guid ActorId, Guid ProjectId, string Name)
    : IRequest<Project>;

public sealed record SearchProjectsQuery(
    Guid ActorId,
    int PageNumber,
    int PageSize,
    string? Keyword)
    : IRequest<PagedList<Project>>;

public sealed class CreateProjectHandler(
    IDocumentSession session,
    TimeProvider timeProvider)
    : IRequestHandler<CreateProjectCommand, CreateProjectResponse>
{
    public async Task<CreateProjectResponse> Handle(
        CreateProjectCommand request,
        CancellationToken cancellationToken)
    {
        var platformPolicy = await session.LoadAsync<PlatformPolicy>(
            SystemConfigurationIds.PlatformPolicy,
            cancellationToken) ?? SystemConfigurationDefaults.Policy(timeProvider.GetUtcNow());
        if (!platformPolicy.ProjectCreationEnabled)
        {
            throw new ProjectManagementValidationException(
                "Project creation is disabled by the platform policy.");
        }

        if (request.ActorId == Guid.Empty)
        {
            throw new ProjectManagementValidationException("Project owner identity is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ProjectManagementValidationException("Project name is required.");
        }

        var name = request.Name.Trim();
        if (name.Length is < 2 or > 150)
        {
            throw new ProjectManagementValidationException(
                "Project name must contain between 2 and 150 characters.");
        }

        var now = timeProvider.GetUtcNow();
        var projectId = Guid.NewGuid();
        var ownerRole = new ProjectRole(
            Guid.NewGuid(),
            projectId,
            "Owner",
            IsSystemDefined: true,
            IsOwner: true,
            PermissionCatalog.ProjectDefinitions
                .Select(definition => new RolePermissionGrant(
                    definition.Id,
                    ResourceScopeKind.Project,
                    null,
                    []))
                .ToArray());
        var project = new Project(projectId, name, request.ActorId, now);
        var state = new ProjectState(
            projectId,
            projectId,
            ProjectLifecycleStatus.Active,
            now,
            request.ActorId);
        var membership = new ProjectMembership(
            Guid.NewGuid(),
            projectId,
            request.ActorId,
            IsActive: true,
            [new MemberRoleAssignment(ownerRole.Id, now, request.ActorId)]);
        var authorityPolicy = new AuthorityPolicy(
            projectId,
            projectId,
            [
                new AuthorityRule(AuthorityKnowledgeKind.ApiContract, AuthoritySourceKind.Backend),
                new AuthorityRule(AuthorityKnowledgeKind.UiRequirement, AuthoritySourceKind.Frontend),
                new AuthorityRule(AuthorityKnowledgeKind.BusinessLogic, AuthoritySourceKind.Backend),
                new AuthorityRule(AuthorityKnowledgeKind.Persistence, AuthoritySourceKind.Backend)
            ],
            now,
            request.ActorId);
        var conventionProfile = new ConventionProfile(
            projectId,
            projectId,
            ConventionProfileStatus.PendingAnalysis,
            [],
            [],
            [],
            [],
            [],
            now,
            request.ActorId);
        var aiPolicy = new AiPermissionPolicy(
            projectId,
            projectId,
            AiTrustLevel.SuggestOnly,
            [
                ProjectPermissionCodes.AiAnalysisRun,
                ProjectPermissionCodes.AiTaskSuggest
            ],
            request.ActorId,
            now);
        var audit = AuditRecordFactory.Create(
            projectId,
            request.ActorId,
            "user",
            "project.create",
            nameof(Project),
            projectId.ToString(),
            null,
            new
            {
                Project = project,
                State = state,
                OwnerMembershipId = membership.Id,
                OwnerRoleId = ownerRole.Id,
                AuthorityPolicyId = authorityPolicy.Id,
                ConventionProfileId = conventionProfile.Id,
                AiPolicyId = aiPolicy.Id
            },
            timeProvider);

        session.Store(project);
        session.Store(state);
        session.Store(membership);
        session.Store(ownerRole);
        session.Store(authorityPolicy);
        session.Store(conventionProfile);
        session.Store(aiPolicy);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);

        return new CreateProjectResponse(
            project,
            state,
            membership,
            ownerRole,
            authorityPolicy,
            conventionProfile,
            aiPolicy);
    }
}

public sealed class GetProjectHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<GetProjectQuery, Project>
{
    public async Task<Project> Handle(GetProjectQuery request, CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.ProjectView,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        return await session.LoadAsync<Project>(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");
    }
}

public sealed class UpdateProjectHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateProjectCommand, Project>
{
    public async Task<Project> Handle(
        UpdateProjectCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.ProjectUpdate,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);
        var current = await session.LoadAsync<Project>(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ProjectManagementValidationException("Project name is required.");
        }

        var name = request.Name.Trim();
        if (name.Length is < 2 or > 150)
        {
            throw new ProjectManagementValidationException(
                "Project name must contain between 2 and 150 characters.");
        }

        var updated = current with { Name = name };
        session.Store(updated);
        session.Store(AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "project.update",
            nameof(Project),
            request.ProjectId.ToString(),
            current,
            updated,
            timeProvider));
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }
}

public sealed class SearchProjectsHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<SearchProjectsQuery, PagedList<Project>>
{
    public async Task<PagedList<Project>> Handle(
        SearchProjectsQuery request,
        CancellationToken cancellationToken)
    {
        var (pageNumber, pageSize) = PageRequest.Validate(request.PageNumber, request.PageSize);
        var memberships = await session.Query<ProjectMembership>()
            .Where(membership => membership.UserId == request.ActorId && membership.IsActive)
            .ToListAsync(cancellationToken);

        var visible = new List<Project>();
        foreach (var membership in memberships)
        {
            var effective = await evaluator.GetEffectivePermissionsAsync(
                request.ActorId,
                new AuthorizationResourceContext(membership.ProjectId),
                cancellationToken);
            if (!effective.HasPermission(ProjectPermissionCodes.ProjectView))
            {
                continue;
            }

            var project = await session.LoadAsync<Project>(membership.ProjectId, cancellationToken);
            if (project is not null && Matches(project.Name, request.Keyword))
            {
                visible.Add(project);
            }
        }

        var ordered = visible.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        return new PagedList<Project>(
            ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(),
            pageNumber,
            pageSize,
            ordered.Length);
    }

    private static bool Matches(string name, string? keyword) =>
        string.IsNullOrWhiteSpace(keyword) || name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
}

internal static class PageRequest
{
    public static (int PageNumber, int PageSize) Validate(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
        {
            throw new ProjectManagementValidationException("Page number must be at least 1.");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ProjectManagementValidationException("Page size must contain between 1 and 100 items.");
        }

        return (pageNumber, pageSize);
    }
}
