using System.Security.Claims;
using Asp.Versioning;
using Carter;
using FSH.Framework.Core.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

public sealed record CreateProjectRoleRequest(string Name);

public sealed record UpdateProjectRolePermissionsRequest(RolePermissionRequest[] Permissions);

public sealed record AssignMemberRolesRequest(Guid[] RoleIds);

public sealed record UpdateAiPermissionPolicyRequest(
    AiTrustLevel TrustLevel,
    string[] AllowedPermissions);

public sealed record TransferProjectOwnershipRequest(Guid NewOwnerId, bool Confirmed);

public sealed class ProjectAuthorizationEndpoints : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        var projects = app.MapGroup("projects/{projectId:guid}")
            .WithTags("project-authorization")
            .RequireAuthorization();

        projects.MapGet("permission-definitions", GetPermissionDefinitions)
            .WithName(nameof(GetPermissionDefinitions))
            .Produces<IReadOnlyList<PermissionDefinition>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPost("roles", CreateRole)
            .WithName(nameof(CreateRole))
            .Produces<ProjectRole>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPut("roles/{roleId:guid}/permissions", UpdateRolePermissions)
            .WithName(nameof(UpdateRolePermissions))
            .Produces<ProjectRole>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPut("members/{memberId:guid}/roles", AssignMemberRoles)
            .WithName(nameof(AssignMemberRoles))
            .Produces<ProjectMembership>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapGet("members/{memberId:guid}/effective-permissions", GetEffectivePermissions)
            .WithName(nameof(GetEffectivePermissions))
            .Produces<EffectivePermissionResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPut("ai-policy", UpdateAiPolicy)
            .WithName(nameof(UpdateAiPolicy))
            .Produces<AiPermissionPolicy>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPost("ownership-transfers", TransferOwnership)
            .WithName(nameof(TransferOwnership))
            .Produces<Project>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapGet("audit", GetAudit)
            .WithName(nameof(GetAudit))
            .Produces<IReadOnlyList<AuditRecord>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));
    }

    private static async Task<IResult> GetPermissionDefinitions(
        Guid projectId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetProjectPermissionDefinitionsQuery(GetActorId(httpContext), projectId),
            cancellationToken));

    private static async Task<IResult> CreateRole(
        Guid projectId,
        CreateProjectRoleRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var role = await mediator.Send(
            new CreateProjectRoleCommand(GetActorId(httpContext), projectId, request.Name),
            cancellationToken);
        return Results.Created($"projects/{projectId}/roles/{role.Id}", role);
    }

    private static async Task<IResult> UpdateRolePermissions(
        Guid projectId,
        Guid roleId,
        UpdateProjectRolePermissionsRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new UpdateProjectRolePermissionsCommand(
                GetActorId(httpContext),
                projectId,
                roleId,
                request.Permissions),
            cancellationToken));

    private static async Task<IResult> AssignMemberRoles(
        Guid projectId,
        Guid memberId,
        AssignMemberRolesRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new AssignMemberRolesCommand(
                GetActorId(httpContext),
                projectId,
                memberId,
                request.RoleIds),
            cancellationToken));

    private static async Task<IResult> GetEffectivePermissions(
        Guid projectId,
        Guid memberId,
        Guid? repositoryId,
        ComponentScopeKind? component,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetEffectivePermissionsQuery(
                GetActorId(httpContext),
                projectId,
                memberId,
                repositoryId,
                component),
            cancellationToken));

    private static async Task<IResult> UpdateAiPolicy(
        Guid projectId,
        UpdateAiPermissionPolicyRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new UpdateAiPermissionPolicyCommand(
                GetActorId(httpContext),
                projectId,
                request.TrustLevel,
                request.AllowedPermissions),
            cancellationToken));

    private static async Task<IResult> TransferOwnership(
        Guid projectId,
        TransferProjectOwnershipRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new TransferProjectOwnershipCommand(
                GetActorId(httpContext),
                projectId,
                request.NewOwnerId,
                request.Confirmed),
            cancellationToken));

    private static async Task<IResult> GetAudit(
        Guid projectId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetProjectAuditQuery(GetActorId(httpContext), projectId),
            cancellationToken));

    private static Guid GetActorId(HttpContext httpContext)
    {
        var value = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var actorId)
            ? actorId
            : throw new UnauthorizedException();
    }
}
