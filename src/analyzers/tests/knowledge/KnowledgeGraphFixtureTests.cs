using System.Text.Json;
using Marten;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.Analyzers.AspNet;
using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Marten;
using VietAIS.TCFlow.Analyzers.Vue;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Knowledge.Tests;

public sealed class KnowledgeGraphFixtureTests
{
    [Fact]
    public async Task FullFixtureConnectsFrontendEndpointAndPersistenceWithoutUnrelatedContext()
    {
        var analyses = await AnalyzeFixture();
        var graph = new RepositoryKnowledgeGraphAssembler().Build("fixture-repository", analyses.All);
        var change = Assert.Single(analyses.Vue.Changes);
        var context = new KnowledgeRetriever().RetrieveForChanges(graph, [change.Id], maxDepth: 2);
        var expected = JsonSerializer.Deserialize<ExpectedRetrieval>(
            await File.ReadAllTextAsync(
                Path.Combine(FixtureRoot, "expected", "retrieval.json"),
                TestContext.Current.CancellationToken),
            AnalysisJson.Options)!;

        foreach (var artifactName in expected.RequiredArtifacts)
        {
            Assert.Contains(context.Artifacts, artifact => artifact.Name == artifactName);
        }

        foreach (var artifactName in expected.ExcludedArtifacts)
        {
            Assert.DoesNotContain(context.Artifacts, artifact => artifact.Name == artifactName);
        }

        foreach (var edge in expected.RequiredEdges)
        {
            var source = context.Artifacts.Single(artifact =>
                artifact.Name == edge.Source &&
                artifact.Kind.ToString().Equals(edge.SourceKind, StringComparison.OrdinalIgnoreCase));
            var target = context.Artifacts.Single(artifact =>
                artifact.Name == edge.Target &&
                artifact.Kind.ToString().Equals(edge.TargetKind, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(context.Dependencies, dependency =>
                dependency.SourceArtifactId == source.Id &&
                dependency.Target == target.Id &&
                dependency.Kind.ToString().Equals(edge.Kind, StringComparison.OrdinalIgnoreCase));
        }

        Assert.NotEmpty(context.Evidence);
        Assert.All(context.Evidence, evidence => Assert.Contains(context.Provenance,
            provenance => provenance.EvidenceId == evidence.Id && provenance.SupportingRecordIds.Count > 0));
        Assert.Single(graph.ContractPairs, pair => pair.Status == ContractPairStatus.Matched);
        Assert.Empty(graph.ContractMismatches);
    }

    [Fact]
    public async Task FullScanAndIncrementalUpdateProduceEquivalentAffectedNeighborhoods()
    {
        var analyses = await AnalyzeFixture();
        var assembler = new RepositoryKnowledgeGraphAssembler();
        var full = assembler.Build("fixture-repository", analyses.All);
        var withoutVue = assembler.Build("fixture-repository", [analyses.AspNet, analyses.Marten]);
        var incremental = assembler.ApplyIncremental(withoutVue, [analyses.Vue]);
        var change = Assert.Single(analyses.Vue.Changes);
        var retriever = new KnowledgeRetriever();

        var fullContext = retriever.RetrieveForChanges(full, [change.Id], maxDepth: 2);
        var incrementalContext = retriever.RetrieveForChanges(incremental, [change.Id], maxDepth: 2);

        Assert.Equal(1, full.Revision);
        Assert.Equal(2, incremental.Revision);
        Assert.Equal(
            JsonSerializer.Serialize(fullContext, AnalysisJson.Options),
            JsonSerializer.Serialize(incrementalContext, AnalysisJson.Options));
    }

    [Fact]
    public async Task TraversalHonorsDepthAndCanWalkDependenciesInReverse()
    {
        var analyses = await AnalyzeFixture();
        var graph = new RepositoryKnowledgeGraphAssembler().Build("fixture-repository", analyses.All);
        var api = graph.Artifacts.Single(artifact =>
            artifact.Kind == ArtifactKind.ApiCall && artifact.Name == "POST /api/v1/catalog/products");
        var product = graph.Artifacts.Single(artifact =>
            artifact.Kind == ArtifactKind.MartenDocument && artifact.Name == "Product");
        var traversal = new KnowledgeGraphTraversal();

        var zero = traversal.FindNeighborhood(graph, [api.Id], maxDepth: 0);
        var two = traversal.FindNeighborhood(graph, [api.Id], maxDepth: 2);
        var reverse = traversal.FindNeighborhood(graph, [product.Id], maxDepth: 2);

        Assert.Equal([api.Id], zero.ArtifactIds);
        Assert.DoesNotContain(product.Id, zero.ArtifactIds);
        Assert.Contains(product.Id, two.ArtifactIds);
        Assert.Contains(api.Id, reverse.ArtifactIds);
    }

    [Fact]
    public async Task MartenPersistsLoadsAndReconcilesRepositoryKnowledge()
    {
        var analyses = await AnalyzeFixture();
        var assembler = new RepositoryKnowledgeGraphAssembler();
        var graph = assembler.Build("persisted-fixture", analyses.All);
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        using var store = DocumentStore.For(options =>
        {
            options.Connection(postgres.GetConnectionString());
            options.DatabaseSchemaName = "knowledge_graph_test";
            KnowledgeGraphStorage.Configure(options);
        });
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        await using (var writeSession = store.LightweightSession())
        {
            await new MartenKnowledgeGraphWriter(writeSession, TimeProvider.System)
                .SaveAsync(graph, TestContext.Current.CancellationToken);
        }

        await using (var querySession = store.QuerySession())
        {
            var loaded = await new MartenKnowledgeGraphReader(querySession)
                .LoadAsync(graph.RepositoryId, TestContext.Current.CancellationToken);
            Assert.NotNull(loaded);
            Assert.Equal(
                JsonSerializer.Serialize(graph, AnalysisJson.Options),
                JsonSerializer.Serialize(loaded, AnalysisJson.Options));
            Assert.Equal(graph.Artifacts.Count, await querySession.Query<KnowledgeArtifactDocument>()
                .CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(graph.Dependencies.Count, await querySession.Query<KnowledgeDependencyDocument>()
                .CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(graph.Evidence.Count, await querySession.Query<KnowledgeEvidenceDocument>()
                .CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(graph.Capabilities.Count, await querySession.Query<KnowledgeCapabilityDocument>()
                .CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(graph.Contracts.Count, await querySession.Query<KnowledgeContractDocument>()
                .CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(graph.Changes.Count, await querySession.Query<KnowledgeChangeDocument>()
                .CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(graph.Impacts.Count, await querySession.Query<KnowledgeImpactDocument>()
                .CountAsync(TestContext.Current.CancellationToken));
            Assert.NotEmpty(graph.Impacts);
        }

        var withoutVue = assembler.ApplyIncremental(graph, [AnalysisResult.Empty("vue-v1", "vue")]);
        await using (var updateSession = store.LightweightSession())
        {
            await new MartenKnowledgeGraphWriter(updateSession, TimeProvider.System)
                .SaveAsync(withoutVue, TestContext.Current.CancellationToken);
        }

        await using (var querySession = store.QuerySession())
        {
            var loaded = await new MartenKnowledgeGraphReader(querySession)
                .LoadAsync(graph.RepositoryId, TestContext.Current.CancellationToken);
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded.Revision);
            Assert.DoesNotContain(loaded.Artifacts, artifact => artifact.Technology == "vue");
            Assert.DoesNotContain(loaded.Dependencies, dependency =>
                !loaded.Artifacts.Any(artifact => artifact.Id == dependency.SourceArtifactId));
        }

        await using (var staleSession = store.LightweightSession())
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new MartenKnowledgeGraphWriter(staleSession, TimeProvider.System)
                    .SaveAsync(graph, TestContext.Current.CancellationToken));
            Assert.Contains("not newer", exception.Message);
        }
    }

    private static async Task<FixtureAnalyses> AnalyzeFixture()
    {
        var files = await new FileDiscovery().DiscoverAsync(
            FixtureRoot,
            new FileDiscoveryOptions(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".vue",
                ".ts",
                ".cs"
            }),
            TestContext.Current.CancellationToken);
        var vue = await new VueAnalyzer().AnalyzeAsync(files, TestContext.Current.CancellationToken);
        var change = new SourceChange(
            StableIdentity.Create("change", "src/frontend/CreateProductView.vue", "fixture-final"),
            "src/frontend/CreateProductView.vue",
            ChangeKind.Modified,
            "fixture-before",
            "fixture-final",
            true,
            "Executable Vue contract changed.");
        var apiArtifact = vue.Artifacts.Single(artifact =>
            artifact.Kind == ArtifactKind.ApiCall && artifact.Name == "POST /api/v1/catalog/products");
        var impact = new Impact(
            StableIdentity.Create("impact", change.Id, apiArtifact.Id),
            change.Id,
            apiArtifact.Id,
            ImpactSeverity.High,
            "The changed Vue contract affects its API call and downstream graph neighborhood.",
            0.95m,
            EvidenceLevel.Inferred,
            apiArtifact.EvidenceIds);
        vue = vue with { Changes = [change], Impacts = [impact] };
        var aspNet = await new AspNetAnalyzer().AnalyzeAsync(files, TestContext.Current.CancellationToken);
        var marten = await new MartenAnalyzer().AnalyzeAsync(files, TestContext.Current.CancellationToken);
        return new FixtureAnalyses(vue, aspNet, marten);
    }

    private static string FixtureRoot =>
        Path.Combine(RepositoryRoot, "samples", "knowledge-graph-full-application");

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PROJECT_PLAN.md")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
        }
    }

    private sealed record FixtureAnalyses(
        AnalysisResult Vue,
        AnalysisResult AspNet,
        AnalysisResult Marten)
    {
        public IReadOnlyList<AnalysisResult> All => [Vue, AspNet, Marten];
    }

    private sealed record ExpectedRetrieval(
        IReadOnlyList<string> RequiredArtifacts,
        IReadOnlyList<string> ExcludedArtifacts,
        IReadOnlyList<ExpectedEdge> RequiredEdges);

    private sealed record ExpectedEdge(
        string Source,
        string SourceKind,
        string Target,
        string TargetKind,
        string Kind);
}
