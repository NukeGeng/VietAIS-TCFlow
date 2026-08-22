using System.Net;
using System.Net.Http.Json;
using Asp.Versioning.Conventions;
using Carter;
using FSH.Framework.Infrastructure.Exceptions;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;
using Xunit;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.IntegrationTests;

public sealed class ProjectGovernanceIntegrationTests
{
    [Fact]
    public void Authority_source_values_match_the_repository_intelligence_contract()
    {
        Assert.Equal(0, (int)AuthoritySourceKind.Backend);
        Assert.Equal(1, (int)AuthoritySourceKind.Frontend);
        Assert.Equal(2, (int)AuthoritySourceKind.OpenApi);
        Assert.Equal(3, (int)AuthoritySourceKind.Database);
        Assert.Equal(4, (int)AuthoritySourceKind.Tests);
        Assert.Equal(5, (int)AuthoritySourceKind.Documentation);
    }

    [Fact]
    public async Task Governance_mutations_return_401_then_403_and_audit_authorized_updates()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(postgres.GetConnectionString());
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var project = await CreateProjectAsync(app.Services, ownerId);
        var authorityRequest = new UpdateAuthorityPolicyRequest(
        [
            new AuthorityRule(AuthorityKnowledgeKind.ApiContract, AuthoritySourceKind.Frontend),
            new AuthorityRule(AuthorityKnowledgeKind.UiRequirement, AuthoritySourceKind.Frontend),
            new AuthorityRule(AuthorityKnowledgeKind.BusinessLogic, AuthoritySourceKind.Backend),
            new AuthorityRule(AuthorityKnowledgeKind.Persistence, AuthoritySourceKind.Database)
        ]);
        using var client = CreateClient(app);
        var authorityRoute = $"api/v1/projects/{project.Id}/authority-policy";

        var unauthenticated = await client.PutAsJsonAsync(
            authorityRoute,
            authorityRequest,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, outsiderId.ToString());
        var forbidden = await client.PutAsJsonAsync(
            authorityRoute,
            authorityRequest,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.UserHeader);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, ownerId.ToString());
        var authorized = await client.PutAsJsonAsync(
            authorityRoute,
            authorityRequest,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        var authority = await authorized.Content.ReadFromJsonAsync<AuthorityPolicy>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(authority);
        Assert.Equal(ownerId, authority.UpdatedBy);
        Assert.Equal(
            AuthoritySourceKind.Frontend,
            authority.Rules.Single(rule => rule.Knowledge == AuthorityKnowledgeKind.ApiContract).Source);

        var conventionRequest = new UpdateConventionProfileRequest(
            ConventionProfileStatus.Confirmed,
            ["feature-based", "module-based"],
            ["minimal-api"],
            ["marten-document-database"],
            ["fluent-validation"],
            ["Command", "Response"]);
        var conventionRoute = $"api/v1/projects/{project.Id}/convention-profile";
        var conventionUpdated = await client.PutAsJsonAsync(
            conventionRoute,
            conventionRequest,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, conventionUpdated.StatusCode);
        var convention = await conventionUpdated.Content.ReadFromJsonAsync<ConventionProfile>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(convention);
        Assert.Equal(ConventionProfileStatus.Confirmed, convention.Status);
        Assert.Equal(["feature-based", "module-based"], convention.Architectures);

        var authorityRead = await client.GetFromJsonAsync<AuthorityPolicy>(
            authorityRoute,
            TestContext.Current.CancellationToken);
        Assert.NotNull(authorityRead);
        Assert.Equal(authority.Rules, authorityRead.Rules);
        var conventionRead = await client.GetFromJsonAsync<ConventionProfile>(
            conventionRoute,
            TestContext.Current.CancellationToken);
        Assert.NotNull(conventionRead);
        Assert.Equal(convention.Id, conventionRead.Id);
        Assert.Equal(convention.ProjectId, conventionRead.ProjectId);
        Assert.Equal(convention.Status, conventionRead.Status);
        Assert.Equal(convention.Architectures, conventionRead.Architectures);
        Assert.Equal(convention.ApiStyles, conventionRead.ApiStyles);
        Assert.Equal(convention.PersistencePatterns, conventionRead.PersistencePatterns);
        Assert.Equal(convention.ValidationPatterns, conventionRead.ValidationPatterns);
        Assert.Equal(convention.DtoPatterns, conventionRead.DtoPatterns);
        Assert.Equal(convention.UpdatedAt, conventionRead.UpdatedAt);
        Assert.Equal(convention.UpdatedBy, conventionRead.UpdatedBy);

        await using var verificationScope = app.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<IQuerySession>();
        var audits = await verification.Query<AuditRecord>()
            .Where(record => record.ProjectId == project.Id &&
                (record.Action == "authority.policy.update" ||
                    record.Action == "convention.profile.update"))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit =>
        {
            Assert.Equal(ownerId, audit.ActorId);
            Assert.Equal("user", audit.ActorType);
            Assert.NotNull(audit.Before);
            Assert.NotNull(audit.After);
        });
    }

    [Fact]
    public async Task Authority_update_rejects_incomplete_or_duplicate_knowledge_rules()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var app = await BuildApplicationAsync(postgres.GetConnectionString());
        var ownerId = Guid.NewGuid();
        var project = await CreateProjectAsync(app.Services, ownerId);
        using var client = CreateClient(app);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, ownerId.ToString());

        var invalid = await client.PutAsJsonAsync(
            $"api/v1/projects/{project.Id}/authority-policy",
            new UpdateAuthorityPolicyRequest(
            [
                new AuthorityRule(AuthorityKnowledgeKind.ApiContract, AuthoritySourceKind.Frontend),
                new AuthorityRule(AuthorityKnowledgeKind.ApiContract, AuthoritySourceKind.Backend),
                new AuthorityRule(AuthorityKnowledgeKind.BusinessLogic, AuthoritySourceKind.Backend),
                new AuthorityRule(AuthorityKnowledgeKind.Persistence, AuthoritySourceKind.Backend)
            ]),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        await using var verificationScope = app.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<IQuerySession>();
        var policy = await verification.LoadAsync<AuthorityPolicy>(
            project.Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(policy);
        Assert.Equal(
            AuthoritySourceKind.Backend,
            policy.Rules.Single(rule => rule.Knowledge == AuthorityKnowledgeKind.ApiContract).Source);
        Assert.False(await verification.Query<AuditRecord>()
            .AnyAsync(
                audit => audit.ProjectId == project.Id && audit.Action == "authority.policy.update",
                TestContext.Current.CancellationToken));
    }

    private static async Task<Project> CreateProjectAsync(IServiceProvider services, Guid ownerId)
    {
        await using var scope = services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        var response = await mediator.Send(
            new CreateProjectCommand(ownerId, "Governance Project"),
            TestContext.Current.CancellationToken);
        return response.Project;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static async Task<WebApplication> BuildApplicationAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["DatabaseOptions:ConnectionString"] = connectionString;
        builder.RegisterRepositoryIntelligenceServices();
        builder.Services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<GetAuthorityPolicyQuery>());
        builder.Services.AddCarter(configurator: configuration =>
            configuration.WithModule<ProjectGovernanceEndpoints>());
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
