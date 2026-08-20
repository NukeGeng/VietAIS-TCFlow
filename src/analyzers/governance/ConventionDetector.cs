using System.Text.RegularExpressions;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Knowledge;

namespace VietAIS.TCFlow.Analyzers.Governance;

public sealed partial class ConventionDetector
{
    public RepositoryConventionProfile Detect(RepositoryKnowledgeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var observations = new List<ConventionObservation>();
        AddPresenceConvention(
            observations,
            graph,
            ConventionKind.ApiStyle,
            "minimal-api",
            ArtifactKind.AspNetEndpoint,
            EvidenceLevel.Confirmed,
            1m);
        AddPresenceConvention(
            observations,
            graph,
            ConventionKind.Persistence,
            "marten-document-database",
            ArtifactKind.MartenDocument,
            EvidenceLevel.Confirmed,
            1m);
        AddPresenceConvention(
            observations,
            graph,
            ConventionKind.Validation,
            "fluent-validation",
            ArtifactKind.Validator,
            EvidenceLevel.Confirmed,
            1m);
        AddPresenceConvention(
            observations,
            graph,
            ConventionKind.FrontendState,
            "pinia",
            ArtifactKind.PiniaStore,
            EvidenceLevel.Confirmed,
            1m);
        AddPresenceConvention(
            observations,
            graph,
            ConventionKind.FrontendRouting,
            "vue-router",
            ArtifactKind.VueRoute,
            EvidenceLevel.Confirmed,
            1m);
        AddNamingConvention(observations, graph, ConventionKind.RequestDtoNaming, ArtifactKind.RequestDto,
            ["Command", "Query", "Request"]);
        AddNamingConvention(observations, graph, ConventionKind.ResponseDtoNaming, ArtifactKind.ResponseDto,
            ["Response"]);
        AddNamingConvention(observations, graph, ConventionKind.HandlerNaming, ArtifactKind.Handler, ["Handler"]);
        AddNamingConvention(observations, graph, ConventionKind.EndpointNaming, ArtifactKind.AspNetEndpoint,
            ["Endpoint"]);

        var moduleArtifacts = graph.Artifacts.Where(artifact => ModulePathRegex().IsMatch(artifact.Path)).ToArray();
        if (moduleArtifacts.Length > 0)
        {
            AddObservation(
                observations,
                ConventionKind.Architecture,
                "module-based",
                EvidenceLevel.Confirmed,
                1m,
                moduleArtifacts);
            AddObservation(
                observations,
                ConventionKind.ModuleLayout,
                "src/api/modules/{module}",
                EvidenceLevel.Confirmed,
                1m,
                moduleArtifacts);
        }

        var featureArtifacts = graph.Artifacts.Where(artifact => FeaturePathRegex().IsMatch(artifact.Path)).ToArray();
        if (featureArtifacts.Length > 0)
        {
            AddObservation(
                observations,
                ConventionKind.Architecture,
                "feature-based",
                EvidenceLevel.Inferred,
                0.86m,
                featureArtifacts);
        }

        return new RepositoryConventionProfile(
            graph.RepositoryId,
            graph.Revision,
            ConventionProfileStatus.Detected,
            observations.OrderBy(observation => observation.Kind)
                .ThenBy(observation => observation.Value, StringComparer.Ordinal)
                .ToArray());
    }

    private static void AddPresenceConvention(
        ICollection<ConventionObservation> observations,
        RepositoryKnowledgeGraph graph,
        ConventionKind kind,
        string value,
        ArtifactKind artifactKind,
        EvidenceLevel level,
        decimal confidence)
    {
        var artifacts = graph.Artifacts.Where(artifact => artifact.Kind == artifactKind).ToArray();
        if (artifacts.Length > 0)
        {
            AddObservation(observations, kind, value, level, confidence, artifacts);
        }
    }

    private static void AddNamingConvention(
        ICollection<ConventionObservation> observations,
        RepositoryKnowledgeGraph graph,
        ConventionKind kind,
        ArtifactKind artifactKind,
        IReadOnlyList<string> suffixes)
    {
        var artifacts = graph.Artifacts.Where(artifact => artifact.Kind == artifactKind).ToArray();
        var matches = artifacts.Select(artifact => new
        {
            Artifact = artifact,
            Suffix = suffixes.FirstOrDefault(suffix => artifact.Name.EndsWith(suffix, StringComparison.Ordinal))
        })
            .Where(item => item.Suffix is not null)
            .GroupBy(item => item.Suffix!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (matches is null)
        {
            return;
        }

        var matchingArtifacts = matches.Select(item => item.Artifact).ToArray();
        var confidence = artifacts.Length == 0 ? 0m : decimal.Divide(matchingArtifacts.Length, artifacts.Length);
        AddObservation(
            observations,
            kind,
            matches.Key,
            confidence == 1m ? EvidenceLevel.Confirmed : EvidenceLevel.Inferred,
            confidence,
            matchingArtifacts);
    }

    private static void AddObservation(
        ICollection<ConventionObservation> observations,
        ConventionKind kind,
        string value,
        EvidenceLevel level,
        decimal confidence,
        IReadOnlyCollection<Artifact> artifacts)
    {
        var evidenceIds = artifacts.SelectMany(artifact => artifact.EvidenceIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        observations.Add(new ConventionObservation(
            StableIdentity.Create("convention", kind.ToString(), value, string.Join(',', evidenceIds)),
            kind,
            value,
            level,
            confidence,
            evidenceIds,
            artifacts.Select(artifact => artifact.Path)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Take(5)
                .ToArray()));
    }

    [GeneratedRegex(@"(?:^|/)src/api/modules/[^/]+/", RegexOptions.IgnoreCase)]
    private static partial Regex ModulePathRegex();

    [GeneratedRegex(@"/(?:Create|Update|Delete|Get|Search|List)(?:/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex FeaturePathRegex();
}
