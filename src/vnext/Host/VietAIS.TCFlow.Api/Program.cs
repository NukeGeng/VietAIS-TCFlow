using JasperFx;
using JasperFx.Events.Daemon;
using JasperFx.Resources;
using FSH.Framework.Eventing;
using FSH.Framework.Web;
using FSH.Framework.Web.Modules;
using Marten;
using VietAIS.TCFlow.BuildingBlocks.Application.Identity;
using VietAIS.TCFlow.BuildingBlocks.Application.Time;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Configuration;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Projections;
using VietAIS.TCFlow.BuildingBlocks.Messaging;
using VietAIS.TCFlow.Api;
using VietAIS.TCFlow.Modules.AccessControl.Authorization;
using VietAIS.TCFlow.Modules.AccessControl.Configuration;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Commands;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Queries;
using VietAIS.TCFlow.Modules.AccessControl.Features;
using VietAIS.TCFlow.Modules.AccessControl.Projections;
using VietAIS.TCFlow.Modules.Planning.Configuration;
using VietAIS.TCFlow.Modules.Planning.Contracts.Commands;
using VietAIS.TCFlow.Modules.Planning.Contracts.Queries;
using VietAIS.TCFlow.Modules.Planning.Features;
using VietAIS.TCFlow.Modules.Projects.Configuration;
using VietAIS.TCFlow.Modules.Projects.Contracts.Commands;
using VietAIS.TCFlow.Modules.Projects.Contracts.Queries;
using VietAIS.TCFlow.Modules.Projects.Features;
using VietAIS.TCFlow.Modules.Projects.Projections;
using VietAIS.TCFlow.Modules.TaskFlow.Configuration;
using VietAIS.TCFlow.Modules.TaskFlow.Contracts.Commands;
using VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries;
using VietAIS.TCFlow.Modules.TaskFlow.Features;
using VietAIS.TCFlow.Modules.TaskFlow.Projections;
using VietAIS.TCFlow.Modules.EventStorming.Configuration;
using VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;
using VietAIS.TCFlow.Modules.EventStorming.Contracts.Queries;
using VietAIS.TCFlow.Modules.EventStorming.Features;
using VietAIS.TCFlow.Modules.EventStorming.Projections;
using VietAIS.TCFlow.Modules.Architecture.Configuration;
using VietAIS.TCFlow.Modules.Architecture.Contracts.Commands;
using VietAIS.TCFlow.Modules.Architecture.Contracts.Queries;
using VietAIS.TCFlow.Modules.Architecture.Features;
using VietAIS.TCFlow.Modules.Architecture.Projections;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Configuration;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Queries;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Features;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Projections;
using VietAIS.TCFlow.Modules.Integrations.Configuration;
using VietAIS.TCFlow.Modules.Integrations.Webhooks;
using FSH.Modules.Auditing;
using FSH.Modules.Identity;
using FSH.Modules.Multitenancy;
using Mediator;
using VietAIS.TCFlow.Modules.PlatformAdministration.Configuration;
using VietAIS.TCFlow.Modules.PlatformAdministration.Contracts.Commands;
using VietAIS.TCFlow.Modules.PlatformAdministration.Contracts.Queries;
using VietAIS.TCFlow.Modules.PlatformAdministration.Features;
using VietAIS.TCFlow.Modules.PlatformAdministration.Projections;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

var martenConnection = builder.Configuration.GetConnectionString("marten");
if (string.IsNullOrWhiteSpace(martenConnection))
{
    throw new InvalidOperationException(
        "ConnectionStrings:marten is required. Supply it through user-secrets, environment, or Aspire.");
}

// FSH Identity and the vNext Marten store intentionally share the same PostgreSQL
// connection, while retaining separate schemas/ownership. These defaults keep
// direct local execution equivalent to Aspire wiring without embedding secrets.
builder.Configuration["DatabaseOptions:Provider"] ??= "POSTGRESQL";
builder.Configuration["DatabaseOptions:ConnectionString"] ??= martenConnection;
builder.Configuration["DatabaseOptions:MigrationsAssembly"] ??= "FSH.Starter.Migrations.PostgreSQL";
builder.Configuration["OpenApiOptions:Title"] ??= "VietAIS TCFlow vNext API";
builder.Configuration["OpenApiOptions:Description"] ??= "Goal2 event-sourced planning and repository intelligence API";
builder.Configuration["OpenApi:Title"] ??= builder.Configuration["OpenApiOptions:Title"];

// FullStackHero module endpoints use Mediator, while the vNext bounded
// contexts use Wolverine. Register both pipelines explicitly at this
// composition root so module endpoint metadata can be generated at startup.
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    // Omitting Assemblies deliberately scans all referenced FullStackHero
    // module assemblies, including contracts and their generated handlers.
});

