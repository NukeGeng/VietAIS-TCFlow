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
            .ReadFromJsonAsync<SystemPermissionDefinition[]>(TestContext.Current.CancellationToken);
        Assert.NotNull(definitions);
        Assert.Contains(definitions, definition =>
            definition.Id == SystemPermissionCodes.ProjectInspect &&
            definition.Scope == PermissionDefinitionScope.System);
        Assert.Contains(definitions, definition =>
            definition.Id == ProjectPermissionCodes.ProjectView &&
            definition.Scope == PermissionDefinitionScope.Project);
        Assert.Contains(definitions, definition =>
            definition.Id == "Permissions.Users.Update" &&
            definition.Scope == PermissionDefinitionScope.System);

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

    [Fact]
    public async Task System_admin_manages_global_configuration_usage_and_enforced_policies()
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

        var unauthenticated = await client.GetAsync(
            "api/v1/system/settings",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, ownerId.ToString());
        var forbidden = await client.GetAsync(
            "api/v1/system/usage",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.UserHeader);
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserHeader,
            systemAdminId.ToString());

        var providersResponse = await client.GetAsync(
            "api/v1/system/ai-providers",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, providersResponse.StatusCode);
        var providers = await providersResponse.Content
            .ReadFromJsonAsync<GlobalAiProviderConfiguration[]>(
                TestContext.Current.CancellationToken);
        var provider = Assert.Single(providers!);
        Assert.Equal(GlobalAiProviderKind.CodexAppServer, provider.Kind);
        Assert.True(provider.IsEnabled);

        var providerUpdateResponse = await client.PutAsJsonAsync(
            $"api/v1/system/ai-providers/{provider.Id}",
            new UpdateGlobalAiProviderRequest(
                "Managed Codex",
                IsEnabled: false),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, providerUpdateResponse.StatusCode);
        var updatedProvider = await providerUpdateResponse.Content
            .ReadFromJsonAsync<GlobalAiProviderConfiguration>(
                TestContext.Current.CancellationToken);
        Assert.NotNull(updatedProvider);
        Assert.False(updatedProvider.IsEnabled);
        Assert.Equal(systemAdminId, updatedProvider.UpdatedBy);

        var unknownProviderResponse = await client.PutAsJsonAsync(
            $"api/v1/system/ai-providers/{Guid.NewGuid()}",
            new UpdateGlobalAiProviderRequest("Unknown", true),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, unknownProviderResponse.StatusCode);

        var settingsUpdateResponse = await client.PutAsJsonAsync(
            "api/v1/system/settings",
            new UpdateGlobalSystemSettingsRequest(
                "TCFlow Platform",
                "UTC",
                new Uri("https://support.example.com/tcflow")),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, settingsUpdateResponse.StatusCode);
        var settings = await settingsUpdateResponse.Content
            .ReadFromJsonAsync<GlobalSystemSettings>(TestContext.Current.CancellationToken);
        Assert.NotNull(settings);
        Assert.Equal("TCFlow Platform", settings.PlatformName);
        Assert.Equal(systemAdminId, settings.UpdatedBy);

        var invalidPolicyResponse = await client.PutAsJsonAsync(
            "api/v1/system/policies",
            new UpdatePlatformPolicyRequest(true, true, 0),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPolicyResponse.StatusCode);

        var policyUpdateResponse = await client.PutAsJsonAsync(
            "api/v1/system/policies",
            new UpdatePlatformPolicyRequest(
                ProjectCreationEnabled: false,
                RepositoryConnectionsEnabled: false,
                MaximumRepositoriesPerProject: 1),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, policyUpdateResponse.StatusCode);
        var policy = await policyUpdateResponse.Content
            .ReadFromJsonAsync<PlatformPolicy>(TestContext.Current.CancellationToken);
        Assert.NotNull(policy);
        Assert.False(policy.ProjectCreationEnabled);
        Assert.False(policy.RepositoryConnectionsEnabled);

        var usageResponse = await client.GetAsync(
            "api/v1/system/usage",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, usageResponse.StatusCode);
        var usage = await usageResponse.Content
            .ReadFromJsonAsync<SystemUsageSummary>(TestContext.Current.CancellationToken);
        Assert.NotNull(usage);
        Assert.Equal(1, usage.Projects);
        Assert.Equal(1, usage.ActiveProjects);
        Assert.Equal(0, usage.Repositories);
        Assert.Equal(0, usage.Tasks);
        Assert.Equal(3, usage.AuditRecords);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            await using var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
            var persistedProvider = await session.LoadAsync<GlobalAiProviderConfiguration>(
                provider.Id,
                TestContext.Current.CancellationToken);
            var persistedSettings = await session.LoadAsync<GlobalSystemSettings>(
                SystemConfigurationIds.GlobalSettings,
                TestContext.Current.CancellationToken);
            var persistedPolicy = await session.LoadAsync<PlatformPolicy>(
                SystemConfigurationIds.PlatformPolicy,
                TestContext.Current.CancellationToken);
            Assert.Equal("Managed Codex", persistedProvider!.DisplayName);
            Assert.Equal("TCFlow Platform", persistedSettings!.PlatformName);
            Assert.False(persistedPolicy!.ProjectCreationEnabled);

            var createProjectHandler = new CreateProjectHandler(session, TimeProvider.System);
            var projectFailure = await Assert.ThrowsAsync<ProjectManagementValidationException>(() =>
                createProjectHandler.Handle(
                    new CreateProjectCommand(ownerId, "Blocked project"),
                    TestContext.Current.CancellationToken));
            Assert.Contains(
                "disabled by the platform policy",
                projectFailure.Message,
                StringComparison.Ordinal);

            var repositoryHandler = new CreateProjectRepositoryHandler(
                session,
                new AllowAllProjectPermissionEvaluator(),
                TimeProvider.System);
            var repositoryFailure = await Assert.ThrowsAsync<ProjectManagementValidationException>(() =>
                repositoryHandler.Handle(
                    new CreateProjectRepositoryCommand(
                        ownerId,
                        projectId,
                        "blocked-repository",
                        RepositoryProviderKind.GitHub,
                        null,
                        "https://github.com/NukeGeng/VietAIS-TCFlow.git",
                        "main"),
                    TestContext.Current.CancellationToken));
            Assert.Contains(
                "disabled by the platform policy",
                repositoryFailure.Message,
                StringComparison.Ordinal);
        }

        var auditResponse = await client.GetAsync(
            "api/v1/system/audit?pageNumber=1&pageSize=100",
            TestContext.Current.CancellationToken);
        var audits = await auditResponse.Content.ReadFromJsonAsync<PagedList<AuditRecord>>(
            TestContext.Current.CancellationToken);
        Assert.Contains(audits!.Items, record =>
            record.ProjectId is null && record.Action == "ai-provider.update");
        Assert.Contains(audits.Items, record =>
            record.ProjectId is null && record.Action == "system-settings.update");
        Assert.Contains(audits.Items, record =>
            record.ProjectId is null && record.Action == "platform-policy.update");

        Assert.Contains(FshPermissions.Root, permission =>
            permission.Name == TcFlowSystemPermissions.AiProviderManage);
        Assert.Contains(FshPermissions.Root, permission =>
            permission.Name == TcFlowSystemPermissions.SystemSettingsManage);
        Assert.Contains(FshPermissions.Root, permission =>
            permission.Name == TcFlowSystemPermissions.PlatformPolicyManage);
        Assert.Contains(FshPermissions.Root, permission =>
            permission.Name == TcFlowSystemPermissions.PlatformUsageView);
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

    private sealed class AllowAllProjectPermissionEvaluator : IProjectPermissionEvaluator
    {
        public Task<IReadOnlyList<PermissionGrantTrace>> GetProjectPermissionGrantsAsync(
            Guid userId,
            Guid projectId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<PermissionGrantTrace>>([]);
        }

        public Task<EffectivePermissionResult> GetEffectivePermissionsAsync(
            Guid userId,
            AuthorizationResourceContext resource,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new EffectivePermissionResult(resource.ProjectId, userId, []));
        }

        public Task EnsureAuthorizedAsync(
            Guid userId,
            string permissionCode,
            AuthorizationResourceContext resource,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
