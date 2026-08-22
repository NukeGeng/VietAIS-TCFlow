using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Knowledge;

namespace VietAIS.TCFlow.Analyzers.Governance;

public sealed class ConventionAwarePlanBuilder
{
    public ConventionAwarePlan Build(
        AuthorityImpactDecision decision,
        ContractMismatch mismatch,
        RepositoryKnowledgeGraph graph,
        RepositoryConventionProfile conventions)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(mismatch);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(conventions);
        if (decision.ContractMismatchId != mismatch.Id)
        {
            throw new ArgumentException("Authority decision does not belong to the supplied mismatch.",
                nameof(decision));
        }

        var pair = graph.ContractPairs.Single(pair => pair.Id == mismatch.ContractPairId);
        var seed = decision.TargetComponent switch
        {
            PlanTargetComponent.Backend when pair.BackendContractId is not null =>
                FindContractArtifact(graph, pair.BackendContractId, ArtifactKind.AspNetEndpoint),
            PlanTargetComponent.Frontend =>
                FindContractArtifact(graph, pair.FrontendContractId, ArtifactKind.ApiCall),
            _ => null
        };
        var candidates = seed is null
            ? Array.Empty<Artifact>()
            : new KnowledgeGraphTraversal().FindNeighborhood(graph, [seed.Id], 2).ArtifactIds
                .Select(id => graph.Artifacts.Single(artifact => artifact.Id == id))
                .Where(artifact => AppliesToTarget(artifact, decision.TargetComponent))
                .Where(IsPlanningArtifact)
                .OrderBy(PlanningOrder)
                .ThenBy(artifact => artifact.Path, StringComparer.Ordinal)
                .ThenBy(artifact => artifact.Name, StringComparer.Ordinal)
                .ToArray();
        var observationIds = RelevantConventions(conventions, decision.TargetComponent)
            .Select(observation => observation.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var conventionSummary = RelevantConventions(conventions, decision.TargetComponent)
            .Select(observation => $"{observation.Kind}={observation.Value}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var steps = candidates.Select((artifact, index) => new ConventionPlanStep(
                index + 1,
                artifact.Id,
                artifact.Name,
                artifact.Path,
                ActionFor(artifact, mismatch.Subject),
                conventionSummary.Length == 0
                    ? decision.Explanation
                    : $"{decision.Explanation} Follow {string.Join(", ", conventionSummary)}."))
            .ToArray();
        return new ConventionAwarePlan(
            StableIdentity.Create("convention-plan", decision.Id, mismatch.Id, string.Join(',', steps.Select(step => step.ArtifactId))),
            mismatch.Id,
            decision.Action,
            decision.TargetComponent,
            decision.EvidenceLevel,
            decision.Confidence,
            steps,
            observationIds,
            decision.EvidenceIds);
    }

    private static Artifact? FindContractArtifact(
        RepositoryKnowledgeGraph graph,
        string contractId,
        ArtifactKind artifactKind)
    {
        var contract = graph.Contracts.Single(item => item.Id == contractId);
        var evidenceIds = contract.EvidenceIds.ToHashSet(StringComparer.Ordinal);
        var paths = graph.Evidence.Where(evidence => evidenceIds.Contains(evidence.Id))
            .Select(evidence => evidence.Location.Path)
            .ToHashSet(StringComparer.Ordinal);
        return graph.Artifacts.SingleOrDefault(artifact =>
            artifact.Kind == artifactKind &&
            paths.Contains(artifact.Path) &&
            artifact.Metadata.TryGetValue("method", out var method) &&
            artifact.Metadata.TryGetValue("route", out var route) &&
            string.Equals(method, contract.HttpMethod, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(route, contract.Route, StringComparison.Ordinal));
    }

    private static bool AppliesToTarget(Artifact artifact, PlanTargetComponent target) => target switch
    {
        PlanTargetComponent.Frontend => artifact.Technology == "vue",
        PlanTargetComponent.Backend => artifact.Technology is "aspnet" or "marten",
        _ => true
    };

    private static bool IsPlanningArtifact(Artifact artifact) => artifact.Kind is
        ArtifactKind.ApiCall or
        ArtifactKind.VueComponent or
        ArtifactKind.TypeScriptInterface or
        ArtifactKind.AspNetEndpoint or
        ArtifactKind.RequestDto or
        ArtifactKind.ResponseDto or
        ArtifactKind.Validator or
        ArtifactKind.Handler or
        ArtifactKind.MartenDocument;

    private static int PlanningOrder(Artifact artifact) => artifact.Kind switch
    {
        ArtifactKind.ApiCall or ArtifactKind.AspNetEndpoint => 0,
        ArtifactKind.TypeScriptInterface or ArtifactKind.RequestDto or ArtifactKind.ResponseDto => 1,
        ArtifactKind.Validator => 2,
        ArtifactKind.Handler => 3,
        ArtifactKind.MartenDocument => 4,
        _ => 5
    };

    private static string ActionFor(Artifact artifact, string subject) => artifact.Kind switch
    {
        ArtifactKind.RequestDto => $"Align request DTO {artifact.Name} for '{subject}'.",
        ArtifactKind.ResponseDto => $"Align response DTO {artifact.Name} for '{subject}'.",
        ArtifactKind.Validator => $"Align validation in {artifact.Name} for '{subject}'.",
        ArtifactKind.Handler => $"Align handler {artifact.Name} for '{subject}'.",
        ArtifactKind.MartenDocument => $"Align Marten document {artifact.Name} for '{subject}'.",
        ArtifactKind.AspNetEndpoint => $"Align endpoint {artifact.Name} for '{subject}'.",
        ArtifactKind.ApiCall => $"Align frontend API call {artifact.Name} for '{subject}'.",
        ArtifactKind.TypeScriptInterface => $"Align TypeScript contract {artifact.Name} for '{subject}'.",
        _ => $"Align {artifact.Name} for '{subject}'."
    };

    private static IEnumerable<ConventionObservation> RelevantConventions(
        RepositoryConventionProfile profile,
        PlanTargetComponent target) => profile.Observations.Where(observation => target switch
        {
            PlanTargetComponent.Backend => observation.Kind is
                ConventionKind.Architecture or
                ConventionKind.ApiStyle or
                ConventionKind.Persistence or
                ConventionKind.Validation or
                ConventionKind.RequestDtoNaming or
                ConventionKind.ResponseDtoNaming or
                ConventionKind.HandlerNaming or
                ConventionKind.EndpointNaming or
                ConventionKind.ModuleLayout,
            PlanTargetComponent.Frontend => observation.Kind is
                ConventionKind.Architecture or
                ConventionKind.FrontendState or
                ConventionKind.FrontendRouting or
                ConventionKind.RequestDtoNaming or
                ConventionKind.ResponseDtoNaming,
            _ => true
        });
}