builder.AddHeroPlatform(options =>
{
    options.EnableCaching = true;
    options.EnableOpenTelemetry = false;
    // FullStackHero Identity's registration and password flows depend on the
    // existing IJobService/IMailService contracts. Wolverine remains the
    // business-message transport; Hangfire is only the platform adapter here.
    options.EnableJobs = true;
    options.EnableMailing = true;
    options.EnableQuotas = false;
    options.EnableSse = false;
    options.EnableRealtime = false;
});
builder.AddModules(
    typeof(IdentityModule).Assembly,
    typeof(MultitenancyModule).Assembly,
    typeof(AuditingModule).Assembly);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IIdGenerator, UuidV7IdGenerator>();
builder.Services.AddOptions<GitHubWebhookOptions>().BindConfiguration("Integrations:GitHub:Webhook");
builder.Services.AddScoped<GitHubWebhookProcessor>();
builder.Services.AddScoped<IProjectOwnerReader, MartenProjectOwnerReader>();
builder.Services.AddScoped<IProjectPermissionEvaluator, ProjectPermissionEvaluator>();
builder.Services.AddTcFlowProjectionAdministration(options =>
{
    options.AllowedProjectionNames.Add(ProjectProjectionNames.Current);
    options.AllowedProjectionNames.Add(ProjectProjectionNames.PortfolioSummary);
    options.AllowedProjectionNames.Add(TaskProjectionNames.Current);
    options.AllowedProjectionNames.Add(TaskProjectionNames.Board);
    options.AllowedProjectionNames.Add(TaskProjectionNames.Analytics);
    options.AllowedProjectionNames.Add(StormingProjectionNames.BoardCanvas);
    options.AllowedProjectionNames.Add(StormingProjectionNames.DomainEventCatalog);
    options.AllowedProjectionNames.Add(ArchitectureProjectionNames.Current);
    options.AllowedProjectionNames.Add(ArchitectureProjectionNames.Overview);
    options.AllowedProjectionNames.Add(RepositoryProjectionNames.Current);
    options.AllowedProjectionNames.Add(RepositoryProjectionNames.KnowledgeGraph);
    options.AllowedProjectionNames.Add(RepositoryProjectionNames.ImpactGraph);
    options.AllowedProjectionNames.Add(PlatformProjectionNames.Current);
});

builder.Services.AddMarten(options =>
{
    options.Connection(martenConnection);
    TcFlowEventStoreConfiguration.Configure(options);
    ProjectsMartenConfiguration.Configure(options);
    AccessControlMartenConfiguration.Configure(options);
    PlanningMartenConfiguration.Configure(options);
    TaskFlowMartenConfiguration.Configure(options);
    StormingMartenConfiguration.Configure(options);
    ArchitectureMartenConfiguration.Configure(options);
    RepositoryMartenConfiguration.Configure(options);
    IntegrationsMartenConfiguration.Configure(options);
    PlatformMartenConfiguration.Configure(options);
})
.IntegrateWithWolverine(options => options.MessageStorageSchemaName = "wolverine")
.AddAsyncDaemon(DaemonMode.HotCold);

builder.Services.AddResourceSetupOnStartup();
builder.Host.ApplyJasperFxExtensions();
builder.Host.UseWolverine(options =>
{
    options.Discovery.IncludeAssembly(typeof(ProjectCommandHandlers).Assembly);
    options.Discovery.IncludeAssembly(typeof(AccessCommandHandlers).Assembly);
    options.Discovery.IncludeAssembly(typeof(PlanningHandlers).Assembly);
    options.Discovery.IncludeAssembly(typeof(TaskFlowHandlers).Assembly);
    options.Discovery.IncludeAssembly(typeof(StormingHandlers).Assembly);
    options.Discovery.IncludeAssembly(typeof(ArchitectureHandlers).Assembly);
    options.Discovery.IncludeAssembly(typeof(RepositoryHandlers).Assembly);
    options.Discovery.IncludeAssembly(typeof(PlatformHandlers).Assembly);
    TcFlowMessagingConfiguration.Configure(options);
});

builder.Services.AddWolverineHttp();

var app = builder.Build();

app.UseHeroMultiTenantDatabases();
app.UseHeroPlatform(options =>
{
    options.MapModules = true;
    options.ServeStaticFiles = false;
    options.MapSseEndpoints = false;
    options.MapRealtime = false;
    options.MapJobsDashboard = false;
});

