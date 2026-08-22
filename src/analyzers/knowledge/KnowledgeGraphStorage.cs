using Marten;

namespace VietAIS.TCFlow.Analyzers.Knowledge;

public static class KnowledgeGraphStorage
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Schema.For<RepositoryKnowledgeManifest>().UseOptimisticConcurrency(true);
        options.Schema.For<KnowledgeArtifactDocument>().Index(document => document.RepositoryId);
        options.Schema.For<KnowledgeDependencyDocument>().Index(document => document.RepositoryId);
        options.Schema.For<KnowledgeEvidenceDocument>().Index(document => document.RepositoryId);
        options.Schema.For<KnowledgeCapabilityDocument>().Index(document => document.RepositoryId);
        options.Schema.For<KnowledgeContractDocument>().Index(document => document.RepositoryId);
        options.Schema.For<KnowledgeChangeDocument>().Index(document => document.RepositoryId);
        options.Schema.For<KnowledgeImpactDocument>().Index(document => document.RepositoryId);
        options.Schema.For<KnowledgeContractPairDocument>().Index(document => document.RepositoryId);
        options.Schema.For<KnowledgeContractMismatchDocument>().Index(document => document.RepositoryId);
    }
}
