using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Knowledge;

namespace VietAIS.TCFlow.Analyzers.Governance;

public static class AuthorityPolicyDefaults
{
    public static RepositoryAuthorityPolicy Suggest(string projectId, RepositoryKnowledgeGraph graph)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("Project identity is required.", nameof(projectId));
        }

        ArgumentNullException.ThrowIfNull(graph);
        var hasBackendContracts = graph.Contracts.Any(contract =>
            contract.Direction == ContractDirection.BackendActual);
        var hasFrontendContracts = graph.Contracts.Any(contract =>
            contract.Direction == ContractDirection.FrontendExpected);
        var apiSource = hasBackendContracts
            ? AuthoritySourceKind.Backend
            : hasFrontendContracts
                ? AuthoritySourceKind.Frontend
                : AuthoritySourceKind.Documentation;
        return new RepositoryAuthorityPolicy(
            projectId,
            1,
            IsConfigured: false,
            [
                SuggestedRule(
                    AuthorityKnowledgeKind.ApiContract,
                    apiSource,
                    "Suggested from the contract producers detected during onboarding."),
                SuggestedRule(
                    AuthorityKnowledgeKind.UiRequirement,
                    AuthoritySourceKind.Frontend,
                    "Vue artifacts indicate that UI requirements originate in frontend source."),
                SuggestedRule(
                    AuthorityKnowledgeKind.BusinessLogic,
                    AuthoritySourceKind.Backend,
                    "Handler artifacts indicate that business logic originates in backend source."),
                SuggestedRule(
                    AuthorityKnowledgeKind.Persistence,
                    AuthoritySourceKind.Backend,
                    "Marten document artifacts indicate that persistence originates in backend source.")
            ]);
    }

    private static KnowledgeAuthorityRule SuggestedRule(
        AuthorityKnowledgeKind knowledge,
        AuthoritySourceKind source,
        string reason) => new(
            knowledge,
            source,
            EvidenceLevel.Proposed,
            0.5m,
            reason,
            []);
}

public sealed class AuthorityImpactEvaluator
{
    public AuthorityImpactDecision Evaluate(
        ContractMismatch mismatch,
        RepositoryAuthorityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(mismatch);
        ArgumentNullException.ThrowIfNull(policy);
        var rule = policy.GetRequiredRule(AuthorityKnowledgeKind.ApiContract);
        var (action, target, explanation) = rule.Source switch
        {
            AuthoritySourceKind.Frontend => (
                AuthorityImpactAction.AlignBackendToFrontend,
                PlanTargetComponent.Backend,
                $"Frontend is authoritative for API contracts; backend must align with mismatch '{mismatch.Subject}'."),
            AuthoritySourceKind.Backend => (
                AuthorityImpactAction.AlignFrontendToBackend,
                PlanTargetComponent.Frontend,
                $"Backend is authoritative for API contracts; frontend must align with mismatch '{mismatch.Subject}'."),
            AuthoritySourceKind.OpenApi => (
                AuthorityImpactAction.ResolveAgainstOpenApi,
                PlanTargetComponent.Shared,
                $"OpenAPI is authoritative; both source contracts must be checked for mismatch '{mismatch.Subject}'."),
            _ => (
                AuthorityImpactAction.EscalateAuthorityConflict,
                PlanTargetComponent.Shared,
                $"Configured authority '{rule.Source}' cannot deterministically resolve API mismatch '{mismatch.Subject}'.")
        };
        var evidenceLevel = LeastCertain(mismatch.EvidenceLevel, rule.EvidenceLevel);
        var confidence = Math.Min(mismatch.Confidence, rule.Confidence);
        var evidenceIds = mismatch.EvidenceIds.Concat(rule.EvidenceIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new AuthorityImpactDecision(
            StableIdentity.Create(
                "authority-impact",
                mismatch.Id,
                rule.Knowledge.ToString(),
                rule.Source.ToString(),
                action.ToString()),
            mismatch.Id,
            rule.Knowledge,
            rule.Source,
            action,
            target,
            evidenceLevel,
            confidence,
            explanation,
            evidenceIds);
    }

    private static EvidenceLevel LeastCertain(EvidenceLevel first, EvidenceLevel second) =>
        (EvidenceLevel)Math.Max((int)first, (int)second);
}
