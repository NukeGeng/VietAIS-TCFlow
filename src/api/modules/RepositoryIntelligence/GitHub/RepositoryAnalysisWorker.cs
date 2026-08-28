using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.GitHub;
using VietAIS.TCFlow.Analyzers.Monitoring;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using AnalyzerChangedFile = VietAIS.TCFlow.Analyzers.GitHub.GitHubChangedFileContract;
using AnalyzerChangedFileStatus = VietAIS.TCFlow.Analyzers.GitHub.GitHubChangedFileStatus;
using AnalyzerRequest = VietAIS.TCFlow.Analyzers.GitHub.GitHubAnalysisRequestContract;
using AnalyzerRequestStatus = VietAIS.TCFlow.Analyzers.GitHub.GitHubAnalysisRequestStatus;
using AnalyzerTrigger = VietAIS.TCFlow.Analyzers.GitHub.GitHubAnalysisTrigger;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

public sealed class RepositoryAnalysisWorkerOptions
{
    public const string SectionName = "RepositoryAnalysis";

    public bool Enabled { get; set; } = true;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan ProcessingLease { get; set; } = TimeSpan.FromMinutes(5);
}

internal sealed record ClaimedRepositoryAnalysis(
    Guid RequestId,
    RepositoryAnalysisWorkItem WorkItem);

