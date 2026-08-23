using System.Security.Cryptography;
using System.Text;
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Monitoring;
using VietAIS.TCFlow.Analyzers.Reasoning;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;
using AnalyzerAiPolicy = VietAIS.TCFlow.Analyzers.Reasoning.AiActionPolicy;
using AnalyzerAiTrustLevel = VietAIS.TCFlow.Analyzers.Reasoning.AiTrustLevel;
using AnalyzerAuthorityKind = VietAIS.TCFlow.Analyzers.Governance.AuthorityKnowledgeKind;
using AnalyzerAuthorityPolicy = VietAIS.TCFlow.Analyzers.Governance.RepositoryAuthorityPolicy;
using AnalyzerAuthoritySource = VietAIS.TCFlow.Analyzers.Governance.AuthoritySourceKind;
using AnalyzerTask = VietAIS.TCFlow.Analyzers.Reasoning.SourceAwareEngineeringTask;
using AnalyzerTaskStatus = VietAIS.TCFlow.Analyzers.Reasoning.SourceAwareTaskStatus;
using ApiAiPolicy = VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization.AiPermissionPolicy;
using ApiAuthorityPolicy = VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management.AuthorityPolicy;
using ApiSourceChange = VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management.SourceChange;
using ApiTaskMutation = VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management.TaskMutation;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

public sealed class RepositoryReasoningWorkerOptions
{
    public const string SectionName = "RepositoryReasoning";

    public bool Enabled { get; set; }

    public string ExecutablePath { get; set; } = "codex";

    public string WorkingDirectory { get; set; } = ".tcflow/codex-reasoning";

    public string? Model { get; set; }

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan ProcessingLease { get; set; } = TimeSpan.FromMinutes(10);

    public int MaxAttempts { get; set; } = 3;
}

internal sealed class RepositoryDeepReasoningWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RepositoryReasoningWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<RepositoryDeepReasoningWorker> logger) : BackgroundService
{
    private static readonly Guid AiActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly RepositoryReasoningWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Repository deep reasoning worker is disabled.");
            return;
        }

        ValidateOptions();
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
                logger.LogError(exception, "Repository deep reasoning worker iteration failed.");
            }

            if (!processed)
            {
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
        }
    }

    private void ValidateOptions()
    {
        if (_options.PollInterval <= TimeSpan.Zero ||
            _options.ProcessingLease <= TimeSpan.Zero ||
            _options.MaxAttempts <= 0)
        {
            throw new InvalidOperationException(
                "Repository reasoning intervals, lease, and maximum attempts must be positive.");
        }

        if (string.IsNullOrWhiteSpace(_options.ExecutablePath) ||
            string.IsNullOrWhiteSpace(_options.WorkingDirectory))
        {
            throw new InvalidOperationException(
                "Repository reasoning requires a Codex executable and isolated working directory.");
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var jobId = await ClaimNextAsync(cancellationToken);
        if (jobId is null)
        {
            return false;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
            var job = await session.LoadAsync<RepositoryReasoningJob>(jobId, cancellationToken)
                ?? throw new InvalidOperationException("Claimed repository reasoning job was not found.");
            var graph = await new MartenKnowledgeGraphReader(session).LoadAsync(
                job.WorkItem.RepositoryId,
                cancellationToken)
                ?? throw new InvalidOperationException("Repository reasoning graph was not found.");
            var conventions = await new MartenConventionProfileReader(session).LoadAsync(
                job.WorkItem.RepositoryId,
                cancellationToken)
                ?? throw new InvalidOperationException("Repository convention profile was not found.");
            var settings = await LoadSettingsAsync(session, job.WorkItem, graph, conventions, cancellationToken);
            var provider = scope.ServiceProvider.GetRequiredService<IAiReasoningProvider>();
            var processor = new IncrementalDeepReasoningProcessor(
                provider,
                new MartenIncrementalTaskGateway(session, timeProvider),
                timeProvider: timeProvider);
            var result = await processor.ProcessAsync(
                job.WorkItem,
                graph,
                settings,
                cancellationToken);
            var projector = new RepositoryTaskProjector(session, timeProvider, AiActorId);
            var taskCount = await projector.ProjectAsync(job.WorkItem, graph, cancellationToken);
            await CompleteAsync(session, job, result, taskCount, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Repository reasoning job {ReasoningJobId} failed.", jobId);
            await RecordFailureAsync(jobId, exception, cancellationToken);
        }

        return true;
    }

    private async Task<string?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var now = timeProvider.GetUtcNow();
        var staleBefore = now - _options.ProcessingLease;
        var job = await session.Query<RepositoryReasoningJob>()
            .Where(item =>
                (item.Status == RepositoryReasoningJobStatus.Pending && item.NextAttemptAt <= now) ||
                (item.Status == RepositoryReasoningJobStatus.Processing && item.UpdatedAt < staleBefore))
            .OrderBy(item => item.NextAttemptAt)
            .ThenBy(item => item.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            return null;
        }

        session.Store(job with
        {
            Status = RepositoryReasoningJobStatus.Processing,
            Attempt = job.Attempt + 1,
            UpdatedAt = now,
            ErrorCode = null,
            ErrorMessage = null
        });
        try
        {
            await session.SaveChangesAsync(cancellationToken);
            return job.Id;
        }
        catch (ConcurrencyException)
        {
            return null;
        }
    }

    private static async Task<IncrementalDeepReasoningSettings> LoadSettingsAsync(
        IQuerySession session,
        DeepReasoningWorkItem workItem,
        RepositoryKnowledgeGraph graph,
        RepositoryConventionProfile conventions,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(workItem.ProjectId, out var projectId))
        {
            throw new InvalidOperationException("Repository reasoning project identity is invalid.");
        }

        var authority = await session.LoadAsync<ApiAuthorityPolicy>(projectId, cancellationToken)
            ?? throw new InvalidOperationException("Project authority policy was not found.");
        var aiPolicy = await session.LoadAsync<ApiAiPolicy>(projectId, cancellationToken)
            ?? throw new InvalidOperationException("Project AI permission policy was not found.");
        var analyzerPolicy = new AnalyzerAiPolicy(
            workItem.ProjectId,
            (AnalyzerAiTrustLevel)(int)aiPolicy.TrustLevel,
            aiPolicy.AllowedPermissions);
        var generationMode = aiPolicy.Allows(ProjectPermissionCodes.AiTaskCreate)
            ? TaskGenerationMode.Create
            : TaskGenerationMode.Suggest;
        return new IncrementalDeepReasoningSettings(
            MapAuthority(authority, graph),
            conventions,
            analyzerPolicy,
            generationMode,
            "ai:codex");
    }

    private static AnalyzerAuthorityPolicy MapAuthority(
        ApiAuthorityPolicy policy,
        RepositoryKnowledgeGraph graph) => new(
        policy.ProjectId.ToString(),
        Math.Max(1, graph.Revision),
        IsConfigured: true,
        policy.Rules.Select(rule => new KnowledgeAuthorityRule(
                (AnalyzerAuthorityKind)(int)rule.Knowledge,
                (AnalyzerAuthoritySource)(int)rule.Source,
                EvidenceLevel.Confirmed,
                1m,
                "Configured project authority policy.",
                []))
            .ToArray());

    private async Task CompleteAsync(
        IDocumentSession session,
        RepositoryReasoningJob job,
        IncrementalDeepReasoningResult result,
        int taskCount,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(job.WorkItem.RequestId, out var requestId))
        {
            throw new InvalidOperationException("Repository reasoning request identity is invalid.");
        }

        var request = await session.LoadAsync<RepositoryAnalysisRequest>(requestId, cancellationToken)
            ?? throw new InvalidOperationException("Repository analysis request was not found.");
        var run = await session.LoadAsync<RepositoryAnalysisRun>(requestId, cancellationToken)
            ?? throw new InvalidOperationException("Repository analysis run was not found.");
        var now = timeProvider.GetUtcNow();
        var completedJob = job with
        {
            Status = RepositoryReasoningJobStatus.Completed,
            UpdatedAt = now,
            CompletedAt = now,
            AiRequestCount = result.AiRequestCount,
            ReconciledTaskCount = taskCount,
            ErrorCode = null,
            ErrorMessage = null
        };
        var completedRequest = request with { Status = GitHubAnalysisRequestStatus.Completed };
        var completedRun = run with
        {
            Status = RepositoryAnalysisRunStatus.Completed,
            GeneratedTaskCount = taskCount,
            Diagnostics = run.Diagnostics.Concat(
                [new RepositoryAnalysisDiagnostic(
                    "ANALYSIS005",
                    $"AI reasoning completed with {result.AiRequestCount} request(s) and " +
                    $"{taskCount} reconciled task(s).",
                    EvidenceLevel.Inferred.ToString(),
                    null)])
                .ToArray(),
            UpdatedAt = now,
            CompletedAt = now
        };
        session.Store(completedJob);
        session.Store(completedRequest);
        session.Store(completedRun);
        session.Store(AuditRecordFactory.Create(
            request.ProjectId,
            AiActorId,
            "ai",
            "repository.analysis.reasoning.completed",
            nameof(RepositoryAnalysisRequest),
            request.Id.ToString(),
            request,
            new { Request = completedRequest, Run = completedRun, Job = completedJob },
            timeProvider));
        await session.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(
        string jobId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var job = await session.LoadAsync<RepositoryReasoningJob>(jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var terminal = job.Attempt >= _options.MaxAttempts;
        var failedJob = job with
        {
            Status = terminal ? RepositoryReasoningJobStatus.Failed : RepositoryReasoningJobStatus.Pending,
            NextAttemptAt = terminal ? now : now + RetryDelay(job.Attempt),
            UpdatedAt = now,
            CompletedAt = terminal ? now : null,
            ErrorCode = exception.GetType().Name,
            ErrorMessage = SafeErrorMessage(exception.Message)
        };
        session.Store(failedJob);
        if (terminal && Guid.TryParse(job.WorkItem.RequestId, out var requestId))
        {
            var request = await session.LoadAsync<RepositoryAnalysisRequest>(requestId, cancellationToken);
            var run = await session.LoadAsync<RepositoryAnalysisRun>(requestId, cancellationToken);
            if (request is not null && run is not null)
            {
                var failedRequest = request with { Status = GitHubAnalysisRequestStatus.Failed };
                var failedRun = run with
                {
                    Status = RepositoryAnalysisRunStatus.Failed,
                    ErrorCode = failedJob.ErrorCode,
                    ErrorMessage = failedJob.ErrorMessage,
                    UpdatedAt = now,
                    CompletedAt = now
                };
                session.Store(failedRequest);
                session.Store(failedRun);
                session.Store(AuditRecordFactory.Create(
                    request.ProjectId,
                    AiActorId,
                    "ai",
                    "repository.analysis.reasoning.failed",
                    nameof(RepositoryAnalysisRequest),
                    request.Id.ToString(),
                    request,
                    new { Request = failedRequest, Run = failedRun, Job = failedJob },
                    timeProvider));
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan RetryDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Math.Max(0, attempt - 1))));

    private static string SafeErrorMessage(string message)
    {
        var normalized = string.IsNullOrWhiteSpace(message)
            ? "Repository reasoning failed."
            : string.Join(' ', message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 1000 ? normalized : normalized[..1000];
    }
}

internal sealed class RepositoryTaskProjector(
    IDocumentSession session,
    TimeProvider timeProvider,
    Guid aiActorId)
{
    public async Task<int> ProjectAsync(
        DeepReasoningWorkItem workItem,
        RepositoryKnowledgeGraph graph,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(workItem.ProjectId, out var projectId) ||
            !Guid.TryParse(workItem.RepositoryId, out var repositoryId))
        {
            throw new InvalidOperationException("Repository task projection identities are invalid.");
        }

        var changeIds = workItem.SourceChangeIds.ToHashSet(StringComparer.Ordinal);
        var revision = workItem.GraphRevision.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (Guid.TryParse(workItem.RequestId, out var requestId))
        {
            var request = await session.LoadAsync<RepositoryAnalysisRequest>(requestId, cancellationToken);
            revision = request?.HeadRevision ?? revision;
        }

        var candidates = await session.Query<AnalyzerTask>()
            .Where(task => task.ProjectId == workItem.ProjectId && task.RepositoryId == workItem.RepositoryId)
            .ToListAsync(cancellationToken);
        var tasks = candidates.Where(task => task.SourceChangeIds.Any(changeIds.Contains))
            .OrderBy(task => task.CreatedAt)
            .ThenBy(task => task.Id, StringComparer.Ordinal)
            .ToArray();
        foreach (var sourceTask in tasks)
        {
            await ProjectTaskAsync(
                projectId,
                repositoryId,
                revision,
                workItem,
                graph,
                sourceTask,
                cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
        return tasks.Count(task => task.Status != AnalyzerTaskStatus.Cancelled);
    }

    private async Task ProjectTaskAsync(
        Guid projectId,
        Guid repositoryId,
        string revision,
        DeepReasoningWorkItem workItem,
        RepositoryKnowledgeGraph graph,
        AnalyzerTask sourceTask,
        CancellationToken cancellationToken)
    {
        var sourceChangeIds = sourceTask.SourceChangeIds
            .Select(id => StableGuid("source-change", id))
            .ToArray();
        var artifactIds = sourceTask.ArtifactIds
            .Select(id => StableGuid("source-artifact", id))
            .ToArray();
        var impactIds = graph.Impacts.Where(impact =>
                sourceTask.SourceChangeIds.Contains(impact.SourceChangeId, StringComparer.Ordinal) ||
                sourceTask.ArtifactIds.Contains(impact.AffectedArtifactId, StringComparer.Ordinal))
            .Select(impact => StableGuid("source-impact", impact.Id))
            .Distinct()
            .ToArray();
        var taskId = StableGuid("engineering-task", sourceTask.Id);
        StoreSourceDocuments(projectId, repositoryId, revision, workItem, graph, sourceTask);
        var existing = await session.LoadAsync<EngineeringTask>(taskId, cancellationToken);
        var projection = await session.LoadAsync<RepositoryTaskProjection>(sourceTask.Id, cancellationToken);
        if (projection?.SourceVersion >= sourceTask.Version)
        {
            return;
        }

        var sourceStatus = MapStatus(sourceTask.Status);
        if (existing is not null &&
            existing.Status != TaskLifecycleStatus.Suggested &&
            projection?.SourceStatus == AnalyzerTaskStatus.Suggested &&
            sourceTask.Status is AnalyzerTaskStatus.Suggested or AnalyzerTaskStatus.Cancelled)
        {
            StoreEvidence(projectId, taskId, sourceChangeIds, artifactIds, impactIds, graph, sourceTask);
            session.Store(new RepositoryTaskProjection(
                sourceTask.Id,
                projectId,
                repositoryId,
                taskId,
                sourceTask.Version,
                sourceTask.Status,
                timeProvider.GetUtcNow()));
            session.Store(AuditRecordFactory.Create(
                projectId,
                aiActorId,
                "ai",
                ProjectPermissionCodes.AiTaskSuggest,
                nameof(EngineeringTask),
                taskId.ToString(),
                existing,
                new
                {
                    Suggestion = sourceTask,
                    Reason = "Human-promoted task retained; source update remains a suggestion."
                },
                timeProvider));
            return;
        }

        var projectedStatus = existing is not null &&
            sourceStatus == TaskLifecycleStatus.Suggested &&
            existing.Status != TaskLifecycleStatus.Suggested
                ? existing.Status
                : sourceStatus;
        var projected = new EngineeringTask(
            taskId,
            projectId,
            repositoryId,
            ComponentId: null,
            MapComponent(sourceTask.TargetComponent),
            FeatureId: null,
            sourceTask.Title,
            sourceTask.Description,
            projectedStatus,
            Priority(sourceTask.Confidence),
            new TaskSourceTrace(
                sourceChangeIds.FirstOrDefault() == Guid.Empty ? null : sourceChangeIds.First(),
                artifactIds,
                sourceTask.EvidenceIds.Select(id => StableGuid("task-evidence", $"{taskId}:{id}")).ToArray(),
                impactIds),
            graph.Artifacts.Where(artifact => sourceTask.ArtifactIds.Contains(artifact.Id, StringComparer.Ordinal))
                .Select(artifact => artifact.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Inputs: [],
            Outputs: [],
            BusinessRules: sourceTask.Requirements.ToArray(),
            Dependencies: [],
            existing?.CreatedBy ?? aiActorId,
            existing?.CreatedByType ?? TaskActorType.Ai,
            existing?.CreatedAt ?? sourceTask.CreatedAt,
            sourceTask.UpdatedAt,
            existing is null ? 1 : existing.CurrentVersion + 1,
            existing?.AiVerification ?? AiVerificationStatus.NotRun,
            existing?.HumanApproval ?? HumanApprovalStatus.Pending);
        StoreEvidence(projectId, taskId, sourceChangeIds, artifactIds, impactIds, graph, sourceTask);
        session.Store(new RepositoryTaskProjection(
            sourceTask.Id,
            projectId,
            repositoryId,
            taskId,
            sourceTask.Version,
            sourceTask.Status,
            timeProvider.GetUtcNow()));
        if (existing is null)
        {
            session.Store(projected);
            session.Store(TaskVersionFactory.Create(
                projected,
                aiActorId,
                TaskActorType.Ai,
                "source-aware AI task projected",
                timeProvider));
            session.Store(AuditRecordFactory.Create(
                projectId,
                aiActorId,
                "ai",
                projected.Status == TaskLifecycleStatus.Suggested
                    ? ProjectPermissionCodes.AiTaskSuggest
                    : ProjectPermissionCodes.AiTaskCreate,
                nameof(EngineeringTask),
                projected.Id.ToString(),
                null,
                projected,
                timeProvider));
            return;
        }

        var action = projected.Status == TaskLifecycleStatus.Cancelled
            ? ProjectPermissionCodes.AiTaskClose
            : projected.Status == TaskLifecycleStatus.Suggested && existing.Status == TaskLifecycleStatus.Suggested
                ? ProjectPermissionCodes.AiTaskSuggest
                : ProjectPermissionCodes.AiTaskUpdate;
        ApiTaskMutation.Store(
            session,
            existing,
            projected,
            aiActorId,
            TaskActorType.Ai,
            "source-aware AI task reconciled",
            action,
            timeProvider);
    }

    private void StoreSourceDocuments(
        Guid projectId,
        Guid repositoryId,
        string revision,
        DeepReasoningWorkItem workItem,
        RepositoryKnowledgeGraph graph,
        AnalyzerTask sourceTask)
    {
        var impacts = graph.Impacts.Where(impact =>
                sourceTask.SourceChangeIds.Contains(impact.SourceChangeId, StringComparer.Ordinal) ||
                sourceTask.ArtifactIds.Contains(impact.AffectedArtifactId, StringComparer.Ordinal))
            .ToArray();
        var referencedChangeIds = sourceTask.SourceChangeIds.Concat(impacts.Select(impact => impact.SourceChangeId))
            .ToHashSet(StringComparer.Ordinal);
        var referencedArtifactIds = sourceTask.ArtifactIds
            .Concat(impacts.Select(impact => impact.AffectedArtifactId))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var change in graph.Changes.Where(change => referencedChangeIds.Contains(change.Id)))
        {
            session.Store(new ApiSourceChange(
                StableGuid("source-change", change.Id),
                projectId,
                repositoryId,
                revision,
                change.Reason,
                workItem.QueuedAt));
        }

        foreach (var artifact in graph.Artifacts.Where(artifact => referencedArtifactIds.Contains(artifact.Id)))
        {
            session.Store(new SourceArtifact(
                StableGuid("source-artifact", artifact.Id),
                projectId,
                repositoryId,
                ComponentId: null,
                artifact.Kind.ToString(),
                artifact.Name,
                artifact.Path));
        }

        foreach (var impact in impacts)
        {
            session.Store(new SourceImpact(
                StableGuid("source-impact", impact.Id),
                projectId,
                StableGuid("source-change", impact.SourceChangeId),
                StableGuid("source-artifact", impact.AffectedArtifactId),
                impact.Severity.ToString(),
                impact.Reason,
                impact.Confidence));
        }
    }

    private void StoreEvidence(
        Guid projectId,
        Guid taskId,
        IReadOnlyList<Guid> sourceChangeIds,
        IReadOnlyList<Guid> artifactIds,
        IReadOnlyList<Guid> impactIds,
        RepositoryKnowledgeGraph graph,
        AnalyzerTask sourceTask)
    {
        foreach (var evidence in graph.Evidence.Where(evidence =>
                     sourceTask.EvidenceIds.Contains(evidence.Id, StringComparer.Ordinal)))
        {
            session.Store(new TaskEvidence(
                StableGuid("task-evidence", $"{taskId}:{evidence.Id}"),
                projectId,
                taskId,
                TaskEvidenceKind.Artifact,
                evidence.Statement,
                $"{evidence.Location.Path}:{evidence.Location.StartLine}",
                sourceChangeIds.FirstOrDefault() == Guid.Empty ? null : sourceChangeIds.First(),
                artifactIds.FirstOrDefault() == Guid.Empty ? null : artifactIds.First(),
                impactIds.FirstOrDefault() == Guid.Empty ? null : impactIds.First(),
                evidence.Confidence,
                timeProvider.GetUtcNow(),
                aiActorId,
                TaskActorType.Ai));
        }
    }

    private static TaskLifecycleStatus MapStatus(AnalyzerTaskStatus status) => status switch
    {
        AnalyzerTaskStatus.Suggested => TaskLifecycleStatus.Suggested,
        AnalyzerTaskStatus.Upcoming => TaskLifecycleStatus.Upcoming,
        AnalyzerTaskStatus.InProgress => TaskLifecycleStatus.InProgress,
        AnalyzerTaskStatus.ReadyForReview => TaskLifecycleStatus.ReadyForReview,
        AnalyzerTaskStatus.Completed => TaskLifecycleStatus.Completed,
        AnalyzerTaskStatus.Blocked => TaskLifecycleStatus.Blocked,
        AnalyzerTaskStatus.Rejected => TaskLifecycleStatus.Rejected,
        AnalyzerTaskStatus.Cancelled => TaskLifecycleStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown source-aware task status.")
    };

    private static ComponentScopeKind MapComponent(PlanTargetComponent component) => component switch
    {
        PlanTargetComponent.Frontend => ComponentScopeKind.Frontend,
        PlanTargetComponent.Backend => ComponentScopeKind.Backend,
        PlanTargetComponent.Shared => ComponentScopeKind.SharedLibrary,
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown target component.")
    };

    private static TaskPriority Priority(decimal confidence) => confidence switch
    {
        >= 0.9m => TaskPriority.High,
        >= 0.7m => TaskPriority.Medium,
        _ => TaskPriority.Low
    };

    private static Guid StableGuid(string scope, string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"vietais-tcflow:{scope}:{value}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}
