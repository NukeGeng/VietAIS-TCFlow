using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Reasoning;

public sealed class TaskReconciliationService
{
    public TaskReconciliationDecision Reconcile(
        StructuredTaskProposal proposal,
        IReadOnlyCollection<SourceAwareEngineeringTask> existingTasks,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(existingTasks);
        var related = existingTasks.Where(task =>
                string.Equals(task.ProjectId, proposal.ProjectId, StringComparison.Ordinal) &&
                string.Equals(task.RepositoryId, proposal.RepositoryId, StringComparison.Ordinal) &&
                string.Equals(task.CorrelationKey, proposal.CorrelationKey, StringComparison.Ordinal))
            .OrderBy(task => task.CreatedAt)
            .ThenBy(task => task.Id, StringComparer.Ordinal)
            .ToArray();
        return proposal.ChangeState == SourceChangeState.Reverted
            ? ReconcileRevert(proposal, related, now)
            : ReconcileActive(proposal, related, now);
    }

    private static TaskReconciliationDecision ReconcileActive(
        StructuredTaskProposal proposal,
        IReadOnlyList<SourceAwareEngineeringTask> related,
        DateTimeOffset now)
    {
        if (related.Count == 0)
        {
            var created = CreateTask(proposal, now);
            return Decision(
                proposal,
                TaskReconciliationAction.Create,
                "No related task exists for the source-backed correlation key; create one task.",
                requiresHumanReview: false,
                [new TaskMutation(null, created, "source-backed task created")]);
        }

        var open = related.Where(task => task.Status != SourceAwareTaskStatus.Cancelled).ToArray();
        if (open.Length == 0)
        {
            var previous = related[0];
            var reopened = ApplyProposal(
                previous,
                proposal,
                proposal.Disposition == TaskProposalDisposition.Create
                    ? SourceAwareTaskStatus.Upcoming
                    : SourceAwareTaskStatus.Suggested,
                now);
            return Decision(
                proposal,
                TaskReconciliationAction.Reopen,
                "The source requirement reappeared after the related task was cancelled; reopen it.",
                requiresHumanReview: false,
                [new TaskMutation(previous, reopened, "source requirement reappeared")]);
        }

        if (open.Length == 1)
        {
            var current = open[0];
            if (SameContent(current, proposal))
            {
                return Decision(
                    proposal,
                    TaskReconciliationAction.Ignore,
                    "The related task already contains the same requirements and evidence; do not duplicate it.",
                    requiresHumanReview: false,
                    []);
            }

            var reopen = current.Status == SourceAwareTaskStatus.Completed;
            var updated = ApplyProposal(
                current,
                proposal,
                reopen ? SourceAwareTaskStatus.Upcoming : PromoteSuggestion(current.Status, proposal.Disposition),
                now);
            return Decision(
                proposal,
                reopen ? TaskReconciliationAction.Reopen : TaskReconciliationAction.Update,
                reopen
                    ? "New source evidence changes a completed task; reopen it for explicit re-evaluation."
                    : "A related task exists; update it with the new requirements and evidence instead of creating another.",
                requiresHumanReview: reopen,
                [new TaskMutation(current, updated, reopen ? "completed task requires re-evaluation" : "source evidence updated")]);
        }

        var canonical = open[0];
        var merged = ApplyProposal(
            canonical with
            {
                ArtifactIds = Union(open.SelectMany(task => task.ArtifactIds)),
                EvidenceIds = Union(open.SelectMany(task => task.EvidenceIds)),
                SourceChangeIds = Union(open.SelectMany(task => task.SourceChangeIds)),
                Requirements = Union(open.SelectMany(task => task.Requirements))
            },
            proposal,
            PromoteSuggestion(canonical.Status, proposal.Disposition),
            now);
        var mutations = new List<TaskMutation>
        {
            new(canonical, merged, "related tasks merged into canonical task")
        };
        mutations.AddRange(open.Skip(1).Select(task => new TaskMutation(
            task,
            task with
            {
                Status = SourceAwareTaskStatus.Cancelled,
                Version = task.Version + 1,
                MergedIntoTaskId = canonical.Id,
                UpdatedAt = now
            },
            $"merged into task {canonical.Id}")));
        return Decision(
            proposal,
            TaskReconciliationAction.Merge,
            $"{open.Length} related active tasks share the same source correlation; merge them into one canonical task.",
            requiresHumanReview: false,
            mutations);
    }

