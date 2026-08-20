using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence;

public static class RepositoryIntelligenceModule
{
    private const string SchemaName = "repository_intelligence";

    public static WebApplicationBuilder RegisterRepositoryIntelligenceServices(
        this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var connectionString = builder.Configuration["DatabaseOptions:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DatabaseOptions:ConnectionString is required for Repository Intelligence storage.");
        }

        builder.Services
            .AddMarten(options =>
            {
                options.Connection(connectionString);
                options.DatabaseSchemaName = SchemaName;

                options.Schema.For<Project>()
                    .UseOptimisticConcurrency(true);
                options.Schema.For<ProjectMembership>()
                    .UseOptimisticConcurrency(true)
                    .UniqueIndex(membership => membership.ProjectId, membership => membership.UserId);
                options.Schema.For<ProjectRole>()
                    .UseOptimisticConcurrency(true)
                    .Index(role => role.ProjectId);
                options.Schema.For<AuditRecord>()
                    .Index(record => record.ProjectId);
            })
            .UseLightweightSessions();

        builder.Services.AddScoped<IProjectPermissionEvaluator, ProjectPermissionEvaluator>();
        builder.Services.AddSingleton(TimeProvider.System);

        return builder;
    }

    public static WebApplication UseRepositoryIntelligenceModule(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
    }
}
