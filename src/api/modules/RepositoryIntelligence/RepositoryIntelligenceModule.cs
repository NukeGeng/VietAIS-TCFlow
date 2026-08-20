using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

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
                options.Schema.For<ProjectState>()
                    .UseOptimisticConcurrency(true);
                options.Schema.For<AuthorityPolicy>()
                    .UseOptimisticConcurrency(true);
                options.Schema.For<ConventionProfile>()
                    .UseOptimisticConcurrency(true);
                options.Schema.For<ProjectRepository>()
                    .UseOptimisticConcurrency(true)
                    .UniqueIndex(repository => repository.ProjectId, repository => repository.Name);
                options.Schema.For<ProjectComponent>()
                    .UseOptimisticConcurrency(true)
                    .Index(component => component.ProjectId)
                    .Index(component => component.RepositoryId);
                options.Schema.For<ProjectFeature>()
                    .UseOptimisticConcurrency(true)
                    .Index(feature => feature.ProjectId);
                options.Schema.For<EngineeringTask>()
                    .UseOptimisticConcurrency(true)
                    .Index(task => task.ProjectId)
                    .Index(task => task.RepositoryId)
                    .Index(task => task.FeatureId)
                    .Index(task => task.Status);
                options.Schema.For<TaskAssignment>()
                    .UseOptimisticConcurrency(true)
                    .UniqueIndex(assignment => assignment.TaskId)
                    .Index(assignment => assignment.AssigneeId);
                options.Schema.For<TaskReview>()
                    .Index(review => review.TaskId);
                options.Schema.For<TaskEvidence>()
                    .Index(evidence => evidence.TaskId);
                options.Schema.For<TaskVersion>()
                    .UniqueIndex(version => version.TaskId, version => version.Version);
                options.Schema.For<SourceChange>()
                    .Index(change => change.ProjectId);
                options.Schema.For<SourceArtifact>()
                    .Index(artifact => artifact.ProjectId);
                options.Schema.For<SourceImpact>()
                    .Index(impact => impact.ProjectId);
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
