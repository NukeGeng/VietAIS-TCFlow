using System.Text.Json;
using VietAIS.TCFlow.Analyzers.AspNet;
using VietAIS.TCFlow.Analyzers.Core;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.AspNet.Tests;

public sealed class AspNetAnalyzerFixtureTests
{
    [Fact]
    public async Task FullStackHeroAndTcFlowFixtureMatchesExpectedGroundTruth()
    {
        var fixtureRoot = Path.Combine(RepositoryRoot, "samples", "aspnet-full-application");
        var result = await AnalyzeFixture(fixtureRoot);
        var expected = JsonSerializer.Deserialize<ExpectedAnalysis>(
            await File.ReadAllTextAsync(
                Path.Combine(fixtureRoot, "expected", "aspnet-analysis.json"),
                TestContext.Current.CancellationToken),
            AnalysisJson.Options)!;

        var endpoints = result.Artifacts
            .Where(artifact => artifact.Kind == ArtifactKind.AspNetEndpoint)
            .Select(artifact =>
            {
                var evidence = result.Evidence.Single(item => artifact.EvidenceIds.Contains(item.Id) &&
                    item.Extractor == "aspnet.endpoint");
                return new ExpectedEndpoint(
                    artifact.Name,
                    artifact.Path,
                    evidence.Location.StartLine,
                    artifact.Metadata["method"],
                    artifact.Metadata["route"],
                    artifact.Metadata["commandType"],
                    artifact.Metadata["responseType"],
                    artifact.Metadata["successStatus"],
                    artifact.Metadata["apiVersion"]);
            })
            .OrderBy(endpoint => endpoint.Route, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal)
            .ToArray();
        AssertJsonEqual(expected.Endpoints, endpoints);

        var contracts = result.Contracts.Select(contract => new ExpectedContract(
            contract.HttpMethod,
            contract.Route,
            contract.EvidenceLevel.ToString().ToLowerInvariant(),
            contract.RequestFields.Select(ProjectField).ToArray(),
            contract.ResponseFields.Select(ProjectField).ToArray(),
            contract.ErrorStates,
            contract.Permissions,
            contract.HasPagination)).ToArray();
        AssertJsonEqual(expected.Contracts, contracts);

        var openApiOperations = result.Artifacts
            .Where(artifact => artifact.Kind == ArtifactKind.OpenApiOperation)
            .Select(artifact =>
            {
                var evidence = result.Evidence.Single(item => artifact.EvidenceIds.Contains(item.Id));
                return new ExpectedOpenApiOperation(
                    artifact.Name,
                    artifact.Path,
                    evidence.Location.StartLine,
                    artifact.Metadata["summary"],
                    artifact.Metadata["description"],
                    artifact.Metadata["responseType"],
                    artifact.Metadata["successStatus"],
                    SplitMetadata(artifact.Metadata["errorStatuses"]),
                    artifact.Metadata["apiVersion"]);
            })
            .OrderBy(operation => operation.Path, StringComparer.Ordinal)
            .ThenBy(operation => operation.Line)
            .ToArray();
        AssertJsonEqual(expected.OpenApiOperations, openApiOperations);

        foreach (var artifact in expected.Artifacts)
        {
            Assert.Single(result.Artifacts, actual =>
                actual.Kind.ToString().Equals(artifact.Kind, StringComparison.OrdinalIgnoreCase) &&
                actual.Name == artifact.Name);
        }

        Assert.Empty(result.Diagnostics);
        Assert.All(result.Contracts, contract => Assert.Equal(EvidenceLevel.Confirmed, contract.EvidenceLevel));
        Assert.DoesNotContain(result.Evidence, evidence => evidence.Level == EvidenceLevel.Proposed);
        AssertAuthorizationEvidence(
            result,
            "Authorization requires Permissions.Products.Create.",
            "src/api/modules/Catalog/Catalog.Infrastructure/Endpoints/v1/CreateProductEndpoint.cs",
            24);
        AssertAuthorizationEvidence(
            result,
            "Authorization requires authenticated.",
            "src/api/modules/RepositoryIntelligence/Management/ProjectManagementEndpoints.cs",
            16);
        AssertAuthorizationEvidence(
            result,
            "Authorization requires project.view.",
            "src/api/modules/RepositoryIntelligence/Management/ProjectFeatures.cs",
            53);

        var createEndpoint = result.Artifacts.Single(artifact =>
            artifact.Kind == ArtifactKind.AspNetEndpoint && artifact.Name == "CreateProductEndpoint");
        var createHandler = result.Artifacts.Single(artifact =>
            artifact.Kind == ArtifactKind.Handler && artifact.Name == "CreateProductHandler");
        var writer = result.Artifacts.Single(artifact =>
            artifact.Kind == ArtifactKind.Interface && artifact.Name == "IProductWriter");
        Assert.Contains(result.Dependencies, dependency =>
            dependency.SourceArtifactId == createEndpoint.Id &&
            dependency.Target == createHandler.Id &&
            dependency.Kind == DependencyKind.DelegatesTo);
        Assert.Contains(result.Dependencies, dependency =>
            dependency.SourceArtifactId == createHandler.Id &&
            dependency.Target == writer.Id &&
            dependency.Kind == DependencyKind.Uses);
    }