internal sealed class RepositoryAnalysisWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RepositoryAnalysisWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<RepositoryAnalysisWorker> logger) : BackgroundService
{
    private static readonly Guid SystemActorId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly RepositoryAnalysisWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Repository analysis worker is disabled.");
            return;
        }

        if (_options.PollInterval <= TimeSpan.Zero || _options.ProcessingLease <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Repository analysis poll interval and processing lease must be positive.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            try
            {
                processed = await ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Repository analysis worker iteration failed.");
            }

            if (!processed)
            {
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var claim = await ClaimNextAsync(cancellationToken);
        if (claim is null)
        {
            return false;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<RepositoryAnalysisProcessor>();
            var result = await processor.ProcessAsync(claim.WorkItem, cancellationToken);
            await CompleteAsync(claim.RequestId, result, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Repository analysis request {AnalysisRequestId} failed.",
                claim.RequestId);
            await FailAsync(claim.RequestId, exception, cancellationToken);
        }

        return true;
    }

    private async Task<ClaimedRepositoryAnalysis?> ClaimNextAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var request = await session.Query<RepositoryAnalysisRequest>()
            .Where(item => item.Status == GitHubAnalysisRequestStatus.Pending)
            .OrderBy(item => item.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        RepositoryAnalysisRun? previousRun = null;
        if (request is null)
        {
            var staleBefore = timeProvider.GetUtcNow() - _options.ProcessingLease;
            previousRun = await session.Query<RepositoryAnalysisRun>()
                .Where(run => run.Status == RepositoryAnalysisRunStatus.Processing &&
                    run.UpdatedAt < staleBefore)
                .OrderBy(run => run.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            request = previousRun is null
                ? null
                : await session.LoadAsync<RepositoryAnalysisRequest>(
                    previousRun.Id,
                    cancellationToken);
            if (request?.Status != GitHubAnalysisRequestStatus.Processing)
            {
                return null;
            }
        }

        var workItem = Adapt(request);
        var now = timeProvider.GetUtcNow();
        var run = new RepositoryAnalysisRun(
            request.Id,
            request.ProjectId,
            request.RepositoryId,
            RepositoryAnalysisRunStatus.Processing,
            (previousRun?.Attempt ?? 0) + 1,
            null,
            [],
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            [],
            null,
            null,
            previousRun?.StartedAt ?? now,
            now,
            null);
        session.Store(request with { Status = GitHubAnalysisRequestStatus.Processing });
        session.Store(run);
        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return null;
        }

        return new ClaimedRepositoryAnalysis(request.Id, workItem);
    }

    private async Task CompleteAsync(
        Guid requestId,
        RepositoryAnalysisProcessingResult processing,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var request = await session.LoadAsync<RepositoryAnalysisRequest>(requestId, cancellationToken);
        var run = await session.LoadAsync<RepositoryAnalysisRun>(requestId, cancellationToken);
        if (request is null || run is null || request.Status != GitHubAnalysisRequestStatus.Processing)
        {
            throw new InvalidOperationException("Claimed repository analysis state was not found.");
        }

        var unsupported = processing.Outcome == RepositoryAnalysisProcessingOutcome.Unsupported;
        var ignored = processing.Outcome == RepositoryAnalysisProcessingOutcome.Ignored;
        var awaitingReasoning = processing.Outcome == RepositoryAnalysisProcessingOutcome.AwaitingReasoning;
        var now = timeProvider.GetUtcNow();
        var completedRequest = request with
        {
            Status = awaitingReasoning
                ? GitHubAnalysisRequestStatus.AwaitingReasoning
                : unsupported || ignored
                    ? GitHubAnalysisRequestStatus.Ignored
                    : GitHubAnalysisRequestStatus.Completed
        };
        var completedRun = run with
        {
            Status = awaitingReasoning
                ? RepositoryAnalysisRunStatus.AwaitingReasoning
                : unsupported
                    ? RepositoryAnalysisRunStatus.Unsupported
                    : RepositoryAnalysisRunStatus.Completed,
            SourceRevision = processing.SourceRevision,
            Technologies = processing.Technologies,
            ArtifactCount = processing.Graph.Artifacts.Count,
            DependencyCount = processing.Graph.Dependencies.Count,
            ContractCount = processing.Graph.Contracts.Count,
            MismatchCount = processing.Graph.ContractMismatches.Count,
            ChangeCount = processing.Graph.Changes.Count,
            ImpactCount = processing.Graph.Impacts.Count,
            GeneratedTaskCount = processing.GeneratedTaskCount,
            Diagnostics = processing.Diagnostics.Select(diagnostic =>
                    new RepositoryAnalysisDiagnostic(
                        diagnostic.Code,
                        diagnostic.Message,
                        diagnostic.Level.ToString(),
                        diagnostic.Location?.Path))
                .ToArray(),
            ErrorCode = null,
            ErrorMessage = null,
            UpdatedAt = now,
            CompletedAt = awaitingReasoning ? null : now
        };
        var auditAction = "repository.analysis.completed";
        if (unsupported)
        {
            auditAction = "repository.analysis.unsupported";
        }
        else if (ignored)
        {
            auditAction = "repository.analysis.ignored";
        }
        else if (awaitingReasoning)
        {
            auditAction = "repository.analysis.reasoning.queued";
        }

        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            SystemActorId,
            "system",
            auditAction,
            nameof(RepositoryAnalysisRequest),
            request.Id.ToString(),
            request,
            new { Request = completedRequest, Run = completedRun },
            timeProvider);
        session.Store(completedRequest);
        session.Store(completedRun);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
    }

    private async Task FailAsync(
        Guid requestId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var request = await session.LoadAsync<RepositoryAnalysisRequest>(requestId, cancellationToken);
        var run = await session.LoadAsync<RepositoryAnalysisRun>(requestId, cancellationToken);
        if (request is null || run is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var failedRequest = request with { Status = GitHubAnalysisRequestStatus.Failed };
        var failedRun = run with
        {
            Status = RepositoryAnalysisRunStatus.Failed,
            ErrorCode = exception.GetType().Name,
            ErrorMessage = SafeErrorMessage(exception.Message),
            UpdatedAt = now,
            CompletedAt = now
        };
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            SystemActorId,
            "system",
            "repository.analysis.failed",
            nameof(RepositoryAnalysisRequest),
            request.Id.ToString(),
            request,
            new { Request = failedRequest, Run = failedRun },
            timeProvider);
        session.Store(failedRequest);
        session.Store(failedRun);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static RepositoryAnalysisWorkItem Adapt(RepositoryAnalysisRequest request)
    {
        var contract = new AnalyzerRequest(
            request.Id,
            request.ProjectId,
            request.RepositoryId,
            (AnalyzerTrigger)(int)request.Trigger,
            request.DeliveryId,
            request.BaseRevision,
            request.HeadRevision,
            request.Reference,
            request.PullRequestNumber,
            request.FullScan,
            request.RequiresChangedFileFetch,
            request.ChangedFiles.Select(file => new AnalyzerChangedFile(
                    file.Path,
                    (AnalyzerChangedFileStatus)(int)file.Status))
                .ToArray(),
            AnalyzerRequestStatus.Pending,
            request.RequestedAt,
            request.RequestedByType,
            request.RequestedBy);
        return new GitHubAnalysisRequestAdapter().Adapt(contract);
    }

    private static string SafeErrorMessage(string message)
    {
        var normalized = string.IsNullOrWhiteSpace(message)
            ? "Repository analysis failed."
            : string.Join(' ', message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 1000 ? normalized : normalized[..1000];
    }
}
