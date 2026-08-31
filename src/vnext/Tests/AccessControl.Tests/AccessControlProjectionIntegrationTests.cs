using Marten;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Configuration;
using VietAIS.TCFlow.Modules.AccessControl.Authorization;
using VietAIS.TCFlow.Modules.AccessControl.Configuration;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;
using VietAIS.TCFlow.Modules.AccessControl.Domain;
using VietAIS.TCFlow.Modules.AccessControl.Projections;

namespace VietAIS.TCFlow.Modules.AccessControl.Tests;

public sealed class AccessControlProjectionIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("tcflow_access_control_tests")
        .WithUsername("postgres")
        .WithPassword("integration_test_pwd")
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    private DocumentStore _store = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _store = DocumentStore.For(options =>
        {
            options.Connection(_postgres.GetConnectionString());
            TcFlowEventStoreConfiguration.Configure(options);
            AccessControlMartenConfiguration.Configure(options);
        });
        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task InlineAccessProjectionProducesScopedEffectivePermissions()
    {
        var projectId = Guid.NewGuid();
        var streamId = ProjectAccessStreamIdentity.ForProject(projectId);
        var ownerRoleId = Guid.NewGuid();
        var reviewerRoleId = Guid.NewGuid();
        await using (var session = _store.LightweightSession())
        {
            session.Events.StartStream<ProjectAccessAggregate>(
                streamId,
                new ProjectAccessInitialized(
                    projectId,
                    "owner-1",
                    ownerRoleId,
                    "owner-1",
                    "c-1",
                    DateTimeOffset.UtcNow),
                new ProjectRoleCreated(
                    projectId,
                    reviewerRoleId,
                    "Reviewer",
                    "owner-1",
                    "c-2",
                    DateTimeOffset.UtcNow),
                new ProjectRolePermissionsUpdated(
                    projectId,
                    reviewerRoleId,
                    [new ProjectPermissionGrant(
                        ProjectPermissionCatalog.RepositoryView,
                        ProjectResourceScope.Repository,
                        "repo-1")],
                    "owner-1",
                    "c-3",
                    DateTimeOffset.UtcNow),
                new ProjectMemberAdded(
                    projectId,
                    "reviewer-1",
                    "owner-1",
                    "c-4",
                    DateTimeOffset.UtcNow),
                new ProjectMemberRolesAssigned(
                    projectId,
                    "reviewer-1",
                    [reviewerRoleId],
                    "owner-1",
                    "c-5",
                    DateTimeOffset.UtcNow));
            await session.SaveChangesAsync();
        }

        await using var query = _store.QuerySession();
        var current = await query.Query<ProjectAccessCurrent>()
            .SingleAsync(item => item.ProjectId == projectId);
        current.Roles.Count.ShouldBe(2);
        current.Members.Count.ShouldBe(2);

        var evaluator = new ProjectPermissionEvaluator(query);
        var scoped = await evaluator.GetEffectivePermissionsAsync(
            "reviewer-1",
            projectId,
            "repo-1",
            component: null,
            CancellationToken.None);
        scoped.Has(ProjectPermissionCatalog.RepositoryView).ShouldBeTrue();

        var wrongRepository = await evaluator.GetEffectivePermissionsAsync(
            "reviewer-1",
            projectId,
            "repo-2",
            component: null,
            CancellationToken.None);
        wrongRepository.Has(ProjectPermissionCatalog.RepositoryView).ShouldBeFalse();
    }
}
