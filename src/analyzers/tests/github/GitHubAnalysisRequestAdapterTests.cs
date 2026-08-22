using System.Text;
using System.Text.Json;
using VietAIS.TCFlow.Analyzers.Core;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.GitHub.Tests;

public sealed class GitHubAnalysisRequestAdapterTests
{
    private static readonly Guid RequestId = Guid.Parse("1a111111-1111-1111-1111-111111111111");
    private static readonly Guid ProjectId = Guid.Parse("2b222222-2222-2222-2222-222222222222");
    private static readonly Guid RepositoryId = Guid.Parse("3c333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Backend_contract_fixture_maps_to_incremental_work_item()
    {
        var payload = await File.ReadAllBytesAsync(
            Fixture("samples", "github", "analysis-request.push.json"),
            TestContext.Current.CancellationToken);

        var workItem = new GitHubAnalysisRequestAdapter().DeserializeAndAdapt(payload);

        Assert.Equal(RequestId.ToString("D"), workItem.RequestId);
        Assert.Equal(ProjectId.ToString("D"), workItem.ProjectId);
        Assert.Equal(RepositoryId.ToString("D"), workItem.RepositoryId);
        Assert.Equal("delivery-push-001", workItem.CorrelationId);
        Assert.Equal("github", workItem.SourceProvider);
        Assert.Equal(RepositoryAnalysisKind.Incremental, workItem.Kind);
        Assert.Equal(RepositoryAnalysisTrigger.Push, workItem.Trigger);
        Assert.Equal("base-sha", workItem.BaseRevision);
        Assert.Equal("head-sha", workItem.HeadRevision);
        Assert.Equal("refs/heads/main", workItem.Reference);
        Assert.False(workItem.RequiresContentFetch);
        Assert.Equal(RepositoryAnalysisRequesterKind.System, workItem.RequesterKind);
        Assert.Collection(
            workItem.ChangedPaths,
            file =>
            {
                Assert.Equal("src/added.cs", file.Path);
                Assert.Equal(ChangeKind.Added, file.Kind);
            },
            file =>
            {
                Assert.Equal("src/removed.cs", file.Path);
                Assert.Equal(ChangeKind.Deleted, file.Kind);
            });
    }

    [Fact]
    public void Initial_scan_uses_request_identity_for_correlation_and_contains_no_incremental_metadata()
    {
        var request = ValidRequest() with
        {
            Trigger = GitHubAnalysisTrigger.InitialScan,
            DeliveryId = null,
            BaseRevision = null,
            HeadRevision = null,
            FullScan = true,
            ChangedFiles = [],
            RequestedByType = "user",
            RequestedBy = Guid.Parse("4d444444-4444-4444-4444-444444444444")
        };

        var workItem = new GitHubAnalysisRequestAdapter().Adapt(request);

        Assert.Equal(RepositoryAnalysisKind.FullScan, workItem.Kind);
        Assert.Equal(RepositoryAnalysisTrigger.InitialScan, workItem.Trigger);
        Assert.Equal(RequestId.ToString("D"), workItem.CorrelationId);
        Assert.Empty(workItem.ChangedPaths);
        Assert.Equal(RepositoryAnalysisRequesterKind.User, workItem.RequesterKind);
        Assert.Equal(request.RequestedBy?.ToString("D"), workItem.RequestedBy);
    }

    [Theory]
    [InlineData(GitHubAnalysisTrigger.PullRequest, RepositoryAnalysisTrigger.PullRequest)]
    [InlineData(GitHubAnalysisTrigger.Merge, RepositoryAnalysisTrigger.Merge)]
    public void Pull_request_and_merge_require_deferred_changed_file_fetch(
        GitHubAnalysisTrigger trigger,
        RepositoryAnalysisTrigger expectedTrigger)
    {
        var request = ValidRequest() with
        {
            Trigger = trigger,
            PullRequestNumber = 42,
            RequiresChangedFileFetch = true,
            ChangedFiles = []
        };

        var workItem = new GitHubAnalysisRequestAdapter().Adapt(request);

        Assert.Equal(expectedTrigger, workItem.Trigger);
        Assert.Equal(42, workItem.PullRequestNumber);
        Assert.True(workItem.RequiresContentFetch);
    }

    [Fact]
    public void Adapter_rejects_non_pending_requests_and_unsafe_or_duplicate_paths()
    {
        var adapter = new GitHubAnalysisRequestAdapter();
        Assert.Throws<InvalidOperationException>(() => adapter.Adapt(
            ValidRequest() with { Status = GitHubAnalysisRequestStatus.Processing }));
        Assert.Throws<InvalidOperationException>(() => adapter.Adapt(
            ValidRequest() with { RequestedByType = "user", RequestedBy = null }));
        Assert.Throws<InvalidOperationException>(() => adapter.Adapt(
            ValidRequest() with
            {
                ChangedFiles = [new GitHubChangedFileContract("../secret.txt", GitHubChangedFileStatus.Modified)]
            }));
        Assert.Throws<InvalidOperationException>(() => adapter.Adapt(
            ValidRequest() with
            {
                ChangedFiles =
                [
                    new GitHubChangedFileContract("src/App.vue", GitHubChangedFileStatus.Modified),
                    new GitHubChangedFileContract("src/App.vue", GitHubChangedFileStatus.Modified)
                ]
            }));
    }

    [Fact]
    public void Adapter_rejects_invalid_trigger_shapes_and_malformed_json()
    {
        var adapter = new GitHubAnalysisRequestAdapter();
        Assert.Throws<InvalidOperationException>(() => adapter.Adapt(
            ValidRequest() with { FullScan = true }));
        Assert.Throws<InvalidOperationException>(() => adapter.Adapt(
            ValidRequest() with { DeliveryId = null }));
        Assert.Throws<InvalidOperationException>(() => adapter.Adapt(
            ValidRequest() with { Trigger = (GitHubAnalysisTrigger)99 }));
        Assert.Throws<InvalidOperationException>(() => adapter.DeserializeAndAdapt(
            Encoding.UTF8.GetBytes("{not-json}")));
    }

    [Fact]
    public void Contract_serialization_remains_compatible_with_numeric_backend_enums()
    {
        var request = ValidRequest();
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            request,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var workItem = new GitHubAnalysisRequestAdapter().DeserializeAndAdapt(payload);

        Assert.Equal(RepositoryAnalysisTrigger.Push, workItem.Trigger);
        Assert.Equal(ChangeKind.Modified, Assert.Single(workItem.ChangedPaths).Kind);
    }

    private static GitHubAnalysisRequestContract ValidRequest() => new(
        RequestId,
        ProjectId,
        RepositoryId,
        GitHubAnalysisTrigger.Push,
        "delivery-push-001",
        "base-sha",
        "head-sha",
        "refs/heads/main",
        null,
        FullScan: false,
        RequiresChangedFileFetch: false,
        [new GitHubChangedFileContract("src/App.vue", GitHubChangedFileStatus.Modified)],
        GitHubAnalysisRequestStatus.Pending,
        DateTimeOffset.Parse("2026-08-22T08:30:00Z"),
        "system",
        null);

    private static string Fixture(params string[] parts)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "GOAL.md")))
        {
            root = root.Parent;
        }

        return root is null
            ? throw new InvalidOperationException("Repository root could not be located.")
            : Path.Combine([root.FullName, .. parts]);
    }
}
