using System.Text.Json;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.Tools.Migration;

namespace VietAIS.TCFlow.Tools.Migration.Tests;

public sealed class MartenMigrationReconcilerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("tcflow_goal2_reconciliation_tests")
        .WithUsername("postgres")
        .WithPassword("integration_test_pwd")
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task ReconcilesSourceMarkersAndHashesAfterTypedApply()
    {
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "Project",
                    "project-1",
                    null,
                    "sha256:project-1",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Reconciliation project",
                        ownerId = "owner-1",
                        createdAtUtc = "2026-08-30T10:00:00Z"
                    }))
            ]);
        var plan = Goal2MigrationPlanner.Plan(export);

        await MartenProjectMigrationApplier.ApplyAsync(
            plan,
            export,
            _postgres.GetConnectionString(),
            CancellationToken.None);

        var report = await MartenMigrationReconciler.ReconcileAsync(
            plan,
            _postgres.GetConnectionString(),
            CancellationToken.None);

        Assert.True(report.Reconciled);
        Assert.Equal(1, report.EventStreamOperations);
        Assert.Equal(0, report.OperationalDocumentOperations);
        Assert.Equal(1, report.ExpectedSourceMarkers);
        Assert.Equal(1, report.FoundSourceMarkers);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public async Task ReportsMissingAndChangedMarkersWithoutWriting()
    {
        var original = new LegacyRecord(
            "Project",
            "project-1",
            null,
            "sha256:project-1",
            JsonSerializer.SerializeToElement(new
            {
                name = "Reconciliation project",
                ownerId = "owner-1",
                createdAtUtc = "2026-08-30T10:00:00Z"
            }));
        var appliedExport = new LegacyExport(1, [original]);
        await MartenProjectMigrationApplier.ApplyAsync(
            Goal2MigrationPlanner.Plan(appliedExport),
            appliedExport,
            _postgres.GetConnectionString(),
            CancellationToken.None);

        var changed = original with { PayloadHash = "sha256:changed" };
        var missing = new LegacyRecord(
            "Project",
            "project-2",
            null,
            "sha256:missing",
            original.Payload);
        var report = await MartenMigrationReconciler.ReconcileAsync(
            Goal2MigrationPlanner.Plan(new LegacyExport(1, [changed, missing])),
            _postgres.GetConnectionString(),
            CancellationToken.None);

        Assert.False(report.Reconciled);
        Assert.Equal(2, report.ExpectedSourceMarkers);
        Assert.Equal(1, report.FoundSourceMarkers);
        Assert.Contains(
            report.HashMismatches,
            item => item.StartsWith("v0.1:Project:project-1", StringComparison.Ordinal));
        Assert.Contains("v0.1:Project:project-2", report.MissingSourceReferences);
        Assert.Contains(report.Issues, item => item.Contains("hash mismatches", StringComparison.Ordinal));
        Assert.Contains(report.Issues, item => item.Contains("Missing migration markers", StringComparison.Ordinal));
    }
}
