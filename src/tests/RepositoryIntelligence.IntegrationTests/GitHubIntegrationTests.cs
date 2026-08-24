using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asp.Versioning.Conventions;
using Carter;
using FSH.Framework.Infrastructure.Exceptions;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Monitoring;
using VietAIS.TCFlow.Analyzers.Reasoning;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;
using Xunit;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.IntegrationTests;

public sealed class GitHubIntegrationTests
{
    private const string WebhookSecret = "tcflow-integration-webhook-secret-123456";

    [Fact]
    public async Task Installation_selection_and_initial_scan_enforce_permissions_scope_and_audit()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(postgres.GetConnectionString());
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var project = await CreateProjectAsync(app.Services, ownerId);
        using var client = CreateClient(app);
        var connectionRoute = $"api/v1/projects/{project.Id}/github/connections";

        var unauthenticated = await client.PostAsync(
            connectionRoute,
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, outsiderId.ToString());
        var forbidden = await client.PostAsync(
            connectionRoute,
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.UserHeader);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, ownerId.ToString());
        var startedResponse = await client.PostAsync(
            connectionRoute,
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, startedResponse.StatusCode);
        var started = await startedResponse.Content.ReadFromJsonAsync<GitHubInstallationStart>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(started);
        var installationState = QueryValue(started.InstallationUrl, "state");

        var authorizationResponse = await client.PostAsJsonAsync(
            "api/v1/github/connections/authorize",
            new PrepareGitHubAuthorizationRequest(installationState, 101),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, authorizationResponse.StatusCode);
        var authorization = await authorizationResponse.Content
            .ReadFromJsonAsync<GitHubAuthorizationStart>(TestContext.Current.CancellationToken);
        Assert.NotNull(authorization);
        Assert.Equal(project.Id, authorization.ProjectId);
        Assert.Equal(authorization.State, QueryValue(authorization.AuthorizationUrl, "state"));

        var completedResponse = await client.PostAsJsonAsync(
            "api/v1/github/connections/complete",
            new CompleteGitHubConnectionRequest(
                authorization.State,
                "one-time-oauth-code",
                authorization.CodeVerifier),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        var completed = await completedResponse.Content.ReadFromJsonAsync<GitHubConnectionResult>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(completed);
        Assert.Equal(project.Id, completed.ProjectId);
        Assert.Equal(101, completed.Installation.InstallationId);
        Assert.Contains(completed.Repositories, repository => repository.Id == 303 && repository.Private);

        var replay = await client.PostAsJsonAsync(
            "api/v1/github/connections/complete",
            new CompleteGitHubConnectionRequest(
                authorization.State,
                "one-time-oauth-code",
                authorization.CodeVerifier),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        var unavailableRepository = await client.PostAsJsonAsync(
            $"api/v1/projects/{project.Id}/github/repositories",
            new ConnectGitHubRepositoryRequest(101, 999),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, unavailableRepository.StatusCode);

        var connectedResponse = await client.PostAsJsonAsync(
            $"api/v1/projects/{project.Id}/github/repositories",
            new ConnectGitHubRepositoryRequest(101, 303),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, connectedResponse.StatusCode);
        var connected = await connectedResponse.Content.ReadFromJsonAsync<ConnectedGitHubRepository>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(connected);
        Assert.Equal(RepositoryProviderKind.GitHub, connected.Repository.Provider);
        Assert.Equal(RepositoryLifecycleStatus.Active, connected.Repository.Status);
        Assert.Equal("https://github.com/NukeGeng/VietAIS-TCFlow", connected.Repository.RemoteUrl);
        Assert.True(connected.Access.IsSelected);

        var scanRoute =
            $"api/v1/projects/{project.Id}/github/repositories/{connected.Repository.Id}/initial-scan";
        var firstScan = await client.PostAsync(scanRoute, null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, firstScan.StatusCode);
        var firstRequest = await firstScan.Content.ReadFromJsonAsync<RepositoryAnalysisRequest>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(firstRequest);
        Assert.True(firstRequest.FullScan);
        Assert.Equal(GitHubAnalysisTriggerKind.InitialScan, firstRequest.Trigger);

        var repeatedScan = await client.PostAsync(scanRoute, null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, repeatedScan.StatusCode);
        var repeatedRequest = await repeatedScan.Content.ReadFromJsonAsync<RepositoryAnalysisRequest>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(repeatedRequest);
        Assert.Equal(firstRequest.Id, repeatedRequest.Id);

        var unselected = await CreateUnselectedGitHubRepositoryAsync(app.Services, ownerId, project.Id);
        var unselectedScan = await client.PostAsync(
            $"api/v1/projects/{project.Id}/github/repositories/{unselected.Id}/initial-scan",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, unselectedScan.StatusCode);

        await using var scope = app.Services.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IQuerySession>();
        var requests = await session.Query<RepositoryAnalysisRequest>()
            .Where(request => request.ProjectId == project.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(requests);
        var audits = await session.Query<AuditRecord>()
            .Where(record => record.ProjectId == project.Id &&
                (record.Action == "github.installation.connect" ||
                    record.Action == "github.repository.select" ||
                    record.Action == "repository.analysis.initial.request"))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, audits.Count);
        Assert.DoesNotContain(audits, audit =>
            (audit.Before?.Contains(WebhookSecret, StringComparison.Ordinal) ?? false) ||
            (audit.After?.Contains(WebhookSecret, StringComparison.Ordinal) ?? false) ||
            (audit.Before?.Contains(authorization.CodeVerifier, StringComparison.Ordinal) ?? false) ||
            (audit.After?.Contains(authorization.CodeVerifier, StringComparison.Ordinal) ?? false) ||
            (audit.Before?.Contains("one-time-oauth-code", StringComparison.Ordinal) ?? false) ||
            (audit.After?.Contains("one-time-oauth-code", StringComparison.Ordinal) ?? false));
        Assert.DoesNotContain(typeof(GitHubAppInstallation).GetProperties(), property =>
            property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Cookie", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Webhook_rejects_invalid_signatures_and_deduplicates_push_pull_request_and_merge_ingestion()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(postgres.GetConnectionString());
        var ownerId = Guid.NewGuid();
        var project = await CreateProjectAsync(app.Services, ownerId);
        var connected = await ConnectRepositoryAsync(app.Services, ownerId, project.Id);
        using var client = CreateClient(app);
        var pushPayload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            installation = new { id = 101 },
            repository = new { id = 303 },
            @ref = "refs/heads/main",
            before = "base-sha",
            after = "head-sha",
            commits = new[]
            {
                new
                {
                    added = new[] { "src/new.cs" },
                    modified = new[] { "src/changed.cs" },
                    removed = new[] { "src/removed.cs" }
                }
            }
        });

        var invalid = await SendWebhookAsync(client, "delivery-push", "push", pushPayload, "sha256=00");
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);

        var accepted = await SendWebhookAsync(
            client,
            "delivery-push",
            "push",
            pushPayload,
            Signature(pushPayload));
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var receipt = await accepted.Content.ReadFromJsonAsync<GitHubWebhookReceipt>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(receipt);
        Assert.True(receipt.Accepted);
        Assert.False(receipt.Duplicate);
        Assert.NotNull(receipt.AnalysisRequestId);

        var duplicate = await SendWebhookAsync(
            client,
            "delivery-push",
            "push",
            pushPayload,
            Signature(pushPayload));
        var duplicateReceipt = await duplicate.Content.ReadFromJsonAsync<GitHubWebhookReceipt>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(duplicateReceipt);
        Assert.True(duplicateReceipt.Duplicate);
        Assert.Equal(receipt.AnalysisRequestId, duplicateReceipt.AnalysisRequestId);

        var pullRequestPayload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            action = "synchronize",
            number = 41,
            installation = new { id = 101 },
            repository = new { id = 303 },
            pull_request = new
            {
                merged = false,
                @base = new { sha = "base-pr", @ref = "main" },
                head = new { sha = "head-pr", @ref = "feature" }
            }
        });
        var pullRequest = await SendWebhookAsync(
            client,
            "delivery-pull-request",
            "pull_request",
            pullRequestPayload,
            Signature(pullRequestPayload));
        Assert.Equal(HttpStatusCode.Accepted, pullRequest.StatusCode);

        var mergedPayload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            action = "closed",
            number = 42,
            installation = new { id = 101 },
            repository = new { id = 303 },
            pull_request = new
            {
                merged = true,
                @base = new { sha = "base-pr", @ref = "main" },
                head = new { sha = "head-pr", @ref = "feature" }
            }
        });
        var merged = await SendWebhookAsync(
            client,
            "delivery-merge",
            "pull_request",
            mergedPayload,
            Signature(mergedPayload));
        Assert.Equal(HttpStatusCode.Accepted, merged.StatusCode);

        var raceResponses = await Task.WhenAll(
            SendWebhookAsync(
                client,
                "delivery-race",
                "push",
                pushPayload,
                Signature(pushPayload)),
            SendWebhookAsync(
                client,
                "delivery-race",
                "push",
                pushPayload,
                Signature(pushPayload)));
        Assert.All(raceResponses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
        var raceReceipts = await Task.WhenAll(raceResponses.Select(response =>
            response.Content.ReadFromJsonAsync<GitHubWebhookReceipt>(
                TestContext.Current.CancellationToken)));
        Assert.DoesNotContain(raceReceipts, item => item is null || !item.Accepted);
        Assert.Equal(1, raceReceipts.Count(item => item!.Duplicate));

        var unselectedPayload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            installation = new { id = 101 },
            repository = new { id = 999 },
            @ref = "refs/heads/main",
            before = "a",
            after = "b",
            commits = Array.Empty<object>()
        });
        var unselected = await SendWebhookAsync(
            client,
            "delivery-unselected",
            "push",
            unselectedPayload,
            Signature(unselectedPayload));
        var unselectedReceipt = await unselected.Content.ReadFromJsonAsync<GitHubWebhookReceipt>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(unselectedReceipt);
        Assert.False(unselectedReceipt.Accepted);
        Assert.Equal("repository-not-selected", unselectedReceipt.Disposition);

        await using var scope = app.Services.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IQuerySession>();
        var deliveries = await session.Query<GitHubWebhookDelivery>()
            .Where(delivery => delivery.ProjectId == project.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, deliveries.Count);
        var analyses = await session.Query<RepositoryAnalysisRequest>()
            .Where(request => request.ProjectId == project.Id)
            .OrderBy(request => request.DeliveryId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, analyses.Count);
        var push = analyses.Single(request => request.DeliveryId == "delivery-push");
        Assert.Equal(GitHubAnalysisTriggerKind.Push, push.Trigger);
        Assert.Equal(connected.Repository.Id, push.RepositoryId);
        Assert.Equal(3, push.ChangedFiles.Length);
        Assert.Contains(push.ChangedFiles,
            file => file.Path == "src/new.cs" && file.Status == GitHubChangedFileStatus.Added);
        var pullRequestAnalysis = analyses.Single(
            request => request.Trigger == GitHubAnalysisTriggerKind.PullRequest);
        Assert.Equal(41, pullRequestAnalysis.PullRequestNumber);
        Assert.True(pullRequestAnalysis.RequiresChangedFileFetch);
        Assert.Empty(pullRequestAnalysis.ChangedFiles);
        var merge = analyses.Single(request => request.Trigger == GitHubAnalysisTriggerKind.Merge);
        Assert.Equal(42, merge.PullRequestNumber);
        Assert.True(merge.RequiresChangedFileFetch);
        Assert.Empty(merge.ChangedFiles);
        Assert.Equal(4, await session.Query<AuditRecord>()
            .CountAsync(
                audit => audit.ProjectId == project.Id && audit.Action == "github.webhook.ingest",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Initial_scan_worker_reports_unsupported_repository_without_creating_tasks()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(
            postgres.GetConnectionString(),
            enableAnalysisWorker: true);
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var project = await CreateProjectAsync(app.Services, ownerId);
        await ConnectRepositoryAsync(app.Services, ownerId, project.Id);
        ConnectedGitHubRepository portfolio;
        RepositoryAnalysisRequest requested;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
            portfolio = await mediator.Send(
                new ConnectGitHubRepositoryCommand(ownerId, project.Id, 101, 304),
                TestContext.Current.CancellationToken);
            requested = await mediator.Send(
                new TriggerInitialRepositoryScanCommand(
                    ownerId,
                    project.Id,
                    portfolio.Repository.Id),
                TestContext.Current.CancellationToken);
        }

        var completed = await WaitForAnalysisAsync(app.Services, requested.Id);

        Assert.Equal(RepositoryAnalysisRunStatus.Unsupported, completed.Run!.Status);
        Assert.Equal("portfolio-commit", completed.Run.SourceRevision);
        Assert.Contains("TypeScript", completed.Run.Technologies);
        Assert.Equal(0, completed.Run.ArtifactCount);
        Assert.Equal(0, completed.Run.GeneratedTaskCount);
        Assert.Contains(completed.Run.Diagnostics, diagnostic => diagnostic.Code == "ANALYSIS001");
        Assert.True(
            completed.Request.Status == GitHubAnalysisRequestStatus.Ignored,
            $"Expected ignored but received {completed.Request.Status}: " +
            $"{completed.Run?.ErrorCode} {completed.Run?.ErrorMessage}");

        using var client = CreateClient(app);
        var route = $"api/v1/projects/{project.Id}/github/repositories/" +
            $"{portfolio.Repository.Id}/analyses/{requested.Id}";
        var routeUri = new Uri(route, UriKind.Relative);
        var unauthorized = await client.GetAsync(routeUri, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, outsiderId.ToString());
        var forbidden = await client.GetAsync(routeUri, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.UserHeader);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, ownerId.ToString());
        var authorized = await client.GetAsync(routeUri, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        var details = await authorized.Content.ReadFromJsonAsync<RepositoryAnalysisDetails>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(details);
        Assert.Equal(RepositoryAnalysisRunStatus.Unsupported, details.Run?.Status);
    }

    [Fact]
    public async Task Incremental_worker_updates_the_graph_for_a_meaningful_push()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(
            postgres.GetConnectionString(),
            enableAnalysisWorker: true);
        var ownerId = Guid.NewGuid();
        var project = await CreateProjectAsync(app.Services, ownerId);
        var connected = await ConnectRepositoryAsync(app.Services, ownerId, project.Id);
        RepositoryAnalysisRequest initial;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            initial = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new TriggerInitialRepositoryScanCommand(
                    ownerId,
                    project.Id,
                    connected.Repository.Id),
                TestContext.Current.CancellationToken);
        }

        var initialResult = await WaitForAnalysisAsync(app.Services, initial.Id);
        Assert.Equal(RepositoryAnalysisRunStatus.Completed, initialResult.Run!.Status);

        var incremental = new RepositoryAnalysisRequest(
            Guid.NewGuid(),
            project.Id,
            connected.Repository.Id,
            GitHubAnalysisTriggerKind.Push,
            "incremental-delivery-1",
            "vue-base",
            "vue-head",
            "refs/heads/main",
            null,
            FullScan: false,
            RequiresChangedFileFetch: false,
            [new GitHubChangedFile("src/App.vue", GitHubChangedFileStatus.Modified)],
            GitHubAnalysisRequestStatus.Pending,
            DateTimeOffset.UtcNow,
            "system",
            null);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
            session.Store(incremental);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var completed = await WaitForAnalysisAsync(app.Services, incremental.Id);

        Assert.Equal(GitHubAnalysisRequestStatus.Completed, completed.Request.Status);
        Assert.Equal(RepositoryAnalysisRunStatus.Completed, completed.Run!.Status);
        Assert.Equal("vue-head", completed.Run.SourceRevision);
        Assert.True(completed.Run.ArtifactCount > 0);
        Assert.True(completed.Run.ChangeCount > 0);
        Assert.True(completed.Run.ImpactCount > 0);
        Assert.Contains(completed.Run.Diagnostics, diagnostic => diagnostic.Code == "ANALYSIS003");
    }

    [Fact]
    public async Task Pull_request_worker_discovers_changed_files_from_immutable_revisions()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(
            postgres.GetConnectionString(),
            enableAnalysisWorker: true);
        var ownerId = Guid.NewGuid();
        var project = await CreateProjectAsync(app.Services, ownerId);
        var connected = await ConnectRepositoryAsync(app.Services, ownerId, project.Id);
        await RunInitialAnalysisAsync(app.Services, ownerId, project.Id, connected.Repository.Id);
        var pullRequest = new RepositoryAnalysisRequest(
            Guid.NewGuid(),
            project.Id,
            connected.Repository.Id,
            GitHubAnalysisTriggerKind.PullRequest,
            "pull-request-delivery-1",
            "vue-base",
            "vue-head",
            "main",
            42,
            FullScan: false,
            RequiresChangedFileFetch: true,
            [],
            GitHubAnalysisRequestStatus.Pending,
            DateTimeOffset.UtcNow,
            "system",
            null);
        await StoreAnalysisRequestAsync(app.Services, pullRequest);

        var completed = await WaitForAnalysisAsync(app.Services, pullRequest.Id);

        Assert.Equal(GitHubAnalysisRequestStatus.Completed, completed.Request.Status);
        Assert.Equal(RepositoryAnalysisRunStatus.Completed, completed.Run!.Status);
        Assert.Equal("vue-head", completed.Run.SourceRevision);
        Assert.True(completed.Run.ChangeCount > 0);
        Assert.True(completed.Run.ImpactCount > 0);
    }

    [Fact]
    public async Task Documentation_only_push_is_ignored_without_failing_the_worker()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(
            postgres.GetConnectionString(),
            enableAnalysisWorker: true);
        var ownerId = Guid.NewGuid();
        var project = await CreateProjectAsync(app.Services, ownerId);
        var connected = await ConnectRepositoryAsync(app.Services, ownerId, project.Id);
        await RunInitialAnalysisAsync(app.Services, ownerId, project.Id, connected.Repository.Id);
        var documentation = new RepositoryAnalysisRequest(
            Guid.NewGuid(),
            project.Id,
            connected.Repository.Id,
            GitHubAnalysisTriggerKind.Push,
            "documentation-delivery-1",
            "docs-base",
            "docs-head",
            "refs/heads/main",
            null,
            FullScan: false,
            RequiresChangedFileFetch: false,
            [new GitHubChangedFile("README.md", GitHubChangedFileStatus.Modified)],
            GitHubAnalysisRequestStatus.Pending,
            DateTimeOffset.UtcNow,
            "system",
            null);
        await StoreAnalysisRequestAsync(app.Services, documentation);

        var completed = await WaitForAnalysisAsync(app.Services, documentation.Id);

        Assert.True(
            completed.Request.Status == GitHubAnalysisRequestStatus.Ignored,
            $"Expected ignored but received {completed.Request.Status}: " +
            $"{completed.Run?.ErrorCode} {completed.Run?.ErrorMessage}");
        Assert.Equal(RepositoryAnalysisRunStatus.Completed, completed.Run!.Status);
        Assert.Null(completed.Run.ErrorCode);
        Assert.Contains(completed.Run.Diagnostics, diagnostic =>
            diagnostic.Code == "ANALYSIS003" &&
            diagnostic.Message.Contains("cosmetic or non-behavioral", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deep_reasoning_projects_a_source_aware_suggestion_with_trace_and_audit()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(
            postgres.GetConnectionString(),
            enableReasoningWorker: true);
        var ownerId = Guid.NewGuid();
        var project = await CreateProjectAsync(app.Services, ownerId);
        var connected = await ConnectRepositoryAsync(app.Services, ownerId, project.Id);
        var requestId = Guid.NewGuid();
        var graph = ReasoningGraph(connected.Repository.Id);
        var workItem = new DeepReasoningWorkItem(
            "reasoning-job-1",
            requestId.ToString(),
            project.Id.ToString(),
            connected.Repository.Id.ToString(),
            "reasoning-correlation-1",
            graph.Revision,
            ["change-1"],
            ["mismatch-1"],
            [],
            DateTimeOffset.UtcNow);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
            await new MartenKnowledgeGraphWriter(session, TimeProvider.System).SaveAsync(
                graph,
                TestContext.Current.CancellationToken);
            await new MartenConventionProfileWriter(session, TimeProvider.System).SaveAsync(
                new RepositoryConventionProfile(
                    connected.Repository.Id.ToString(),
                    graph.Revision,
                    VietAIS.TCFlow.Analyzers.Governance.ConventionProfileStatus.Confirmed,
                    []),
                TestContext.Current.CancellationToken);
            session.Store(new GlobalAiProviderConfiguration(
                SystemConfigurationIds.CodexAppServerProvider,
                GlobalAiProviderKind.CodexAppServer,
                "Codex App Server",
                IsEnabled: false,
                DateTimeOffset.UtcNow,
                ownerId));
            session.Store(new RepositoryAnalysisRequest(
                requestId,
                project.Id,
                connected.Repository.Id,
                GitHubAnalysisTriggerKind.Push,
                "reasoning-delivery-1",
                "base-revision",
                "head-revision",
                "refs/heads/main",
                null,
                FullScan: false,
                RequiresChangedFileFetch: false,
                [new GitHubChangedFile("src/CreateProduct.vue", GitHubChangedFileStatus.Modified)],
                GitHubAnalysisRequestStatus.AwaitingReasoning,
                DateTimeOffset.UtcNow,
                "system",
                null));
            session.Store(new RepositoryAnalysisRun(
                requestId,
                project.Id,
                connected.Repository.Id,
                RepositoryAnalysisRunStatus.AwaitingReasoning,
                Attempt: 1,
                "head-revision",
                ["Vue", "AspNet", "Marten"],
                ArtifactCount: 1,
                DependencyCount: 0,
                ContractCount: 2,
                MismatchCount: 1,
                ChangeCount: 1,
                ImpactCount: 1,
                GeneratedTaskCount: 0,
                [],
                ErrorCode: null,
                ErrorMessage: null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                CompletedAt: null));
            await scope.ServiceProvider.GetRequiredService<IDeepReasoningQueue>()
                .EnqueueAsync(workItem, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken);
        await using (var disabledScope = app.Services.CreateAsyncScope())
        {
            var session = disabledScope.ServiceProvider.GetRequiredService<IDocumentSession>();
            var pendingRequest = await session.LoadAsync<RepositoryAnalysisRequest>(
                requestId,
                TestContext.Current.CancellationToken);
            Assert.NotNull(pendingRequest);
            Assert.Equal(GitHubAnalysisRequestStatus.AwaitingReasoning, pendingRequest.Status);
            var provider = await session.LoadAsync<GlobalAiProviderConfiguration>(
                SystemConfigurationIds.CodexAppServerProvider,
                TestContext.Current.CancellationToken);
            Assert.NotNull(provider);
            session.Store(provider with
            {
                IsEnabled = true,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = ownerId
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var completed = await WaitForAnalysisAsync(app.Services, requestId);

        Assert.Equal(GitHubAnalysisRequestStatus.Completed, completed.Request.Status);
        Assert.Equal(RepositoryAnalysisRunStatus.Completed, completed.Run!.Status);
        Assert.Equal(1, completed.Run.GeneratedTaskCount);
        Assert.Contains(completed.Run.Diagnostics, diagnostic => diagnostic.Code == "ANALYSIS005");
        await using var verificationScope = app.Services.CreateAsyncScope();
        var query = verificationScope.ServiceProvider.GetRequiredService<IQuerySession>();
        var task = Assert.Single(await query.Query<EngineeringTask>()
            .Where(task => task.ProjectId == project.Id)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(TaskLifecycleStatus.Suggested, task.Status);
        Assert.Equal(TaskActorType.Ai, task.CreatedByType);
        Assert.NotNull(task.SourceTrace.SourceChangeId);
        Assert.NotEmpty(task.SourceTrace.ArtifactIds);
        Assert.NotEmpty(task.SourceTrace.EvidenceIds);
        Assert.NotEmpty(task.SourceTrace.ImpactIds);
        Assert.Single(await query.Query<VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management.SourceChange>()
            .Where(change => change.ProjectId == project.Id)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await query.Query<SourceArtifact>()
            .Where(artifact => artifact.ProjectId == project.Id)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await query.Query<SourceImpact>()
            .Where(impact => impact.ProjectId == project.Id)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await query.Query<TaskEvidence>()
            .Where(evidence => evidence.TaskId == task.Id)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Contains(await query.Query<AuditRecord>()
                .Where(audit => audit.ProjectId == project.Id)
                .ToListAsync(TestContext.Current.CancellationToken),
            audit => audit.Action == "repository.analysis.reasoning.completed");
        Assert.Contains(await query.Query<AiActionAudit>()
                .Where(audit => audit.ProjectId == project.Id.ToString())
                .ToListAsync(TestContext.Current.CancellationToken),
            audit => audit.Action == AiPermissionCodes.TaskSuggest);

        await using (var promotionScope = app.Services.CreateAsyncScope())
        {
            var session = promotionScope.ServiceProvider.GetRequiredService<IDocumentSession>();
            var current = await session.LoadAsync<EngineeringTask>(
                task.Id,
                TestContext.Current.CancellationToken);
            Assert.NotNull(current);
            session.Store(current with
            {
                Status = TaskLifecycleStatus.Upcoming,
                CurrentVersion = current.CurrentVersion + 1,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var secondRequestId = Guid.NewGuid();
        var secondWorkItem = workItem with
        {
            Id = "reasoning-job-2",
            RequestId = secondRequestId.ToString(),
            CorrelationId = "reasoning-correlation-2",
            QueuedAt = DateTimeOffset.UtcNow
        };
        await using (var secondScope = app.Services.CreateAsyncScope())
        {
            var session = secondScope.ServiceProvider.GetRequiredService<IDocumentSession>();
            session.Store(new RepositoryAnalysisRequest(
                secondRequestId,
                project.Id,
                connected.Repository.Id,
                GitHubAnalysisTriggerKind.Push,
                "reasoning-delivery-2",
                "head-revision",
                "second-head-revision",
                "refs/heads/main",
                null,
                FullScan: false,
                RequiresChangedFileFetch: false,
                [new GitHubChangedFile("src/CreateProduct.vue", GitHubChangedFileStatus.Modified)],
                GitHubAnalysisRequestStatus.AwaitingReasoning,
                DateTimeOffset.UtcNow,
                "system",
                null));
            session.Store(new RepositoryAnalysisRun(
                secondRequestId,
                project.Id,
                connected.Repository.Id,
                RepositoryAnalysisRunStatus.AwaitingReasoning,
                Attempt: 1,
                "second-head-revision",
                ["Vue", "AspNet", "Marten"],
                ArtifactCount: 1,
                DependencyCount: 0,
                ContractCount: 2,
                MismatchCount: 1,
                ChangeCount: 1,
                ImpactCount: 1,
                GeneratedTaskCount: 0,
                [],
                ErrorCode: null,
                ErrorMessage: null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                CompletedAt: null));
            await secondScope.ServiceProvider.GetRequiredService<IDeepReasoningQueue>()
                .EnqueueAsync(secondWorkItem, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var secondCompleted = await WaitForAnalysisAsync(app.Services, secondRequestId);
        Assert.Equal(RepositoryAnalysisRunStatus.Completed, secondCompleted.Run!.Status);
        await using var secondVerificationScope = app.Services.CreateAsyncScope();
        var secondQuery = secondVerificationScope.ServiceProvider.GetRequiredService<IQuerySession>();
        var promoted = await secondQuery.LoadAsync<EngineeringTask>(task.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(promoted);
        Assert.Equal(TaskLifecycleStatus.Upcoming, promoted.Status);
        Assert.Equal(task.BusinessRules, promoted.BusinessRules);
        var sourceAware = Assert.Single(await secondQuery.Query<SourceAwareEngineeringTask>()
            .Where(item => item.ProjectId == project.Id.ToString())
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, sourceAware.Version);
        Assert.Contains("Persist categoryId consistently.", sourceAware.Requirements);
    }

    private static RepositoryKnowledgeGraph ReasoningGraph(Guid repositoryId)
    {
        var location = new SourceLocation("src/CreateProduct.vue", 1, 5, "submit");
        var evidence = new Evidence(
            "evidence-1",
            "The frontend sends categoryId.",
            EvidenceLevel.Confirmed,
            location,
            "integration-fixture",
            1m);
        var artifact = new Artifact(
            "artifact-1",
            ArtifactKind.ApiCall,
            "Vue",
            "POST /api/products",
            location.Path,
            EvidenceLevel.Confirmed,
            [evidence.Id],
            new Dictionary<string, string>());
        var frontend = new Contract(
            "frontend-contract",
            ContractDirection.FrontendExpected,
            "POST",
            "/api/products",
            EvidenceLevel.Confirmed,
            [new ContractField("categoryId", "string", true, EvidenceLevel.Confirmed, location)],
            [],
            [],
            HasPagination: false,
            [],
            [evidence.Id]);
        var backend = new Contract(
            "backend-contract",
            ContractDirection.BackendActual,
            "POST",
            "/api/products",
            EvidenceLevel.Confirmed,
            [],
            [],
            [],
            HasPagination: false,
            [],
            [evidence.Id]);
        var pair = new ContractPair(
            "pair-1",
            frontend.Id,
            backend.Id,
            [backend.Id],
            ContractPairStatus.Matched,
            EvidenceLevel.Confirmed,
            1m,
            "Contracts share method and route.",
            [evidence.Id]);
        var mismatch = new ContractMismatch(
            "mismatch-1",
            pair.Id,
            ContractMismatchKind.RequestFieldMissingBackend,
            "categoryId",
            "required string",
            "missing",
            EvidenceLevel.Confirmed,
            1m,
            "The frontend field is absent from the backend request.",
            [evidence.Id],
            [location]);
        var change = new VietAIS.TCFlow.Analyzers.Core.SourceChange(
            "change-1",
            location.Path,
            ChangeKind.Modified,
            "before",
            "after",
            IsMeaningful: true,
            "Frontend request contract changed.");
        var impact = new Impact(
            "impact-1",
            change.Id,
            artifact.Id,
            ImpactSeverity.High,
            "The API request contract is affected.",
            0.95m,
            EvidenceLevel.Inferred,
            [evidence.Id]);
        var records = new[]
        {
            evidence.Id,
            artifact.Id,
            frontend.Id,
            backend.Id,
            pair.Id,
            mismatch.Id,
            change.Id,
            impact.Id
        }.ToDictionary(id => id, _ => "integration-fixture", StringComparer.Ordinal);
        return new RepositoryKnowledgeGraph(
            repositoryId.ToString(),
            Revision: 1,
            [artifact],
            [],
            [evidence],
            [],
            [frontend, backend],
            [change],
            [impact],
            [pair],
            [mismatch],
            records);
    }

    private static async Task RunInitialAnalysisAsync(
        IServiceProvider services,
        Guid ownerId,
        Guid projectId,
        Guid repositoryId)
    {
        RepositoryAnalysisRequest initial;
        await using (var scope = services.CreateAsyncScope())
        {
            initial = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new TriggerInitialRepositoryScanCommand(ownerId, projectId, repositoryId),
                TestContext.Current.CancellationToken);
        }

        var completed = await WaitForAnalysisAsync(services, initial.Id);
        Assert.Equal(RepositoryAnalysisRunStatus.Completed, completed.Run!.Status);
    }

    private static async Task StoreAnalysisRequestAsync(
        IServiceProvider services,
        RepositoryAnalysisRequest request)
    {
        await using var scope = services.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        session.Store(request);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Project> CreateProjectAsync(IServiceProvider services, Guid ownerId)
    {
        await using var scope = services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        var response = await mediator.Send(
            new CreateProjectCommand(ownerId, "GitHub Integration Project"),
            TestContext.Current.CancellationToken);
        return response.Project;
    }

    private static async Task<ConnectedGitHubRepository> ConnectRepositoryAsync(
        IServiceProvider services,
        Guid ownerId,
        Guid projectId)
    {
        await using var scope = services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        await mediator.Send(
            new RegisterGitHubInstallationCommand(
                ownerId,
                projectId,
                101,
                202,
                "NukeGeng",
                GitHubAccountKind.User,
                GitHubRepositorySelectionKind.Selected),
            TestContext.Current.CancellationToken);
        return await mediator.Send(
            new ConnectGitHubRepositoryCommand(
                ownerId,
                projectId,
                101,
                303),
            TestContext.Current.CancellationToken);
    }

    private static async Task<ProjectRepository> CreateUnselectedGitHubRepositoryAsync(
        IServiceProvider services,
        Guid ownerId,
        Guid projectId)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateProjectRepositoryCommand(
                ownerId,
                projectId,
                "unselected",
                RepositoryProviderKind.GitHub,
                null,
                "https://github.com/NukeGeng/unselected",
                "main"),
            TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> SendWebhookAsync(
        HttpClient client,
        string deliveryId,
        string eventName,
        byte[] payload,
        string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/github/webhooks")
        {
            Content = new ByteArrayContent(payload)
        };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add("X-GitHub-Delivery", deliveryId);
        request.Headers.Add("X-GitHub-Event", eventName);
        request.Headers.Add("X-Hub-Signature-256", signature);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string Signature(byte[] payload) =>
        $"sha256={Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), payload))}";

    private static string QueryValue(string url, string name)
    {
        var query = new Uri(url).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var pair = query.Select(item => item.Split('=', 2))
            .Single(item => Uri.UnescapeDataString(item[0]) == name);
        return Uri.UnescapeDataString(pair[1]);
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static async Task<WebApplication> BuildApplicationAsync(
        string connectionString,
        bool enableAnalysisWorker = false,
        bool enableReasoningWorker = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["DatabaseOptions:ConnectionString"] = connectionString;
        builder.Configuration["GitHub:WebhookSecret"] = WebhookSecret;
        builder.Configuration["RepositoryAnalysis:Enabled"] = enableAnalysisWorker.ToString();
        builder.Configuration["RepositoryAnalysis:PollInterval"] = "00:00:00.025";
        builder.Configuration["RepositoryReasoning:Enabled"] = enableReasoningWorker.ToString();
        builder.Configuration["RepositoryReasoning:PollInterval"] = "00:00:00.025";
        builder.Configuration["RepositoryReasoning:ProcessingLease"] = "00:00:05";
        builder.RegisterRepositoryIntelligenceServices();
        builder.Services.RemoveAll<IGitHubAppClient>();
        builder.Services.AddSingleton<IGitHubAppClient, FakeGitHubAppClient>();
        if (enableReasoningWorker)
        {
            builder.Services.RemoveAll<IAiReasoningProvider>();
            builder.Services.AddSingleton<IAiReasoningProvider, FakeReasoningProvider>();
        }
        builder.Services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<RegisterGitHubInstallationCommand>());
        builder.Services.AddCarter(configurator: configuration =>
            configuration.WithModule<GitHubIntegrationEndpoints>());
        builder.Services.AddApiVersioning();
        builder.Services
            .AddAuthentication(TestAuthenticationHandler.AuthenticationSchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.AuthenticationSchemeName,
                _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddExceptionHandler<CustomExceptionHandler>();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        var versions = app.NewApiVersionSet().HasApiVersion(1).Build();
        app.MapGroup("api/v{version:apiVersion}")
            .WithApiVersionSet(versions)
            .MapCarter();
        app.Urls.Add("http://127.0.0.1:0");

        await app.StartAsync(TestContext.Current.CancellationToken);
        var store = app.Services.GetRequiredService<IDocumentStore>();
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        return app;
    }

    private sealed class FakeGitHubAppClient : IGitHubAppClient
    {
        private static readonly GitHubRemoteInstallation Installation = new(
            101,
            202,
            "NukeGeng",
            GitHubAccountKind.User,
            GitHubRepositorySelectionKind.Selected,
            Suspended: false);

        private static readonly GitHubRepositorySummary[] Repositories =
        [
            new(
                303,
                "VietAIS-TCFlow",
                "NukeGeng/VietAIS-TCFlow",
                Private: true,
                "main",
                "https://github.com/NukeGeng/VietAIS-TCFlow"),
            new(
                304,
                "Portfolio",
                "NukeGeng/Portfolio",
                Private: true,
                "main",
                "https://github.com/NukeGeng/Portfolio")
        ];

        public Uri CreateInstallationUrl(string state) =>
            new($"https://github.test/apps/tcflow/installations/new?state={Uri.EscapeDataString(state)}");

        public Uri CreateUserAuthorizationUrl(string state, string codeChallenge) =>
            new(
                "https://github.test/login/oauth/authorize" +
                $"?state={Uri.EscapeDataString(state)}&code_challenge={Uri.EscapeDataString(codeChallenge)}");

        public Task<GitHubRemoteInstallation> GetInstallationAsync(
            long installationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(installationId == Installation.InstallationId
                ? Installation
                : throw new GitHubAppRequestException("Unknown installation."));

        public Task<GitHubVerifiedConnection> VerifyUserInstallationAsync(
            long installationId,
            string code,
            string codeVerifier,
            CancellationToken cancellationToken) =>
            Task.FromResult(new GitHubVerifiedConnection(Installation, Repositories));

        public Task<IReadOnlyList<GitHubRepositorySummary>> GetInstallationRepositoriesAsync(
            long installationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitHubRepositorySummary>>(Repositories);

        public Task<GitHubRepositorySnapshot> GetRepositorySnapshotAsync(
            long installationId,
            string fullName,
            string reference,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = string.Equals(fullName, "NukeGeng/Portfolio", StringComparison.Ordinal)
                ? new GitHubRepositorySnapshot(
                    "portfolio-commit",
                    [
                        new GitHubRepositorySnapshotFile(
                            "package.json",
                            "{\"dependencies\":{\"next\":\"latest\",\"react\":\"latest\"}}"),
                        new GitHubRepositorySnapshotFile(
                            "src/app/page.tsx",
                            "export default function Page() { return <main>Portfolio</main>; }")
                    ])
                : VueSnapshot(reference);
            return Task.FromResult(snapshot);
        }

        private static GitHubRepositorySnapshot VueSnapshot(string reference)
        {
            var head = string.Equals(reference, "vue-head", StringComparison.Ordinal);
            var docsHead = string.Equals(reference, "docs-head", StringComparison.Ordinal);
            var revision = head || docsHead ? reference : "vue-base";
            return new GitHubRepositorySnapshot(
                revision,
                [
                    new GitHubRepositorySnapshotFile(
                        "package.json",
                        "{\"dependencies\":{\"vue\":\"latest\"}}"),
                    new GitHubRepositorySnapshotFile(
                        "README.md",
                        docsHead ? "# Updated documentation" : "# Documentation"),
                    new GitHubRepositorySnapshotFile(
                        "src/App.vue",
                        head
                            ? "<script setup lang=\"ts\">defineProps<{ message: string }>()</script>" +
                                "<template><main>{{ message }}</main></template>"
                            : "<template><main>TCFlow</main></template>")
                ]);
        }
    }

    private sealed class FakeReasoningProvider : IAiReasoningProvider
    {
        private int _calls;

        public Task<AiImpactReasoningResult> AnalyzeImpactAsync(
            AiReasoningContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = Interlocked.Increment(ref _calls);
            var artifact = context.GraphContext.Artifacts.Single();
            var evidence = context.GraphContext.Evidence.Single();
            return Task.FromResult(new AiImpactReasoningResult(
                "The backend request must align with the authoritative source contract.",
                ImpactSeverity.High,
                EvidenceLevel.Inferred,
                0.95m,
                [evidence.Id],
                [new AiTaskReasoningResult(
                    "Accept categoryId in the backend request",
                    "Align the backend request with confirmed frontend evidence.",
                    PlanTargetComponent.Backend,
                    EvidenceLevel.Inferred,
                    0.95m,
                    [artifact.Id],
                    [evidence.Id],
                    [call == 1
                        ? "Accept and validate categoryId."
                        : "Persist categoryId consistently."])]));
        }
    }

    private static async Task<RepositoryAnalysisDetails> WaitForAnalysisAsync(
        IServiceProvider services,
        Guid requestId)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var scope = services.CreateAsyncScope();
            var session = scope.ServiceProvider.GetRequiredService<IQuerySession>();
            var request = await session.LoadAsync<RepositoryAnalysisRequest>(
                requestId,
                TestContext.Current.CancellationToken);
            var run = await session.LoadAsync<RepositoryAnalysisRun>(
                requestId,
                TestContext.Current.CancellationToken);
            if (request is not null && run?.Status is
                RepositoryAnalysisRunStatus.Completed or
                RepositoryAnalysisRunStatus.Unsupported or
                RepositoryAnalysisRunStatus.Failed)
            {
                return new RepositoryAnalysisDetails(request, run);
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Repository analysis did not reach a terminal status.");
    }
}
