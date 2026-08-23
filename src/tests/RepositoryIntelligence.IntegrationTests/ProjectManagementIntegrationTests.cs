using System.Net;
using System.Net.Http.Json;
using Asp.Versioning.Conventions;
using Carter;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Infrastructure.Exceptions;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;
using Xunit;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.IntegrationTests;

public sealed class ProjectManagementIntegrationTests
{
    [Theory]
    [InlineData(TaskLifecycleStatus.Upcoming, TaskLifecycleStatus.InProgress)]
    [InlineData(TaskLifecycleStatus.Upcoming, TaskLifecycleStatus.Cancelled)]
    [InlineData(TaskLifecycleStatus.InProgress, TaskLifecycleStatus.ReadyForReview)]
    [InlineData(TaskLifecycleStatus.InProgress, TaskLifecycleStatus.Blocked)]
    [InlineData(TaskLifecycleStatus.InProgress, TaskLifecycleStatus.Cancelled)]
    [InlineData(TaskLifecycleStatus.ReadyForReview, TaskLifecycleStatus.Completed)]
    [InlineData(TaskLifecycleStatus.ReadyForReview, TaskLifecycleStatus.Rejected)]
    [InlineData(TaskLifecycleStatus.ReadyForReview, TaskLifecycleStatus.InProgress)]
    [InlineData(TaskLifecycleStatus.Blocked, TaskLifecycleStatus.InProgress)]
    [InlineData(TaskLifecycleStatus.Blocked, TaskLifecycleStatus.Cancelled)]
    [InlineData(TaskLifecycleStatus.Rejected, TaskLifecycleStatus.InProgress)]
    [InlineData(TaskLifecycleStatus.Rejected, TaskLifecycleStatus.Cancelled)]
    public void Task_lifecycle_allows_defined_transitions(
        TaskLifecycleStatus from,
        TaskLifecycleStatus to)
    {
        Assert.True(TaskLifecycle.CanTransition(from, to));
    }

    [Theory]
    [InlineData(TaskLifecycleStatus.Upcoming, TaskLifecycleStatus.Completed)]
    [InlineData(TaskLifecycleStatus.Blocked, TaskLifecycleStatus.Completed)]
    [InlineData(TaskLifecycleStatus.Completed, TaskLifecycleStatus.InProgress)]
    [InlineData(TaskLifecycleStatus.Cancelled, TaskLifecycleStatus.InProgress)]
    [InlineData(TaskLifecycleStatus.Rejected, TaskLifecycleStatus.Completed)]
    public void Task_lifecycle_rejects_undefined_or_terminal_transitions(
        TaskLifecycleStatus from,
        TaskLifecycleStatus to)
    {
        Assert.False(TaskLifecycle.CanTransition(from, to));
    }

