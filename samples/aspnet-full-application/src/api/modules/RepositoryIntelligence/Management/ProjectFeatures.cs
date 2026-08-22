using MediatR;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record Project(Guid Id, string Name, Guid PrimaryOwnerId);

public sealed record CreateProjectCommand(Guid ActorId, string Name)
    : IRequest<CreateProjectResponse>;

public sealed record CreateProjectResponse(Project Project);

public sealed record GetProjectQuery(Guid ActorId, Guid ProjectId)
    : IRequest<Project>;

public interface IProjectCreator
{
    Task<Project> CreateAsync(string name, Guid actorId, CancellationToken cancellationToken);
}

public interface IProjectReader
{
    Task<Project> GetAsync(Guid projectId, CancellationToken cancellationToken);
}

public interface IProjectPermissionEvaluator
{
    Task EnsureAuthorizedAsync(
        Guid actorId,
        string permission,
        Guid projectId,
        CancellationToken cancellationToken);
}

public sealed class CreateProjectHandler(IProjectCreator creator)
    : IRequestHandler<CreateProjectCommand, CreateProjectResponse>
{
    public async Task<CreateProjectResponse> Handle(
        CreateProjectCommand request,
        CancellationToken cancellationToken)
    {
        var project = await creator.CreateAsync(request.Name, request.ActorId, cancellationToken);
        return new CreateProjectResponse(project);
    }
}

public sealed class GetProjectHandler(
    IProjectReader reader,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<GetProjectQuery, Project>
{
    public async Task<Project> Handle(GetProjectQuery request, CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.ProjectView,
            request.ProjectId,
            cancellationToken);
        return await reader.GetAsync(request.ProjectId, cancellationToken);
    }
}

public static class ProjectPermissionCodes
{
    public const string ProjectView = "project.view";
}
