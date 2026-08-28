using Marten;

namespace VietAIS.TCFlow.Analyzers.Reasoning;

public static class TaskReconciliationStorage
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Schema.For<SourceAwareEngineeringTask>()
            .UseOptimisticConcurrency(true)
            .Index(task => task.ProjectId)
            .Index(task => task.RepositoryId)
            .Index(task => task.CorrelationKey);
        options.Schema.For<SourceAwareTaskVersion>()
            .UniqueIndex(version => version.TaskId, version => version.Version)
            .Index(version => version.ProjectId);
        options.Schema.For<AiActionAudit>()
            .Index(audit => audit.ProjectId)
            .Index(audit => audit.RepositoryId);
    }
}

public sealed class MartenTaskReconciliationWriter(
    IDocumentSession session,
    TimeProvider timeProvider)
{
    public async Task ApplyAsync(
        TaskReconciliationDecision decision,
        AiActionPolicy policy,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(policy);
        if (string.IsNullOrWhiteSpace(actorId))
        {
            throw new ArgumentException("AI actor identity is required.", nameof(actorId));
        }

        if (!string.Equals(decision.ProjectId, policy.ProjectId, StringComparison.Ordinal))
        {
            throw new AiPolicyViolationException("AI policy does not belong to the reconciliation project.");
        }

        var authorizedAction = AiActionAuthorizer.RequiredAction(decision);
        AiActionAuthorizer.EnsureAllowed(policy, authorizedAction);
        foreach (var mutation in decision.Mutations)
        {
            await ValidateCurrentAsync(mutation, cancellationToken);
            session.Store(mutation.After);
            session.Store(new SourceAwareTaskVersion(
                $"{mutation.After.Id}:{mutation.After.Version}",
                mutation.After.ProjectId,
                mutation.After.Id,
                mutation.After.Version,
                mutation.After,
                decision.Action,
                mutation.Reason,
                actorId,
                timeProvider.GetUtcNow()));
        }

        session.Store(new AiActionAudit(
            decision.Id,
            decision.ProjectId,
            decision.RepositoryId,
            actorId,
            AiActionAuthorizer.RequiredPermission(authorizedAction),
            decision.ProposalId,
            decision.Mutations.Select(mutation => mutation.After.Id)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            decision.EvidenceIds,
            decision.Confidence,
            decision.Reason,
            timeProvider.GetUtcNow()));
        await session.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateCurrentAsync(TaskMutation mutation, CancellationToken cancellationToken)
    {
        var current = await session.LoadAsync<SourceAwareEngineeringTask>(mutation.After.Id, cancellationToken);
        if (mutation.Before is null)
        {
            if (current is not null)
            {
                throw new InvalidOperationException(
                    $"Task '{mutation.After.Id}' already exists and must be reconciled before creation.");
            }

            return;
        }

        if (current is null || current.Version != mutation.Before.Version)
        {
            throw new InvalidOperationException(
                $"Task '{mutation.After.Id}' changed after reconciliation and must be re-evaluated.");
        }
    }

}

public sealed class MartenTaskReconciliationReader(IQuerySession session)
{
    public async Task<IReadOnlyList<SourceAwareEngineeringTask>> FindRelatedAsync(
        string projectId,
        string repositoryId,
        string correlationKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId) ||
            string.IsNullOrWhiteSpace(repositoryId) ||
            string.IsNullOrWhiteSpace(correlationKey))
        {
            throw new ArgumentException("Project, repository, and correlation identities are required.");
        }

        return await session.Query<SourceAwareEngineeringTask>()
            .Where(task => task.ProjectId == projectId &&
                task.RepositoryId == repositoryId &&
                task.CorrelationKey == correlationKey)
            .OrderBy(task => task.CreatedAt)
            .ThenBy(task => task.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SourceAwareTaskVersion>> GetHistoryAsync(
        string projectId,
        string taskId,
        CancellationToken cancellationToken = default) =>
        await session.Query<SourceAwareTaskVersion>()
            .Where(version => version.ProjectId == projectId && version.TaskId == taskId)
            .OrderBy(version => version.Version)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SourceAwareEngineeringTask>> FindBySourceChangesAsync(
        string projectId,
        string repositoryId,
        IReadOnlyCollection<string> sourceChangeIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(repositoryId))
        {
            throw new ArgumentException("Project and repository identities are required.");
        }

        ArgumentNullException.ThrowIfNull(sourceChangeIds);
        var changeIds = sourceChangeIds.ToHashSet(StringComparer.Ordinal);
        if (changeIds.Count == 0)
        {
            return [];
        }

        var candidates = await session.Query<SourceAwareEngineeringTask>()
            .Where(task => task.ProjectId == projectId && task.RepositoryId == repositoryId)
            .OrderBy(task => task.CreatedAt)
            .ThenBy(task => task.Id)
            .ToListAsync(cancellationToken);
        return candidates.Where(task => task.SourceChangeIds.Any(changeIds.Contains)).ToArray();
    }
}
