using Marten;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Reasoning;

namespace VietAIS.TCFlow.Analyzers.Monitoring;

public sealed record IncrementalDeepReasoningSettings(
    RepositoryAuthorityPolicy Authority,
    RepositoryConventionProfile Conventions,
    AiActionPolicy AiPolicy,
    TaskGenerationMode TaskGenerationMode,
    string ActorId,
    decimal AutomaticCreationThreshold = 0.75m);

public sealed record IncrementalDeepReasoningResult(
    string WorkItemId,
    int AiRequestCount,
    IReadOnlyList<TaskReconciliationDecision> Decisions,
    IReadOnlyList<string> ProcessedMismatchIds,
    IReadOnlyList<string> RevertedSourceChangeIds);

public interface IIncrementalTaskGateway
{
    Task<IReadOnlyList<SourceAwareEngineeringTask>> FindRelatedAsync(
        string projectId,
        string repositoryId,
        string correlationKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceAwareEngineeringTask>> FindBySourceChangesAsync(
        string projectId,
        string repositoryId,
        IReadOnlyCollection<string> sourceChangeIds,
        CancellationToken cancellationToken = default);

    Task ApplyAsync(
        TaskReconciliationDecision decision,
        AiActionPolicy policy,
        string actorId,
        CancellationToken cancellationToken = default);
}

public sealed class IncrementalDeepReasoningProcessor(
    IAiReasoningProvider provider,
    IIncrementalTaskGateway taskGateway,
    TaskReconciliationService? reconciliation = null,
    AuthorityImpactEvaluator? authorityEvaluator = null,
    KnowledgeRetriever? retriever = null,
    TimeProvider? timeProvider = null)
{
    private readonly TaskReconciliationService _reconciliation = reconciliation ?? new();
    private readonly AuthorityImpactEvaluator _authorityEvaluator = authorityEvaluator ?? new();
    private readonly KnowledgeRetriever _retriever = retriever ?? new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<IncrementalDeepReasoningResult> ProcessAsync(
        DeepReasoningWorkItem workItem,
        RepositoryKnowledgeGraph graph,
        IncrementalDeepReasoningSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(settings);
        Validate(workItem, graph, settings);
        var decisions = new List<TaskReconciliationDecision>();
        await ReconcileRevertsAsync(workItem, settings, decisions, cancellationToken);

        var mismatchIds = workItem.ContractMismatchIds.ToHashSet(StringComparer.Ordinal);
        var mismatches = graph.ContractMismatches
            .Where(mismatch => mismatchIds.Contains(mismatch.Id))
            .OrderBy(mismatch => mismatch.Id, StringComparer.Ordinal)
            .ToArray();
        var reasoning = new SourceAwareReasoningService(provider, _retriever);
        var aiRequests = 0;
        foreach (var mismatch in mismatches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var authority = _authorityEvaluator.Evaluate(mismatch, settings.Authority);
            var impact = await reasoning.AnalyzeAsync(
                workItem.ProjectId,
                graph,
                workItem.SourceChangeIds,
                authority,
                settings.Conventions,
                settings.AiPolicy,
                maxDepth: 2,
                cancellationToken);
            aiRequests++;
            var proposals = TaskGenerationService.Prepare(
                impact,
                settings.TaskGenerationMode,
                settings.AiPolicy,
                settings.AutomaticCreationThreshold);
            foreach (var proposal in proposals)
            {
                var existing = await taskGateway.FindRelatedAsync(
                    proposal.ProjectId,
                    proposal.RepositoryId,
                    proposal.CorrelationKey,
                    cancellationToken);
                var decision = _reconciliation.Reconcile(proposal, existing, _timeProvider.GetUtcNow());
                EnsureAuthorized(settings.AiPolicy, decision);
                await taskGateway.ApplyAsync(decision, settings.AiPolicy, settings.ActorId, cancellationToken);
                decisions.Add(decision);
            }
        }

        return new IncrementalDeepReasoningResult(
            workItem.Id,
            aiRequests,
            decisions,
            mismatches.Select(mismatch => mismatch.Id).ToArray(),
            workItem.RevertedSourceChangeIds);
    }

    private async Task ReconcileRevertsAsync(
        DeepReasoningWorkItem workItem,
        IncrementalDeepReasoningSettings settings,
        ICollection<TaskReconciliationDecision> decisions,
        CancellationToken cancellationToken)
    {
        if (workItem.RevertedSourceChangeIds.Count == 0)
        {
            return;
        }

        var affectedTasks = await taskGateway.FindBySourceChangesAsync(
            workItem.ProjectId,
            workItem.RepositoryId,
            workItem.RevertedSourceChangeIds,
            cancellationToken);
        foreach (var group in affectedTasks.GroupBy(task => task.CorrelationKey, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var tasks = group.OrderBy(task => task.CreatedAt).ThenBy(task => task.Id, StringComparer.Ordinal).ToArray();
            var canonical = tasks[0];
            var evidenceIds = tasks.SelectMany(task => task.EvidenceIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var revertedIds = tasks.SelectMany(task => task.SourceChangeIds)
                .Where(workItem.RevertedSourceChangeIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var proposal = new StructuredTaskProposal(
                StableIdentity.Create("revert-task-proposal", workItem.Id, group.Key),
                canonical.ProjectId,
                canonical.RepositoryId,
                canonical.CorrelationKey,
                canonical.ContractMismatchId,
                canonical.Title,
                canonical.Description,
                canonical.TargetComponent,
                canonical.EvidenceLevel,
                canonical.Confidence,
                tasks.SelectMany(task => task.ArtifactIds).Distinct(StringComparer.Ordinal).ToArray(),
                evidenceIds,
                revertedIds,
                tasks.SelectMany(task => task.Requirements).Distinct(StringComparer.Ordinal).ToArray(),
                SourceChangeState.Reverted,
                TaskProposalDisposition.Suggested);
            var decision = _reconciliation.Reconcile(proposal, tasks, _timeProvider.GetUtcNow());
            EnsureAuthorized(settings.AiPolicy, decision);
            await taskGateway.ApplyAsync(decision, settings.AiPolicy, settings.ActorId, cancellationToken);
            decisions.Add(decision);
        }
    }

    private static void Validate(
        DeepReasoningWorkItem workItem,
        RepositoryKnowledgeGraph graph,
        IncrementalDeepReasoningSettings settings)
    {
        if (!string.Equals(workItem.RepositoryId, graph.RepositoryId, StringComparison.Ordinal) ||
            !string.Equals(workItem.ProjectId, settings.AiPolicy.ProjectId, StringComparison.Ordinal) ||
            !string.Equals(workItem.ProjectId, settings.Authority.ProjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Deep reasoning work, graph, authority, and AI policy must share scope.");
        }

        if (graph.Revision < workItem.GraphRevision)
        {
            throw new InvalidOperationException("The knowledge graph is older than the queued reasoning work.");
        }

        if (workItem.SourceChangeIds.Count == 0 ||
            workItem.SourceChangeIds.Any(id => graph.Changes.All(change => change.Id != id)))
        {
            throw new InvalidOperationException("Queued reasoning references missing source changes.");
        }

        if (string.IsNullOrWhiteSpace(settings.ActorId))
        {
            throw new InvalidOperationException("AI actor identity is required for audited reconciliation.");
        }
    }

    private static void EnsureAuthorized(AiActionPolicy policy, TaskReconciliationDecision decision) =>
        AiActionAuthorizer.EnsureAllowed(policy, AiActionAuthorizer.RequiredAction(decision));
}

public sealed class MartenIncrementalTaskGateway(
    IDocumentSession session,
    TimeProvider timeProvider) : IIncrementalTaskGateway
{
    public Task<IReadOnlyList<SourceAwareEngineeringTask>> FindRelatedAsync(
        string projectId,
        string repositoryId,
        string correlationKey,
        CancellationToken cancellationToken = default) =>
        new MartenTaskReconciliationReader(session).FindRelatedAsync(
            projectId,
            repositoryId,
            correlationKey,
            cancellationToken);

    public Task<IReadOnlyList<SourceAwareEngineeringTask>> FindBySourceChangesAsync(
        string projectId,
        string repositoryId,
        IReadOnlyCollection<string> sourceChangeIds,
        CancellationToken cancellationToken = default) =>
        new MartenTaskReconciliationReader(session).FindBySourceChangesAsync(
            projectId,
            repositoryId,
            sourceChangeIds,
            cancellationToken);

    public Task ApplyAsync(
        TaskReconciliationDecision decision,
        AiActionPolicy policy,
        string actorId,
        CancellationToken cancellationToken = default) =>
        new MartenTaskReconciliationWriter(session, timeProvider).ApplyAsync(
            decision,
            policy,
            actorId,
            cancellationToken);
}
