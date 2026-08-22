using System.Text.Json;
using Marten;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.Analyzers.AspNet;
using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Marten;
using VietAIS.TCFlow.Analyzers.Vue;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Governance.Tests;

public sealed class GovernanceEngineTests
{
    [Fact]
    public async Task DetectorProducesEvidenceBackedRepositoryConventions()
    {
        var graph = await BuildGraph();
        var profile = new ConventionDetector().Detect(graph);
        var expected = JsonSerializer.Deserialize<ExpectedConventions>(
            await File.ReadAllTextAsync(
                Path.Combine(RepositoryRoot, "samples", "governance", "expected", "conventions.json"),
                TestContext.Current.CancellationToken),
            AnalysisJson.Options)!;
        var actual = profile.Observations.Select(observation => new ExpectedObservation(
                observation.Kind,
                observation.Value))
            .ToArray();

        Assert.Equal(ConventionProfileStatus.Detected, profile.Status);
        Assert.Equal(
            JsonSerializer.Serialize(expected.Observations, AnalysisJson.Options),
            JsonSerializer.Serialize(actual, AnalysisJson.Options));
        Assert.All(profile.Observations, observation =>
        {
            Assert.NotEmpty(observation.EvidenceIds);
            Assert.NotEmpty(observation.ExamplePaths);
            Assert.InRange(observation.Confidence, 0.01m, 1m);
            Assert.NotEqual(EvidenceLevel.Proposed, observation.EvidenceLevel);
        });
    }

    [Fact]
    public async Task FrontendAndBackendAuthorityProduceDifferentExplainableImpacts()
    {
        var graph = await BuildGraph();
        var mismatch = graph.ContractMismatches.Single(item =>
            item.Kind == ContractMismatchKind.RequestFieldMissingBackend && item.Subject == "categoryId");
        var evaluator = new AuthorityImpactEvaluator();
        var frontendDecision = evaluator.Evaluate(mismatch, ConfiguredPolicy(AuthoritySourceKind.Frontend));
        var backendDecision = evaluator.Evaluate(mismatch, ConfiguredPolicy(AuthoritySourceKind.Backend));
        var expected = JsonSerializer.Deserialize<ExpectedAuthorityDecisions>(
            await File.ReadAllTextAsync(
                Path.Combine(RepositoryRoot, "samples", "governance", "expected", "authority-decisions.json"),
                TestContext.Current.CancellationToken),
            AnalysisJson.Options)!;

        AssertDecision(expected.FrontendAuthority, frontendDecision);
        AssertDecision(expected.BackendAuthority, backendDecision);
        Assert.NotEqual(frontendDecision.Action, backendDecision.Action);
        Assert.NotEqual(frontendDecision.TargetComponent, backendDecision.TargetComponent);
        Assert.Contains("Frontend is authoritative", frontendDecision.Explanation);
        Assert.Contains("Backend is authoritative", backendDecision.Explanation);
        Assert.Equal(mismatch.EvidenceLevel, frontendDecision.EvidenceLevel);
        Assert.Equal(mismatch.Confidence, frontendDecision.Confidence);
        Assert.NotEmpty(frontendDecision.EvidenceIds);
    }

    [Fact]
    public async Task OnboardingDefaultsRemainProposedUntilProjectConfiguration()
    {
        var graph = await BuildGraph();

        var policy = AuthorityPolicyDefaults.Suggest("project-1", graph);

        Assert.False(policy.IsConfigured);
        Assert.Equal(4, policy.Rules.Count);
        Assert.All(policy.Rules, rule =>
        {
            Assert.Equal(EvidenceLevel.Proposed, rule.EvidenceLevel);
            Assert.Equal(0.5m, rule.Confidence);
        });
        Assert.Equal(
            AuthoritySourceKind.Backend,
            policy.GetRequiredRule(AuthorityKnowledgeKind.ApiContract).Source);
        Assert.Equal(
            AuthoritySourceKind.Frontend,
            policy.GetRequiredRule(AuthorityKnowledgeKind.UiRequirement).Source);
    }