    private static TaskReconciliationDecision ReconcileRevert(
        StructuredTaskProposal proposal,
        IReadOnlyList<SourceAwareEngineeringTask> related,
        DateTimeOffset now)
    {
        if (related.Count == 0)
        {
            return Decision(
                proposal,
                TaskReconciliationAction.Ignore,
                "The reverted change has no related task; no task mutation is required.",
                requiresHumanReview: false,
                []);
        }

        var mutable = related.Where(task => task.Status is not
                SourceAwareTaskStatus.Cancelled and not
                SourceAwareTaskStatus.Completed)
            .ToArray();
        var completed = related.Where(task => task.Status == SourceAwareTaskStatus.Completed).ToArray();
        if (mutable.Length == 0)
        {
            return Decision(
                proposal,
                TaskReconciliationAction.Ignore,
                completed.Length > 0
                    ? "The reverted source has completed work; retain history and require human re-evaluation."
                    : "All related tasks are already cancelled; no further mutation is required.",
                requiresHumanReview: completed.Length > 0,
                []);
        }

        var mutations = mutable.Select(task => new TaskMutation(
                task,
                task with
                {
                    Status = SourceAwareTaskStatus.Cancelled,
                    Version = task.Version + 1,
                    UpdatedAt = now
                },
                "source change reverted; task is obsolete"))
            .ToArray();
        return Decision(
            proposal,
            TaskReconciliationAction.Close,
            "The originating source change was reverted; cancel related unfinished tasks while preserving their history.",
            requiresHumanReview: completed.Length > 0,
            mutations);
    }

    private static SourceAwareEngineeringTask CreateTask(
        StructuredTaskProposal proposal,
        DateTimeOffset now) => new(
        StableIdentity.Create("source-aware-task", proposal.ProjectId, proposal.RepositoryId, proposal.CorrelationKey),
        proposal.ProjectId,
        proposal.RepositoryId,
        proposal.CorrelationKey,
        proposal.ContractMismatchId,
        proposal.Title,
        proposal.Description,
        proposal.TargetComponent,
        proposal.Disposition == TaskProposalDisposition.Create
            ? SourceAwareTaskStatus.Upcoming
            : SourceAwareTaskStatus.Suggested,
        proposal.EvidenceLevel,
        proposal.Confidence,
        proposal.ArtifactIds,
        proposal.EvidenceIds,
        proposal.SourceChangeIds,
        proposal.Requirements,
        Version: 1,
        MergedIntoTaskId: null,
        now,
        now);

    private static SourceAwareEngineeringTask ApplyProposal(
        SourceAwareEngineeringTask task,
        StructuredTaskProposal proposal,
        SourceAwareTaskStatus status,
        DateTimeOffset now) => task with
        {
            ContractMismatchId = proposal.ContractMismatchId,
            Title = proposal.Title,
            Description = proposal.Description,
            TargetComponent = proposal.TargetComponent,
            Status = status,
            EvidenceLevel = proposal.EvidenceLevel,
            Confidence = proposal.Confidence,
            ArtifactIds = Union(task.ArtifactIds.Concat(proposal.ArtifactIds)),
            EvidenceIds = Union(task.EvidenceIds.Concat(proposal.EvidenceIds)),
            SourceChangeIds = Union(task.SourceChangeIds.Concat(proposal.SourceChangeIds)),
            Requirements = Union(task.Requirements.Concat(proposal.Requirements)),
            Version = task.Version + 1,
            MergedIntoTaskId = null,
            UpdatedAt = now
        };

    private static SourceAwareTaskStatus PromoteSuggestion(
        SourceAwareTaskStatus current,
        TaskProposalDisposition disposition) =>
        current == SourceAwareTaskStatus.Suggested && disposition == TaskProposalDisposition.Create
            ? SourceAwareTaskStatus.Upcoming
            : current;

    private static bool SameContent(SourceAwareEngineeringTask task, StructuredTaskProposal proposal) =>
        string.Equals(task.Title, proposal.Title, StringComparison.Ordinal) &&
        string.Equals(task.Description, proposal.Description, StringComparison.Ordinal) &&
        task.TargetComponent == proposal.TargetComponent &&
        task.EvidenceLevel == proposal.EvidenceLevel &&
        task.Confidence == proposal.Confidence &&
        SameSet(task.ArtifactIds, proposal.ArtifactIds) &&
        SameSet(task.EvidenceIds, proposal.EvidenceIds) &&
        SameSet(task.SourceChangeIds, proposal.SourceChangeIds) &&
        SameSet(task.Requirements, proposal.Requirements) &&
        !(task.Status == SourceAwareTaskStatus.Suggested && proposal.Disposition == TaskProposalDisposition.Create);

    private static bool SameSet(IEnumerable<string> first, IEnumerable<string> second) =>
        Union(first).SequenceEqual(Union(second), StringComparer.Ordinal);

    private static string[] Union(IEnumerable<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static TaskReconciliationDecision Decision(
        StructuredTaskProposal proposal,
        TaskReconciliationAction action,
        string reason,
        bool requiresHumanReview,
        IReadOnlyList<TaskMutation> mutations) => new(
        StableIdentity.Create(
            "task-reconciliation",
            proposal.Id,
            action.ToString(),
            string.Join(',', mutations.Select(mutation => mutation.After.Id))),
        proposal.ProjectId,
        proposal.RepositoryId,
        proposal.Id,
        action,
        reason,
        requiresHumanReview,
        mutations,
        proposal.EvidenceIds,
        proposal.Confidence);
}
