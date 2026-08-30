using JasperFx;
using JasperFx.Events.Daemon;
using JasperFx.Resources;
using Marten;
using VietAIS.TCFlow.Modules.Projects.Configuration;
using VietAIS.TCFlow.Modules.Projects.Contracts.Commands;
using VietAIS.TCFlow.Modules.Projects.Contracts.Queries;
using VietAIS.TCFlow.Modules.Projects.Features;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);

var martenConnection = builder.Configuration.GetConnectionString("marten");
if (string.IsNullOrWhiteSpace(martenConnection))
{
    throw new InvalidOperationException(
        "ConnectionStrings:marten is required. Supply it through user-secrets, environment, or Aspire.");
}

builder.Services.AddMarten(options =>
{
    options.Connection(martenConnection);
    ProjectsMartenConfiguration.Configure(options);
})
.IntegrateWithWolverine()
.AddAsyncDaemon(DaemonMode.HotCold);

builder.Services.AddResourceSetupOnStartup();
builder.Host.ApplyJasperFxExtensions();
builder.Host.UseWolverine(options =>
{
    options.Durability.MessageStorageSchemaName = "wolverine";
    options.Discovery.IncludeAssembly(typeof(ProjectCommandHandlers).Assembly);
    // Command handlers own the Marten unit-of-work and call SaveChangesAsync
    // explicitly. This keeps the commit boundary visible while the migration
    // slice is still being introduced; automatic transaction middleware will
    // be enabled once all handlers share the same convention.
    options.Policies.UseDurableLocalQueues();
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

app.MapGet("/api/vnext/projects/{projectId:guid}", async (
    Guid projectId,
    IQuerySession session,
    CancellationToken cancellationToken) =>
{
    var result = await ProjectQueries.Handle(new GetProject(projectId), session, cancellationToken)
        .ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapOpenApi();

return await app.RunJasperFxCommands(args).ConfigureAwait(false);
