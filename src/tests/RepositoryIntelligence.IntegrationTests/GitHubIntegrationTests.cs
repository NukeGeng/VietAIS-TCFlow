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

    private static async Task<WebApplication> BuildApplicationAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["DatabaseOptions:ConnectionString"] = connectionString;
        builder.Configuration["GitHub:WebhookSecret"] = WebhookSecret;
        builder.RegisterRepositoryIntelligenceServices();
        builder.Services.RemoveAll<IGitHubAppClient>();
        builder.Services.AddSingleton<IGitHubAppClient, FakeGitHubAppClient>();
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
                "https://github.com/NukeGeng/VietAIS-TCFlow")
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
    }
}
