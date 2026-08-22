using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Knowledge;

public sealed record RepositoryKnowledgeManifest(
    string Id,
    long Revision,
    DateTimeOffset UpdatedAt);

public sealed record KnowledgeArtifactDocument(
    string Id,
    string RepositoryId,
    string Producer,
    Artifact Value);

public sealed record KnowledgeDependencyDocument(
    string Id,
    string RepositoryId,
    string Producer,
    Dependency Value);

public sealed record KnowledgeEvidenceDocument(
    string Id,
    string RepositoryId,
    string Producer,
    Evidence Value);

public sealed record KnowledgeCapabilityDocument(
    string Id,
    string RepositoryId,
    string Producer,
    Capability Value);

public sealed record KnowledgeContractDocument(
    string Id,
    string RepositoryId,
    string Producer,
    Contract Value);

public sealed record KnowledgeChangeDocument(
    string Id,
    string RepositoryId,
    string Producer,
    SourceChange Value);

public sealed record KnowledgeImpactDocument(
    string Id,
    string RepositoryId,
    string Producer,
    Impact Value);

public sealed record KnowledgeContractPairDocument(
    string Id,
    string RepositoryId,
    string Producer,
    ContractPair Value);

public sealed record KnowledgeContractMismatchDocument(
    string Id,
    string RepositoryId,
    string Producer,
    ContractMismatch Value);
