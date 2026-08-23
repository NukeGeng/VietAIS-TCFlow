using System.Net;
using System.Net.Http.Json;
using Asp.Versioning.Conventions;
using Carter;
using FSH.Framework.Core.Paging;
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

public sealed class ProjectAdministrationIntegrationTests
{
    [Fact]
    public async Task Administration_reads_survive_reload_and_member_role_mutations_are_scoped_and_audited()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(postgres.GetConnectionString());
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        Project project;
        ProjectRepository repository;

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
            project = (await mediator.Send(
                new CreateProjectCommand(ownerId, "Administration Project"),
                TestContext.Current.CancellationToken)).Project;
            repository = await mediator.Send(
                new CreateProjectRepositoryCommand(
                    ownerId,
                    project.Id,
                    "Platform",
                    RepositoryProviderKind.Local,
                    "/workspace/platform",
                    null,
                    "main"),
                TestContext.Current.CancellationToken);
            await mediator.Send(
                new CreateProjectComponentCommand(
                    ownerId,
                    project.Id,
                    repository.Id,
                    "Backend",
                    ComponentScopeKind.Backend,
                    "src/api"),
                TestContext.Current.CancellationToken);
            await mediator.Send(
                new CreateProjectFeatureCommand(
                    ownerId,
                    project.Id,
                    "Member administration",
                    "Persistent project administration read models."),
                TestContext.Current.CancellationToken);
        }

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var rolesRoute = $"api/v1/projects/{project.Id}/roles";
        var membersRoute = $"api/v1/projects/{project.Id}/members";

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync(rolesRoute, TestContext.Current.CancellationToken)).StatusCode);

        Authenticate(client, outsiderId);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync(rolesRoute, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync(
                $"api/v1/projects/{project.Id}/features?pageNumber=1&pageSize=20",
                TestContext.Current.CancellationToken)).StatusCode);

        Authenticate(client, ownerId);
        var initialRoles = await client.GetFromJsonAsync<IReadOnlyList<ProjectRole>>(
            rolesRoute,
            TestContext.Current.CancellationToken);
        var ownerRole = Assert.Single(initialRoles!);
        Assert.True(ownerRole.IsOwner);

        var added = await client.PostAsJsonAsync(
            membersRoute,
            new AddProjectMemberRequest(invitedUserId),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var membership = await added.Content.ReadFromJsonAsync<ProjectMembership>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(membership);
        Assert.True(membership.IsActive);

        var roleResponse = await client.PostAsJsonAsync(
            rolesRoute,
            new CreateProjectRoleRequest("Backend Reviewer"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, roleResponse.StatusCode);
        var role = await roleResponse.Content.ReadFromJsonAsync<ProjectRole>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(role);

        var assign = await client.PutAsJsonAsync(
            $"{membersRoute}/{invitedUserId}/roles",
            new AssignMemberRolesRequest([role.Id]),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.DeleteAsync(
                $"{rolesRoute}/{role.Id}",
                TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.DeleteAsync(
                $"{membersRoute}/{ownerId}",
                TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync(
                $"{membersRoute}/{invitedUserId}",
                TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync(
                $"{rolesRoute}/{role.Id}",
                TestContext.Current.CancellationToken)).StatusCode);

        var membersAfterReload = await client.GetFromJsonAsync<IReadOnlyList<ProjectMembership>>(
            membersRoute,
            TestContext.Current.CancellationToken);
        var onlyMember = Assert.Single(membersAfterReload!);
        Assert.Equal(ownerId, onlyMember.UserId);
        var rolesAfterReload = await client.GetFromJsonAsync<IReadOnlyList<ProjectRole>>(
            rolesRoute,
            TestContext.Current.CancellationToken);
        var onlyRole = Assert.Single(rolesAfterReload!);
        Assert.True(onlyRole.IsOwner);
        var aiPolicy = await client.GetFromJsonAsync<AiPermissionPolicy>(
            $"api/v1/projects/{project.Id}/ai-policy",
            TestContext.Current.CancellationToken);
        Assert.NotNull(aiPolicy);
        Assert.Equal(AiTrustLevel.SuggestOnly, aiPolicy.TrustLevel);
        Assert.Contains(ProjectPermissionCodes.AiTaskSuggest, aiPolicy.AllowedPermissions);

        var components = await client.GetFromJsonAsync<PagedList<ProjectComponent>>(
            $"api/v1/projects/{project.Id}/components?pageNumber=1&pageSize=20&repositoryId={repository.Id}",
            TestContext.Current.CancellationToken);
        var onlyComponent = Assert.Single(components!.Items);
        Assert.Equal(ComponentScopeKind.Backend, onlyComponent.Scope);
        var features = await client.GetFromJsonAsync<PagedList<ProjectFeature>>(
            $"api/v1/projects/{project.Id}/features?pageNumber=1&pageSize=20&keyword=member",
            TestContext.Current.CancellationToken);
        Assert.Single(features!.Items);

        await using var query = app.Services.GetRequiredService<IQuerySession>();
        var auditActions = await query.Query<AuditRecord>()
            .Where(record => record.ProjectId == project.Id)
            .Select(record => record.Action)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains("member.invite", auditActions);
        Assert.Contains("member.roles.assign", auditActions);
        Assert.Contains("member.remove", auditActions);
        Assert.Contains("role.create", auditActions);
        Assert.Contains("role.delete", auditActions);
    }

    private static void Authenticate(HttpClient client, Guid userId)
    {
        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.UserHeader);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, userId.ToString());
    }

    private static async Task<WebApplication> BuildApplicationAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["DatabaseOptions:ConnectionString"] = connectionString;
        builder.RegisterRepositoryIntelligenceServices();
        builder.Services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<CreateProjectCommand>());
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
}
