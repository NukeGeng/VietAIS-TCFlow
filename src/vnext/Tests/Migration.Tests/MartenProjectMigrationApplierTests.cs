using System.Text.Json;
using System.Text.Json.Serialization;
using JasperFx.Events;
using Marten;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Configuration;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.Modules.AccessControl.Authorization;
using VietAIS.TCFlow.Modules.AccessControl.Configuration;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;
using VietAIS.TCFlow.Modules.AccessControl.Domain;
using VietAIS.TCFlow.Modules.AccessControl.Projections;
using VietAIS.TCFlow.Modules.Planning.Configuration;
using VietAIS.TCFlow.Modules.Planning.Domain;
using VietAIS.TCFlow.Modules.Planning.Projections;
using VietAIS.TCFlow.Modules.TaskFlow.Configuration;
using VietAIS.TCFlow.Modules.TaskFlow.Domain;
using VietAIS.TCFlow.Modules.TaskFlow.Projections;
using TaskStatus = VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries.TaskStatus;
using VietAIS.TCFlow.Modules.Projects.Configuration;
using VietAIS.TCFlow.Modules.Projects.Domain;
using VietAIS.TCFlow.Modules.Projects.Projections;
using VietAIS.TCFlow.Tools.Migration;

namespace VietAIS.TCFlow.Tools.Migration.Tests;

public sealed class MartenProjectMigrationApplierTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("tcflow_goal2_migration_tests")
        .WithUsername("postgres")
        .WithPassword("integration_test_pwd")
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task AppliesTypedProjectEventsWithMarkersAndIsIdempotent()
    {
        var projectSourceId = "legacy-project-1";
        var stateSourceId = "legacy-state-1";
        var projectPayload = JsonSerializer.SerializeToElement(new
        {
            name = "Imported TCFlow",
            ownerId = "legacy-owner-1",
            createdAtUtc = "2026-08-30T10:00:00Z"
        });
        var statePayload = JsonSerializer.SerializeToElement(new
        {
            status = "Suspended",
            updatedAtUtc = "2026-08-30T11:00:00Z"
        });
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "Project",
                    projectSourceId,
                    null,
                    "sha256:project-1",
                    projectPayload),
                new LegacyRecord(
                    "ProjectState",
                    stateSourceId,
                    projectSourceId,
                    "sha256:state-1",
                    statePayload)
            ]);
        var plan = Goal2MigrationPlanner.Plan(export);

        var first = await MartenProjectMigrationApplier.ApplyAsync(
            plan,
            export,
            _postgres.GetConnectionString(),
            CancellationToken.None);
        var second = await MartenProjectMigrationApplier.ApplyAsync(
            plan,
            export,
            _postgres.GetConnectionString(),
            CancellationToken.None);

        Assert.Equal(2, first.AppendedEventCount);
        Assert.Equal(0, first.SkippedEventCount);
        Assert.Equal(0, second.AppendedEventCount);
        Assert.Equal(2, second.SkippedEventCount);

        var projectId = Goal2MigrationPlanner.CreateDeterministicId("Project", projectSourceId);
        await using var store = CreateStore();
        await using var query = store.QuerySession();
        var events = await query.Events.FetchStreamAsync(
            projectId,
            long.MaxValue,
            timestamp: null,
            fromVersion: 0,
            token: CancellationToken.None);
        var current = await query.LoadAsync<ProjectCurrent>(projectId);
        var aggregate = await query.Events.AggregateStreamAsync<ProjectAggregate>(projectId);

        Assert.Equal(2, events.Count);
        Assert.IsType<ProjectCreated>(events[0].Data);
        Assert.IsType<ProjectLifecycleReconciled>(events[1].Data);
        Assert.NotNull(aggregate);
        Assert.Equal(projectId, aggregate!.Id);
        Assert.True(aggregate.IsSuspended);
        Assert.NotNull(current);
        Assert.True(current!.IsSuspended);
        Assert.Equal(2, current.Version);
        Assert.Equal(
            Goal2MigrationPlanner.BuildSourceReference("Project", projectSourceId),
            Header(events[0], EventMetadataHeaders.MigrationSourceReference));
        Assert.Equal(
            "sha256:state-1",
            Header(events[1], EventMetadataHeaders.MigrationPayloadHash));
    }

    [Fact]
    public async Task RejectsUnsupportedStatusBeforeAppendingAnyEvent()
    {
        await EnsureSchemaAsync();
        var projectSourceId = "legacy-project-unsupported";
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "Project",
                    projectSourceId,
                    null,
                    "sha256:project-unsupported",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Imported",
                        ownerId = "legacy-owner",
                        createdAtUtc = "2026-08-30T10:00:00Z"
                    })),
                new LegacyRecord(
                    "ProjectState",
                    "legacy-state-unsupported",
                    projectSourceId,
                    "sha256:state-unsupported",
                    JsonSerializer.SerializeToElement(new
                    {
                        status = "Archived",
                        updatedAtUtc = "2026-08-30T11:00:00Z"
                    }))
            ]);
        var plan = Goal2MigrationPlanner.Plan(export);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MartenProjectMigrationApplier.ApplyAsync(
                plan,
                export,
                _postgres.GetConnectionString(),
                CancellationToken.None));

        var projectId = Goal2MigrationPlanner.CreateDeterministicId("Project", projectSourceId);
        await using var store = CreateStore();
        await using var query = store.QuerySession();
        var events = await query.Events.FetchStreamAsync(
            projectId,
            long.MaxValue,
            timestamp: null,
            fromVersion: 0,
            token: CancellationToken.None);
        Assert.Empty(events);
    }

    [Fact]
    public async Task AppliesAccessControlRolesAndMembershipsWithTypedEventsAndIsIdempotent()
    {
        const string projectSourceId = "legacy-access-project";
        const string ownerRoleSourceId = "legacy-owner-role";
        const string reviewerRoleSourceId = "legacy-reviewer-role";
        var ownerPermissions = ProjectPermissionCatalog.OwnerGrants
            .Where(grant => grant.PermissionCode is not
                (ProjectPermissionCatalog.MemberManage or ProjectPermissionCatalog.RoleManage))
            .Select(grant => new
            {
                permissionCode = grant.PermissionCode,
                resourceScope = grant.ResourceScope,
                resourceId = grant.ResourceId,
                componentScopes = grant.Components
            })
            .ToArray();
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "Project",
                    projectSourceId,
                    null,
                    "sha256:access-project",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Access project",
                        ownerId = "owner-1",
                        createdAtUtc = "2026-08-30T10:00:00Z"
                    })),
                // Deliberately place members before roles to prove the writer
                // orders the resulting events by aggregate invariant.
                new LegacyRecord(
                    "ProjectMembership",
                    "legacy-owner-membership",
                    projectSourceId,
                    "sha256:owner-membership",
                    JsonSerializer.SerializeToElement(new
                    {
                        userId = "owner-1",
                        isActive = true,
                        roleIds = new[] { ownerRoleSourceId },
                        createdAtUtc = "2026-08-30T10:02:00Z"
                    })),
                new LegacyRecord(
                    "ProjectRole",
                    ownerRoleSourceId,
                    projectSourceId,
                    "sha256:owner-role",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Owner",
                        isOwner = true,
                        isSystemDefined = true,
                        permissions = ownerPermissions,
                        createdAtUtc = "2026-08-30T10:01:00Z"
                    }, JsonOptions)),
                new LegacyRecord(
                    "ProjectRole",
                    reviewerRoleSourceId,
                    projectSourceId,
                    "sha256:reviewer-role",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Reviewer",
                        isOwner = false,
                        isSystemDefined = false,
                        permissions = new[]
                        {
                            new
                            {
                                permissionCode = ProjectPermissionCatalog.RepositoryView,
                                resourceScope = ProjectResourceScope.Repository,
                                resourceId = "repo-1",
                                componentScopes = Array.Empty<ProjectComponentScope>()
                            }
                        },
                        createdAtUtc = "2026-08-30T10:03:00Z"
                    }, JsonOptions)),
                new LegacyRecord(
                    "ProjectMembership",
                    "legacy-reviewer-membership",
                    projectSourceId,
                    "sha256:reviewer-membership",
                    JsonSerializer.SerializeToElement(new
                    {
                        userId = "reviewer-1",
                        isActive = true,
                        roleIds = new[] { reviewerRoleSourceId },
                        createdAtUtc = "2026-08-30T10:04:00Z"
                    }))
            ]);
        var plan = Goal2MigrationPlanner.Plan(export);

        var first = await MartenProjectMigrationApplier.ApplyAsync(
            plan,
            export,
            _postgres.GetConnectionString(),
            CancellationToken.None);
        var second = await MartenProjectMigrationApplier.ApplyAsync(
            plan,
            export,
            _postgres.GetConnectionString(),
            CancellationToken.None);

        Assert.Equal(5, first.AppendedEventCount);
        Assert.Equal(0, first.SkippedEventCount);
        Assert.Equal(0, second.AppendedEventCount);
        Assert.Equal(5, second.SkippedEventCount);

        var projectId = Goal2MigrationPlanner.CreateDeterministicId("Project", projectSourceId);
        var accessStreamId = Goal2MigrationPlanner.AccessControlStreamId(projectId);
        await using var store = CreateStore();
        await using var query = store.QuerySession();
        var events = await query.Events.FetchStreamAsync(
            accessStreamId,
            long.MaxValue,
            timestamp: null,
            fromVersion: 0,
            token: CancellationToken.None);
        var current = await query.Query<ProjectAccessCurrent>()
            .SingleAsync(item => item.ProjectId == projectId);
        var aggregate = await query.Events.AggregateStreamAsync<ProjectAccessAggregate>(accessStreamId);

        Assert.Equal(6, events.Count);
        Assert.IsType<ProjectAccessInitialized>(events[0].Data);
        Assert.IsType<ProjectRoleCreated>(events[1].Data);
        Assert.IsType<ProjectRolePermissionsUpdated>(events[2].Data);
        Assert.IsType<ProjectMemberAdded>(events[3].Data);
        Assert.IsType<ProjectMemberRolesAssigned>(events[4].Data);
        Assert.IsType<ProjectMemberRolesAssigned>(events[5].Data);
        Assert.NotNull(aggregate);
        Assert.Equal(2, current.Roles.Count);
        Assert.Equal(2, current.Members.Count);
        var reviewer = current.Members.Single(member => member.UserId == "reviewer-1");
        Assert.Equal(
            [Goal2MigrationPlanner.CreateDeterministicId("ProjectRole", reviewerRoleSourceId)],
            reviewer.RoleIds);
        var reviewerPermissions = new ProjectPermissionEvaluator(query);
        var effective = await reviewerPermissions.GetEffectivePermissionsAsync(
            "reviewer-1",
            projectId,
            "repo-1",
            component: null,
            CancellationToken.None);
        Assert.True(effective.Has(ProjectPermissionCatalog.RepositoryView));
        Assert.Equal(
            Goal2MigrationPlanner.BuildSourceReference("ProjectRole", reviewerRoleSourceId),
            Header(events[1], EventMetadataHeaders.MigrationSourceReference));
        Assert.Equal(
            "sha256:reviewer-role",
            Header(events[2], EventMetadataHeaders.MigrationPayloadHash));
    }

    [Fact]
    public async Task RejectsUnknownAccessPermissionBeforeAppendingAnyEvent()
    {
        await EnsureSchemaAsync();
        const string projectSourceId = "legacy-invalid-access-project";
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "Project",
                    projectSourceId,
                    null,
                    "sha256:invalid-access-project",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Invalid access project",
                        ownerId = "owner-1",
                        createdAtUtc = "2026-08-30T10:00:00Z"
                    })),
                new LegacyRecord(
                    "ProjectRole",
                    "legacy-invalid-role",
                    projectSourceId,
                    "sha256:invalid-role",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Invalid role",
                        isOwner = false,
                        isSystemDefined = false,
                        permissions = new[]
                        {
                            new
                            {
                                permissionCode = "system.permission.grant",
                                resourceScope = ProjectResourceScope.Project
                            }
                        },
                        createdAtUtc = "2026-08-30T10:01:00Z"
                    }, JsonOptions))
            ]);
        var plan = Goal2MigrationPlanner.Plan(export);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MartenProjectMigrationApplier.ApplyAsync(
                plan,
                export,
                _postgres.GetConnectionString(),
                CancellationToken.None));

        var projectId = Goal2MigrationPlanner.CreateDeterministicId("Project", projectSourceId);
        var accessStreamId = Goal2MigrationPlanner.AccessControlStreamId(projectId);
        await using var store = CreateStore();
        await using var query = store.QuerySession();
        var events = await query.Events.FetchStreamAsync(
            accessStreamId,
            long.MaxValue,
            timestamp: null,
            fromVersion: 0,
            token: CancellationToken.None);
        Assert.Empty(events);
    }

    [Fact]
    public async Task AppliesPlanningAggregateAndChildRecordsWithDeterministicPlanStream()
    {
        const string projectSourceId = "legacy-planning-project";
        const string planSourceId = "legacy-plan-1";
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "Project",
                    projectSourceId,
                    null,
                    "sha256:planning-project",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Planning project",
                        ownerId = "owner-1",
                        createdAtUtc = "2026-08-30T10:00:00Z"
                    })),
                new LegacyRecord(
                    "Plan",
                    planSourceId,
                    projectSourceId,
                    "sha256:plan-1",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Release plan",
                        purpose = "Migrate the planning history",
                        createdAtUtc = "2026-08-30T10:01:00Z"
                    })),
                new LegacyRecord(
                    "Requirement",
                    "legacy-requirement-1",
                    projectSourceId,
                    "sha256:requirement-1",
                    JsonSerializer.SerializeToElement(new
                    {
                        planSourceId,
                        title = "Preserve planning intent",
                        description = "Keep the source reference traceable.",
                        createdAtUtc = "2026-08-30T10:02:00Z"
                    })),
                new LegacyRecord(
                    "Milestone",
                    "legacy-milestone-1",
                    projectSourceId,
                    "sha256:milestone-1",
                    JsonSerializer.SerializeToElement(new
                    {
                        planId = planSourceId,
                        name = "Cutover rehearsal",
                        targetDate = "2026-09-15",
                        createdAtUtc = "2026-08-30T10:03:00Z"
                    }))
            ]);
        var plan = Goal2MigrationPlanner.Plan(export);

        var first = await MartenProjectMigrationApplier.ApplyAsync(
            plan,
            export,
            _postgres.GetConnectionString(),
            CancellationToken.None);
        var second = await MartenProjectMigrationApplier.ApplyAsync(
            plan,
            export,
            _postgres.GetConnectionString(),
            CancellationToken.None);

        Assert.Equal(4, first.AppendedEventCount);
        Assert.Equal(0, first.SkippedEventCount);
        Assert.Equal(0, second.AppendedEventCount);
        Assert.Equal(4, second.SkippedEventCount);

        var planId = Goal2MigrationPlanner.CreateDeterministicId("Plan", planSourceId);
        await using var store = CreateStore();
        await using var query = store.QuerySession();
        var events = await query.Events.FetchStreamAsync(
            planId,
            long.MaxValue,
            timestamp: null,
            fromVersion: 0,
            token: CancellationToken.None);
        var current = await query.LoadAsync<PlanCurrent>(planId);
        var aggregate = await query.Events.AggregateStreamAsync<PlanAggregate>(planId);

        Assert.Equal(3, events.Count);
        Assert.IsType<PlanCreated>(events[0].Data);
        Assert.IsType<RequirementAdded>(events[1].Data);
        Assert.IsType<MilestoneAdded>(events[2].Data);
        Assert.NotNull(aggregate);
        Assert.Equal(planId, aggregate!.Id);
        Assert.NotNull(current);
        Assert.Equal("Release plan", current!.Name);
        Assert.Single(current.Requirements);
        Assert.Single(current.Milestones);
        Assert.Equal(3, current.Version);
        Assert.Equal(
            Goal2MigrationPlanner.BuildSourceReference("Requirement", "legacy-requirement-1"),
            Header(events[1], EventMetadataHeaders.MigrationSourceReference));
    }

    [Fact]
    public async Task AppliesTaskSnapshotWithoutInventingTransitionHistory()
    {
        const string projectSourceId = "legacy-task-project";
        const string taskSourceId = "legacy-task-1";
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "Project",
                    projectSourceId,
                    null,
                    "sha256:task-project",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Task project",
                        ownerId = "owner-1",
                        createdAtUtc = "2026-08-30T10:00:00Z"
                    })),
                new LegacyRecord(
                    "EngineeringTask",
                    taskSourceId,
                    projectSourceId,
                    "sha256:task-1",
                    JsonSerializer.SerializeToElement(new
                    {
                        title = "Reconcile task state",
                        description = "Keep AI verification distinct from approval.",
                        status = "ReadyForReview",
                        assigneeId = "engineer-1",
                        aiVerificationPassed = true,
                        humanReviewRequested = true,
                        humanReviewApproved = true,
                        sourceChangeKey = "commit:abc123",
                        updatedAtUtc = "2026-08-30T10:05:00Z"
                    }))
            ]);
        var plan = Goal2MigrationPlanner.Plan(export);

        var first = await MartenProjectMigrationApplier.ApplyAsync(
            plan,
            export,
            _postgres.GetConnectionString(),
            CancellationToken.None);
        var second = await MartenProjectMigrationApplier.ApplyAsync(
            plan,
            export,
            _postgres.GetConnectionString(),
            CancellationToken.None);

        Assert.Equal(2, first.AppendedEventCount);
        Assert.Equal(0, first.SkippedEventCount);
        Assert.Equal(0, second.AppendedEventCount);
        Assert.Equal(2, second.SkippedEventCount);

        var taskId = Goal2MigrationPlanner.CreateDeterministicId("EngineeringTask", taskSourceId);
        await using var store = CreateStore();
        await using var query = store.QuerySession();
        var events = await query.Events.FetchStreamAsync(
            taskId,
            long.MaxValue,
            timestamp: null,
            fromVersion: 0,
            token: CancellationToken.None);
        var current = await query.LoadAsync<TaskCurrent>(taskId);
        var aggregate = await query.Events.AggregateStreamAsync<EngineeringTask>(taskId);

        Assert.Equal(2, events.Count);
        Assert.IsType<TaskProposed>(events[0].Data);
        Assert.IsType<TaskLifecycleReconciled>(events[1].Data);
        Assert.NotNull(aggregate);
        Assert.Equal(TaskStatus.ReadyForReview, current!.Status);
        Assert.Equal("engineer-1", current.AssigneeId);
        Assert.True(current.AiVerificationPassed);
        Assert.True(current.HumanReviewRequested);
        Assert.Equal(2, current.Version);
        Assert.Equal("commit:abc123", current.SourceChangeKey);
    }

    private DocumentStore CreateStore()
    {
        return DocumentStore.For(options =>
        {
            options.Connection(_postgres.GetConnectionString());
            TcFlowEventStoreConfiguration.Configure(options);
            AccessControlMartenConfiguration.Configure(options);
            PlanningMartenConfiguration.Configure(options);
            TaskFlowMartenConfiguration.Configure(options);
            ProjectsMartenConfiguration.Configure(options);
        });
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private async Task EnsureSchemaAsync()
    {
        await using var store = CreateStore();
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    private static string? Header(IEvent @event, string key)
    {
        return @event.Headers is not null && @event.Headers.TryGetValue(key, out var value)
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            : null;
    }
}
