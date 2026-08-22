using System.Text.Json;
using VietAIS.TCFlow.Analyzers.AspNet;
using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Vue;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Contracts.Tests;

public sealed class ContractComparatorTests
{
    [Fact]
    public async Task CanonicalFixtureDetectsCategoryIdAndExplainableContractDifferences()
    {
        var vueRoot = Path.Combine(RepositoryRoot, "samples", "vue-full-application");
        var backendRoot = Path.Combine(RepositoryRoot, "samples", "aspnet-full-application");
        var vueFiles = await Discover(vueRoot, ".vue", ".ts");
        var backendFiles = await Discover(backendRoot, ".cs");
        var frontend = await new VueAnalyzer().AnalyzeAsync(vueFiles, TestContext.Current.CancellationToken);
        var backend = await new AspNetAnalyzer().AnalyzeAsync(backendFiles, TestContext.Current.CancellationToken);

        var result = new ContractComparator().Compare(frontend.Contracts, backend.Contracts);
        var frontendContract = frontend.Contracts.Single(contract =>
            contract.HttpMethod == "POST" && contract.Route == "/api/products");
        var pair = result.Pairs.Single(item => item.FrontendContractId == frontendContract.Id);
        var backendContract = backend.Contracts.Single(contract => contract.Id == pair.BackendContractId);
        var expected = JsonSerializer.Deserialize<ExpectedComparison>(
            await File.ReadAllTextAsync(
                Path.Combine(RepositoryRoot, "samples", "contract-comparison", "expected", "comparison.json"),
                TestContext.Current.CancellationToken),
            AnalysisJson.Options)!;
        var actual = new ExpectedComparison(
            new ExpectedPair(
                frontendContract.HttpMethod,
                frontendContract.Route,
                backendContract.HttpMethod,
                backendContract.Route,
                pair.Status,
                pair.EvidenceLevel,
                pair.Confidence),
            result.Mismatches.Where(mismatch => mismatch.ContractPairId == pair.Id)
                .Select(mismatch => new ExpectedMismatch(
                    mismatch.Kind,
                    mismatch.Subject,
                    mismatch.FrontendValue,
                    mismatch.BackendValue,
                    mismatch.EvidenceLevel,
                    mismatch.Confidence))
                .ToArray());

        AssertJsonEqual(expected, actual);
        var categoryMismatch = Assert.Single(result.Mismatches, mismatch =>
            mismatch.ContractPairId == pair.Id &&
            mismatch.Kind == ContractMismatchKind.RequestFieldMissingBackend &&
            mismatch.Subject == "categoryId");
        Assert.Contains(categoryMismatch.Locations, location =>
            location.Path == "src/views/CreateProductView.vue" && location.StartLine == 21);
        Assert.NotEmpty(categoryMismatch.EvidenceIds);
        var nameValidation = Assert.Single(result.Mismatches, mismatch =>
            mismatch.ContractPairId == pair.Id &&
            mismatch.Kind == ContractMismatchKind.RequestFieldValidation &&
            mismatch.Subject == "name");
        Assert.Contains(frontend.Evidence, evidence =>
            nameValidation.EvidenceIds.Contains(evidence.Id) &&
            evidence.Extractor == "vue.contract-validation" &&
            evidence.Location.Path == "src/views/CreateProductView.vue" &&
            evidence.Location.StartLine == 3);
    }