// All vNext command/query routes inherit FullStackHero authentication and the
// actor-consistency check. Explicitly anonymous operational endpoints (health
// and GitHub webhook) remain outside this group.
var vnext = app.MapGroup(string.Empty)
    .AddEndpointFilter<ActorConsistencyEndpointFilter>();

app.MapGet("/health", () => Results.Ok(new { status = "ok", architecture = "goal2-vnext" }))
    .AllowAnonymous();

vnext.MapPost("/api/vnext/projects", async (
    CreateProject command,
    IMessageBus bus,
    CancellationToken cancellationToken) =>
{
    var result = await bus.InvokeAsync<ProjectCommandResult>(command, cancellationToken)
        .ConfigureAwait(false);
    return Results.Created($"/api/vnext/projects/{result.ProjectId}", result);
});

vnext.MapPost("/api/vnext/projects/{projectId:guid}/rename", async (
    Guid projectId,
    RenameProject command,
    IMessageBus bus,
    CancellationToken cancellationToken) =>
{
    if (projectId != command.ProjectId)
    {
        return Results.BadRequest(new { error = "The route and command project IDs must match." });
    }

    var result = await bus.InvokeAsync<ProjectCommandResult>(command, cancellationToken)
        .ConfigureAwait(false);
    return Results.Ok(result);
});

vnext.MapPost("/api/vnext/projects/{projectId:guid}/suspend", async (
    Guid projectId,
    SuspendProject command,
    IMessageBus bus,
    CancellationToken cancellationToken) =>
{
    if (projectId != command.ProjectId)
    {
        return Results.BadRequest(new { error = "The route and command project IDs must match." });
    }

    var result = await bus.InvokeAsync<ProjectCommandResult>(command, cancellationToken)
        .ConfigureAwait(false);
    return Results.Ok(result);
});

vnext.MapPost("/api/vnext/projects/{projectId:guid}/activate", async (
    Guid projectId,
    ActivateProject command,
    IMessageBus bus,
    CancellationToken cancellationToken) =>
{
    if (projectId != command.ProjectId)
    {
        return Results.BadRequest(new { error = "The route and command project IDs must match." });
    }

    var result = await bus.InvokeAsync<ProjectCommandResult>(command, cancellationToken)
        .ConfigureAwait(false);
    return Results.Ok(result);
});

