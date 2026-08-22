using System.Text.Json;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Vue;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Vue.Tests;

public sealed class VueAnalyzerFixtureTests
{
    [Fact]
    public async Task FullApplicationFixtureMatchesExpectedGroundTruth()
    {
        var fixtureRoot = Path.Combine(RepositoryRoot, "samples", "vue-full-application");
        var files = await new FileDiscovery().DiscoverAsync(
            fixtureRoot,
            new FileDiscoveryOptions(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".vue", ".ts" }),
            TestContext.Current.CancellationToken);

        var result = await new VueAnalyzer().AnalyzeAsync(files, TestContext.Current.CancellationToken);
        var expectedPath = Path.Combine(fixtureRoot, "expected", "vue-analysis.json");
        var expected = JsonSerializer.Deserialize<ExpectedAnalysis>(
            await File.ReadAllTextAsync(expectedPath, TestContext.Current.CancellationToken),
            AnalysisJson.Options)!;

        foreach (var artifact in expected.Artifacts)
        {
            var actual = Assert.Single(result.Artifacts, actual =>
                actual.Kind.ToString().Equals(artifact.Kind, StringComparison.OrdinalIgnoreCase) &&
                actual.Name == artifact.Name &&
                actual.Path == artifact.Path &&
                actual.EvidenceLevel.ToString().Equals(artifact.EvidenceLevel, StringComparison.OrdinalIgnoreCase));
            foreach (var metadata in artifact.Metadata ?? new Dictionary<string, string>())
            {
                Assert.True(actual.Metadata.TryGetValue(metadata.Key, out var value));
                Assert.Equal(metadata.Value, value);
            }
        }

        var apiArtifacts = result.Artifacts
            .Where(artifact => artifact.Kind == ArtifactKind.ApiCall)
            .Select(artifact => new ExpectedApiArtifact(
                artifact.Metadata["method"],
                artifact.Metadata["route"],
                artifact.EvidenceLevel.ToString().ToLowerInvariant(),
                SplitMetadata(artifact.Metadata["responseUsage"])))
            .ToArray();
        Assert.Equal(
            JsonSerializer.Serialize(expected.ApiArtifacts, AnalysisJson.Options),
            JsonSerializer.Serialize(apiArtifacts, AnalysisJson.Options));

        var contracts = result.Contracts
            .Select(contract => new ExpectedContract(
                contract.HttpMethod,
                contract.Route,
                contract.EvidenceLevel.ToString().ToLowerInvariant(),
                contract.RequestFields.Select(field => field.Name).ToArray(),
                contract.ResponseFields.Select(field => field.Name).ToArray(),
                contract.HasPagination,
                contract.Permissions,
                contract.ErrorStates))
            .ToArray();
        Assert.Equal(
            JsonSerializer.Serialize(expected.Contracts, AnalysisJson.Options),
            JsonSerializer.Serialize(contracts, AnalysisJson.Options));
        var postEvidence = Assert.Single(result.Evidence, evidence =>
            evidence.Statement == "POST /api/products" && evidence.Extractor == "vue.api-call");
        Assert.Equal(EvidenceLevel.Confirmed, postEvidence.Level);
        Assert.Equal("src/views/CreateProductView.vue", postEvidence.Location.Path);
        Assert.True(postEvidence.Location.StartLine > 0);
        Assert.NotEmpty(result.Dependencies);
        Assert.All(result.Capabilities, capability =>
            Assert.Equal(EvidenceLevel.Inferred, capability.EvidenceLevel));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "VUE001" && diagnostic.Level == EvidenceLevel.Inferred);
    }

    [Fact]
    public async Task AnalysisIsDeterministicRegardlessOfInputOrder()
    {
        var fixtureRoot = Path.Combine(RepositoryRoot, "samples", "vue-full-application");
        var files = await new FileDiscovery().DiscoverAsync(
            fixtureRoot,
            new FileDiscoveryOptions(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".vue", ".ts" }),
            TestContext.Current.CancellationToken);
        var analyzer = new VueAnalyzer();

        var first = await analyzer.AnalyzeAsync(files, TestContext.Current.CancellationToken);
        var second = await analyzer.AnalyzeAsync(files.Reverse().ToArray(), TestContext.Current.CancellationToken);

        Assert.Equal(
            JsonSerializer.Serialize(first, AnalysisJson.Options),
            JsonSerializer.Serialize(second, AnalysisJson.Options));
    }

    [Fact]
    public async Task FormIntentWithoutApiCallDoesNotBecomeConfirmedApiEvidence()
    {
        var file = new RepositoryFile(
            "src/FormOnly.vue",
            "/fixture/src/FormOnly.vue",
            """
            <template><input v-model="name" required /></template>
            <script setup lang="ts">import { ref } from 'vue'; const name = ref('')</script>
            """);

        var result = await new VueAnalyzer().AnalyzeAsync([file], TestContext.Current.CancellationToken);

        Assert.Contains(result.Artifacts, artifact => artifact.Kind == ArtifactKind.FormField);
        Assert.Empty(result.Contracts);
        Assert.DoesNotContain(result.Evidence, evidence =>
            evidence.Extractor == "vue.api-call" && evidence.Level == EvidenceLevel.Confirmed);
    }

    private static string[] SplitMetadata(string value) => string.IsNullOrEmpty(value)
        ? []
        : value.Split(',', StringSplitOptions.RemoveEmptyEntries);

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

    private sealed record ExpectedAnalysis(
        IReadOnlyList<ExpectedArtifact> Artifacts,
        IReadOnlyList<ExpectedApiArtifact> ApiArtifacts,
        IReadOnlyList<ExpectedContract> Contracts);

    private sealed record ExpectedArtifact(
        string Kind,
        string Name,
        string Path,
        string EvidenceLevel,
        IReadOnlyDictionary<string, string>? Metadata);

    private sealed record ExpectedApiArtifact(
        string Method,
        string Route,
        string EvidenceLevel,
        IReadOnlyList<string> ResponseUsage);

    private sealed record ExpectedContract(
        string Method,
        string Route,
        string EvidenceLevel,
        IReadOnlyList<string> RequestFields,
        IReadOnlyList<string> ResponseFields,
        bool HasPagination,
        IReadOnlyList<string> Permissions,
        IReadOnlyList<string> ErrorStates);
}
