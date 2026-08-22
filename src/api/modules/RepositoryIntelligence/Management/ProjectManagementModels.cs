using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public enum ProjectLifecycleStatus
{
    Active,
    Suspended,
    Archived
}

public enum AuthorityKnowledgeKind
{
    ApiContract,
    UiRequirement,
    BusinessLogic,
    Persistence
}

public enum AuthoritySourceKind
{
    Backend,
    Frontend,
    Database,
    OpenApi,
    Tests,
    Documentation
}

public enum ConventionProfileStatus
{
    PendingAnalysis,
    Confirmed
}

public enum RepositoryProviderKind
{
    Local,
    GitHub
}

public enum RepositoryLifecycleStatus
{
    Pending,
    Active,
    Disabled
}

public enum TaskLifecycleStatus
{
    Upcoming,
    InProgress,
    ReadyForReview,
    Completed,
    Blocked,
    Rejected,
    Cancelled
}

public enum TaskPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum TaskActorType
{
    User,
    Ai,
    System
}

public enum AiVerificationStatus
{
    NotRun,
    Passed,
    Failed,
    Inconclusive
}

public enum HumanApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    ChangesRequested
}

public enum TaskReviewDecision
{
    Approve,
    Reject,
    RequestChanges
}

public enum TaskEvidenceKind
{
    SourceChange,
    Artifact,
    Contract,
    Dependency,
    Impact,
    Verification
}

public sealed record ProjectState(
    Guid Id,
    Guid ProjectId,
    ProjectLifecycleStatus Status,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record AuthorityRule(
    AuthorityKnowledgeKind Knowledge,
    AuthoritySourceKind Source);

public sealed record AuthorityPolicy(
    Guid Id,
    Guid ProjectId,
    AuthorityRule[] Rules,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record ConventionProfile(
    Guid Id,
    Guid ProjectId,
    ConventionProfileStatus Status,
    string[] Architectures,
    string[] ApiStyles,
    string[] PersistencePatterns,
    string[] ValidationPatterns,
    string[] DtoPatterns,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record ProjectRepository(
    Guid Id,
    Guid ProjectId,
    string Name,
    RepositoryProviderKind Provider,
    string? LocalPath,
    string? RemoteUrl,
    string DefaultBranch,
    RepositoryLifecycleStatus Status,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);

public sealed record ProjectComponent(
    Guid Id,
    Guid ProjectId,
    Guid RepositoryId,
    string Name,
    ComponentScopeKind Scope,
    string? RootPath,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);

public sealed record ProjectFeature(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);

public sealed record SourceChange(
    Guid Id,
    Guid ProjectId,
    Guid RepositoryId,
    string Revision,
    string Summary,
    DateTimeOffset ObservedAt);

public sealed record SourceArtifact(
    Guid Id,
    Guid ProjectId,
    Guid RepositoryId,
    Guid? ComponentId,
    string Type,
    string Name,
    string Path);

public sealed record SourceImpact(
    Guid Id,
    Guid ProjectId,
    Guid SourceChangeId,
    Guid AffectedArtifactId,
    string Severity,
    string Reason,
    decimal Confidence);

public sealed record TaskSourceTrace(
    Guid? SourceChangeId,
    Guid[] ArtifactIds,
    Guid[] EvidenceIds,
    Guid[] ImpactIds);

public sealed record EngineeringTask(
    Guid Id,
    Guid ProjectId,
    Guid? RepositoryId,
    Guid? ComponentId,
    ComponentScopeKind? ComponentScope,
    Guid? FeatureId,
    string Title,
    string? Description,
    TaskLifecycleStatus Status,
    TaskPriority Priority,
    TaskSourceTrace SourceTrace,
    string[] AffectedArtifacts,
    string[] Inputs,
    string[] Outputs,
    string[] BusinessRules,
    Guid[] Dependencies,
    Guid CreatedBy,
    TaskActorType CreatedByType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int CurrentVersion,
    AiVerificationStatus AiVerification,
    HumanApprovalStatus HumanApproval);

public sealed record TaskAssignment(
    Guid Id,
    Guid ProjectId,
    Guid TaskId,
    Guid AssigneeId,
    Guid AssignedBy,
    DateTimeOffset AssignedAt);

public sealed record TaskReview(
    Guid Id,
    Guid ProjectId,
    Guid TaskId,
    Guid ReviewerId,
    TaskReviewDecision Decision,
    string? Comment,
    DateTimeOffset CreatedAt);

public sealed record TaskEvidence(
    Guid Id,
    Guid ProjectId,
    Guid TaskId,
    TaskEvidenceKind Kind,
    string Summary,
    string? Location,
    Guid? SourceChangeId,
    Guid? ArtifactId,
    Guid? ImpactId,
    decimal? Confidence,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    TaskActorType CreatedByType);

public sealed record EngineeringTaskSnapshot(
    string Title,
    string? Description,
    TaskLifecycleStatus Status,
    TaskPriority Priority,
    TaskSourceTrace SourceTrace,
    string[] AffectedArtifacts,
    string[] Inputs,
    string[] Outputs,
    string[] BusinessRules,
    Guid[] Dependencies,
    int Version,
    AiVerificationStatus AiVerification,
    HumanApprovalStatus HumanApproval);

public sealed record TaskVersion(
    Guid Id,
    Guid ProjectId,
    Guid TaskId,
    int Version,
    EngineeringTaskSnapshot Snapshot,
    TaskAssignment? Assignment,
    TaskReview? Review,
    TaskEvidence? Evidence,
    Guid ChangedBy,
    TaskActorType ChangedByType,
    string ChangeReason,
    DateTimeOffset ChangedAt);
