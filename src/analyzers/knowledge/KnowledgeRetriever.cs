using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Knowledge;

public sealed class KnowledgeRetriever(KnowledgeGraphTraversal? traversal = null)
{
    private readonly KnowledgeGraphTraversal _traversal = traversal ?? new KnowledgeGraphTraversal();

    public RetrievalContext RetrieveForArtifacts(
        RepositoryKnowledgeGraph graph,
        IReadOnlyCollection<string> seedArtifactIds,
        int maxDepth = 3)
    {
        var neighborhood = _traversal.FindNeighborhood(graph, seedArtifactIds, maxDepth);
        return BuildContext(graph, neighborhood, new HashSet<string>(StringComparer.Ordinal));
    }

    public RetrievalContext RetrieveForChanges(
        RepositoryKnowledgeGraph graph,
        IReadOnlyCollection<string> sourceChangeIds,
        int maxDepth = 3)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(sourceChangeIds);
        var selectedChangeIds = sourceChangeIds.ToHashSet(StringComparer.Ordinal);
        var changes = graph.Changes.Where(change => selectedChangeIds.Contains(change.Id)).ToArray();
        var paths = changes.Select(change => change.Path).ToHashSet(StringComparer.Ordinal);
        var seedIds = graph.Artifacts.Where(artifact => paths.Contains(artifact.Path))
            .Select(artifact => artifact.Id)
            .Concat(graph.Impacts.Where(impact => selectedChangeIds.Contains(impact.SourceChangeId))
                .Select(impact => impact.AffectedArtifactId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var neighborhood = _traversal.FindNeighborhood(graph, seedIds, maxDepth);
        return BuildContext(graph, neighborhood, selectedChangeIds);
    }

    private static RetrievalContext BuildContext(
        RepositoryKnowledgeGraph graph,
        KnowledgeNeighborhood neighborhood,
        IReadOnlySet<string> requestedChangeIds)
    {
        var artifactIds = neighborhood.ArtifactIds.ToHashSet(StringComparer.Ordinal);
        var dependencyIds = neighborhood.DependencyIds.ToHashSet(StringComparer.Ordinal);
        var artifacts = graph.Artifacts.Where(artifact => artifactIds.Contains(artifact.Id)).ToArray();
        var dependencies = graph.Dependencies.Where(dependency => dependencyIds.Contains(dependency.Id)).ToArray();
        var artifactPaths = artifacts.Select(artifact => artifact.Path).ToHashSet(StringComparer.Ordinal);
        var evidenceIds = artifacts.SelectMany(artifact => artifact.EvidenceIds)
            .Concat(dependencies.Select(dependency => dependency.EvidenceId))
            .ToHashSet(StringComparer.Ordinal);

        var capabilities = graph.Capabilities.Where(capability =>
                capability.ArtifactIds.Any(artifactIds.Contains) || capability.EvidenceIds.Any(evidenceIds.Contains))
            .ToArray();
        evidenceIds.UnionWith(capabilities.SelectMany(capability => capability.EvidenceIds));
        var contracts = graph.Contracts.Where(contract =>
                contract.EvidenceIds.Any(evidenceIds.Contains) || ContractTouchesPaths(contract, artifactPaths))
            .ToArray();
        evidenceIds.UnionWith(contracts.SelectMany(contract => contract.EvidenceIds));
        var contractIds = contracts.Select(contract => contract.Id).ToHashSet(StringComparer.Ordinal);
        var pairs = graph.ContractPairs.Where(pair =>
                contractIds.Contains(pair.FrontendContractId) ||
                pair.BackendContractId is not null && contractIds.Contains(pair.BackendContractId))
            .ToArray();
        evidenceIds.UnionWith(pairs.SelectMany(pair => pair.EvidenceIds));
        var pairIds = pairs.Select(pair => pair.Id).ToHashSet(StringComparer.Ordinal);
        var mismatches = graph.ContractMismatches.Where(mismatch => pairIds.Contains(mismatch.ContractPairId)).ToArray();
        evidenceIds.UnionWith(mismatches.SelectMany(mismatch => mismatch.EvidenceIds));
        var changes = graph.Changes.Where(change =>
                requestedChangeIds.Contains(change.Id) || artifactPaths.Contains(change.Path))
            .ToArray();
        var changeIds = changes.Select(change => change.Id).ToHashSet(StringComparer.Ordinal);
        var impacts = graph.Impacts.Where(impact =>
                artifactIds.Contains(impact.AffectedArtifactId) || changeIds.Contains(impact.SourceChangeId))
            .ToArray();
        evidenceIds.UnionWith(impacts.SelectMany(impact => impact.EvidenceIds));
        var evidence = graph.Evidence.Where(item => evidenceIds.Contains(item.Id)).ToArray();
        var provenance = evidence.Select(item => new RetrievalProvenance(
                item.Id,
                SupportingRecords(
                    item.Id,
                    artifacts,
                    dependencies,
                    capabilities,
                    contracts,
                    impacts,
                    pairs,
                    mismatches)))
            .OrderBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();

        return new RetrievalContext(
            graph.RepositoryId,
            neighborhood.SeedArtifactIds,
            artifacts,
            dependencies,
            evidence,
            capabilities,
            contracts,
            changes,
            impacts,
            pairs,
            mismatches,
            provenance);
    }

    private static bool ContractTouchesPaths(Contract contract, IReadOnlySet<string> paths) =>
        contract.RequestFields.Any(field => paths.Contains(field.Location.Path)) ||
        contract.ResponseFields.Any(field => paths.Contains(field.Location.Path));

    private static IReadOnlyList<string> SupportingRecords(
        string evidenceId,
        IEnumerable<Artifact> artifacts,
        IEnumerable<Dependency> dependencies,
        IEnumerable<Capability> capabilities,
        IEnumerable<Contract> contracts,
        IEnumerable<Impact> impacts,
        IEnumerable<ContractPair> pairs,
        IEnumerable<ContractMismatch> mismatches) => artifacts
        .Where(item => item.EvidenceIds.Contains(evidenceId, StringComparer.Ordinal))
        .Select(item => item.Id)
        .Concat(dependencies.Where(item => item.EvidenceId == evidenceId).Select(item => item.Id))
        .Concat(capabilities.Where(item => item.EvidenceIds.Contains(evidenceId, StringComparer.Ordinal))
            .Select(item => item.Id))
        .Concat(contracts.Where(item => item.EvidenceIds.Contains(evidenceId, StringComparer.Ordinal))
            .Select(item => item.Id))
        .Concat(impacts.Where(item => item.EvidenceIds.Contains(evidenceId, StringComparer.Ordinal))
            .Select(item => item.Id))
        .Concat(pairs.Where(item => item.EvidenceIds.Contains(evidenceId, StringComparer.Ordinal))
            .Select(item => item.Id))
        .Concat(mismatches.Where(item => item.EvidenceIds.Contains(evidenceId, StringComparer.Ordinal))
            .Select(item => item.Id))
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();
}
