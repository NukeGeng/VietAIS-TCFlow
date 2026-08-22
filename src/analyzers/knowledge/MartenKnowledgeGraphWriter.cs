using Marten;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Knowledge;

public sealed class MartenKnowledgeGraphWriter(
    IDocumentSession session,
    TimeProvider timeProvider)
{
    public async Task SaveAsync(
        RepositoryKnowledgeGraph graph,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (string.IsNullOrWhiteSpace(graph.RepositoryId))
        {
            throw new ArgumentException("Repository identity is required.", nameof(graph));
        }

        if (graph.Revision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(graph), "Knowledge revision must be positive.");
        }

        var currentManifest = await session.LoadAsync<RepositoryKnowledgeManifest>(
            graph.RepositoryId,
            cancellationToken);
        if (currentManifest is not null && graph.Revision <= currentManifest.Revision)
        {
            throw new InvalidOperationException(
                $"Knowledge revision {graph.Revision} is not newer than persisted revision {currentManifest.Revision}.");
        }

        var artifacts = graph.Artifacts.Select(value => new KnowledgeArtifactDocument(
            DocumentId(graph.RepositoryId, "artifact", value.Id),
            graph.RepositoryId,
            Producer(graph, value.Id),
            value)).ToArray();
        var dependencies = graph.Dependencies.Select(value => new KnowledgeDependencyDocument(
            DocumentId(graph.RepositoryId, "dependency", value.Id),
            graph.RepositoryId,
            Producer(graph, value.Id),
            value)).ToArray();
        var evidence = graph.Evidence.Select(value => new KnowledgeEvidenceDocument(
            DocumentId(graph.RepositoryId, "evidence", value.Id),
            graph.RepositoryId,
            Producer(graph, value.Id),
            value)).ToArray();
        var capabilities = graph.Capabilities.Select(value => new KnowledgeCapabilityDocument(
            DocumentId(graph.RepositoryId, "capability", value.Id),
            graph.RepositoryId,
            Producer(graph, value.Id),
            value)).ToArray();
        var contracts = graph.Contracts.Select(value => new KnowledgeContractDocument(
            DocumentId(graph.RepositoryId, "contract", value.Id),
            graph.RepositoryId,
            Producer(graph, value.Id),
            value)).ToArray();
        var changes = graph.Changes.Select(value => new KnowledgeChangeDocument(
            DocumentId(graph.RepositoryId, "change", value.Id),
            graph.RepositoryId,
            Producer(graph, value.Id),
            value)).ToArray();
        var impacts = graph.Impacts.Select(value => new KnowledgeImpactDocument(
            DocumentId(graph.RepositoryId, "impact", value.Id),
            graph.RepositoryId,
            Producer(graph, value.Id),
            value)).ToArray();
        var pairs = graph.ContractPairs.Select(value => new KnowledgeContractPairDocument(
            DocumentId(graph.RepositoryId, "contract-pair", value.Id),
            graph.RepositoryId,
            Producer(graph, value.Id),
            value)).ToArray();
        var mismatches = graph.ContractMismatches.Select(value => new KnowledgeContractMismatchDocument(
            DocumentId(graph.RepositoryId, "contract-mismatch", value.Id),
            graph.RepositoryId,
            Producer(graph, value.Id),
            value)).ToArray();

        Replace(
            await session.Query<KnowledgeArtifactDocument>()
                .Where(document => document.RepositoryId == graph.RepositoryId)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken),
            artifacts,
            document => document.Id,
            cancellationToken);
        Replace(
            await session.Query<KnowledgeDependencyDocument>()
                .Where(document => document.RepositoryId == graph.RepositoryId)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken),
            dependencies,
            document => document.Id,
            cancellationToken);
        Replace(
            await session.Query<KnowledgeEvidenceDocument>()
                .Where(document => document.RepositoryId == graph.RepositoryId)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken),
            evidence,
            document => document.Id,
            cancellationToken);
        Replace(
            await session.Query<KnowledgeCapabilityDocument>()
                .Where(document => document.RepositoryId == graph.RepositoryId)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken),
            capabilities,
            document => document.Id,
            cancellationToken);
        Replace(
            await session.Query<KnowledgeContractDocument>()
                .Where(document => document.RepositoryId == graph.RepositoryId)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken),
            contracts,
            document => document.Id,
            cancellationToken);
        Replace(
            await session.Query<KnowledgeChangeDocument>()
                .Where(document => document.RepositoryId == graph.RepositoryId)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken),
            changes,
            document => document.Id,
            cancellationToken);
        Replace(
            await session.Query<KnowledgeImpactDocument>()
                .Where(document => document.RepositoryId == graph.RepositoryId)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken),
            impacts,
            document => document.Id,
            cancellationToken);
        Replace(
            await session.Query<KnowledgeContractPairDocument>()
                .Where(document => document.RepositoryId == graph.RepositoryId)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken),
            pairs,
            document => document.Id,
            cancellationToken);
        Replace(
            await session.Query<KnowledgeContractMismatchDocument>()
                .Where(document => document.RepositoryId == graph.RepositoryId)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken),
            mismatches,
            document => document.Id,
            cancellationToken);

        session.Store(new RepositoryKnowledgeManifest(
            graph.RepositoryId,
            graph.Revision,
            timeProvider.GetUtcNow()));
        await session.SaveChangesAsync(cancellationToken);
    }

    private void Replace<T>(
        IReadOnlyCollection<string> currentIds,
        IReadOnlyCollection<T> documents,
        Func<T, string> idSelector,
        CancellationToken cancellationToken)
        where T : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nextIds = documents.Select(idSelector).ToHashSet(StringComparer.Ordinal);
        foreach (var staleId in currentIds.Where(id => !nextIds.Contains(id)))
        {
            session.Delete<T>(staleId);
        }

        foreach (var document in documents)
        {
            session.Store(document);
        }
    }

    private static string Producer(RepositoryKnowledgeGraph graph, string recordId) =>
        graph.RecordProducers.TryGetValue(recordId, out var producer)
            ? producer
            : throw new InvalidOperationException($"Knowledge record '{recordId}' has no producer provenance.");

    private static string DocumentId(string repositoryId, string kind, string recordId) =>
        StableIdentity.Create("knowledge-document", repositoryId, kind, recordId);
}
