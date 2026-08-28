using FSH.Framework.Core.Exceptions;
using Marten;
using MediatR;
using Microsoft.Extensions.Options;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

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
    IProjectPermissionEvaluator evaluator,
    IOptions<RepositoryReasoningWorkerOptions> reasoningOptions)
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
        var reasoningJob = await session.Query<RepositoryReasoningJob>()
            .Where(job => job.WorkItem.RequestId == request.Id.ToString())
            .OrderByDescending(job => job.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        RepositoryReasoningDetails? reasoning = null;
        if (reasoningJob is not null ||
            request.Status == GitHubAnalysisRequestStatus.AwaitingReasoning ||
            run?.Status == RepositoryAnalysisRunStatus.AwaitingReasoning)
        {
            var provider = await session.LoadAsync<GlobalAiProviderConfiguration>(
                SystemConfigurationIds.CodexAppServerProvider,
                cancellationToken);
            reasoning = new RepositoryReasoningDetails(
                reasoningOptions.Value.Enabled,
                provider?.IsEnabled ?? true,
                reasoningJob?.Status,
                reasoningJob?.Attempt ?? 0,
                reasoningJob?.UpdatedAt);
        }

        return new RepositoryAnalysisDetails(request, run, reasoning);
    }
}