    [Fact]
    public void MatchingContractsProduceNoMismatchNoise()
    {
        var frontend = Contract(
            "frontend-match",
            ContractDirection.FrontendExpected,
            "PUT",
            "/api/v1/products/{id}",
            [Field("id", "string", true), Field("price", "number", true, "min:0")],
            [Field("id", "string", true)],
            ["400"],
            false,
            ["product.update"]);
        var backend = Contract(
            "backend-match",
            ContractDirection.BackendActual,
            "PUT",
            "/api/v{version:apiVersion}/products/{productId:guid}",
            [Field("Id", "Guid", true), Field("Price", "decimal", true, "min:0")],
            [Field("Id", "Guid", true)],
            ["400"],
            false,
            ["Permissions.Products.Update"]);

        var result = new ContractComparator().Compare([frontend], [backend]);

        var pair = Assert.Single(result.Pairs);
        Assert.Equal(ContractPairStatus.Matched, pair.Status);
        Assert.Equal(EvidenceLevel.Confirmed, pair.EvidenceLevel);
        Assert.Equal(1m, pair.Confidence);
        Assert.Empty(result.Mismatches);
        Assert.Empty(result.UnmatchedBackendContractIds);
    }

    [Fact]
    public void ComparatorReportsEveryContractDimensionWithSourceLocations()
    {
        var frontend = Contract(
            "frontend-differences",
            ContractDirection.FrontendExpected,
            "PATCH",
            "/api/widgets",
            [Field("amount", "string", true, "maxlength:5")],
            [Field("enabled", "boolean", true)],
            ["400"],
            true,
            ["widget.update"]);
        var backend = Contract(
            "backend-differences",
            ContractDirection.BackendActual,
            "POST",
            "/api/widgets",
            [Field("Amount", "decimal", false, "maximumLength:6"), Field("Token", "string", true)],
            [Field("Enabled", "string", false)],
            ["422"],
            false,
            ["widget.create"]);

        var result = new ContractComparator().Compare([frontend], [backend]);

        var kinds = result.Mismatches.Select(mismatch => mismatch.Kind).ToHashSet();
        Assert.Equal(
            new HashSet<ContractMismatchKind>
            {
                ContractMismatchKind.Method,
                ContractMismatchKind.RequestFieldMissingFrontend,
                ContractMismatchKind.RequestFieldType,
                ContractMismatchKind.RequestFieldOptionality,
                ContractMismatchKind.RequestFieldValidation,
                ContractMismatchKind.ResponseFieldType,
                ContractMismatchKind.ResponseFieldOptionality,
                ContractMismatchKind.ErrorStates,
                ContractMismatchKind.Pagination,
                ContractMismatchKind.Authorization
            },
            kinds);
        Assert.All(result.Mismatches, mismatch =>
        {
            Assert.Equal(EvidenceLevel.Confirmed, mismatch.EvidenceLevel);
            Assert.Equal(1m, mismatch.Confidence);
            Assert.NotEmpty(mismatch.EvidenceIds);
        });
        Assert.All(result.Mismatches.Where(mismatch => mismatch.Subject is "amount" or "Token" or "enabled"),
            mismatch => Assert.NotEmpty(mismatch.Locations));
    }

    [Fact]
    public void AmbiguousRouteCandidatesRemainInferredAndDoNotEmitMismatches()
    {
        var frontend = Contract(
            "frontend-ambiguous",
            ContractDirection.FrontendExpected,
            "POST",
            "/api/products");
        var catalog = Contract(
            "backend-catalog",
            ContractDirection.BackendActual,
            "POST",
            "/api/catalog/products");
        var inventory = Contract(
            "backend-inventory",
            ContractDirection.BackendActual,
            "POST",
            "/api/inventory/products");

        var result = new ContractComparator().Compare([frontend], [catalog, inventory]);

        var pair = Assert.Single(result.Pairs);
        Assert.Equal(ContractPairStatus.Ambiguous, pair.Status);
        Assert.Equal(EvidenceLevel.Inferred, pair.EvidenceLevel);
        Assert.Equal(0.55m, pair.Confidence);
        Assert.Null(pair.BackendContractId);
        Assert.Equal(2, pair.CandidateBackendContractIds.Count);
        Assert.Equal(3, pair.EvidenceIds.Count);
        Assert.Empty(result.Mismatches);
        Assert.Equal(2, result.UnmatchedBackendContractIds.Count);
    }

