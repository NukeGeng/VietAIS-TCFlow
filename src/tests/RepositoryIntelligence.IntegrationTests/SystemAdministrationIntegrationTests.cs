using System.Net;
using System.Net.Http.Json;
using Asp.Versioning.Conventions;
using Carter;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Paging;
using FSH.Framework.Infrastructure.Exceptions;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.Shared.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;
using Xunit;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.IntegrationTests;

public sealed class SystemAdministrationIntegrationTests
{
    [Fact]
    public async Task System_admin_inspects_and_suspends_projects_without_becoming_project_owner()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var systemAdminId = Guid.NewGuid();
        await using var app = await BuildApplicationAsync(
            postgres.GetConnectionString(),
            systemAdminId);
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await SeedProjectAsync(app.Services, projectId, ownerId);

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        const string projectsRoute = "api/v1/system/projects?pageNumber=1&pageSize=100";

        var unauthenticated = await client.GetAsync(
            projectsRoute,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, ownerId.ToString());
        var forbidden = await client.GetAsync(projectsRoute, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.UserHeader);
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserHeader,
            systemAdminId.ToString());
        var authorized = await client.GetAsync(projectsRoute, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        var projects = await authorized.Content.ReadFromJsonAsync<PagedList<SystemProjectSummary>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(projects);
        Assert.Equal(projectId, Assert.Single(projects.Items).Project.Id);

        var suspendedResponse = await client.PutAsJsonAsync(
            $"api/v1/system/projects/{projectId}/status",
            new UpdateProjectLifecycleStatusRequest(ProjectLifecycleStatus.Suspended),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, suspendedResponse.StatusCode);
        var suspended = await suspendedResponse.Content.ReadFromJsonAsync<SystemProjectSummary>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(suspended);
        Assert.Equal(ProjectLifecycleStatus.Suspended, suspended.State.Status);
        Assert.Equal(systemAdminId, suspended.State.UpdatedBy);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var projectPermissions = scope.ServiceProvider
                .GetRequiredService<IProjectPermissionEvaluator>();
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                projectPermissions.EnsureAuthorizedAsync(
                    ownerId,
                    ProjectPermissionCodes.ProjectView,
                    new AuthorizationResourceContext(projectId),
                    TestContext.Current.CancellationToken));
            var adminProjectGrants = await projectPermissions.GetEffectivePermissionsAsync(
                systemAdminId,
                new AuthorizationResourceContext(projectId),
                TestContext.Current.CancellationToken);
            Assert.Empty(adminProjectGrants.Grants);
        }

        var definitionsResponse = await client.GetAsync(
            "api/v1/system/permission-definitions",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, definitionsResponse.StatusCode);
        var definitions = await definitionsResponse.Content
            .ReadFromJsonAsync<PermissionDefinition[]>(TestContext.Current.CancellationToken);
        Assert.NotNull(definitions);
        Assert.Contains(definitions, definition =>
            definition.Id == SystemPermissionCodes.ProjectInspect &&
            definition.Scope == PermissionDefinitionScope.System);
        Assert.Contains(definitions, definition =>
            definition.Id == ProjectPermissionCodes.ProjectView &&
            definition.Scope == PermissionDefinitionScope.Project);

        var auditResponse = await client.GetAsync(
            $"api/v1/system/audit?pageNumber=1&pageSize=100&projectId={projectId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedList<AuditRecord>>(
            TestContext.Current.CancellationToken);
        var suspension = Assert.Single(audit!.Items, record => record.Action == "project.suspend");
        Assert.Equal(systemAdminId, suspension.ActorId);
        Assert.Equal("system-admin", suspension.ActorType);
        Assert.NotNull(suspension.Before);
        Assert.NotNull(suspension.After);

        Assert.Contains(FshPermissions.Root, permission =>
            permission.Name == TcFlowSystemPermissions.ProjectInspect);
        Assert.DoesNotContain(FshPermissions.Admin, permission =>
            permission.Name == TcFlowSystemPermissions.ProjectInspect);
    }

    private static async Task<WebApplication> BuildApplicationAsync(
        string connectionString,
        Guid systemAdminId)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["DatabaseOptions:ConnectionString"] = connectionString;
        builder.Configuration["RepositoryAnalysis:Enabled"] = "false";
        builder.RegisterRepositoryIntelligenceServices();
        builder.Services.AddSingleton<ISystemPermissionEvaluator>(
            new TestSystemPermissionEvaluator(systemAdminId));
        builder.Services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<SearchSystemProjectsQuery>());
        builder.Services.AddCarter(configurator: configuration =>
            configuration.WithModule<SystemAdministrationEndpoints>());
        builder.Services.AddApiVersioning();
        builder.Services
            .AddAuthentication(TestAuthenticationHandler.AuthenticationSchemeName)
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                TestAuthenticationHandler>(
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

    private static async Task SeedProjectAsync(
        IServiceProvider services,
        Guid projectId,
        Guid ownerId)
    {
        var now = DateTimeOffset.UtcNow;
        var ownerRole = new ProjectRole(
            Guid.NewGuid(),
            projectId,
            "Owner",
            IsSystemDefined: true,
            IsOwner: true,
            [
                new RolePermissionGrant(
                    ProjectPermissionCodes.ProjectView,
                    ResourceScopeKind.Project,
                    null,
                    [])
            ]);

        await using var scope = services.CreateAsyncScope();
        await using var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        session.Store(new Project(projectId, "Platform project", ownerId, now));
        session.Store(new ProjectState(
            projectId,
            projectId,
            ProjectLifecycleStatus.Active,
            now,
            ownerId));
        session.Store(ownerRole);
        session.Store(new ProjectMembership(
            Guid.NewGuid(),
            projectId,
            ownerId,
            IsActive: true,
            [new MemberRoleAssignment(ownerRole.Id, now, ownerId)]));
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class TestSystemPermissionEvaluator(Guid systemAdminId)
        : ISystemPermissionEvaluator
    {
        public Task EnsureAuthorizedAsync(
            Guid userId,
            string permissionCode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (userId != systemAdminId ||
                !PermissionCatalog.All.Any(definition =>
                    definition.Scope == PermissionDefinitionScope.System &&
                    definition.Id == permissionCode))
            {
                throw new ForbiddenException(
                    $"System permission '{permissionCode}' is not granted for this actor.");
            }

            return Task.CompletedTask;
        }
    }
}
