using System.Security.Claims;
using Asp.Versioning;
using Carter;
using FSH.Framework.Core.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record UpdateAuthorityPolicyRequest(AuthorityRule[] Rules);

public sealed record UpdateConventionProfileRequest(
    ConventionProfileStatus Status,
    string[] Architectures,
    string[] ApiStyles,
    string[] PersistencePatterns,
    string[] ValidationPatterns,
    string[] DtoPatterns);

public sealed class ProjectGovernanceEndpoints : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        var projects = app.MapGroup("projects/{projectId:guid}")
            .WithTags("project-governance")
            .RequireAuthorization();

        projects.MapGet("authority-policy", GetAuthorityPolicy)
            .WithName(nameof(GetAuthorityPolicy))
            .Produces<AuthorityPolicy>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPut("authority-policy", UpdateAuthorityPolicy)
            .WithName(nameof(UpdateAuthorityPolicy))
            .Produces<AuthorityPolicy>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapGet("convention-profile", GetConventionProfile)
            .WithName(nameof(GetConventionProfile))
            .Produces<ConventionProfile>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPut("convention-profile", UpdateConventionProfile)
            .WithName(nameof(UpdateConventionProfile))
            .Produces<ConventionProfile>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));
    }

    private static async Task<IResult> GetAuthorityPolicy(
        Guid projectId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetAuthorityPolicyQuery(GetActorId(httpContext), projectId),
            cancellationToken));

    private static async Task<IResult> UpdateAuthorityPolicy(
        Guid projectId,
        UpdateAuthorityPolicyRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new UpdateAuthorityPolicyCommand(
                GetActorId(httpContext),
                projectId,
                request.Rules),
            cancellationToken));

    private static async Task<IResult> GetConventionProfile(
        Guid projectId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetConventionProfileQuery(GetActorId(httpContext), projectId),
            cancellationToken));

    private static async Task<IResult> UpdateConventionProfile(
        Guid projectId,
        UpdateConventionProfileRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new UpdateConventionProfileCommand(
                GetActorId(httpContext),
                projectId,
                request.Status,
                request.Architectures,
                request.ApiStyles,
                request.PersistencePatterns,
                request.ValidationPatterns,
                request.DtoPatterns),
            cancellationToken));

    private static Guid GetActorId(HttpContext httpContext)
    {
        var value = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var actorId)
            ? actorId
            : throw new UnauthorizedException();
    }
}
