using Marten;
using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Reasoning;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;
using ApiTaskMutation = VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management.TaskMutation;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

internal sealed record RepositoryTaskVerificationEvaluation(
    AiVerificationStatus Status,
    string Summary,
    string? Location,
    decimal Confidence);

internal sealed record RepositoryTaskVerificationBatch(
    int CandidateCount,
    int UpdatedCount,
    int PassedCount,
    int FailedCount,
    int InconclusiveCount,
    int SkippedByPolicyCount)
{
    public static RepositoryTaskVerificationBatch Empty { get; } = new(0, 0, 0, 0, 0, 0);
}

internal static class RepositoryTaskVerificationTargetFactory
{
    public static RepositoryTaskVerificationTarget? Create(
        RepositoryKnowledgeGraph graph,
        string contractMismatchId)
    {
        var mismatch = graph.ContractMismatches.SingleOrDefault(item => item.Id == contractMismatchId);
        var pair = mismatch is null
            ? null
            : graph.ContractPairs.SingleOrDefault(item => item.Id == mismatch.ContractPairId);
        return mismatch is not null && pair?.BackendContractId is not null
            ? new RepositoryTaskVerificationTarget(
                pair.FrontendContractId,
                pair.BackendContractId,
                mismatch.Kind,
                mismatch.Subject)
            : null;
    }
}

internal static class RepositoryTaskVerificationEvaluator
{
    public static RepositoryTaskVerificationEvaluation Evaluate(
        RepositoryTaskProjection projection,
        RepositoryKnowledgeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(graph);
        var target = projection.VerificationTarget;
        if (target is null)
        {
            return new RepositoryTaskVerificationEvaluation(
                AiVerificationStatus.Inconclusive,
                "Static verification is inconclusive because the task has no persisted contract target.",
                null,
                0m);
        }

        var pair = graph.ContractPairs.SingleOrDefault(item =>
            item.Status == ContractPairStatus.Matched &&
            item.FrontendContractId == target.FrontendContractId &&
            item.BackendContractId == target.BackendContractId);
        if (pair is null)
        {
            return new RepositoryTaskVerificationEvaluation(
                AiVerificationStatus.Inconclusive,
                $"Static verification is inconclusive because the contract pair for '{target.Subject}' " +
                "is no longer uniquely matched.",
                null,
                0m);
        }

        var remaining = graph.ContractMismatches
            .Where(item => item.ContractPairId == pair.Id && item.Subject == target.Subject)
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        if (remaining.Length > 0)
        {
            var missing = string.Join(" ", remaining.Select(item =>
                $"{item.Explanation} Expected: {item.FrontendValue}. Actual: {item.BackendValue}."));
            return new RepositoryTaskVerificationEvaluation(
                AiVerificationStatus.Failed,
                $"Missing requirement '{target.Subject}': {missing}",
                Location(remaining.SelectMany(item => item.Locations)),
                remaining.Min(item => item.Confidence));
        }

        var frontend = graph.Contracts.SingleOrDefault(item => item.Id == target.FrontendContractId);
        var backend = graph.Contracts.SingleOrDefault(item => item.Id == target.BackendContractId);
        if (frontend is null || backend is null)
        {
            return new RepositoryTaskVerificationEvaluation(
                AiVerificationStatus.Inconclusive,
                $"Static verification is inconclusive because source evidence for '{target.Subject}' is missing.",
                null,
                0m);
        }

        return new RepositoryTaskVerificationEvaluation(
            AiVerificationStatus.Passed,
            $"Static contract comparison passed for '{target.Subject}'; expected and actual source now match.",
            Location(graph.Evidence
                .Where(item => frontend.EvidenceIds.Contains(item.Id, StringComparer.Ordinal) ||
                    backend.EvidenceIds.Contains(item.Id, StringComparer.Ordinal))
                .Select(item => item.Location)),
            pair.Confidence);
    }

    private static string? Location(IEnumerable<VietAIS.TCFlow.Analyzers.Core.SourceLocation> locations)
    {
        var location = locations.OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.StartLine)
            .FirstOrDefault();
        return location is null ? null : $"{location.Path}:{location.StartLine}";
    }
}

