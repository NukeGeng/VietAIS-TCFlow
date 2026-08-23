using FSH.Framework.Core.Exceptions;
using Marten;
using MediatR;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

public sealed record GetRepositoryAnalysisQuery(
    Guid ActorId,
    Guid ProjectId,
    Guid RepositoryId,
    Guid AnalysisRequestId) : IRequest<RepositoryAnalysisDetails>;

public sealed record GetLatestRepositoryAnalysisQuery(
    Guid ActorId,
    Guid ProjectId,
    Guid RepositoryId) : IRequest<RepositoryAnalysisDetails>;

public sealed class GetRepositoryAnalysisHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<GetRepositoryAnalysisQuery, RepositoryAnalysisDetails>,
        IRequestHandler<GetLatestRepositoryAnalysisQuery, RepositoryAnalysisDetails>
{
    public async Task<RepositoryAnalysisDetails> Handle(
        GetRepositoryAnalysisQuery request,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(
            request.ActorId,
            request.ProjectId,
            request.RepositoryId,
            cancellationToken);
        var analysis = await session.LoadAsync<RepositoryAnalysisRequest>(
            request.AnalysisRequestId,
            cancellationToken);
        return await LoadDetailsAsync(
            analysis,
            request.ProjectId,
            request.RepositoryId,
            cancellationToken);
    }

    public async Task<RepositoryAnalysisDetails> Handle(
        GetLatestRepositoryAnalysisQuery request,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(
            request.ActorId,
            request.ProjectId,
            request.RepositoryId,
            cancellationToken);
        var analysis = await session.Query<RepositoryAnalysisRequest>()
            .Where(item => item.ProjectId == request.ProjectId &&
                item.RepositoryId == request.RepositoryId)
            .OrderByDescending(item => item.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return await LoadDetailsAsync(
            analysis,
            request.ProjectId,
            request.RepositoryId,
            cancellationToken);
    }

    private async Task EnsureAuthorizedAsync(
        Guid actorId,
        Guid projectId,
        Guid repositoryId,
        CancellationToken cancellationToken) =>
        await evaluator.EnsureAuthorizedAsync(
            actorId,
            ProjectPermissionCodes.SourceAnalyze,
            new AuthorizationResourceContext(projectId, repositoryId),
            cancellationToken);

    private async Task<RepositoryAnalysisDetails> LoadDetailsAsync(
        RepositoryAnalysisRequest? request,
        Guid projectId,
        Guid repositoryId,
        CancellationToken cancellationToken)
    {
        if (request is null || request.ProjectId != projectId || request.RepositoryId != repositoryId)
        {
            throw new NotFoundException("Repository analysis request not found.");
        }

        var run = await session.LoadAsync<RepositoryAnalysisRun>(request.Id, cancellationToken);
        return new RepositoryAnalysisDetails(request, run);
    }
}