vnext.MapGet("/api/vnext/projects/{projectId:guid}", async (
    Guid projectId,
    IQuerySession session,
    CancellationToken cancellationToken) =>
{
    var result = await ProjectQueries.Handle(new GetProject(projectId), session, cancellationToken)
        .ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

vnext.MapGet("/api/vnext/projects/{projectId:guid}/summary", async (
    Guid projectId,
    IQuerySession session,
    CancellationToken cancellationToken) =>
{
    var result = await ProjectQueries.Handle(
            new GetProjectPortfolioSummary(projectId),
            session,
            cancellationToken)
        .ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

vnext.MapPost("/api/vnext/projects/{projectId:guid}/roles", async (
    Guid projectId,
    CreateProjectRole command,
    IMessageBus bus,
    CancellationToken cancellationToken) =>
{
    if (projectId != command.ProjectId)
    {
        return Results.BadRequest(new { error = "The route and command project IDs must match." });
    }

    var result = await bus.InvokeAsync<AccessCommandResult>(command, cancellationToken)
        .ConfigureAwait(false);
    return Results.Ok(result);
});

vnext.MapGet("/api/vnext/projects/{projectId:guid}/permissions/{userId}", async (
    Guid projectId,
    string userId,
    IProjectPermissionEvaluator evaluator,
    CancellationToken cancellationToken) =>
{
    var result = await evaluator.GetEffectivePermissionsAsync(
            userId,
            projectId,
            repositoryId: null,
            component: null,
            cancellationToken)
        .ConfigureAwait(false);
    return Results.Ok(result);
});

vnext.MapPost("/api/vnext/plans", async (
    CreatePlan command,
    IMessageBus bus,
    CancellationToken cancellationToken) =>
{
    var result = await bus.InvokeAsync<PlanningCommandResult>(command, cancellationToken)
        .ConfigureAwait(false);
    return Results.Created($"/api/vnext/plans/{result.PlanId}", result);
});

vnext.MapPost("/api/vnext/plans/{planId:guid}/requirements", async (
    Guid planId,
    AddRequirement command,
    IMessageBus bus,
    CancellationToken cancellationToken) =>
{
    if (planId != command.PlanId)
    {
        return Results.BadRequest(new { error = "The route and command plan IDs must match." });
    }

    var result = await bus.InvokeAsync<PlanningCommandResult>(command, cancellationToken)
        .ConfigureAwait(false);
    return Results.Ok(result);
});

vnext.MapPost("/api/vnext/plans/{planId:guid}/milestones", async (
    Guid planId,
    AddMilestone command,
    IMessageBus bus,
    CancellationToken cancellationToken) =>
{
    if (planId != command.PlanId)
    {
        return Results.BadRequest(new { error = "The route and command plan IDs must match." });
    }

    var result = await bus.InvokeAsync<PlanningCommandResult>(command, cancellationToken)
        .ConfigureAwait(false);
    return Results.Ok(result);
});

vnext.MapGet("/api/vnext/plans/{planId:guid}", async (
    Guid planId,
    IQuerySession session,
    CancellationToken cancellationToken) =>
{
    var result = await PlanningQueries.Handle(new GetPlan(planId), session, cancellationToken)
        .ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

vnext.MapPost("/api/vnext/tasks", async (CreateTask command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    var result = await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false);
    return Results.Created($"/api/vnext/tasks/{result.TaskId}", result);
});

vnext.MapPost("/api/vnext/tasks/source-proposal", async (ApplySourceChangeProposal command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    var result = await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false);
    return Results.Ok(result);
});

vnext.MapGet("/api/vnext/tasks/{taskId:guid}", async (Guid taskId, IQuerySession session, CancellationToken cancellationToken) =>
{
    var result = await TaskFlowQueries.Handle(new GetTask(taskId), session, cancellationToken).ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

vnext.MapPost("/api/vnext/event-storming/boards", async (CreateBoard command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    var result = await bus.InvokeAsync<StormingCommandResult>(command, cancellationToken).ConfigureAwait(false);
    return Results.Created($"/api/vnext/event-storming/boards/{result.BoardId}", result);
});

vnext.MapPost("/api/vnext/event-storming/boards/{boardId:guid}/nodes", async (Guid boardId, AddNode command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (boardId != command.BoardId) return Results.BadRequest(new { error = "The route and command board IDs must match." });
    return Results.Ok(await bus.InvokeAsync<StormingCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/event-storming/boards/{boardId:guid}/connections", async (Guid boardId, ConnectNodes command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (boardId != command.BoardId) return Results.BadRequest(new { error = "The route and command board IDs must match." });
    return Results.Ok(await bus.InvokeAsync<StormingCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/event-storming/boards/{boardId:guid}/hotspots", async (Guid boardId, MarkHotspot command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (boardId != command.BoardId) return Results.BadRequest(new { error = "The route and command board IDs must match." });
    return Results.Ok(await bus.InvokeAsync<StormingCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapGet("/api/vnext/event-storming/boards/{boardId:guid}", async (Guid boardId, IQuerySession session, CancellationToken cancellationToken) =>
{
    var result = await StormingQueries.Handle(new GetBoard(boardId), session, cancellationToken).ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

vnext.MapPost("/api/vnext/architecture/models", async (CreateArchitectureModel command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    var result = await bus.InvokeAsync<ArchitectureCommandResult>(command, cancellationToken).ConfigureAwait(false);
    return Results.Created($"/api/vnext/architecture/models/{result.ModelId}", result);
});

vnext.MapPost("/api/vnext/architecture/models/{modelId:guid}/modules", async (Guid modelId, AddModule command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (modelId != command.ModelId) return Results.BadRequest(new { error = "The route and command model IDs must match." });
    return Results.Ok(await bus.InvokeAsync<ArchitectureCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/architecture/models/{modelId:guid}/module-relationships", async (Guid modelId, ConnectModules command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (modelId != command.ModelId) return Results.BadRequest(new { error = "The route and command model IDs must match." });
    return Results.Ok(await bus.InvokeAsync<ArchitectureCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/architecture/models/{modelId:guid}/entities", async (Guid modelId, AddDataEntity command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (modelId != command.ModelId) return Results.BadRequest(new { error = "The route and command model IDs must match." });
    return Results.Ok(await bus.InvokeAsync<ArchitectureCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/architecture/models/{modelId:guid}/data-relationships", async (Guid modelId, AddDataRelationship command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (modelId != command.ModelId) return Results.BadRequest(new { error = "The route and command model IDs must match." });
    return Results.Ok(await bus.InvokeAsync<ArchitectureCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/architecture/models/{modelId:guid}/drifts", async (Guid modelId, RecordArchitectureDrift command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (modelId != command.ModelId) return Results.BadRequest(new { error = "The route and command model IDs must match." });
    return Results.Ok(await bus.InvokeAsync<ArchitectureCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapGet("/api/vnext/architecture/models/{modelId:guid}", async (Guid modelId, IQuerySession session, CancellationToken cancellationToken) =>
{
    var result = await ArchitectureQueries.Handle(new GetArchitectureModel(modelId), session, cancellationToken).ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

vnext.MapPost("/api/vnext/repository-intelligence/analyses", async (StartAnalysis command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    var result = await bus.InvokeAsync<AnalysisCommandResult>(command, cancellationToken).ConfigureAwait(false);
    return Results.Created($"/api/vnext/repository-intelligence/analyses/{result.AnalysisRunId}", result);
});

vnext.MapPost("/api/vnext/repository-intelligence/analyses/{analysisRunId:guid}/artifacts", async (Guid analysisRunId, ObserveArtifact command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (analysisRunId != command.AnalysisRunId) return Results.BadRequest(new { error = "The route and command analysis IDs must match." });
    return Results.Ok(await bus.InvokeAsync<AnalysisCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/repository-intelligence/analyses/{analysisRunId:guid}/changes", async (Guid analysisRunId, DetectSourceChange command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (analysisRunId != command.AnalysisRunId) return Results.BadRequest(new { error = "The route and command analysis IDs must match." });
    return Results.Ok(await bus.InvokeAsync<AnalysisCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/repository-intelligence/analyses/{analysisRunId:guid}/evidence", async (Guid analysisRunId, RecordEvidence command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (analysisRunId != command.AnalysisRunId) return Results.BadRequest(new { error = "The route and command analysis IDs must match." });
    return Results.Ok(await bus.InvokeAsync<AnalysisCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/repository-intelligence/analyses/{analysisRunId:guid}/complete", async (Guid analysisRunId, CompleteAnalysis command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (analysisRunId != command.AnalysisRunId) return Results.BadRequest(new { error = "The route and command analysis IDs must match." });
    return Results.Ok(await bus.InvokeAsync<AnalysisCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapGet("/api/vnext/repository-intelligence/analyses/{analysisRunId:guid}", async (Guid analysisRunId, IQuerySession session, CancellationToken cancellationToken) =>
{
    var result = await RepositoryQueries.Handle(new GetAnalysis(analysisRunId), session, cancellationToken).ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapPost("/api/vnext/integrations/github/webhook", async (HttpRequest request, GitHubWebhookProcessor processor, CancellationToken cancellationToken) =>
{
    var deliveryId = request.Headers["X-GitHub-Delivery"].FirstOrDefault();
    var eventType = request.Headers["X-GitHub-Event"].FirstOrDefault();
    var signature = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    var correlationId = request.Headers["X-Correlation-Id"].FirstOrDefault() ?? deliveryId ?? Guid.NewGuid().ToString("N");
    if (string.IsNullOrWhiteSpace(deliveryId) || string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(signature)) return Results.BadRequest(new { error = "GitHub delivery, event, and signature headers are required." });
    var result = await processor.ProcessAsync(deliveryId, eventType, signature, body, correlationId, cancellationToken).ConfigureAwait(false);
    return result.InvalidSignature ? Results.Unauthorized() : Results.Ok(result);
}).AllowAnonymous();

vnext.MapPost("/api/vnext/platform/policies/{policyId:guid}", async (Guid policyId, UpdatePlatformPolicy command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (policyId != command.PolicyId) return Results.BadRequest(new { error = "The route and command policy IDs must match." });
    return Results.Ok(await bus.InvokeAsync<PlatformCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/platform/policies/{policyId:guid}/ai-provider", async (Guid policyId, ConfigureAiProvider command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (policyId != command.PolicyId) return Results.BadRequest(new { error = "The route and command policy IDs must match." });
    return Results.Ok(await bus.InvokeAsync<PlatformCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapGet("/api/vnext/platform/policies/{policyId:guid}", async (Guid policyId, IQuerySession session, CancellationToken cancellationToken) =>
{
    var result = await PlatformQueries.Handle(new GetPlatformPolicy(policyId), session, cancellationToken).ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

vnext.MapPost("/api/vnext/tasks/{taskId:guid}/accept", async (Guid taskId, AcceptTask command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/tasks/{taskId:guid}/assign", async (Guid taskId, AssignTask command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/tasks/{taskId:guid}/start", async (Guid taskId, StartTask command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/tasks/{taskId:guid}/ai-verification", async (Guid taskId, CompleteAiVerification command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/tasks/{taskId:guid}/review", async (Guid taskId, RequestReview command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/tasks/{taskId:guid}/review/approve", async (Guid taskId, ApproveReview command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

vnext.MapPost("/api/vnext/tasks/{taskId:guid}/complete", async (Guid taskId, CompleteTask command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

return await app.RunJasperFxCommands(args).ConfigureAwait(false);
