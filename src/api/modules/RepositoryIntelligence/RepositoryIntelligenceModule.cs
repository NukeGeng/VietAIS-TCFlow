using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VietAIS.TCFlow.Analyzers.AspNet;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Monitoring;
using VietAIS.TCFlow.Analyzers.Reasoning;
using VietAIS.TCFlow.Analyzers.Vue;
using IRepositoryAnalyzer = VietAIS.TCFlow.Analyzers.Core.IRepositoryAnalyzer;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;
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
                options.Schema.For<GitHubAppInstallation>()
                    .UseOptimisticConcurrency(true)
                    .UniqueIndex(installation => installation.InstallationId)
                    .Index(installation => installation.ProjectId);
                options.Schema.For<GitHubConnectionAttempt>()
                    .UseOptimisticConcurrency(true)
                    .Index(attempt => attempt.ProjectId)
                    .Index(attempt => attempt.ActorId)
                    .Index(attempt => attempt.ExpiresAt);
                options.Schema.For<GitHubRepositoryAccess>()
                    .UseOptimisticConcurrency(true)
                    .UniqueIndex(
                        "uidx_gh_repository_access_installation_repository",
                        access => access.InstallationId,
                        access => access.GitHubRepositoryId)
                    .UniqueIndex(access => access.ProjectRepositoryId)
                    .Index(access => access.ProjectId);
                options.Schema.For<GitHubWebhookDelivery>()
                    .UseOptimisticConcurrency(true)
                    .Index(delivery => delivery.ProjectId)
                    .Index(delivery => delivery.ProjectRepositoryId);
                options.Schema.For<RepositoryAnalysisRequest>()
                    .UseOptimisticConcurrency(true)
                    .Index(request => request.ProjectId)
                    .Index(request => request.RepositoryId)
                    .Index(request => request.DeliveryId)
                    .Index(request => request.Status);
                options.Schema.For<RepositoryAnalysisRun>()
                    .UseOptimisticConcurrency(true)
                    .Index(run => run.ProjectId)
                    .Index(run => run.RepositoryId)
                    .Index(run => run.Status)
                    .Index(run => run.UpdatedAt);
                options.Schema.For<IncrementalAnalysisDelivery>()
                    .UseOptimisticConcurrency(true)
                    .Index(delivery => delivery.Status);
                options.Schema.For<RepositoryReasoningJob>()
                    .UseOptimisticConcurrency(true)
                    .Index(job => job.Status)
                    .Index(job => job.NextAttemptAt);
                options.Schema.For<RepositoryTaskProjection>()
                    .UseOptimisticConcurrency(true)
                    .UniqueIndex(projection => projection.EngineeringTaskId)
                    .Index(projection => projection.ProjectId)
                    .Index(projection => projection.RepositoryId);
                KnowledgeGraphStorage.Configure(options);
                ConventionProfileStorage.Configure(options);
                TaskReconciliationStorage.Configure(options);
            })
            .UseLightweightSessions();

        builder.Services.AddScoped<IProjectPermissionEvaluator, ProjectPermissionEvaluator>();
        builder.Services.AddOptions<GitHubAppOptions>()
            .BindConfiguration(GitHubAppOptions.SectionName);
        builder.Services.AddHttpClient<IGitHubAppClient, GitHubAppClient>();
        builder.Services.AddScoped<IRepositoryAnalyzer, VueAnalyzer>();
        builder.Services.AddScoped<IRepositoryAnalyzer, AspNetAnalyzer>();
        builder.Services.AddScoped<
            IRepositoryAnalyzer,
            VietAIS.TCFlow.Analyzers.Marten.MartenAnalyzer>();
        builder.Services.AddScoped<IRepositorySnapshotSource, GitHubRepositorySnapshotSource>();
        builder.Services.AddScoped<IIncrementalChangeSource, GitHubIncrementalChangeSource>();
        builder.Services.AddScoped<IIncrementalDeliveryRegistry, MartenIncrementalDeliveryRegistry>();
        builder.Services.AddScoped<IDeepReasoningQueue, MartenDeepReasoningQueue>();
        builder.Services.AddScoped(serviceProvider => new InitialRepositoryAnalysisService(
            serviceProvider.GetRequiredService<IRepositorySnapshotSource>(),
            serviceProvider.GetServices<IRepositoryAnalyzer>().ToArray()));
        builder.Services.AddScoped(serviceProvider => new IncrementalMonitoringService(
            serviceProvider.GetRequiredService<IIncrementalChangeSource>(),
            serviceProvider.GetRequiredService<IIncrementalDeliveryRegistry>(),
            serviceProvider.GetRequiredService<IDeepReasoningQueue>(),
            serviceProvider.GetServices<IRepositoryAnalyzer>().ToArray(),
            serviceProvider.GetRequiredService<TimeProvider>()));
        builder.Services.AddScoped<RepositoryAnalysisProcessor>();
        builder.Services.AddOptions<RepositoryAnalysisWorkerOptions>()
            .BindConfiguration(RepositoryAnalysisWorkerOptions.SectionName);
        builder.Services.AddHostedService<RepositoryAnalysisWorker>();
        builder.Services.AddOptions<RepositoryReasoningWorkerOptions>()
            .BindConfiguration(RepositoryReasoningWorkerOptions.SectionName);
        var contentRoot = builder.Environment.ContentRootPath;
        builder.Services.AddSingleton<ICodexAppServerClient>(serviceProvider =>
        {
            var configured = serviceProvider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<RepositoryReasoningWorkerOptions>>().Value;
            var workingDirectory = Path.IsPathRooted(configured.WorkingDirectory)
                ? configured.WorkingDirectory
                : Path.GetFullPath(configured.WorkingDirectory, contentRoot);
            return new CodexAppServerProcessClient(new CodexAppServerOptions(
                configured.ExecutablePath,
                workingDirectory,
                configured.Model));
        });
        builder.Services.AddSingleton<IAiReasoningProvider, CodexAppServerReasoningProvider>();
        builder.Services.AddHostedService<RepositoryDeepReasoningWorker>();
        builder.Services.AddSingleton<IGitHubWebhookSignatureValidator>(
            new GitHubWebhookSignatureValidator(builder.Configuration["GitHub:WebhookSecret"]));
        builder.Services.AddSingleton(TimeProvider.System);

        return builder;
    }

    public static WebApplication UseRepositoryIntelligenceModule(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
    }
}
