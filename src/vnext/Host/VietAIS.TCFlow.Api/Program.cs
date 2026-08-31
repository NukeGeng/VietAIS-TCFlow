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
using VietAIS.TCFlow.Modules.Projects.Configuration;
using VietAIS.TCFlow.Modules.Projects.Contracts.Commands;
using VietAIS.TCFlow.Modules.Projects.Contracts.Queries;
using VietAIS.TCFlow.Modules.Projects.Features;
using VietAIS.TCFlow.Modules.Projects.Projections;
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
})
.IntegrateWithWolverine(options => options.MessageStorageSchemaName = "wolverine")
.AddAsyncDaemon(DaemonMode.HotCold);

builder.Services.AddResourceSetupOnStartup();
builder.Host.ApplyJasperFxExtensions();
builder.Host.UseWolverine(options =>
{
    options.Discovery.IncludeAssembly(typeof(ProjectCommandHandlers).Assembly);
    options.Discovery.IncludeAssembly(typeof(AccessCommandHandlers).Assembly);
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

app.MapOpenApi();

return await app.RunJasperFxCommands(args).ConfigureAwait(false);
