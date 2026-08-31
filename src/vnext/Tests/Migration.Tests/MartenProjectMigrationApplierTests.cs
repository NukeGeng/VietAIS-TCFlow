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
using VietAIS.TCFlow.Modules.Architecture.Configuration;
using VietAIS.TCFlow.Modules.Architecture.Domain;
using VietAIS.TCFlow.Modules.Architecture.Projections;
using VietAIS.TCFlow.Modules.EventStorming.Configuration;
using VietAIS.TCFlow.Modules.EventStorming.Domain;
using VietAIS.TCFlow.Modules.EventStorming.Projections;
using VietAIS.TCFlow.Modules.Planning.Configuration;
using VietAIS.TCFlow.Modules.Planning.Domain;
using VietAIS.TCFlow.Modules.Planning.Projections;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Configuration;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Domain;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Projections;
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
        Assert.True(current.HumanReviewApproved);
    }

    [Fact]
    public async Task PreservesTaskVersionsAndEvidenceOnTheTaskStreamIdempotently()
    {
        const string projectSourceId = "legacy-history-project";
        const string taskSourceId = "legacy-history-task";
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "Project",
                    projectSourceId,
                    null,
                    "sha256:history-project",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Task history project",
                        ownerId = "owner-1",
                        createdAtUtc = "2026-08-30T10:00:00Z"
                    })),
                new LegacyRecord(
                    "EngineeringTask",
                    taskSourceId,
                    projectSourceId,
                    "sha256:history-task",
                    JsonSerializer.SerializeToElement(new
                    {
                        title = "Import task history",
                        status = "Upcoming",
                        updatedAtUtc = "2026-08-30T10:01:00Z"
                    })),
                new LegacyRecord(
                    "TaskVersion",
                    "legacy-task-version-1",
                    projectSourceId,
                    "sha256:history-version",
                    JsonSerializer.SerializeToElement(new
                    {
                        taskSourceId,
                        version = 1,
                        snapshot = new { title = "Import task history", status = "Suggested" },
                        changeReason = "initial import",
                        changedBy = "legacy-user",
                        changedAtUtc = "2026-08-30T10:02:00Z"
                    })),
                new LegacyRecord(
                    "TaskEvidence",
                    "legacy-task-evidence-1",
                    projectSourceId,
                    "sha256:history-evidence",
                    JsonSerializer.SerializeToElement(new
                    {
                        taskSourceId,
                        evidenceKind = "Verification",
                        summary = "Legacy verification evidence",
                        location = "tests/legacy.txt",
                        sourceChangeKey = "commit:def456",
                        confidence = 0.95m,
                        createdAtUtc = "2026-08-30T10:03:00Z",
                        createdBy = "legacy-user"
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

        Assert.Equal(4, events.Count);
        Assert.IsType<TaskProposed>(events[0].Data);
        Assert.IsType<TaskLifecycleReconciled>(events[1].Data);
        Assert.IsType<TaskVersionImported>(events[2].Data);
        Assert.IsType<TaskEvidenceImported>(events[3].Data);
        Assert.NotNull(current);
        Assert.NotNull(aggregate);
        Assert.Equal(1, aggregate!.ImportedVersionCount);
        Assert.Equal(1, aggregate.ImportedEvidenceCount);
        Assert.Single(current!.ImportedVersions);
        Assert.Equal(1, current.ImportedVersions[0].Version);
        Assert.Contains("initial import", current.ImportedVersions[0].ChangeReason, StringComparison.Ordinal);
        Assert.Single(current.ImportedEvidence);
        Assert.Equal("Verification", current.ImportedEvidence[0].Kind);
        Assert.Equal("commit:def456", current.ImportedEvidence[0].SourceChangeKey);
    }

    [Fact]
    public async Task AppliesRepositoryAnalysisArtifactsAndImpactsOnTheAnalysisStreamIdempotently()
    {
        const string projectSourceId = "legacy-repository-project";
        const string analysisSourceId = "legacy-analysis-run-1";
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "Project",
                    projectSourceId,
                    null,
                    "sha256:repository-project",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Repository project",
                        ownerId = "owner-1",
                        createdAtUtc = "2026-08-30T10:00:00Z"
                    })),
                new LegacyRecord(
                    "AnalysisRun",
                    analysisSourceId,
                    projectSourceId,
                    "sha256:analysis-run",
                    JsonSerializer.SerializeToElement(new
                    {
                        repositoryId = "NukeGeng/Portfolio",
                        commitSha = "abc123",
                        startedAtUtc = "2026-08-30T10:01:00Z"
                    })),
                new LegacyRecord(
                    "SourceArtifact",
                    "legacy-artifact-1",
                    projectSourceId,
                    "sha256:artifact-1",
                    JsonSerializer.SerializeToElement(new
                    {
                        analysisRunSourceId = analysisSourceId,
                        path = "src/Orders.cs",
                        kind = "Aggregate",
                        symbol = "OrderAggregate",
                        details = "Legacy source artifact",
                        observedAtUtc = "2026-08-30T10:02:00Z"
                    })),
                new LegacyRecord(
                    "SourceImpact",
                    "legacy-impact-1",
                    projectSourceId,
                    "sha256:impact-1",
                    JsonSerializer.SerializeToElement(new
                    {
                        analysisRunSourceId = analysisSourceId,
                        impactKey = "impact-1",
                        changeKey = "change-1",
                        affectedArtifactKey = "artifact-1",
                        severity = "High",
                        reason = "Legacy impact evidence",
                        confidence = 0.9m,
                        observedAtUtc = "2026-08-30T10:03:00Z"
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

        var analysisId = Goal2MigrationPlanner.CreateDeterministicId("AnalysisRun", analysisSourceId);
        await using var store = CreateStore();
        await using var query = store.QuerySession();
        var events = await query.Events.FetchStreamAsync(
            analysisId,
            long.MaxValue,
            timestamp: null,
            fromVersion: 0,
            token: CancellationToken.None);
        var current = await query.LoadAsync<AnalysisCurrent>(analysisId);
        var aggregate = await query.Events.AggregateStreamAsync<AnalysisRun>(analysisId);

        Assert.Equal(3, events.Count);
        Assert.IsType<AnalysisStarted>(events[0].Data);
        Assert.IsType<ArtifactObserved>(events[1].Data);
        Assert.IsType<ImpactRecorded>(events[2].Data);
        Assert.NotNull(aggregate);
        Assert.NotNull(current);
        Assert.Equal("NukeGeng/Portfolio", current!.RepositoryId);
        Assert.Equal("abc123", current.CommitSha);
        Assert.Single(current.Artifacts);
        Assert.Equal("src/Orders.cs", current.Artifacts[0].Path);
        Assert.Single(current.Impacts);
        Assert.Equal("High", current.Impacts[0].Severity);
        Assert.Equal(0.9m, current.Impacts[0].Confidence);
        Assert.Equal(3, current.Version);
        Assert.Equal(
            Goal2MigrationPlanner.BuildSourceReference("SourceImpact", "legacy-impact-1"),
            Header(events[2], EventMetadataHeaders.MigrationSourceReference));
    }

    [Fact]
    public async Task AppliesEventStormingBoardNodesAndConnectionsOnTheBoardStreamIdempotently()
    {
        const string projectSourceId = "legacy-storming-project";
        const string boardSourceId = "legacy-board-1";
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "Project",
                    projectSourceId,
                    null,
                    "sha256:storming-project",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Storming project",
                        ownerId = "owner-1",
                        createdAtUtc = "2026-08-30T10:00:00Z"
                    })),
                new LegacyRecord(
                    "StormingBoard",
                    boardSourceId,
                    projectSourceId,
                    "sha256:storming-board",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Order flow",
                        createdAtUtc = "2026-08-30T10:01:00Z"
                    })),
                new LegacyRecord(
                    "StormingNode",
                    "legacy-node-command",
                    projectSourceId,
                    "sha256:storming-node-command",
                    JsonSerializer.SerializeToElement(new
                    {
                        boardSourceId,
                        nodeType = "Command",
                        label = "Create order",
                        description = "Starts the order flow",
                        createdAtUtc = "2026-08-30T10:02:00Z"
                    })),
                new LegacyRecord(
                    "StormingNode",
                    "legacy-node-event",
                    projectSourceId,
                    "sha256:storming-node-event",
                    JsonSerializer.SerializeToElement(new
                    {
                        boardSourceId,
                        nodeType = "DomainEvent",
                        label = "Order created",
                        createdAtUtc = "2026-08-30T10:03:00Z"
                    })),
                new LegacyRecord(
                    "StormingConnection",
                    "legacy-connection-1",
                    projectSourceId,
                    "sha256:storming-connection",
                    JsonSerializer.SerializeToElement(new
                    {
                        boardSourceId,
                        fromNodeSourceId = "legacy-node-command",
                        toNodeSourceId = "legacy-node-event",
                        relationship = "emits",
                        createdAtUtc = "2026-08-30T10:04:00Z"
                    })),
                new LegacyRecord(
                    "StormingHotspot",
                    "legacy-hotspot-1",
                    projectSourceId,
                    "sha256:storming-hotspot",
                    JsonSerializer.SerializeToElement(new
                    {
                        boardSourceId,
                        nodeSourceId = "legacy-node-event",
                        reason = "Need to confirm consistency boundary",
                        markedAtUtc = "2026-08-30T10:05:00Z"
                    })),
                new LegacyRecord(
                    "StormingNodeOrder",
                    "legacy-order-1",
                    projectSourceId,
                    "sha256:storming-order",
                    JsonSerializer.SerializeToElement(new
                    {
                        boardSourceId,
                        nodeSourceId = "legacy-node-event",
                        position = 0,
                        reorderedAtUtc = "2026-08-30T10:06:00Z"
                    }))
            ]);
        var plan = Goal2MigrationPlanner.Plan(export);

        var first = await MartenProjectMigrationApplier.ApplyAsync(plan, export, _postgres.GetConnectionString(), CancellationToken.None);
        var second = await MartenProjectMigrationApplier.ApplyAsync(plan, export, _postgres.GetConnectionString(), CancellationToken.None);

        Assert.Equal(7, first.AppendedEventCount);
        Assert.Equal(7, second.SkippedEventCount);
        var boardId = Goal2MigrationPlanner.CreateDeterministicId("StormingBoard", boardSourceId);
        await using var store = CreateStore();
        await using var query = store.QuerySession();
        var events = await query.Events.FetchStreamAsync(boardId, long.MaxValue, timestamp: null, fromVersion: 0, token: CancellationToken.None);
        var current = await query.LoadAsync<BoardCanvas>(boardId);

        Assert.Equal(6, events.Count);
        Assert.IsType<BoardCreated>(events[0].Data);
        Assert.IsType<StormingNodeAdded>(events[1].Data);
        Assert.IsType<StormingNodeAdded>(events[2].Data);
        Assert.IsType<StormingNodesConnected>(events[3].Data);
        Assert.IsType<StormingHotspotMarked>(events[4].Data);
        Assert.IsType<StormingNodeReordered>(events[5].Data);
        Assert.NotNull(current);
        Assert.Equal("Order flow", current!.Name);
        Assert.Equal(2, current.Nodes.Count);
        Assert.Single(current.Connections);
        Assert.Contains(current.Nodes, node => node.IsHotspot);
        Assert.Equal(6, current.Version);
    }

    [Fact]
    public async Task AppliesArchitectureModelAndRelationshipsOnTheModelStreamIdempotently()
    {
        const string projectSourceId = "legacy-architecture-project";
        const string modelSourceId = "legacy-architecture-model";
        var export = new LegacyExport(
            1,
            [
                new LegacyRecord(
                    "Project",
                    projectSourceId,
                    null,
                    "sha256:architecture-project",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Architecture project",
                        ownerId = "owner-1",
                        createdAtUtc = "2026-08-30T10:00:00Z"
                    })),
                new LegacyRecord(
                    "ArchitectureModel",
                    modelSourceId,
                    projectSourceId,
                    "sha256:architecture-model",
                    JsonSerializer.SerializeToElement(new
                    {
                        name = "Order architecture",
                        createdAtUtc = "2026-08-30T10:01:00Z"
                    })),
                new LegacyRecord(
                    "ArchitectureModule",
                    "legacy-module-api",
                    projectSourceId,
                    "sha256:architecture-module-api",
                    JsonSerializer.SerializeToElement(new
                    {
                        modelSourceId,
                        name = "API",
                        createdAtUtc = "2026-08-30T10:02:00Z"
                    })),
                new LegacyRecord(
                    "ArchitectureModule",
                    "legacy-module-domain",
                    projectSourceId,
                    "sha256:architecture-module-domain",
                    JsonSerializer.SerializeToElement(new
                    {
                        modelSourceId,
                        name = "Domain",
                        createdAtUtc = "2026-08-30T10:03:00Z"
                    })),
                new LegacyRecord(
                    "ArchitectureModuleRelationship",
                    "legacy-module-link",
                    projectSourceId,
                    "sha256:architecture-module-link",
                    JsonSerializer.SerializeToElement(new
                    {
                        modelSourceId,
                        fromModuleSourceId = "legacy-module-api",
                        toModuleSourceId = "legacy-module-domain",
                        relationship = "depends-on",
                        createdAtUtc = "2026-08-30T10:04:00Z"
                    })),
                new LegacyRecord(
                    "ArchitectureEntity",
                    "legacy-entity-order",
                    projectSourceId,
                    "sha256:architecture-entity-order",
                    JsonSerializer.SerializeToElement(new
                    {
                        modelSourceId,
                        name = "Order",
                        createdAtUtc = "2026-08-30T10:05:00Z"
                    })),
                new LegacyRecord(
                    "ArchitectureEntity",
                    "legacy-entity-payment",
                    projectSourceId,
                    "sha256:architecture-entity-payment",
                    JsonSerializer.SerializeToElement(new
                    {
                        modelSourceId,
                        name = "Payment",
                        createdAtUtc = "2026-08-30T10:06:00Z"
                    })),
                new LegacyRecord(
                    "ArchitectureDataRelationship",
                    "legacy-data-link",
                    projectSourceId,
                    "sha256:architecture-data-link",
                    JsonSerializer.SerializeToElement(new
                    {
                        modelSourceId,
                        fromEntitySourceId = "legacy-entity-order",
                        toEntitySourceId = "legacy-entity-payment",
                        relationship = "has-payment",
                        createdAtUtc = "2026-08-30T10:07:00Z"
                    })),
                new LegacyRecord(
                    "ArchitectureDrift",
                    "legacy-drift-1",
                    projectSourceId,
                    "sha256:architecture-drift",
                    JsonSerializer.SerializeToElement(new
                    {
                        modelSourceId,
                        driftKey = "missing-documentation",
                        summary = "The API dependency is undocumented",
                        evidence = "src/Api/Orders.cs references Domain",
                        detectedAtUtc = "2026-08-30T10:08:00Z"
                    }))
            ]);
        var plan = Goal2MigrationPlanner.Plan(export);

        var first = await MartenProjectMigrationApplier.ApplyAsync(plan, export, _postgres.GetConnectionString(), CancellationToken.None);
        var second = await MartenProjectMigrationApplier.ApplyAsync(plan, export, _postgres.GetConnectionString(), CancellationToken.None);

        Assert.Equal(9, first.AppendedEventCount);
        Assert.Equal(9, second.SkippedEventCount);
        var modelId = Goal2MigrationPlanner.CreateDeterministicId("ArchitectureModel", modelSourceId);
        await using var store = CreateStore();
        await using var query = store.QuerySession();
        var events = await query.Events.FetchStreamAsync(modelId, long.MaxValue, timestamp: null, fromVersion: 0, token: CancellationToken.None);
        var current = await query.LoadAsync<ArchitectureCurrent>(modelId);

        Assert.Equal(8, events.Count);
        Assert.IsType<ArchitectureModelCreated>(events[0].Data);
        Assert.IsType<ArchitectureModuleAdded>(events[1].Data);
        Assert.IsType<ArchitectureModuleAdded>(events[2].Data);
        Assert.IsType<ArchitectureModulesConnected>(events[3].Data);
        Assert.IsType<ArchitectureEntityAdded>(events[4].Data);
        Assert.IsType<ArchitectureEntityAdded>(events[5].Data);
        Assert.IsType<ArchitectureDataRelationshipAdded>(events[6].Data);
        Assert.IsType<ArchitectureDriftRecorded>(events[7].Data);
        Assert.NotNull(current);
        Assert.Equal("Order architecture", current!.Name);
        Assert.Equal(2, current.Modules.Count);
        Assert.Equal(2, current.Entities.Count);
        Assert.Single(current.ModuleRelationships);
        Assert.Single(current.DataRelationships);
        Assert.Single(current.Drifts);
        Assert.Equal(8, current.Version);
    }

    private DocumentStore CreateStore()
    {
        return DocumentStore.For(options =>
        {
            options.Connection(_postgres.GetConnectionString());
            TcFlowEventStoreConfiguration.Configure(options);
            AccessControlMartenConfiguration.Configure(options);
            PlanningMartenConfiguration.Configure(options);
            RepositoryMartenConfiguration.Configure(options);
            TaskFlowMartenConfiguration.Configure(options);
            StormingMartenConfiguration.Configure(options);
            ArchitectureMartenConfiguration.Configure(options);
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
