using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed class ProjectEndpoints : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        var projects = app.MapGroup("projects").RequireAuthorization();

        projects.MapPost(string.Empty, async (CreateProjectCommand request, ISender mediator) =>
            Results.Created("projects", await mediator.Send(request)))
            .WithName("CreateProject")
            .Produces<Project>(StatusCodes.Status201Created)
            .MapToApiVersion(1);
    }
}
