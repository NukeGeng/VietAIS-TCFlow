using FSH.Framework.Core.Exceptions;
using Marten;
using MediatR;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

public sealed record CreateProjectRoleCommand(Guid ActorId, Guid ProjectId, string Name)
    : IRequest<ProjectRole>;

public sealed record RolePermissionRequest(
    string PermissionCode,
    ResourceScopeKind ResourceScope,
    Guid? ResourceId,
    ComponentScopeKind[] ComponentScopes);

public sealed record UpdateProjectRolePermissionsCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid RoleId,
    RolePermissionRequest[] Permissions)
    : IRequest<ProjectRole>;

public sealed record AssignMemberRolesCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid MemberId,
    Guid[] RoleIds)
    : IRequest<ProjectMembership>;

public sealed record GetEffectivePermissionsQuery(
    Guid ActorId,
    Guid ProjectId,
    Guid MemberId,
    Guid? RepositoryId,
    ComponentScopeKind? Component)
    : IRequest<EffectivePermissionResult>;

public sealed record UpdateAiPermissionPolicyCommand(
    Guid ActorId,
    Guid ProjectId,
    AiTrustLevel TrustLevel,
    string[] AllowedPermissions)
    : IRequest<AiPermissionPolicy>;

public sealed record TransferProjectOwnershipCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid NewOwnerId,
    bool Confirmed)
    : IRequest<Project>;

public sealed record GetProjectAuditQuery(Guid ActorId, Guid ProjectId)
    : IRequest<IReadOnlyList<AuditRecord>>;

public sealed record GetProjectPermissionDefinitionsQuery(Guid ActorId, Guid ProjectId)
    : IRequest<IReadOnlyList<PermissionDefinition>>;

public sealed class CreateProjectRoleHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<CreateProjectRoleCommand, ProjectRole>
{
    public async Task<ProjectRole> Handle(CreateProjectRoleCommand request, CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RoleCreate,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ProjectAuthorizationValidationException("Role name is required.");
        }

        var name = request.Name.Trim();
        if (name.Length is < 2 or > 100)
        {
            throw new ProjectAuthorizationValidationException("Role name must contain between 2 and 100 characters.");
        }

        var duplicate = await session.Query<ProjectRole>()
            .AnyAsync(role => role.ProjectId == request.ProjectId && role.Name == name, cancellationToken);
        if (duplicate)
        {
            throw new ProjectAuthorizationValidationException("A role with the same name already exists in this project.");
        }

        var role = new ProjectRole(Guid.NewGuid(), request.ProjectId, name, false, false, []);
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "role.create",
            nameof(ProjectRole),
            role.Id.ToString(),
            null,
            role,
            timeProvider);

        session.Store(role);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return role;
    }
}

public sealed class UpdateProjectRolePermissionsHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateProjectRolePermissionsCommand, ProjectRole>
{
    public async Task<ProjectRole> Handle(
        UpdateProjectRolePermissionsCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RoleUpdate,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        var role = await session.LoadAsync<ProjectRole>(request.RoleId, cancellationToken);
        if (role is null || role.ProjectId != request.ProjectId)
        {
            throw new NotFoundException("Project role not found.");
        }

        if (role.IsSystemDefined)
        {
            throw new ProjectAuthorizationValidationException("System-defined project roles cannot be modified.");
        }

        if (request.Permissions is null)
        {
            throw new ProjectAuthorizationValidationException("Permission grants are required.");
        }

        var grants = request.Permissions.Select(ValidateGrant).ToArray();
        if (grants.Select(grant => new
            {
                grant.PermissionCode,
                grant.ResourceScope,
                grant.ResourceId,
                Components = string.Join(',', grant.ComponentScopes.Order())
            }).Distinct().Count() != grants.Length)
        {
            throw new ProjectAuthorizationValidationException("Duplicate permission grants are not allowed.");
        }

        var updated = role with { Permissions = grants };
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "role.permissions.update",
            nameof(ProjectRole),
            role.Id.ToString(),
            role,
            updated,
            timeProvider);

