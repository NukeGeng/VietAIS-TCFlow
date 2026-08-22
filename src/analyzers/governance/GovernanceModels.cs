using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Governance;

public enum ConventionKind
{
    Architecture,
    ApiStyle,
    Persistence,
    Validation,
    RequestDtoNaming,
    ResponseDtoNaming,
    HandlerNaming,
    EndpointNaming,
    ModuleLayout,
    FrontendState,
    FrontendRouting
}

public enum ConventionProfileStatus
{
    Suggested,
    Detected,
    Confirmed
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
    OpenApi,
    Database,
    Tests,
    Documentation
}

public enum AuthorityImpactAction
{
    AlignBackendToFrontend,
    AlignFrontendToBackend,
    ResolveAgainstOpenApi,
    EscalateAuthorityConflict
}

public enum PlanTargetComponent
{
    Frontend,
    Backend,
    Shared
}

public sealed record ConventionObservation(
    string Id,
    ConventionKind Kind,
    string Value,
    EvidenceLevel EvidenceLevel,
    decimal Confidence,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> ExamplePaths);

public sealed record RepositoryConventionProfile(
    string RepositoryId,
    long Revision,
    ConventionProfileStatus Status,
    IReadOnlyList<ConventionObservation> Observations);

public sealed record KnowledgeAuthorityRule(
    AuthorityKnowledgeKind Knowledge,
    AuthoritySourceKind Source,
    EvidenceLevel EvidenceLevel,
    decimal Confidence,
    string Reason,
    IReadOnlyList<string> EvidenceIds);

public sealed record RepositoryAuthorityPolicy(
    string ProjectId,
    long Revision,
    bool IsConfigured,
    IReadOnlyList<KnowledgeAuthorityRule> Rules)
{
    public KnowledgeAuthorityRule GetRequiredRule(AuthorityKnowledgeKind knowledge) =>
        Rules.SingleOrDefault(rule => rule.Knowledge == knowledge)
        ?? throw new InvalidOperationException($"Authority for '{knowledge}' is not configured.");
}

public sealed record AuthorityImpactDecision(
    string Id,
    string ContractMismatchId,
    AuthorityKnowledgeKind Knowledge,
    AuthoritySourceKind AuthoritySource,
    AuthorityImpactAction Action,
    PlanTargetComponent TargetComponent,
    EvidenceLevel EvidenceLevel,
    decimal Confidence,
    string Explanation,
    IReadOnlyList<string> EvidenceIds);

public sealed record ConventionPlanStep(
    int Order,
    string ArtifactId,
    string ArtifactName,
    string Path,
    string Action,
    string Reason);

public sealed record ConventionAwarePlan(
    string Id,
    string ContractMismatchId,
    AuthorityImpactAction AuthorityAction,
    PlanTargetComponent TargetComponent,
    EvidenceLevel EvidenceLevel,
    decimal Confidence,
    IReadOnlyList<ConventionPlanStep> Steps,
    IReadOnlyList<string> ConventionObservationIds,
    IReadOnlyList<string> EvidenceIds);