    [Fact]
    public void ComparisonIsDeterministicRegardlessOfInputOrder()
    {
        var firstFrontend = Contract(
            "frontend-first",
            ContractDirection.FrontendExpected,
            "GET",
            "/api/projects");
        var secondFrontend = Contract(
            "frontend-second",
            ContractDirection.FrontendExpected,
            "POST",
            "/api/products");
        var firstBackend = Contract(
            "backend-first",
            ContractDirection.BackendActual,
            "POST",
            "/api/catalog/products");
        var secondBackend = Contract(
            "backend-second",
            ContractDirection.BackendActual,
            "GET",
            "/api/projects");
        var comparator = new ContractComparator();

        var first = comparator.Compare([firstFrontend, secondFrontend], [firstBackend, secondBackend]);
        var second = comparator.Compare([secondFrontend, firstFrontend], [secondBackend, firstBackend]);

        Assert.Equal(
            JsonSerializer.Serialize(first, AnalysisJson.Options),
            JsonSerializer.Serialize(second, AnalysisJson.Options));
    }

    [Fact]
    public void ProposedSourceEvidenceIsNeverPromotedByRouteInference()
    {
        var frontend = Contract(
            "frontend-proposed",
            ContractDirection.FrontendExpected,
            "POST",
            "/api/products",
            evidenceLevel: EvidenceLevel.Proposed);
        var backend = Contract(
            "backend-confirmed",
            ContractDirection.BackendActual,
            "POST",
            "/api/catalog/products");

        var result = new ContractComparator().Compare([frontend], [backend]);

        var pair = Assert.Single(result.Pairs);
        Assert.Equal(ContractPairStatus.Matched, pair.Status);
        Assert.Equal(EvidenceLevel.Proposed, pair.EvidenceLevel);
        Assert.Equal(0.5m, pair.Confidence);
        Assert.All(result.Mismatches, mismatch => Assert.Equal(EvidenceLevel.Proposed, mismatch.EvidenceLevel));
    }

    private static Contract Contract(
        string id,
        ContractDirection direction,
        string method,
        string route,
        IReadOnlyList<ContractField>? request = null,
        IReadOnlyList<ContractField>? response = null,
        IReadOnlyList<string>? errors = null,
        bool pagination = false,
        IReadOnlyList<string>? permissions = null,
        EvidenceLevel evidenceLevel = EvidenceLevel.Confirmed) => new(
            id,
            direction,
            method,
            route,
            evidenceLevel,
            request ?? [],
            response ?? [],
            errors ?? [],
            pagination,
            permissions ?? [],
            [$"evidence-{id}"]);

    private static ContractField Field(string name, string type, bool required, params string[] validations) => new(
        name,
        type,
        required,
        EvidenceLevel.Confirmed,
        new SourceLocation($"fixture/{name}.source", 1, 1, name))
    {
        Validations = validations
    };

    private static Task<IReadOnlyList<RepositoryFile>> Discover(string root, params string[] extensions) =>
        new FileDiscovery().DiscoverAsync(
            root,
            new FileDiscoveryOptions(new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase)),
            TestContext.Current.CancellationToken);

    private static void AssertJsonEqual<T>(T expected, T actual) => Assert.Equal(
        JsonSerializer.Serialize(expected, AnalysisJson.Options),
        JsonSerializer.Serialize(actual, AnalysisJson.Options));

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

    private sealed record ExpectedComparison(ExpectedPair Pair, IReadOnlyList<ExpectedMismatch> Mismatches);

    private sealed record ExpectedPair(
        string FrontendMethod,
        string FrontendRoute,
        string BackendMethod,
        string BackendRoute,
        ContractPairStatus Status,
        EvidenceLevel EvidenceLevel,
        decimal Confidence);

    private sealed record ExpectedMismatch(
        ContractMismatchKind Kind,
        string Subject,
        string FrontendValue,
        string BackendValue,
        EvidenceLevel EvidenceLevel,
        decimal Confidence);
}
