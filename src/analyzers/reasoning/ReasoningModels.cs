using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;

namespace VietAIS.TCFlow.Analyzers.Reasoning;

public enum AiTrustLevel
{
    SuggestOnly,
    CreateTasks,
    UpdateTasks,
    CodeGeneration,
    PullRequestCreation
}

public enum AiTaskAction
{
    Analyze,
    Suggest,
    Create,
    Update,
    Merge,
    Close,
    Reopen,
    Ignore
}

public enum TaskGenerationMode
{
    Suggest,
    Create
}

public enum TaskProposalDisposition
{
    Suggested,
    Create
}

public enum SourceChangeState
{
    Active,
    Reverted
}

public enum SourceAwareTaskStatus
{
    Suggested,
    Upcoming,
    InProgress,
    ReadyForReview,
    Completed,
    Blocked,
    Rejected,
    Cancelled
}

public enum TaskReconciliationAction
{
    Create,
    Update,
    Merge,
    Close,
    Reopen,
    Ignore
}

public sealed record AiActionPolicy(
    string ProjectId,
    AiTrustLevel TrustLevel,
    IReadOnlyList<string> AllowedPermissions);

public sealed record TargetedConventionSignal(
    ConventionKind Kind,
    string Value,
    EvidenceLevel EvidenceLevel,
    decimal Confidence);

public sealed record AiReasoningContext(
    string ProjectId,
    string RepositoryId,
    IReadOnlyList<string> SourceChangeIds,
    RetrievalContext GraphContext,
    AuthorityImpactDecision Authority,
    IReadOnlyList<TargetedConventionSignal> Conventions);

public sealed record AiImpactReasoningResult(
    string Summary,
    ImpactSeverity Severity,
    EvidenceLevel EvidenceLevel,
    decimal Confidence,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<AiTaskReasoningResult> Tasks);

public sealed record AiTaskReasoningResult(
    string Title,
    string? Description,
    PlanTargetComponent TargetComponent,
    EvidenceLevel EvidenceLevel,
    decimal Confidence,
    IReadOnlyList<string> ArtifactIds,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> Requirements);

public sealed record StructuredImpactPlan(
    string Id,
    string ProjectId,
    string RepositoryId,
    string ContractMismatchId,
    string Summary,
    ImpactSeverity Severity,
    EvidenceLevel EvidenceLevel,
    decimal Confidence,
    IReadOnlyList<string> SourceChangeIds,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<StructuredTaskProposal> Tasks);

public sealed record StructuredTaskProposal(
    string Id,
    string ProjectId,
    string RepositoryId,
    string CorrelationKey,
    string ContractMismatchId,
    string Title,
    string? Description,
    PlanTargetComponent TargetComponent,
    EvidenceLevel EvidenceLevel,
    decimal Confidence,
    IReadOnlyList<string> ArtifactIds,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> SourceChangeIds,
    IReadOnlyList<string> Requirements,
    SourceChangeState ChangeState,
    TaskProposalDisposition Disposition);

public sealed record SourceAwareEngineeringTask(
    string Id,
    string ProjectId,
    string RepositoryId,
    string CorrelationKey,
    string ContractMismatchId,
    string Title,
    string? Description,
    PlanTargetComponent TargetComponent,
    SourceAwareTaskStatus Status,
    EvidenceLevel EvidenceLevel,
    decimal Confidence,
    IReadOnlyList<string> ArtifactIds,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> SourceChangeIds,
    IReadOnlyList<string> Requirements,
    int Version,
    string? MergedIntoTaskId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TaskMutation(
    SourceAwareEngineeringTask? Before,
    SourceAwareEngineeringTask After,
    string Reason);

public sealed record TaskReconciliationDecision(
    string Id,
    string ProjectId,
    string RepositoryId,
    string ProposalId,
    TaskReconciliationAction Action,
    string Reason,
    bool RequiresHumanReview,
    IReadOnlyList<TaskMutation> Mutations,
    IReadOnlyList<string> EvidenceIds,
    decimal Confidence);

public sealed record SourceAwareTaskVersion(
    string Id,
    string ProjectId,
    string TaskId,
    int Version,
    SourceAwareEngineeringTask Snapshot,
    TaskReconciliationAction Action,
    string Reason,
    string ActorId,
    DateTimeOffset ChangedAt);

public sealed record AiActionAudit(
    string Id,
    string ProjectId,
    string RepositoryId,
    string ActorId,
    string Action,
    string ProposalId,
    IReadOnlyList<string> TaskIds,
    IReadOnlyList<string> EvidenceIds,
    decimal Confidence,
    string Reason,
    DateTimeOffset OccurredAt);

public interface IAiReasoningProvider
{
    Task<AiImpactReasoningResult> AnalyzeImpactAsync(
        AiReasoningContext context,
        CancellationToken cancellationToken = default);
}
