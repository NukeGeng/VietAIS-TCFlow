using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Knowledge;

public sealed record RepositoryKnowledgeGraph(
    string RepositoryId,
    long Revision,
    IReadOnlyList<Artifact> Artifacts,
    IReadOnlyList<Dependency> Dependencies,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<Capability> Capabilities,
    IReadOnlyList<Contract> Contracts,
    IReadOnlyList<SourceChange> Changes,
    IReadOnlyList<Impact> Impacts,
    IReadOnlyList<ContractPair> ContractPairs,
    IReadOnlyList<ContractMismatch> ContractMismatches,
    IReadOnlyDictionary<string, string> RecordProducers);

public sealed record KnowledgeNeighborhood(
    IReadOnlyList<string> SeedArtifactIds,
    IReadOnlyList<string> ArtifactIds,
    IReadOnlyList<string> DependencyIds,
    IReadOnlyDictionary<string, int> DepthByArtifactId);

public sealed record RetrievalProvenance(
    string EvidenceId,
    IReadOnlyList<string> SupportingRecordIds);

public sealed record RetrievalContext(
    string RepositoryId,
    IReadOnlyList<string> SeedArtifactIds,
    IReadOnlyList<Artifact> Artifacts,
    IReadOnlyList<Dependency> Dependencies,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<Capability> Capabilities,
    IReadOnlyList<Contract> Contracts,
    IReadOnlyList<SourceChange> Changes,
    IReadOnlyList<Impact> Impacts,
    IReadOnlyList<ContractPair> ContractPairs,
    IReadOnlyList<ContractMismatch> ContractMismatches,
    IReadOnlyList<RetrievalProvenance> Provenance);