    [Fact]
    public async Task Project_creation_requires_authentication_and_initializes_default_state_atomically()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(postgres.GetConnectionString(), mapEndpoints: true);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        var unauthorized = await client.PostAsJsonAsync(
            "api/v1/projects",
            new CreateProjectRequest("Source Planner"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var ownerId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, ownerId.ToString());
        var invalid = await client.PostAsJsonAsync(
            "api/v1/projects",
            new CreateProjectRequest(" "),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var created = await client.PostAsJsonAsync(
            "api/v1/projects",
            new CreateProjectRequest("Source Planner"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var response = await created.Content.ReadFromJsonAsync<CreateProjectResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        Assert.Equal(ownerId, response.Project.PrimaryOwnerId);
        Assert.True(response.OwnerRole.IsOwner);
        Assert.True(response.OwnerRole.IsSystemDefined);
        Assert.Equal(ProjectLifecycleStatus.Active, response.State.Status);
        Assert.Equal(ConventionProfileStatus.PendingAnalysis, response.ConventionProfile.Status);
        Assert.Equal(AiTrustLevel.SuggestOnly, response.AiPolicy.TrustLevel);

        await using var session = app.Services.GetRequiredService<IQuerySession>();
        Assert.NotNull(await session.LoadAsync<Project>(
            response.Project.Id,
            TestContext.Current.CancellationToken));
        Assert.NotNull(await session.LoadAsync<ProjectState>(
            response.Project.Id,
            TestContext.Current.CancellationToken));
        Assert.NotNull(await session.LoadAsync<AuthorityPolicy>(
            response.Project.Id,
            TestContext.Current.CancellationToken));
        Assert.NotNull(await session.LoadAsync<ConventionProfile>(
            response.Project.Id,
            TestContext.Current.CancellationToken));
        Assert.NotNull(await session.LoadAsync<AiPermissionPolicy>(
            response.Project.Id,
            TestContext.Current.CancellationToken));
        var audit = await session.Query<AuditRecord>()
            .SingleAsync(
                item => item.ProjectId == response.Project.Id && item.Action == "project.create",
                TestContext.Current.CancellationToken);
        Assert.Equal(ownerId, audit.ActorId);
        Assert.NotNull(audit.After);
    }

    [Fact]
    public async Task Task_workflow_enforces_scope_preserves_trace_and_separates_ai_from_human_approval()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(postgres.GetConnectionString(), mapEndpoints: false);
        await using var scope = app.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var aiActorId = Guid.NewGuid();
        var bootstrap = await mediator.Send(
            new CreateProjectCommand(ownerId, "Workflow Project"),
            TestContext.Current.CancellationToken);
        var projectId = bootstrap.Project.Id;

        var member = new ProjectMembership(
            Guid.NewGuid(),
            projectId,
            memberId,
            IsActive: true,
            []);
        await using (var memberScope = app.Services.CreateAsyncScope())
        {
            var seed = memberScope.ServiceProvider.GetRequiredService<IDocumentSession>();
            seed.Store(member);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var repository = await mediator.Send(
            new CreateProjectRepositoryCommand(
                ownerId,
                projectId,
                "TCFlow",
                RepositoryProviderKind.Local,
                "/workspace/tcflow",
                null,
                "main"),
            TestContext.Current.CancellationToken);
        var component = await mediator.Send(
            new CreateProjectComponentCommand(
                ownerId,
                projectId,
                repository.Id,
                "Backend",
                ComponentScopeKind.Backend,
                "src/api"),
            TestContext.Current.CancellationToken);
        var feature = await mediator.Send(
            new CreateProjectFeatureCommand(
                ownerId,
                projectId,
                "Project Management",
                "Core project and task workflow"),
            TestContext.Current.CancellationToken);

        var change = new SourceChange(
            Guid.NewGuid(),
            projectId,
            repository.Id,
            "abc123",
            "Add project management workflow",
            DateTimeOffset.UtcNow);
        var artifact = new SourceArtifact(
            Guid.NewGuid(),
            projectId,
            repository.Id,
            component.Id,
            "aspnet_endpoint",
            "CreateProject",
            "Management/ProjectManagementEndpoints.cs");
        var impact = new SourceImpact(
            Guid.NewGuid(),
            projectId,
            change.Id,
            artifact.Id,
            "high",
            "Project workflow requires backend API support.",
            0.96m);
        await using (var traceScope = app.Services.CreateAsyncScope())
        {
            var traceSeed = traceScope.ServiceProvider.GetRequiredService<IDocumentSession>();
            traceSeed.Store(change);
            traceSeed.Store(artifact);
            traceSeed.Store(impact);
            await traceSeed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var task = await mediator.Send(
            new CreateEngineeringTaskCommand(
                ownerId,
                projectId,
                repository.Id,
                component.Id,
                feature.Id,
                "Implement project workflow",
                "Implement and verify P3.",
                TaskPriority.High,
                change.Id,
                [artifact.Id],
                [impact.Id],
                [artifact.Name],
                ["Project contract"],
                ["Marten documents"],
                ["Preserve audit history"],
                []),
            TestContext.Current.CancellationToken);
        Assert.Equal(TaskLifecycleStatus.Upcoming, task.Status);
        Assert.Equal(change.Id, task.SourceTrace.SourceChangeId);
        Assert.Contains(artifact.Id, task.SourceTrace.ArtifactIds);
        Assert.Contains(impact.Id, task.SourceTrace.ImpactIds);

        var role = await mediator.Send(
            new CreateProjectRoleCommand(ownerId, projectId, "Assigned Developer"),
            TestContext.Current.CancellationToken);
        await mediator.Send(
            new UpdateProjectRolePermissionsCommand(
                ownerId,
                projectId,
                role.Id,
                [
                    new RolePermissionRequest(
                        ProjectPermissionCodes.TaskView,
                        ResourceScopeKind.Assigned,
                        null,
                        [ComponentScopeKind.Backend]),
                    new RolePermissionRequest(
                        ProjectPermissionCodes.TaskStatusUpdate,
                        ResourceScopeKind.Assigned,
                        null,
                        [ComponentScopeKind.Backend])
                ]),
            TestContext.Current.CancellationToken);
        await mediator.Send(
            new AssignMemberRolesCommand(ownerId, projectId, memberId, [role.Id]),
            TestContext.Current.CancellationToken);
        await mediator.Send(
            new AssignEngineeringTaskCommand(ownerId, projectId, task.Id, memberId),
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ForbiddenException>(() => mediator.Send(
            new TransitionEngineeringTaskCommand(
                outsiderId,
                projectId,
                task.Id,
                TaskLifecycleStatus.InProgress,
                null),
            TestContext.Current.CancellationToken));
        var inProgress = await mediator.Send(
            new TransitionEngineeringTaskCommand(
                memberId,
                projectId,
                task.Id,
                TaskLifecycleStatus.InProgress,
                "implementation started"),
            TestContext.Current.CancellationToken);
        Assert.Equal(TaskLifecycleStatus.InProgress, inProgress.Status);

        await Assert.ThrowsAsync<ProjectManagementValidationException>(() => mediator.Send(
            new TransitionEngineeringTaskCommand(
                ownerId,
                projectId,
                task.Id,
                TaskLifecycleStatus.Completed,
                null),
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ForbiddenException>(() => mediator.Send(
            new TransitionEngineeringTaskCommand(
                memberId,
                projectId,
                task.Id,
                TaskLifecycleStatus.Completed,
                null),
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ForbiddenException>(() => mediator.Send(
            new ReviewEngineeringTaskCommand(
                memberId,
                projectId,
                task.Id,
                TaskReviewDecision.Approve,
                null),
            TestContext.Current.CancellationToken));

        var evidence = await mediator.Send(
            new AddTaskEvidenceCommand(
                ownerId,
                TaskActorType.User,
                projectId,
                task.Id,
                TaskEvidenceKind.Impact,
                "Impact is confirmed by source evidence.",
                artifact.Path,
                change.Id,
                artifact.Id,
                impact.Id,
                0.96m),
            TestContext.Current.CancellationToken);
        Assert.NotEqual(Guid.Empty, evidence.Id);

        await mediator.Send(
            new UpdateAiPermissionPolicyCommand(
                ownerId,
                projectId,
                AiTrustLevel.UpdateTasks,
                [ProjectPermissionCodes.AiTaskUpdate]),
            TestContext.Current.CancellationToken);
        var aiVerified = await mediator.Send(
            new RecordTaskAiVerificationCommand(
                aiActorId,
                projectId,
                task.Id,
                AiVerificationStatus.Passed,
                "Static comparison matches the expected contract."),
            TestContext.Current.CancellationToken);
        Assert.Equal(AiVerificationStatus.Passed, aiVerified.AiVerification);
        Assert.Equal(HumanApprovalStatus.Pending, aiVerified.HumanApproval);

        await mediator.Send(
            new TransitionEngineeringTaskCommand(
                ownerId,
                projectId,
                task.Id,
                TaskLifecycleStatus.ReadyForReview,
                "verification passed"),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ForbiddenException>(() => mediator.Send(
            new TransitionEngineeringTaskCommand(
                memberId,
                projectId,
                task.Id,
                TaskLifecycleStatus.Rejected,
                null),
            TestContext.Current.CancellationToken));
        await mediator.Send(
            new ReviewEngineeringTaskCommand(
                ownerId,
                projectId,
                task.Id,
                TaskReviewDecision.Approve,
                "Human review approved."),
            TestContext.Current.CancellationToken);
        var completed = await mediator.Send(
            new TransitionEngineeringTaskCommand(
                ownerId,
                projectId,
                task.Id,
                TaskLifecycleStatus.Completed,
                "human approval recorded"),
            TestContext.Current.CancellationToken);
        Assert.Equal(TaskLifecycleStatus.Completed, completed.Status);
        Assert.Equal(AiVerificationStatus.Passed, completed.AiVerification);
        Assert.Equal(HumanApprovalStatus.Approved, completed.HumanApproval);

        await Assert.ThrowsAsync<ProjectManagementValidationException>(() => mediator.Send(
            new TransitionEngineeringTaskCommand(
                ownerId,
                projectId,
                task.Id,
                TaskLifecycleStatus.InProgress,
                null),
            TestContext.Current.CancellationToken));

        var search = await mediator.Send(
            new SearchEngineeringTasksQuery(
                memberId,
                projectId,
                1,
                20,
                "workflow",
                TaskLifecycleStatus.Completed,
                TaskPriority.High,
                repository.Id,
                feature.Id,
                memberId),
            TestContext.Current.CancellationToken);
        Assert.Single(search.Items);
        Assert.Equal(1, search.TotalCount);
        await Assert.ThrowsAsync<ForbiddenException>(() => mediator.Send(
            new SearchEngineeringTasksQuery(
                outsiderId,
                projectId,
                1,
                20,
                null,
                null,
                null,
                null,
                null,
                null),
            TestContext.Current.CancellationToken));

        var history = await mediator.Send(
            new GetTaskHistoryQuery(ownerId, projectId, task.Id),
            TestContext.Current.CancellationToken);
        Assert.Equal(8, history.Count);
        Assert.Equal(Enumerable.Range(1, 8), history.Select(version => version.Version));
        Assert.Contains(history, version => version.Snapshot.AiVerification == AiVerificationStatus.Passed);
        Assert.Contains(history, version => version.Snapshot.HumanApproval == HumanApprovalStatus.Approved);
        Assert.Contains(history, version => version.Assignment?.AssigneeId == memberId);
        Assert.Contains(history, version => version.Review?.Decision == TaskReviewDecision.Approve);
        Assert.Contains(history, version => version.Evidence?.Id == evidence.Id);

        await using var verificationScope = app.Services.CreateAsyncScope();
        await using var verification = verificationScope.ServiceProvider.GetRequiredService<IQuerySession>();
        var persisted = await verification.LoadAsync<EngineeringTask>(
            task.Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.Contains(evidence.Id, persisted.SourceTrace.EvidenceIds);
        var actions = await verification.Query<AuditRecord>()
            .Where(record => record.ProjectId == projectId)
            .Select(record => record.Action)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains("task.create", actions);
        Assert.Contains("task.assign", actions);
        Assert.Contains("task.evidence.add", actions);
        Assert.Contains("task.ai.verify", actions);
        Assert.Contains("task.status.update", actions);
        Assert.Contains("task.review", actions);
    }

    private static async Task<WebApplication> BuildApplicationAsync(
        string connectionString,
        bool mapEndpoints)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["DatabaseOptions:ConnectionString"] = connectionString;
        builder.Configuration["RepositoryAnalysis:Enabled"] = "false";
        builder.RegisterRepositoryIntelligenceServices();
        builder.Services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<CreateProjectCommand>());

        if (mapEndpoints)
        {
            builder.Services.AddCarter(configurator: configuration =>
            {
                configuration.WithModule<ProjectAuthorizationEndpoints>();
                configuration.WithModule<ProjectManagementEndpoints>();
            });
            builder.Services.AddApiVersioning();
            builder.Services
                .AddAuthentication(TestAuthenticationHandler.AuthenticationSchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationSchemeName,
                    _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddExceptionHandler<CustomExceptionHandler>();
            builder.Services.AddProblemDetails();
        }

        var app = builder.Build();
        if (mapEndpoints)
        {
            app.UseExceptionHandler();
            app.UseAuthentication();
            app.UseAuthorization();
            var versions = app.NewApiVersionSet().HasApiVersion(1).Build();
            app.MapGroup("api/v{version:apiVersion}")
                .WithApiVersionSet(versions)
                .MapCarter();
        }

        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync(TestContext.Current.CancellationToken);
        var store = app.Services.GetRequiredService<IDocumentStore>();
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        return app;
    }
}
