using System.Text.Json;
using Marten;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.Modules.Integrations.Configuration;
using VietAIS.TCFlow.Tools.Migration;

namespace VietAIS.TCFlow.Tools.Migration.Tests;

public sealed class MartenOperationalMigrationApplierTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("tcflow_goal2_operational_migration_tests")
        .WithUsername("postgres")
        .WithPassword("integration_test_pwd")
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task AppliesRedactedGitHubOperationalDocumentsIdempotently()
    {
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "GitHubCredential",
                    "credential-1",
                    "project-1",
                    "sha256:credential-1",
                    JsonSerializer.SerializeToElement(new
                    {
                        installationId = 12345,
                        accountId = 67890,
                        accountLogin = "nukegeng",
                        accountKind = "User",
                        repositorySelection = "Selected",
                        status = "Active"
                    })),
                new LegacyRecord(
                    "GitHubDelivery",
                    "delivery-1",
                    "project-1",
                    "sha256:delivery-1",
                    JsonSerializer.SerializeToElement(new
                    {
                        deliveryId = "delivery-1",
                        @event = "push",
                        action = "published",
                        payloadSha256 = "AABB",
                        receivedAtUtc = "2026-08-31T10:00:00Z"
                    }))
            ]);
        var plan = Goal2MigrationPlanner.Plan(export);

        var first = await MartenOperationalMigrationApplier.ApplyAsync(
            plan,
            export,
            _postgres.GetConnectionString(),
            CancellationToken.None);
        var second = await MartenOperationalMigrationApplier.ApplyAsync(
            plan,
            export,
            _postgres.GetConnectionString(),
            CancellationToken.None);

        Assert.Equal(2, first.UpsertedDocumentCount);
        Assert.Equal(0, first.SkippedDocumentCount);
        Assert.Equal(0, second.UpsertedDocumentCount);
        Assert.Equal(2, second.SkippedDocumentCount);

        await using var store = DocumentStore.For(options =>
        {
            options.Connection(_postgres.GetConnectionString());
            IntegrationsMartenConfiguration.Configure(options);
        });
        await using var query = store.QuerySession();
        var documents = await query.Query<GitHubOperationalMigrationDocument>().ToListAsync();

        Assert.Equal(2, documents.Count);
        Assert.Contains(documents, document =>
            document.Kind == "GitHubCredential" &&
            document.ExternalId == "12345" &&
            document.Metadata["accountLogin"] == "nukegeng");
        Assert.Contains(documents, document =>
            document.Kind == "GitHubDelivery" &&
            document.ExternalId == "delivery-1" &&
            document.Metadata["payloadSha256"] == "AABB");
    }

    [Fact]
    public async Task RejectsSecretBearingCredentialPayloadBeforeWriting()
    {
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "GitHubCredential",
                    "credential-secret",
                    "project-1",
                    "sha256:credential-secret",
                    JsonSerializer.SerializeToElement(new
                    {
                        installationId = 12345,
                        accessToken = "must-not-be-persisted"
                    }))
            ]);
        var plan = Goal2MigrationPlanner.Plan(export);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MartenOperationalMigrationApplier.ApplyAsync(
                plan,
                export,
                _postgres.GetConnectionString(),
                CancellationToken.None));

        Assert.Contains("forbidden sensitive property", failure.Message, StringComparison.Ordinal);
    }
}
