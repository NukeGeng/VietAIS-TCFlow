using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning.Conventions;
using Carter;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Infrastructure.Exceptions;
using JasperFx;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using Xunit;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.IntegrationTests;

public sealed class ProjectAuthorizationIntegrationTests
{
    [Fact]
    public async Task Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(postgres.GetConnectionString(), mapEndpoints: false);

        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var systemAdminId = Guid.NewGuid();
        var repositoryId = Guid.NewGuid();
        var seeded = await SeedProjectAsync(app.Services, projectId, ownerId, memberId);
        await SeedProjectAsync(app.Services, otherProjectId, Guid.NewGuid(), Guid.NewGuid());

        await using (var firstScope = app.Services.CreateAsyncScope())
        await using (var secondScope = app.Services.CreateAsyncScope())
        {
            var firstSession = firstScope.ServiceProvider.GetRequiredService<IDocumentSession>();
            var secondSession = secondScope.ServiceProvider.GetRequiredService<IDocumentSession>();
            var firstCopy = await firstSession.LoadAsync<Project>(
                projectId,
                TestContext.Current.CancellationToken);
            var secondCopy = await secondSession.LoadAsync<Project>(
                projectId,
                TestContext.Current.CancellationToken);
            Assert.NotNull(firstCopy);
            Assert.NotNull(secondCopy);

            firstSession.Store(firstCopy with { Name = "Concurrency winner" });
            secondSession.Store(secondCopy with { Name = "Stale concurrent update" });
            await firstSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<ConcurrencyException>(() =>
                secondSession.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var scope = app.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        var evaluator = scope.ServiceProvider.GetRequiredService<IProjectPermissionEvaluator>();

        await Assert.ThrowsAsync<ForbiddenException>(() => evaluator.EnsureAuthorizedAsync(
            ownerId,
            ProjectPermissionCodes.RoleCreate,
            new AuthorizationResourceContext(otherProjectId),
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ForbiddenException>(() => evaluator.EnsureAuthorizedAsync(
            systemAdminId,
            ProjectPermissionCodes.RoleCreate,
            new AuthorizationResourceContext(projectId),
            TestContext.Current.CancellationToken));

        var role = await mediator.Send(
            new CreateProjectRoleCommand(ownerId, projectId, "Backend Lead"),
            TestContext.Current.CancellationToken);
        var updatedRole = await mediator.Send(
            new UpdateProjectRolePermissionsCommand(
                ownerId,
                projectId,
                role.Id,
                [
                    new RolePermissionRequest(
                        ProjectPermissionCodes.TaskAssign,
                        ResourceScopeKind.Repository,
                        repositoryId,
                        [ComponentScopeKind.Backend]),
                    new RolePermissionRequest(
                        ProjectPermissionCodes.TaskUpdate,
                        ResourceScopeKind.Own,
                        null,
                        [ComponentScopeKind.Backend]),
                    new RolePermissionRequest(
                        ProjectPermissionCodes.TaskView,
                        ResourceScopeKind.Assigned,
                        null,
                        [ComponentScopeKind.Backend])
                ]),
            TestContext.Current.CancellationToken);
        Assert.Equal(3, updatedRole.Permissions.Length);

        await Assert.ThrowsAsync<ProjectAuthorizationValidationException>(() => mediator.Send(
            new UpdateProjectRolePermissionsCommand(
                ownerId,
                projectId,
                role.Id,
                [
                    new RolePermissionRequest(
                        SystemPermissionCodes.UserManage,
                        ResourceScopeKind.All,
                        null,
                        [])
                ]),
            TestContext.Current.CancellationToken));

        await mediator.Send(
            new AssignMemberRolesCommand(ownerId, projectId, memberId, [role.Id]),
            TestContext.Current.CancellationToken);

        var matching = await evaluator.GetEffectivePermissionsAsync(
            memberId,
            new AuthorizationResourceContext(projectId, repositoryId, ComponentScopeKind.Backend),
            TestContext.Current.CancellationToken);
        var trace = Assert.Single(matching.Grants);
        Assert.Equal(ProjectPermissionCodes.TaskAssign, trace.PermissionCode);
        Assert.Equal("Backend Lead", trace.RoleName);
        Assert.Equal(ResourceScopeKind.Repository, trace.ResourceScope);
        Assert.Equal(repositoryId, trace.ResourceId);
        Assert.Equal([ComponentScopeKind.Backend], trace.ComponentScopes);

        var ownedAndAssigned = await evaluator.GetEffectivePermissionsAsync(
            memberId,
            new AuthorizationResourceContext(
                projectId,
                repositoryId,
                ComponentScopeKind.Backend,
                OwnerUserId: memberId,
                AssignedUserIds: [memberId]),
            TestContext.Current.CancellationToken);
        Assert.True(ownedAndAssigned.HasPermission(ProjectPermissionCodes.TaskUpdate));
        Assert.True(ownedAndAssigned.HasPermission(ProjectPermissionCodes.TaskView));

        var wrongComponent = await evaluator.GetEffectivePermissionsAsync(
            memberId,
            new AuthorizationResourceContext(projectId, repositoryId, ComponentScopeKind.Frontend),
            TestContext.Current.CancellationToken);
        Assert.Empty(wrongComponent.Grants);

        var aiPolicy = await mediator.Send(
            new UpdateAiPermissionPolicyCommand(
                ownerId,
                projectId,
                AiTrustLevel.CreateTasks,
                [ProjectPermissionCodes.AiTaskSuggest, ProjectPermissionCodes.AiTaskCreate]),
            TestContext.Current.CancellationToken);
        Assert.Equal(AiTrustLevel.CreateTasks, aiPolicy.TrustLevel);
        Assert.True(aiPolicy.Allows(ProjectPermissionCodes.AiTaskCreate));
        Assert.False(aiPolicy.Allows(ProjectPermissionCodes.AiCodeGenerate));
        await Assert.ThrowsAsync<ProjectAuthorizationValidationException>(() => mediator.Send(
            new UpdateAiPermissionPolicyCommand(
                ownerId,
                projectId,
                AiTrustLevel.CreateTasks,
                [ProjectPermissionCodes.AiCodeGenerate]),
            TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ProjectAuthorizationValidationException>(() => mediator.Send(
            new TransferProjectOwnershipCommand(ownerId, projectId, memberId, Confirmed: false),
            TestContext.Current.CancellationToken));
        var transferred = await mediator.Send(
            new TransferProjectOwnershipCommand(ownerId, projectId, memberId, Confirmed: true),
            TestContext.Current.CancellationToken);
        Assert.Equal(memberId, transferred.PrimaryOwnerId);

        var formerOwnerPermissions = await evaluator.GetEffectivePermissionsAsync(
            ownerId,
            new AuthorizationResourceContext(projectId),
            TestContext.Current.CancellationToken);
        Assert.False(formerOwnerPermissions.HasPermission(ProjectPermissionCodes.RoleCreate));
        var newOwnerPermissions = await evaluator.GetEffectivePermissionsAsync(
            memberId,
            new AuthorizationResourceContext(projectId),
            TestContext.Current.CancellationToken);
        Assert.True(newOwnerPermissions.HasPermission(ProjectPermissionCodes.RoleCreate));

        await using var verification = app.Services.GetRequiredService<IQuerySession>();
        var auditActions = await verification.Query<AuditRecord>()
            .Where(record => record.ProjectId == projectId)
            .Select(record => record.Action)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains("role.create", auditActions);
        Assert.Contains("role.permissions.update", auditActions);
        Assert.Contains("member.roles.assign", auditActions);
        Assert.Contains("ai.policy.update", auditActions);
        Assert.Contains("project.ownership.transfer", auditActions);

        var auditRecords = await verification.Query<AuditRecord>()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(auditRecords, record =>
        {
            Assert.Equal(ownerId, record.ActorId);
            Assert.Equal("user", record.ActorType);
            Assert.NotEqual(default, record.OccurredAt);
            Assert.False(string.IsNullOrWhiteSpace(record.TargetType));
            Assert.False(string.IsNullOrWhiteSpace(record.TargetId));
            Assert.NotNull(record.After);
        });

        var definitions = await mediator.Send(
            new GetProjectPermissionDefinitionsQuery(memberId, projectId),
            TestContext.Current.CancellationToken);
        Assert.All(definitions, definition =>
            Assert.Equal(PermissionDefinitionScope.Project, definition.Scope));
        Assert.DoesNotContain(definitions, definition =>
            definition.Id == SystemPermissionCodes.UserManage);

        var persistedOwnerRole = await verification.LoadAsync<ProjectRole>(
            seeded.OwnerRole.Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(persistedOwnerRole);
    }

    [Fact]
    public async Task Project_role_endpoint_returns_401_then_403_then_success()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(postgres.GetConnectionString(), mapEndpoints: true);

        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await SeedProjectAsync(app.Services, projectId, ownerId, memberId);

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        var route = $"api/v1/projects/{projectId}/roles";

        var unauthenticated = await client.PostAsJsonAsync(
            route,
            new CreateProjectRoleRequest("Reviewer"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, memberId.ToString());
        var forbidden = await client.PostAsJsonAsync(
            route,
            new CreateProjectRoleRequest("Reviewer"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.UserHeader);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, ownerId.ToString());
        var authorized = await client.PostAsJsonAsync(
            route,
            new CreateProjectRoleRequest("Reviewer"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, authorized.StatusCode);
        var created = await authorized.Content.ReadFromJsonAsync<ProjectRole>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal(projectId, created.ProjectId);
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
            configuration.RegisterServicesFromAssemblyContaining<CreateProjectRoleCommand>());

        if (mapEndpoints)
        {
            builder.Services.AddCarter(configurator: configuration =>
                configuration.WithModule<ProjectAuthorizationEndpoints>());
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
            var versions = app.NewApiVersionSet()
                .HasApiVersion(1)
                .Build();
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

    private static async Task<SeededProject> SeedProjectAsync(
        IServiceProvider services,
        Guid projectId,
        Guid ownerId,
        Guid memberId)
    {
        var now = DateTimeOffset.UtcNow;
        var ownerRole = new ProjectRole(
            Guid.NewGuid(),
            projectId,
            "Owner",
            IsSystemDefined: true,
            IsOwner: true,
            PermissionCatalog.ProjectDefinitions
                .Select(definition => new RolePermissionGrant(
                    definition.Id,
                    ResourceScopeKind.Project,
                    null,
                    []))
                .ToArray());
        var project = new Project(projectId, $"Project {projectId}", ownerId, now);
        var ownerMembership = new ProjectMembership(
            Guid.NewGuid(),
            projectId,
            ownerId,
            IsActive: true,
            [new MemberRoleAssignment(ownerRole.Id, now, ownerId)]);
        var memberMembership = new ProjectMembership(
            Guid.NewGuid(),
            projectId,
            memberId,
            IsActive: true,
            []);

        await using var scope = services.CreateAsyncScope();
        await using var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        session.Store(project);
        session.Store(ownerRole);
        session.Store(ownerMembership);
        session.Store(memberMembership);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededProject(project, ownerRole);
    }

    private sealed record SeededProject(Project Project, ProjectRole OwnerRole);
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationSchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var value) ||
            !Guid.TryParse(value, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            AuthenticationSchemeName);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            AuthenticationSchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
