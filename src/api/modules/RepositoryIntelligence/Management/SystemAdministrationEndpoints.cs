using System.Security.Claims;
using Asp.Versioning;
using Carter;
using FSH.Framework.Core.Paging;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VietAIS.TCFlow.Shared.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record UpdateProjectLifecycleStatusRequest(ProjectLifecycleStatus Status);

public sealed class SystemAdministrationEndpoints : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        var system = app.MapGroup("system")
            .WithTags("system-administration")
            .RequireAuthorization();

        system.MapGet("projects", SearchProjects)
            .WithName(nameof(SearchProjects))
            .Produces<PagedList<SystemProjectSummary>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        system.MapPut("projects/{projectId:guid}/status", UpdateProjectStatus)
            .WithName(nameof(UpdateProjectStatus))
            .Produces<SystemProjectSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        system.MapGet("permission-definitions", GetPermissionDefinitions)
            .WithName(nameof(GetPermissionDefinitions))
            .Produces<IReadOnlyList<SystemPermissionDefinition>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        system.MapGet("audit", SearchAudit)
            .WithName(nameof(SearchAudit))
            .Produces<PagedList<AuditRecord>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));
    }

    private static async Task<IResult> SearchProjects(
        int pageNumber,
        int pageSize,
        string? keyword,
        ClaimsPrincipal principal,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new SearchSystemProjectsQuery(
                GetActorId(principal),
                pageNumber,
                pageSize,
                keyword),
            cancellationToken));

    private static async Task<IResult> UpdateProjectStatus(
        Guid projectId,
        UpdateProjectLifecycleStatusRequest request,
        ClaimsPrincipal principal,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new UpdateProjectLifecycleStatusCommand(
                GetActorId(principal),
                projectId,
                request.Status),
            cancellationToken));

    private static async Task<IResult> GetPermissionDefinitions(
        ClaimsPrincipal principal,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetSystemPermissionDefinitionsQuery(GetActorId(principal)),
            cancellationToken));

    private static async Task<IResult> SearchAudit(
        int pageNumber,
        int pageSize,
        Guid? projectId,
        string? action,
        ClaimsPrincipal principal,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new SearchSystemAuditQuery(
                GetActorId(principal),
                pageNumber,
                pageSize,
                projectId,
                action),
            cancellationToken));

    private static Guid GetActorId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.GetUserId(), out var actorId) ? actorId : Guid.Empty;
}