        session.Store(updated);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }

    private static RolePermissionGrant ValidateGrant(RolePermissionRequest request)
    {
        if (!PermissionCatalog.TryGetProjectDefinition(request.PermissionCode, out var definition))
        {
            throw new ProjectAuthorizationValidationException(
                $"Permission '{request.PermissionCode}' is not an available project permission.");
        }

        if (!definition.AllowedResourceScopes.Contains(request.ResourceScope))
        {
            throw new ProjectAuthorizationValidationException(
                $"Resource scope '{request.ResourceScope}' is not allowed for '{request.PermissionCode}'.");
        }

        var componentScopes = request.ComponentScopes ?? [];
        if (componentScopes.Any(component => !definition.AllowedComponentScopes.Contains(component)))
        {
            throw new ProjectAuthorizationValidationException(
                $"One or more component scopes are not allowed for '{request.PermissionCode}'.");
        }

        if (request.ResourceScope == ResourceScopeKind.Repository && request.ResourceId is null)
        {
            throw new ProjectAuthorizationValidationException("Repository-scoped grants require a repository id.");
        }

        return new RolePermissionGrant(
            request.PermissionCode,
            request.ResourceScope,
            request.ResourceId,
            componentScopes.Distinct().Order().ToArray());
    }
}

public sealed class AssignMemberRolesHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<AssignMemberRolesCommand, ProjectMembership>
{
    public async Task<ProjectMembership> Handle(AssignMemberRolesCommand request, CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.MemberRoleAssign,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        var membership = await session.Query<ProjectMembership>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == request.ProjectId && item.UserId == request.MemberId && item.IsActive,
                cancellationToken)
            ?? throw new NotFoundException("Active project membership not found.");

        var project = await session.LoadAsync<Project>(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");
        if (project.PrimaryOwnerId == request.MemberId)
        {
            throw new ProjectAuthorizationValidationException(
                "The primary owner's roles can only change through ownership transfer.");
        }

        if (request.RoleIds is null)
        {
            throw new ProjectAuthorizationValidationException("Role ids are required.");
        }

        var roleIds = request.RoleIds.Distinct().ToArray();
        foreach (var roleId in roleIds)
        {
            var role = await session.LoadAsync<ProjectRole>(roleId, cancellationToken);
            if (role is null || role.ProjectId != request.ProjectId)
            {
                throw new ProjectAuthorizationValidationException("Every assigned role must belong to this project.");
            }

            if (role.IsOwner)
            {
                throw new ProjectAuthorizationValidationException(
                    "The Owner role can only be assigned through ownership transfer.");
            }
        }

        var now = timeProvider.GetUtcNow();
        var updated = membership with
        {
            Roles = roleIds.Select(roleId => new MemberRoleAssignment(roleId, now, request.ActorId)).ToArray()
        };
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "member.roles.assign",
            nameof(ProjectMembership),
            membership.Id.ToString(),
            membership,
            updated,
            timeProvider);

        session.Store(updated);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }
}

public sealed class GetEffectivePermissionsHandler(
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<GetEffectivePermissionsQuery, EffectivePermissionResult>
{
    public async Task<EffectivePermissionResult> Handle(
        GetEffectivePermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var resource = new AuthorizationResourceContext(
            request.ProjectId,
            request.RepositoryId,
            request.Component);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RoleView,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        return await evaluator.GetEffectivePermissionsAsync(request.MemberId, resource, cancellationToken);
    }
}

public sealed class UpdateAiPermissionPolicyHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateAiPermissionPolicyCommand, AiPermissionPolicy>
{
    public async Task<AiPermissionPolicy> Handle(
        UpdateAiPermissionPolicyCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.AiPolicyUpdate,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        if (request.AllowedPermissions is null)
        {
            throw new ProjectAuthorizationValidationException("AI permissions are required.");
        }

        var permissions = request.AllowedPermissions.Distinct(StringComparer.Ordinal).Order().ToArray();
        foreach (var permission in permissions)
        {
            if (!permission.StartsWith("ai.", StringComparison.Ordinal) ||
                !PermissionCatalog.TryGetProjectDefinition(permission, out _))
            {
                throw new ProjectAuthorizationValidationException(
                    $"Permission '{permission}' is not an available AI actor permission.");
            }

            if (!AiTrustPolicy.IsAllowed(request.TrustLevel, permission))
            {
                throw new ProjectAuthorizationValidationException(
                    $"Permission '{permission}' exceeds AI trust level '{request.TrustLevel}'.");
            }
        }

        var before = await session.LoadAsync<AiPermissionPolicy>(request.ProjectId, cancellationToken);
        var updated = new AiPermissionPolicy(
            request.ProjectId,
            request.ProjectId,
            request.TrustLevel,
            permissions,
            request.ActorId,
            timeProvider.GetUtcNow());
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "ai.policy.update",
            nameof(AiPermissionPolicy),
            request.ProjectId.ToString(),
            before,
            updated,
            timeProvider);

        session.Store(updated);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }
}

