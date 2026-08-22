using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record CreateProjectRequest(string Name);

public sealed class ProjectManagementEndpoints : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        var projects = app.MapGroup("projects")
            .WithTags("project-management")
            .RequireAuthorization();

        projects.MapPost(string.Empty, CreateProject)
            .WithName(nameof(CreateProject))
            .Produces<CreateProjectResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .MapToApiVersion(1);

        projects.MapGet("{projectId:guid}", GetProject)
            .WithName(nameof(GetProject))
            .Produces<Project>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(1);
    }

    private static async Task<IResult> CreateProject(
        CreateProjectRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateProjectCommand(Guid.NewGuid(), request.Name),
            cancellationToken);
        return Results.Created("projects", result);
    }

    private static async Task<IResult> GetProject(
        Guid projectId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetProjectQuery(Guid.NewGuid(), projectId),
            cancellationToken);
        return Results.Ok(result);
    }
}