    [Fact]
    public async Task GeneratedPlansTargetExistingArtifactsAndDetectedNamingConventions()
    {
        var graph = await BuildGraph();
        var profile = new ConventionDetector().Detect(graph);
        var mismatch = graph.ContractMismatches.Single(item =>
            item.Kind == ContractMismatchKind.RequestFieldMissingBackend && item.Subject == "categoryId");
        var evaluator = new AuthorityImpactEvaluator();
        var builder = new ConventionAwarePlanBuilder();

        var backendPlan = builder.Build(
            evaluator.Evaluate(mismatch, ConfiguredPolicy(AuthoritySourceKind.Frontend)),
            mismatch,
            graph,
            profile);
        var frontendPlan = builder.Build(
            evaluator.Evaluate(mismatch, ConfiguredPolicy(AuthoritySourceKind.Backend)),
            mismatch,
            graph,
            profile);

        Assert.NotEmpty(backendPlan.Steps);
        Assert.Contains(backendPlan.Steps, step => step.ArtifactName == "CreateProductCommand");
        Assert.Contains(backendPlan.Steps, step => step.ArtifactName == "CreateProductCommandValidator");
        Assert.All(backendPlan.Steps, step =>
        {
            var artifact = graph.Artifacts.Single(item => item.Id == step.ArtifactId);
            Assert.Equal(artifact.Path, step.Path);
            Assert.True(artifact.Technology is "aspnet" or "marten");
        });
        Assert.Contains(profile.Observations, observation =>
            backendPlan.ConventionObservationIds.Contains(observation.Id) &&
            observation.Kind == ConventionKind.RequestDtoNaming &&
            observation.Value == "Command");

        Assert.NotEmpty(frontendPlan.Steps);
        Assert.All(frontendPlan.Steps, step => Assert.Equal(
            "vue",
            graph.Artifacts.Single(artifact => artifact.Id == step.ArtifactId).Technology));
        Assert.Contains(frontendPlan.Steps, step => step.Path == "src/views/CreateProductView.vue");
    }

    [Fact]
    public async Task MartenPersistsDetectedConventionProfileAndRejectsStaleRevision()
    {
        var graph = await BuildGraph();
        var profile = new ConventionDetector().Detect(graph);
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        using var store = DocumentStore.For(options =>
        {
            options.Connection(postgres.GetConnectionString());
            options.DatabaseSchemaName = "governance_test";
            ConventionProfileStorage.Configure(options);
        });
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        await using (var session = store.LightweightSession())
        {
            await new MartenConventionProfileWriter(session, TimeProvider.System)
                .SaveAsync(profile, TestContext.Current.CancellationToken);
        }

        await using (var session = store.QuerySession())
        {
            var loaded = await new MartenConventionProfileReader(session)
                .LoadAsync(profile.RepositoryId, TestContext.Current.CancellationToken);
            Assert.NotNull(loaded);
            Assert.Equal(
                JsonSerializer.Serialize(profile, AnalysisJson.Options),
                JsonSerializer.Serialize(loaded, AnalysisJson.Options));
        }

        var confirmed = profile with
        {
            Revision = profile.Revision + 1,
            Status = ConventionProfileStatus.Confirmed
        };
        await using (var session = store.LightweightSession())
        {
            await new MartenConventionProfileWriter(session, TimeProvider.System)
                .SaveAsync(confirmed, TestContext.Current.CancellationToken);
        }

        await using (var session = store.LightweightSession())
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new MartenConventionProfileWriter(session, TimeProvider.System)
                    .SaveAsync(profile, TestContext.Current.CancellationToken));
            Assert.Contains("not newer", exception.Message);
        }
    }

    private static async Task<RepositoryKnowledgeGraph> BuildGraph()
    {
        var vueFiles = await Discover("vue-full-application", ".vue", ".ts");
        var aspNetFiles = await Discover("aspnet-full-application", ".cs");
        var martenFiles = await Discover("marten-full-application", ".cs");
        var vue = await new VueAnalyzer().AnalyzeAsync(vueFiles, TestContext.Current.CancellationToken);
        var aspNet = await new AspNetAnalyzer().AnalyzeAsync(aspNetFiles, TestContext.Current.CancellationToken);
        var marten = await new MartenAnalyzer().AnalyzeAsync(martenFiles, TestContext.Current.CancellationToken);
        return new RepositoryKnowledgeGraphAssembler().Build(
            "governance-fixture",
            [vue, aspNet, marten]);
    }

    private static RepositoryAuthorityPolicy ConfiguredPolicy(AuthoritySourceKind apiSource) => new(
        "project-1",
        1,
        IsConfigured: true,
        [
            new KnowledgeAuthorityRule(
                AuthorityKnowledgeKind.ApiContract,
                apiSource,
                EvidenceLevel.Confirmed,
                1m,
                "Configured by the project owner.",
                [])
        ]);

    private static void AssertDecision(ExpectedDecision expected, AuthorityImpactDecision actual)
    {
        Assert.Equal(expected.Source, actual.AuthoritySource);
        Assert.Equal(expected.Action, actual.Action);
        Assert.Equal(expected.Target, actual.TargetComponent);
    }

    private static Task<IReadOnlyList<RepositoryFile>> Discover(string fixture, params string[] extensions) =>
        new FileDiscovery().DiscoverAsync(
            Path.Combine(RepositoryRoot, "samples", fixture),
            new FileDiscoveryOptions(new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase)),
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

    private sealed record ExpectedConventions(IReadOnlyList<ExpectedObservation> Observations);

    private sealed record ExpectedObservation(ConventionKind Kind, string Value);

    private sealed record ExpectedAuthorityDecisions(
        ExpectedDecision FrontendAuthority,
        ExpectedDecision BackendAuthority);

    private sealed record ExpectedDecision(
        AuthoritySourceKind Source,
        AuthorityImpactAction Action,
        PlanTargetComponent Target);
}