public sealed class TransferProjectOwnershipHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<TransferProjectOwnershipCommand, Project>
{
    public async Task<Project> Handle(
        TransferProjectOwnershipCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.ProjectOwnershipTransfer,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        if (!request.Confirmed)
        {
            throw new ProjectAuthorizationValidationException("Ownership transfer requires explicit confirmation.");
        }

        var project = await session.LoadAsync<Project>(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");
        if (project.PrimaryOwnerId == request.NewOwnerId)
        {
            throw new ProjectAuthorizationValidationException("The selected member is already the primary owner.");
        }

        var currentOwnerMembership = await FindMembership(project.PrimaryOwnerId, request, cancellationToken);
        var newOwnerMembership = await FindMembership(request.NewOwnerId, request, cancellationToken);
        var ownerRole = await session.Query<ProjectRole>()
            .SingleOrDefaultAsync(
                role => role.ProjectId == request.ProjectId && role.IsSystemDefined && role.IsOwner,
                cancellationToken)
            ?? throw new InvalidOperationException("The project has no system-defined Owner role.");

        var now = timeProvider.GetUtcNow();
        var updatedCurrentOwner = currentOwnerMembership with
        {
            Roles = currentOwnerMembership.Roles.Where(role => role.RoleId != ownerRole.Id).ToArray()
        };
        var updatedNewOwner = newOwnerMembership with
        {
            Roles = newOwnerMembership.Roles
                .Where(role => role.RoleId != ownerRole.Id)
                .Append(new MemberRoleAssignment(ownerRole.Id, now, request.ActorId))
                .ToArray()
        };
        var updatedProject = project with { PrimaryOwnerId = request.NewOwnerId };
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "project.ownership.transfer",
            nameof(Project),
            project.Id.ToString(),
            project,
            updatedProject,
            timeProvider);

        session.Store(updatedProject);
        session.Store(updatedCurrentOwner);
        session.Store(updatedNewOwner);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return updatedProject;
    }

    private async Task<ProjectMembership> FindMembership(
        Guid userId,
        TransferProjectOwnershipCommand request,
        CancellationToken cancellationToken) =>
        await session.Query<ProjectMembership>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == request.ProjectId && item.UserId == userId && item.IsActive,
                cancellationToken)
        ?? throw new ProjectAuthorizationValidationException(
            "Ownership can only be transferred to an active project member.");
}

public sealed class GetProjectAuditHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<GetProjectAuditQuery, IReadOnlyList<AuditRecord>>
{
    public async Task<IReadOnlyList<AuditRecord>> Handle(
        GetProjectAuditQuery request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.AuditView,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        return await session.Query<AuditRecord>()
            .Where(record => record.ProjectId == request.ProjectId)
            .OrderByDescending(record => record.OccurredAt)
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetProjectPermissionDefinitionsHandler(IProjectPermissionEvaluator evaluator)
    : IRequestHandler<GetProjectPermissionDefinitionsQuery, IReadOnlyList<PermissionDefinition>>
{
    public async Task<IReadOnlyList<PermissionDefinition>> Handle(
        GetProjectPermissionDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RoleView,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        return PermissionCatalog.ProjectDefinitions;
    }
}
