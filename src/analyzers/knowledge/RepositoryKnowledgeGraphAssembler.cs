using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Knowledge;

public sealed class RepositoryKnowledgeGraphAssembler
{
    private const string ComparisonProducer = "contract-comparison-v1";

    public RepositoryKnowledgeGraph Build(
        string repositoryId,
        IReadOnlyCollection<AnalysisResult> analyses)
    {
        ValidateRepositoryId(repositoryId);
        ArgumentNullException.ThrowIfNull(analyses);
        var records = new KnowledgeRecords();
        AddAnalyses(records, analyses);
        AddDerivedContractRecords(records);
        return records.Build(repositoryId, 1);
    }

    public RepositoryKnowledgeGraph ApplyIncremental(
        RepositoryKnowledgeGraph current,
        IReadOnlyCollection<AnalysisResult> replacements)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(replacements);
        ValidateRepositoryId(current.RepositoryId);
        if (replacements.Count == 0)
        {
            return current;
        }

        var replacementProducers = replacements.Select(result => result.Analyzer)
            .ToHashSet(StringComparer.Ordinal);
        if (replacementProducers.Count != replacements.Count)
        {
            throw new ArgumentException("Incremental replacements must have unique analyzer names.",
                nameof(replacements));
        }

        replacementProducers.Add(ComparisonProducer);
        var records = KnowledgeRecords.From(current, replacementProducers);
        AddAnalyses(records, replacements);
        records.RemoveDanglingRecords();
        AddDerivedContractRecords(records);
        return records.Build(current.RepositoryId, checked(current.Revision + 1));
    }

    public RepositoryKnowledgeGraph ApplyIncrementalPaths(
        RepositoryKnowledgeGraph current,
        IReadOnlyCollection<AnalysisResult> partialReplacements,
        IReadOnlyCollection<string> changedPaths)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(partialReplacements);
        ArgumentNullException.ThrowIfNull(changedPaths);
        var normalizedPaths = changedPaths
            .Select(NormalizePath)
            .ToHashSet(StringComparer.Ordinal);
        if (normalizedPaths.Count == 0)
        {
            throw new ArgumentException("At least one changed path is required.", nameof(changedPaths));
        }

        var mergedReplacements = partialReplacements
            .Select(replacement => MergePathScopedResult(current, replacement, normalizedPaths))
            .ToArray();
        return ApplyIncremental(current, mergedReplacements);
    }

    private static AnalysisResult MergePathScopedResult(
        RepositoryKnowledgeGraph current,
        AnalysisResult partial,
        IReadOnlySet<string> changedPaths)
    {
        var existingArtifacts = ProducedBy(current.Artifacts, item => item.Id, partial.Analyzer, current)
            .ToArray();
        var existingEvidence = ProducedBy(current.Evidence, item => item.Id, partial.Analyzer, current)
            .ToArray();
        var affectedArtifactIds = existingArtifacts
            .Where(artifact => changedPaths.Contains(NormalizePath(artifact.Path)))
            .Select(artifact => artifact.Id)
            .ToHashSet(StringComparer.Ordinal);
        var affectedEvidenceIds = existingEvidence
            .Where(evidence => changedPaths.Contains(NormalizePath(evidence.Location.Path)))
            .Select(evidence => evidence.Id)
            .ToHashSet(StringComparer.Ordinal);

        var retainedArtifacts = existingArtifacts.Where(artifact => !affectedArtifactIds.Contains(artifact.Id));
        var retainedEvidence = existingEvidence.Where(evidence => !affectedEvidenceIds.Contains(evidence.Id));
        var retainedDependencies = ProducedBy(
                current.Dependencies,
                item => item.Id,
                partial.Analyzer,
                current)
            .Where(dependency =>
                !affectedArtifactIds.Contains(dependency.SourceArtifactId) &&
                !affectedArtifactIds.Contains(dependency.Target) &&
                !affectedEvidenceIds.Contains(dependency.EvidenceId));
        var retainedCapabilities = ProducedBy(
                current.Capabilities,
                item => item.Id,
                partial.Analyzer,
                current)
            .Where(capability =>
                !capability.ArtifactIds.Any(affectedArtifactIds.Contains) &&
                !capability.EvidenceIds.Any(affectedEvidenceIds.Contains));
        var retainedContracts = ProducedBy(current.Contracts, item => item.Id, partial.Analyzer, current)
            .Where(contract =>
                !contract.EvidenceIds.Any(affectedEvidenceIds.Contains) &&
                !contract.RequestFields.Any(field => changedPaths.Contains(NormalizePath(field.Location.Path))) &&
                !contract.ResponseFields.Any(field => changedPaths.Contains(NormalizePath(field.Location.Path))));
        var retainedChanges = ProducedBy(current.Changes, item => item.Id, partial.Analyzer, current);
        var retainedImpacts = ProducedBy(current.Impacts, item => item.Id, partial.Analyzer, current);

        return partial with
        {
            Artifacts = Merge(retainedArtifacts, partial.Artifacts, item => item.Id),
            Dependencies = Merge(retainedDependencies, partial.Dependencies, item => item.Id),
            Evidence = Merge(retainedEvidence, partial.Evidence, item => item.Id),
            Capabilities = Merge(retainedCapabilities, partial.Capabilities, item => item.Id),
            Contracts = Merge(retainedContracts, partial.Contracts, item => item.Id),
            Changes = Merge(retainedChanges, partial.Changes, item => item.Id),
            Impacts = Merge(retainedImpacts, partial.Impacts, item => item.Id)
        };
    }

    private static IReadOnlyList<T> ProducedBy<T>(
        IEnumerable<T> values,
        Func<T, string> idSelector,
        string producer,
        RepositoryKnowledgeGraph graph) => values
        .Where(value => graph.RecordProducers.TryGetValue(idSelector(value), out var valueProducer) &&
            string.Equals(valueProducer, producer, StringComparison.Ordinal))
        .ToArray();

    private static IReadOnlyList<T> Merge<T>(
        IEnumerable<T> retained,
        IEnumerable<T> replacements,
        Func<T, string> idSelector)
    {
        var values = retained.ToDictionary(idSelector, StringComparer.Ordinal);
        foreach (var replacement in replacements)
        {
            values[idSelector(replacement)] = replacement;
        }

        return values.Values.ToArray();
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Repository-relative paths are required.", nameof(path));
        }

        return path.Replace('\\', '/');
    }

    private static void AddAnalyses(KnowledgeRecords records, IEnumerable<AnalysisResult> analyses)
    {
        var ordered = analyses.OrderBy(result => result.Analyzer, StringComparer.Ordinal).ToArray();
        if (ordered.Select(result => result.Analyzer).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new ArgumentException("Analysis results must have unique analyzer names.", nameof(analyses));
        }

        foreach (var analysis in ordered)
        {
            records.Add(analysis);
        }
    }

    private static void AddDerivedContractRecords(KnowledgeRecords records)
    {
        var frontend = records.Contracts.Values
            .Where(contract => contract.Direction == ContractDirection.FrontendExpected)
            .ToArray();
        var backend = records.Contracts.Values
            .Where(contract => contract.Direction == ContractDirection.BackendActual)
            .ToArray();
        if (frontend.Length == 0 || backend.Length == 0)
        {
            return;
        }

        var comparison = new ContractComparator().Compare(frontend, backend);
        foreach (var pair in comparison.Pairs)
        {
            records.Add(pair, ComparisonProducer);
        }

        foreach (var mismatch in comparison.Mismatches)
        {
            records.Add(mismatch, ComparisonProducer);
        }

        foreach (var pair in comparison.Pairs.Where(pair =>
                     pair.Status == ContractPairStatus.Matched && pair.BackendContractId is not null))
        {
            var frontendContract = records.Contracts[pair.FrontendContractId];
            var backendContract = records.Contracts[pair.BackendContractId!];
            var frontendArtifact = FindContractArtifact(
                frontendContract,
                ArtifactKind.ApiCall,
                records.Artifacts.Values,
                records.Evidence);
            var backendArtifact = FindContractArtifact(
                backendContract,
                ArtifactKind.AspNetEndpoint,
                records.Artifacts.Values,
                records.Evidence);
            var evidenceId = pair.EvidenceIds.FirstOrDefault(records.Evidence.ContainsKey);
            if (frontendArtifact is null || backendArtifact is null || evidenceId is null)
            {
                continue;
            }

            records.Add(new Dependency(
                StableIdentity.Create(
                    "dependency",
                    "knowledge",
                    frontendArtifact.Id,
                    backendArtifact.Id,
                    DependencyKind.Calls.ToString()),
                frontendArtifact.Id,
                backendArtifact.Id,
                DependencyKind.Calls,
                pair.EvidenceLevel,
                evidenceId), ComparisonProducer);
        }
    }

    private static Artifact? FindContractArtifact(
        Contract contract,
        ArtifactKind kind,
        IEnumerable<Artifact> artifacts,
        IReadOnlyDictionary<string, Evidence> evidence)
    {
        var paths = contract.EvidenceIds
            .Where(evidence.ContainsKey)
            .Select(id => evidence[id].Location.Path)
            .ToHashSet(StringComparer.Ordinal);
        var matches = artifacts.Where(artifact =>
                artifact.Kind == kind &&
                paths.Contains(artifact.Path) &&
                artifact.Metadata.TryGetValue("method", out var method) &&
                artifact.Metadata.TryGetValue("route", out var route) &&
                string.Equals(method, contract.HttpMethod, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(route, contract.Route, StringComparison.Ordinal))
            .OrderBy(artifact => artifact.Id, StringComparer.Ordinal)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static void ValidateRepositoryId(string repositoryId)
    {
        if (string.IsNullOrWhiteSpace(repositoryId))
        {
            throw new ArgumentException("Repository identity is required.", nameof(repositoryId));
        }
    }

    private sealed class KnowledgeRecords
    {
        public Dictionary<string, Artifact> Artifacts { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Dependency> Dependencies { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Evidence> Evidence { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Capability> Capabilities { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Contract> Contracts { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, SourceChange> Changes { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Impact> Impacts { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, ContractPair> ContractPairs { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, ContractMismatch> ContractMismatches { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Producers { get; } = new(StringComparer.Ordinal);

        public static KnowledgeRecords From(
            RepositoryKnowledgeGraph graph,
            IReadOnlySet<string> excludedProducers)
        {
            var records = new KnowledgeRecords();
            records.Import(graph.Artifacts, item => item.Id, graph, excludedProducers, records.Artifacts);
            records.Import(graph.Dependencies, item => item.Id, graph, excludedProducers, records.Dependencies);
            records.Import(graph.Evidence, item => item.Id, graph, excludedProducers, records.Evidence);
            records.Import(graph.Capabilities, item => item.Id, graph, excludedProducers, records.Capabilities);
            records.Import(graph.Contracts, item => item.Id, graph, excludedProducers, records.Contracts);
            records.Import(graph.Changes, item => item.Id, graph, excludedProducers, records.Changes);
            records.Import(graph.Impacts, item => item.Id, graph, excludedProducers, records.Impacts);
            records.Import(graph.ContractPairs, item => item.Id, graph, excludedProducers, records.ContractPairs);
            records.Import(
                graph.ContractMismatches,
                item => item.Id,
                graph,
                excludedProducers,
                records.ContractMismatches);
            return records;
        }

        public void Add(AnalysisResult result)
        {
            AddRange(result.Artifacts, item => item.Id, result.Analyzer, Artifacts);
            AddRange(result.Dependencies, item => item.Id, result.Analyzer, Dependencies);
            AddRange(result.Evidence, item => item.Id, result.Analyzer, Evidence);
            AddRange(result.Capabilities, item => item.Id, result.Analyzer, Capabilities);
            AddRange(result.Contracts, item => item.Id, result.Analyzer, Contracts);
            AddRange(result.Changes, item => item.Id, result.Analyzer, Changes);
            AddRange(result.Impacts, item => item.Id, result.Analyzer, Impacts);
        }

        public void Add(Dependency value, string producer) => AddValue(value.Id, value, producer, Dependencies);

        public void Add(ContractPair value, string producer) => AddValue(value.Id, value, producer, ContractPairs);

        public void Add(ContractMismatch value, string producer) =>
            AddValue(value.Id, value, producer, ContractMismatches);

        public void RemoveDanglingRecords()
        {
            var artifactIds = Artifacts.Keys.ToHashSet(StringComparer.Ordinal);
            foreach (var dependency in Dependencies.Values
                         .Where(dependency => !artifactIds.Contains(dependency.SourceArtifactId))
                         .ToArray())
            {
                Dependencies.Remove(dependency.Id);
                Producers.Remove(dependency.Id);
            }

            foreach (var impact in Impacts.Values
                         .Where(impact => !artifactIds.Contains(impact.AffectedArtifactId))
                         .ToArray())
            {
                Impacts.Remove(impact.Id);
                Producers.Remove(impact.Id);
            }
        }

        public RepositoryKnowledgeGraph Build(string repositoryId, long revision) => new(
            repositoryId,
            revision,
            Artifacts.Values.OrderBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            Dependencies.Values.OrderBy(item => item.SourceArtifactId, StringComparer.Ordinal)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.Target, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            Evidence.Values.OrderBy(item => item.Location.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Location.StartLine)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            Capabilities.Values.OrderBy(item => item.Name, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            Contracts.Values.OrderBy(item => item.Route, StringComparer.Ordinal)
                .ThenBy(item => item.HttpMethod, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            Changes.Values.OrderBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            Impacts.Values.OrderBy(item => item.SourceChangeId, StringComparer.Ordinal)
                .ThenBy(item => item.AffectedArtifactId, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            ContractPairs.Values.OrderBy(item => item.FrontendContractId, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            ContractMismatches.Values.OrderBy(item => item.ContractPairId, StringComparer.Ordinal)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.Subject, StringComparer.Ordinal)
                .ToArray(),
            new SortedDictionary<string, string>(Producers, StringComparer.Ordinal));

        private void Import<T>(
            IEnumerable<T> values,
            Func<T, string> idSelector,
            RepositoryKnowledgeGraph graph,
            IReadOnlySet<string> excludedProducers,
            IDictionary<string, T> target)
        {
            foreach (var value in values)
            {
                var id = idSelector(value);
                if (!graph.RecordProducers.TryGetValue(id, out var producer))
                {
                    throw new InvalidOperationException($"Knowledge record '{id}' has no producer provenance.");
                }

                if (!excludedProducers.Contains(producer))
                {
                    AddValue(id, value, producer, target);
                }
            }
        }

        private void AddRange<T>(
            IEnumerable<T> values,
            Func<T, string> idSelector,
            string producer,
            IDictionary<string, T> target)
        {
            foreach (var value in values)
            {
                AddValue(idSelector(value), value, producer, target);
            }
        }

        private void AddValue<T>(string id, T value, string producer, IDictionary<string, T> target)
        {
            if (target.TryGetValue(id, out var existing) && !EqualityComparer<T>.Default.Equals(existing, value))
            {
                throw new InvalidOperationException($"Knowledge record '{id}' has conflicting values.");
            }

            if (Producers.TryGetValue(id, out var existingProducer) && existingProducer != producer)
            {
                throw new InvalidOperationException(
                    $"Knowledge record '{id}' is emitted by both '{existingProducer}' and '{producer}'.");
            }

            target[id] = value;
            Producers[id] = producer;
        }
    }
}
