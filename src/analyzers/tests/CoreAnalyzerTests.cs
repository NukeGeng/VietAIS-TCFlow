using VietAIS.TCFlow.Analyzers.Core;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Vue.Tests;

public sealed class CoreAnalyzerTests
{
    [Theory]
    [InlineData("styles/theme.css", "body { color: red; }", "body { color: blue; }")]
    [InlineData(
        "src/App.vue",
        "<template><main /></template><style>main { color: red; }</style>",
        "<template><main /></template><style>main { color: blue; }</style>")]
    public void CosmeticChangesDoNotProduceCrossLayerImpactOrAiRequests(
        string path,
        string before,
        string after)
    {
        var result = new MeaningfulChangeFilter().Evaluate(new SourceFileChange(path, before, after));

        Assert.Equal(ChangeDecision.CosmeticOnly, result.Decision);
        Assert.False(result.HasCrossLayerPotential);
        Assert.Equal(0, result.RecommendedAiRequests);
        Assert.False(result.Change.IsMeaningful);
    }

    [Fact]
    public void ContractSignalIsMeaningfulAndRequestsSingleReconciliationPass()
    {
        var result = new MeaningfulChangeFilter().Evaluate(new SourceFileChange(
            "src/ProductForm.vue",
            "const save = () => undefined",
            "const save = () => api.post('/api/products', request)"));

        Assert.Equal(ChangeDecision.Meaningful, result.Decision);
        Assert.True(result.HasCrossLayerPotential);
        Assert.Equal(1, result.RecommendedAiRequests);
        Assert.True(result.Change.IsMeaningful);
    }

    [Fact]
    public async Task DiscoveryIsSortedAndIgnoresGeneratedDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tcflow-analyzer-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            Directory.CreateDirectory(Path.Combine(root, "node_modules"));
            Directory.CreateDirectory(Path.Combine(root, "bin"));
            var cancellationToken = TestContext.Current.CancellationToken;
            await File.WriteAllTextAsync(
                Path.Combine(root, "src", "z.ts"), "export const z = 1", cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(root, "src", "a.vue"), "<template />", cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(root, "node_modules", "ignored.ts"), string.Empty, cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(root, "bin", "ignored.vue"), string.Empty, cancellationToken);

            var files = await new FileDiscovery().DiscoverAsync(
                root,
                new FileDiscoveryOptions(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".vue", ".ts" }),
                TestContext.Current.CancellationToken);

            Assert.Equal(["src/a.vue", "src/z.ts"], files.Select(file => file.RelativePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void TechnologyDetectionUsesDirectSourceSignals()
    {
        var vue = new RepositoryFile("src/App.vue", "/fixture/src/App.vue", "<script setup lang=\"ts\"></script>");
        var martens = new RepositoryFile("src/Handler.cs", "/fixture/src/Handler.cs", "IDocumentSession session; await session.SaveChangesAsync(token);");

        Assert.Contains(TechnologyDetector.Detect(vue), detection =>
            detection.Technology == TechnologyKind.Vue && detection.EvidenceLevel == EvidenceLevel.Confirmed);
        Assert.Contains(TechnologyDetector.Detect(martens), detection =>
            detection.Technology == TechnologyKind.Marten && detection.EvidenceLevel == EvidenceLevel.Confirmed);
    }
}
