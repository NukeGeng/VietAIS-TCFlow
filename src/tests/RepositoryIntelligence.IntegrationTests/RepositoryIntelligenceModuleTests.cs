using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.IntegrationTests;

public sealed class RepositoryIntelligenceModuleTests
{
    [Fact]
    public void Registration_requires_a_database_connection_string()
    {
        var builder = WebApplication.CreateBuilder();

        var exception = Assert.Throws<InvalidOperationException>(
            () => builder.RegisterRepositoryIntelligenceServices());

        Assert.Contains("DatabaseOptions:ConnectionString", exception.Message);
    }

    [Fact]
    public async Task Registration_supports_explicit_Marten_writes_and_queries()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);

        var builder = WebApplication.CreateBuilder();
        builder.Configuration["DatabaseOptions:ConnectionString"] = postgres.GetConnectionString();
        builder.Configuration["RepositoryAnalysis:Enabled"] = "false";
        builder.RegisterRepositoryIntelligenceServices();

        await using var app = builder.Build();
        var store = app.Services.GetRequiredService<IDocumentStore>();
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        var probe = new StorageProbe(Guid.NewGuid(), "marten-integration");

        await using (var writeSession = app.Services.GetRequiredService<IDocumentSession>())
        {
            writeSession.Store(probe);
            await writeSession.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var querySession = app.Services.GetRequiredService<IQuerySession>();
        var persisted = await querySession.LoadAsync<StorageProbe>(
            probe.Id,
            TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal(probe.Name, persisted.Name);
    }
}

public sealed record StorageProbe(Guid Id, string Name);
