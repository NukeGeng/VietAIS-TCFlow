using Marten;
using MediatR;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record CreateProjectCommand(string Name, Guid OwnerId) : IRequest<Project>;

public sealed record GetProjectQuery(Guid ProjectId) : IRequest<Project>;

public sealed record SearchProjectsQuery(int PageNumber, int PageSize) : IRequest<IReadOnlyList<Project>>;

public sealed record DeleteProjectCommand(Guid ProjectId) : IRequest;

public sealed record CreateRepositoryWithoutSaveCommand(Guid ProjectId, string Name)
    : IRequest<ProjectRepository>;

public sealed class CreateProjectHandler(IDocumentSession session)
    : IRequestHandler<CreateProjectCommand, Project>
{
    public async Task<Project> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = new Project(Guid.NewGuid(), request.Name, request.OwnerId);
        session.Store(project);
        await session.SaveChangesAsync(cancellationToken);
        return project;
    }
}

public sealed class GetProjectHandler(IQuerySession session)
    : IRequestHandler<GetProjectQuery, Project>
{
    public async Task<Project> Handle(GetProjectQuery request, CancellationToken cancellationToken)
    {
        return await session.LoadAsync<Project>(request.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Project not found.");
    }
}

public sealed class SearchProjectsHandler(IQuerySession session)
    : IRequestHandler<SearchProjectsQuery, IReadOnlyList<Project>>
{
    public async Task<IReadOnlyList<Project>> Handle(
        SearchProjectsQuery request,
        CancellationToken cancellationToken)
    {
        return await session.Query<Project>()
            .OrderBy(project => project.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
    }
}

public sealed class DeleteProjectHandler(IDocumentSession session)
    : IRequestHandler<DeleteProjectCommand>
{
    public async Task Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await session.LoadAsync<Project>(request.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Project not found.");
        session.Delete(project);
        await session.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CreateRepositoryWithoutSaveHandler(IDocumentSession session)
    : IRequestHandler<CreateRepositoryWithoutSaveCommand, ProjectRepository>
{
    public Task<ProjectRepository> Handle(
        CreateRepositoryWithoutSaveCommand request,
        CancellationToken cancellationToken)
    {
        var repository = new ProjectRepository(Guid.NewGuid(), request.ProjectId, request.Name);
        session.Store(repository);
        return Task.FromResult(repository);
    }
}
