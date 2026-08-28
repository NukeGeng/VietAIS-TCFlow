using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Monitoring;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Monitoring.Tests;

public sealed class InitialRepositoryAnalysisTests
{
    [Fact]
    public async Task FullScanBuildsVersionedKnowledgeFromApplicableAnalyzers()
    {
        var source = new StaticSnapshotSource(new RepositorySnapshot(
            "commit-123",
            [new RepositoryFile("src/App.vue", "/snapshot/src/App.vue", "<template><main /></template>")]));
        var service = new InitialRepositoryAnalysisService(source, [new StaticAnalyzer("fixture-v1", applies: true)]);

        var result = await service.ProcessAsync(
            InitialWorkItem(),
            graphRevision: 4,
            TestContext.Current.CancellationToken);

        Assert.Equal(InitialRepositoryAnalysisStatus.Completed, result.Status);
        Assert.Equal("commit-123", result.SourceRevision);
        Assert.Equal(4, result.Graph.Revision);
        Assert.Single(result.Graph.Artifacts);
        Assert.Single(result.Graph.Evidence);
        Assert.Contains(result.Technologies, technology => technology.Technology == TechnologyKind.Vue);
        Assert.Equal("project-1", result.SuggestedAuthority.ProjectId);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "ANALYSIS001");
    }

    [Fact]
    public async Task UnsupportedRepositoryIsReportedWithoutInventingSourceFacts()
    {
        var source = new StaticSnapshotSource(new RepositorySnapshot(
            "commit-next",
            [
                new RepositoryFile(
                    "package.json",
                    "/snapshot/package.json",
                    "{\"dependencies\":{\"next\":\"latest\",\"react\":\"latest\"}}"),
                new RepositoryFile(
                    "src/app/page.tsx",
                    "/snapshot/src/app/page.tsx",
                    "export default function Page() { return <main>Portfolio</main>; }")
            ]));
        var service = new InitialRepositoryAnalysisService(source, [new StaticAnalyzer("fixture-v1", applies: false)]);

        var result = await service.ProcessAsync(
            InitialWorkItem(),
            graphRevision: 1,
            TestContext.Current.CancellationToken);

        Assert.Equal(InitialRepositoryAnalysisStatus.Unsupported, result.Status);
        Assert.Empty(result.Analyses);
        Assert.Empty(result.Graph.Artifacts);
        Assert.Contains(result.Technologies, technology => technology.Technology == TechnologyKind.TypeScript);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ANALYSIS001", diagnostic.Code);
        Assert.Equal(EvidenceLevel.Confirmed, diagnostic.Level);
    }

    [Fact]
    public async Task UnsafeOrDuplicateSnapshotPathsAreRejectedBeforeAnalysis()
    {
        var unsafeService = new InitialRepositoryAnalysisService(
            new StaticSnapshotSource(new RepositorySnapshot(
                "commit-unsafe",
                [new RepositoryFile("../secret.txt", "/secret.txt", "secret")])),
            [new StaticAnalyzer("fixture-v1", applies: true)]);
        var duplicateService = new InitialRepositoryAnalysisService(
            new StaticSnapshotSource(new RepositorySnapshot(
                "commit-duplicate",
                [
                    new RepositoryFile("src/App.vue", "/one/App.vue", "one"),
                    new RepositoryFile("src\\App.vue", "/two/App.vue", "two")
                ])),
            [new StaticAnalyzer("fixture-v1", applies: true)]);

        var unsafeError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unsafeService.ProcessAsync(
                InitialWorkItem(),
                graphRevision: 1,
                TestContext.Current.CancellationToken));
        var duplicateError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            duplicateService.ProcessAsync(
                InitialWorkItem(),
                graphRevision: 1,
                TestContext.Current.CancellationToken));

        Assert.Contains("safe repository-relative", unsafeError.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate path", duplicateError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IncrementalWorkAndDuplicateAnalyzerNamesAreRejected()
    {
        var source = new StaticSnapshotSource(new RepositorySnapshot("commit-1", []));
        var duplicateService = new InitialRepositoryAnalysisService(
            source,
            [new StaticAnalyzer("duplicate", applies: true), new StaticAnalyzer("duplicate", applies: true)]);
        var validService = new InitialRepositoryAnalysisService(
            source,
            [new StaticAnalyzer("fixture-v1", applies: true)]);
        var incremental = InitialWorkItem() with
        {
            Kind = RepositoryAnalysisKind.Incremental,
            Trigger = RepositoryAnalysisTrigger.Push,
            ChangedPaths = [new RepositoryChangedPath("src/App.vue", ChangeKind.Modified)]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            duplicateService.ProcessAsync(
                InitialWorkItem(),
                graphRevision: 1,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            validService.ProcessAsync(
                incremental,
                graphRevision: 1,
                TestContext.Current.CancellationToken));
    }

    private static RepositoryAnalysisWorkItem InitialWorkItem() => new(
        "request-1",
        "project-1",
        "repository-1",
        "request-1",
        "github",
        RepositoryAnalysisKind.FullScan,
        RepositoryAnalysisTrigger.InitialScan,
        null,
        null,
        "refs/heads/main",
        null,
        RequiresContentFetch: false,
        [],
        DateTimeOffset.Parse("2026-08-23T00:00:00Z"),
        RepositoryAnalysisRequesterKind.User,
        "actor-1");

    private sealed class StaticSnapshotSource(RepositorySnapshot snapshot) : IRepositorySnapshotSource
    {
        public Task<RepositorySnapshot> LoadAsync(
            RepositoryAnalysisWorkItem workItem,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }
    }

    private sealed class StaticAnalyzer(string name, bool applies)
        : IRepositoryAnalyzer, IRepositoryAnalyzerApplicability
    {
        public string Name => name;

        public bool Supports(RepositoryFile file) => true;

        public bool SupportsRepository(IReadOnlyCollection<RepositoryFile> files) => applies;

        public Task<AnalysisResult> AnalyzeAsync(
            IReadOnlyCollection<RepositoryFile> files,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var evidence = new Evidence(
                "evidence-1",
                "A Vue component exists.",
                EvidenceLevel.Confirmed,
                new SourceLocation("src/App.vue", 1, 1, "App"),
                Name,
                1m);
            var artifact = new Artifact(
                "artifact-1",
                ArtifactKind.VueComponent,
                "vue",
                "App",
                "src/App.vue",
                EvidenceLevel.Confirmed,
                [evidence.Id],
                new Dictionary<string, string>());
            return Task.FromResult(new AnalysisResult(
                Name,
                "fixture",
                [artifact],
                [],
                [evidence],
                [],
                [],
                [],
                [],
                []));
        }
    }
}