    [Fact]
    public async Task AnalysisIsDeterministicRegardlessOfInputOrder()
    {
        var fixtureRoot = Path.Combine(RepositoryRoot, "samples", "aspnet-full-application");
        var files = await DiscoverFixture(fixtureRoot);
        var analyzer = new AspNetAnalyzer();

        var first = await analyzer.AnalyzeAsync(files, TestContext.Current.CancellationToken);
        var second = await analyzer.AnalyzeAsync(files.Reverse().ToArray(), TestContext.Current.CancellationToken);

        Assert.Equal(
            JsonSerializer.Serialize(first, AnalysisJson.Options),
            JsonSerializer.Serialize(second, AnalysisJson.Options));
    }

    [Fact]
    public async Task UnresolvedRoutePrefixRemainsInferredAndProducesDiagnostic()
    {
        var file = new RepositoryFile(
            "Features/LooseEndpoint.cs",
            "/fixture/Features/LooseEndpoint.cs",
            """
            public static class LooseEndpoint
            {
                public static void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints.MapGet("items", () => Results.Ok()).WithName("Loose");
            }
            """);

        var result = await new AspNetAnalyzer().AnalyzeAsync([file], TestContext.Current.CancellationToken);

        var contract = Assert.Single(result.Contracts);
        Assert.Equal(EvidenceLevel.Inferred, contract.EvidenceLevel);
        Assert.Equal("/items", contract.Route);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ASPNET001", diagnostic.Code);
        Assert.Equal(EvidenceLevel.Inferred, diagnostic.Level);
    }

    private static ExpectedField ProjectField(ContractField field) => new(
        field.Name,
        field.Type,
        field.Required,
        field.Validations);

    private static string[] SplitMetadata(string value) => string.IsNullOrEmpty(value)
        ? []
        : value.Split(',', StringSplitOptions.RemoveEmptyEntries);

    private static void AssertAuthorizationEvidence(
        AnalysisResult result,
        string statement,
        string path,
        int line)
    {
        var evidence = Assert.Single(result.Evidence, item =>
            item.Extractor == "aspnet.authorization" && item.Statement == statement);
        Assert.Equal(path, evidence.Location.Path);
        Assert.Equal(line, evidence.Location.StartLine);
        Assert.Equal(EvidenceLevel.Confirmed, evidence.Level);
    }

    private static void AssertJsonEqual<T>(T expected, T actual) => Assert.Equal(
        JsonSerializer.Serialize(expected, AnalysisJson.Options),
        JsonSerializer.Serialize(actual, AnalysisJson.Options));

    private static async Task<AnalysisResult> AnalyzeFixture(string fixtureRoot)
    {
        var files = await DiscoverFixture(fixtureRoot);
        return await new AspNetAnalyzer().AnalyzeAsync(files, TestContext.Current.CancellationToken);
    }

    private static Task<IReadOnlyList<RepositoryFile>> DiscoverFixture(string fixtureRoot) =>
        new FileDiscovery().DiscoverAsync(
            fixtureRoot,
            new FileDiscoveryOptions(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cs" }),
            TestContext.Current.CancellationToken);

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
        IReadOnlyList<ExpectedEndpoint> Endpoints,
        IReadOnlyList<ExpectedContract> Contracts,
        IReadOnlyList<ExpectedOpenApiOperation> OpenApiOperations,
        IReadOnlyList<ExpectedArtifact> Artifacts);

    private sealed record ExpectedEndpoint(
        string Name,
        string Path,
        int Line,
        string Method,
        string Route,
        string CommandType,
        string ResponseType,
        string SuccessStatus,
        string ApiVersion);

    private sealed record ExpectedContract(
        string Method,
        string Route,
        string EvidenceLevel,
        IReadOnlyList<ExpectedField> RequestFields,
        IReadOnlyList<ExpectedField> ResponseFields,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Permissions,
        bool HasPagination);

    private sealed record ExpectedField(
        string Name,
        string Type,
        bool Required,
        IReadOnlyList<string> Validations);

    private sealed record ExpectedOpenApiOperation(
        string Name,
        string Path,
        int Line,
        string Summary,
        string Description,
        string ResponseType,
        string SuccessStatus,
        IReadOnlyList<string> ErrorStatuses,
        string ApiVersion);

    private sealed record ExpectedArtifact(string Kind, string Name);
}
