using Marten;

namespace VietAIS.TCFlow.Analyzers.Knowledge;

public sealed class MartenKnowledgeGraphReader(IQuerySession session)
{
    public async Task<RepositoryKnowledgeGraph?> LoadAsync(
        string repositoryId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryId))
        {
            throw new ArgumentException("Repository identity is required.", nameof(repositoryId));
        }

        var manifest = await session.LoadAsync<RepositoryKnowledgeManifest>(repositoryId, cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        var artifacts = await session.Query<KnowledgeArtifactDocument>()
            .Where(document => document.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);
        var dependencies = await session.Query<KnowledgeDependencyDocument>()
            .Where(document => document.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);
        var evidence = await session.Query<KnowledgeEvidenceDocument>()
            .Where(document => document.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);
        var capabilities = await session.Query<KnowledgeCapabilityDocument>()
            .Where(document => document.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);
        var contracts = await session.Query<KnowledgeContractDocument>()
            .Where(document => document.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);
        var changes = await session.Query<KnowledgeChangeDocument>()
            .Where(document => document.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);
        var impacts = await session.Query<KnowledgeImpactDocument>()
            .Where(document => document.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);
        var pairs = await session.Query<KnowledgeContractPairDocument>()
            .Where(document => document.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);
        var mismatches = await session.Query<KnowledgeContractMismatchDocument>()
            .Where(document => document.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);
        var producers = artifacts.Select(document => (document.Value.Id, document.Producer))
            .Concat(dependencies.Select(document => (document.Value.Id, document.Producer)))
            .Concat(evidence.Select(document => (document.Value.Id, document.Producer)))
            .Concat(capabilities.Select(document => (document.Value.Id, document.Producer)))
            .Concat(contracts.Select(document => (document.Value.Id, document.Producer)))
            .Concat(changes.Select(document => (document.Value.Id, document.Producer)))
            .Concat(impacts.Select(document => (document.Value.Id, document.Producer)))
            .Concat(pairs.Select(document => (document.Value.Id, document.Producer)))
            .Concat(mismatches.Select(document => (document.Value.Id, document.Producer)))
            .ToDictionary(item => item.Id, item => item.Producer, StringComparer.Ordinal);

        return new RepositoryKnowledgeGraph(
            repositoryId,
            manifest.Revision,
            artifacts.Select(document => document.Value with
            {
                Metadata = new SortedDictionary<string, string>(
                    document.Value.Metadata.ToDictionary(
                        item => item.Key,
                        item => item.Value,
                        StringComparer.Ordinal),
                    StringComparer.Ordinal)
            })
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            dependencies.Select(document => document.Value)
                .OrderBy(item => item.SourceArtifactId, StringComparer.Ordinal)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.Target, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            evidence.Select(document => document.Value)
                .OrderBy(item => item.Location.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Location.StartLine)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            capabilities.Select(document => document.Value)
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            contracts.Select(document => document.Value)
                .OrderBy(item => item.Route, StringComparer.Ordinal)
                .ThenBy(item => item.HttpMethod, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            changes.Select(document => document.Value)
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            impacts.Select(document => document.Value)
                .OrderBy(item => item.SourceChangeId, StringComparer.Ordinal)
                .ThenBy(item => item.AffectedArtifactId, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            pairs.Select(document => document.Value)
                .OrderBy(item => item.FrontendContractId, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            mismatches.Select(document => document.Value)
                .OrderBy(item => item.ContractPairId, StringComparer.Ordinal)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.Subject, StringComparer.Ordinal)
                .ToArray(),
            new SortedDictionary<string, string>(producers, StringComparer.Ordinal));
    }
}