internal sealed class RepositoryTaskVerificationService(
    IDocumentSession session,
    TimeProvider timeProvider)
{
    private static readonly Guid AiActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task<RepositoryTaskVerificationBatch> VerifyAsync(
        Guid projectId,
        Guid repositoryId,
        RepositoryKnowledgeGraph previousGraph,
        RepositoryKnowledgeGraph graph,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previousGraph);
        ArgumentNullException.ThrowIfNull(graph);
        var projections = await session.Query<RepositoryTaskProjection>()
            .Where(item => item.ProjectId == projectId && item.RepositoryId == repositoryId)
            .OrderBy(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        if (projections.Count == 0)
        {
            return RepositoryTaskVerificationBatch.Empty;
        }

        var activeTasks = await session.Query<EngineeringTask>()
            .Where(item => item.ProjectId == projectId && item.RepositoryId == repositoryId &&
                (item.Status == TaskLifecycleStatus.InProgress ||
                    item.Status == TaskLifecycleStatus.ReadyForReview))
            .ToListAsync(cancellationToken);
        var tasksById = activeTasks.ToDictionary(item => item.Id);
        var candidates = new List<(RepositoryTaskProjection Projection, EngineeringTask Task)>();
        foreach (var storedProjection in projections)
        {
            var projection = storedProjection;
            if (tasksById.TryGetValue(projection.EngineeringTaskId, out var task))
            {
                if (projection.VerificationTarget is null)
                {
                    var sourceTask = await session.LoadAsync<SourceAwareEngineeringTask>(
                        projection.Id,
                        cancellationToken);
                    var target = sourceTask is null
                        ? null
                        : RepositoryTaskVerificationTargetFactory.Create(
                            previousGraph,
                            sourceTask.ContractMismatchId);
                    if (target is not null)
                    {
                        projection = projection with
                        {
                            VerificationTarget = target,
                            UpdatedAt = timeProvider.GetUtcNow()
                        };
                        session.Store(projection);
                    }
                }

                candidates.Add((projection, task));
            }
        }

        if (candidates.Count == 0)
        {
            return RepositoryTaskVerificationBatch.Empty;
        }

        var policy = await session.LoadAsync<AiPermissionPolicy>(projectId, cancellationToken)
            ?? throw new InvalidOperationException("Project AI permission policy was not found.");
        if (!policy.Allows(ProjectPermissionCodes.AiTaskUpdate))
        {
            return new RepositoryTaskVerificationBatch(
                candidates.Count,
                0,
                0,
                0,
                0,
                candidates.Count);
        }

        var updatedCount = 0;
        var passedCount = 0;
        var failedCount = 0;
        var inconclusiveCount = 0;
        foreach (var candidate in candidates)
        {
            var evaluation = RepositoryTaskVerificationEvaluator.Evaluate(candidate.Projection, graph);
            switch (evaluation.Status)
            {
                case AiVerificationStatus.Passed:
                    passedCount++;
                    break;
                case AiVerificationStatus.Failed:
                    failedCount++;
                    break;
                case AiVerificationStatus.Inconclusive:
                    inconclusiveCount++;
                    break;
            }

            var desiredStatus = DesiredStatus(candidate.Task.Status, evaluation.Status);
            if (candidate.Task.AiVerification == evaluation.Status &&
                candidate.Task.Status == desiredStatus)
            {
                continue;
            }

            var now = timeProvider.GetUtcNow();
            var evidence = new TaskEvidence(
                Guid.NewGuid(),
                projectId,
                candidate.Task.Id,
                TaskEvidenceKind.Verification,
                evaluation.Summary,
                evaluation.Location,
                null,
                null,
                null,
                evaluation.Confidence,
                now,
                AiActorId,
                TaskActorType.Ai);
            var updated = candidate.Task with
            {
                Status = desiredStatus,
                SourceTrace = candidate.Task.SourceTrace with
                {
                    EvidenceIds = candidate.Task.SourceTrace.EvidenceIds
                        .Append(evidence.Id)
                        .Distinct()
                        .ToArray()
                },
                AiVerification = evaluation.Status,
                UpdatedAt = now,
                CurrentVersion = candidate.Task.CurrentVersion + 1
            };
            session.Store(evidence);
            ApiTaskMutation.Store(
                session,
                candidate.Task,
                updated,
                AiActorId,
                TaskActorType.Ai,
                evaluation.Summary,
                "task.ai.verify",
                timeProvider,
                evidence: evidence);
            updatedCount++;
        }

        return new RepositoryTaskVerificationBatch(
            candidates.Count,
            updatedCount,
            passedCount,
            failedCount,
            inconclusiveCount,
            0);
    }

    private static TaskLifecycleStatus DesiredStatus(
        TaskLifecycleStatus current,
        AiVerificationStatus verification) => verification switch
        {
            AiVerificationStatus.Passed when current == TaskLifecycleStatus.InProgress =>
                TaskLifecycleStatus.ReadyForReview,
            AiVerificationStatus.Failed or AiVerificationStatus.Inconclusive
                when current == TaskLifecycleStatus.ReadyForReview => TaskLifecycleStatus.InProgress,
            _ => current
        };
}
