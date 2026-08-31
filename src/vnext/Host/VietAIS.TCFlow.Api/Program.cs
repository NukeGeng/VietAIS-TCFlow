using JasperFx;
using JasperFx.Events.Daemon;
using JasperFx.Resources;
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
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IIdGenerator, UuidV7IdGenerator>();
builder.Services.AddScoped<IProjectOwnerReader, MartenProjectOwnerReader>();
builder.Services.AddScoped<IProjectPermissionEvaluator, ProjectPermissionEvaluator>();
builder.Services.AddTcFlowProjectionAdministration(options =>
{
    options.AllowedProjectionNames.Add(ProjectProjectionNames.Current);
    options.AllowedProjectionNames.Add(ProjectProjectionNames.PortfolioSummary);
    options.AllowedProjectionNames.Add(TaskProjectionNames.Current);
    options.AllowedProjectionNames.Add(TaskProjectionNames.Board);
    options.AllowedProjectionNames.Add(TaskProjectionNames.Analytics);
});

var martenConnection = builder.Configuration.GetConnectionString("marten");
if (string.IsNullOrWhiteSpace(martenConnection))
{
    throw new InvalidOperationException(
        "ConnectionStrings:marten is required. Supply it through user-secrets, environment, or Aspire.");
}

builder.Services.AddMarten(options =>
{
    options.Connection(martenConnection);
    TcFlowEventStoreConfiguration.Configure(options);
    ProjectsMartenConfiguration.Configure(options);
    AccessControlMartenConfiguration.Configure(options);
    PlanningMartenConfiguration.Configure(options);
    TaskFlowMartenConfiguration.Configure(options);
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
    TcFlowMessagingConfiguration.Configure(options);
});

builder.Services.AddOpenApi();
builder.Services.AddWolverineHttp();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", architecture = "goal2-vnext" }))
    .AllowAnonymous();

app.MapPost("/api/vnext/projects", async (
    CreateProject command,
    IMessageBus bus,
    CancellationToken cancellationToken) =>
{
    var result = await bus.InvokeAsync<ProjectCommandResult>(command, cancellationToken)
        .ConfigureAwait(false);
    return Results.Created($"/api/vnext/projects/{result.ProjectId}", result);
});

app.MapPost("/api/vnext/projects/{projectId:guid}/rename", async (
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

app.MapPost("/api/vnext/projects/{projectId:guid}/suspend", async (
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

app.MapPost("/api/vnext/projects/{projectId:guid}/activate", async (
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

app.MapGet("/api/vnext/projects/{projectId:guid}", async (
    Guid projectId,
    IQuerySession session,
    CancellationToken cancellationToken) =>
{
    var result = await ProjectQueries.Handle(new GetProject(projectId), session, cancellationToken)
        .ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/api/vnext/projects/{projectId:guid}/summary", async (
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

app.MapPost("/api/vnext/projects/{projectId:guid}/roles", async (
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

app.MapGet("/api/vnext/projects/{projectId:guid}/permissions/{userId}", async (
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

app.MapPost("/api/vnext/plans", async (
    CreatePlan command,
    IMessageBus bus,
    CancellationToken cancellationToken) =>
{
    var result = await bus.InvokeAsync<PlanningCommandResult>(command, cancellationToken)
        .ConfigureAwait(false);
    return Results.Created($"/api/vnext/plans/{result.PlanId}", result);
});

app.MapPost("/api/vnext/plans/{planId:guid}/requirements", async (
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

app.MapPost("/api/vnext/plans/{planId:guid}/milestones", async (
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

app.MapGet("/api/vnext/plans/{planId:guid}", async (
    Guid planId,
    IQuerySession session,
    CancellationToken cancellationToken) =>
{
    var result = await PlanningQueries.Handle(new GetPlan(planId), session, cancellationToken)
        .ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapPost("/api/vnext/tasks", async (CreateTask command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    var result = await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false);
    return Results.Created($"/api/vnext/tasks/{result.TaskId}", result);
});

app.MapPost("/api/vnext/tasks/source-proposal", async (ApplySourceChangeProposal command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    var result = await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false);
    return Results.Ok(result);
});

app.MapGet("/api/vnext/tasks/{taskId:guid}", async (Guid taskId, IQuerySession session, CancellationToken cancellationToken) =>
{
    var result = await TaskFlowQueries.Handle(new GetTask(taskId), session, cancellationToken).ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapPost("/api/vnext/tasks/{taskId:guid}/accept", async (Guid taskId, AcceptTask command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

app.MapPost("/api/vnext/tasks/{taskId:guid}/assign", async (Guid taskId, AssignTask command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

app.MapPost("/api/vnext/tasks/{taskId:guid}/start", async (Guid taskId, StartTask command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

app.MapPost("/api/vnext/tasks/{taskId:guid}/ai-verification", async (Guid taskId, CompleteAiVerification command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

app.MapPost("/api/vnext/tasks/{taskId:guid}/review", async (Guid taskId, RequestReview command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

app.MapPost("/api/vnext/tasks/{taskId:guid}/review/approve", async (Guid taskId, ApproveReview command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

app.MapPost("/api/vnext/tasks/{taskId:guid}/complete", async (Guid taskId, CompleteTask command, IMessageBus bus, CancellationToken cancellationToken) =>
{
    if (taskId != command.TaskId) return Results.BadRequest(new { error = "The route and command task IDs must match." });
    return Results.Ok(await bus.InvokeAsync<TaskCommandResult>(command, cancellationToken).ConfigureAwait(false));
});

app.MapOpenApi();

return await app.RunJasperFxCommands(args).ConfigureAwait(false);
