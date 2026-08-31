using Marten;
using VietAIS.TCFlow.BuildingBlocks.Application.Identity;
using VietAIS.TCFlow.BuildingBlocks.Application.Time;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.Modules.Projects.Contracts.Commands;
using VietAIS.TCFlow.Modules.Projects.Domain;
using Wolverine.Attributes;

namespace VietAIS.TCFlow.Modules.Projects.Features;

public sealed record ProjectCommandResult(Guid ProjectId, long ExpectedVersion);

[WolverineHandler]
public static class ProjectCommandHandlers
{
    public static ProjectCommandResult Handle(
        CreateProject command,
        IDocumentSession session,
        IClock clock,
        IIdGenerator idGenerator)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(idGenerator);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.OwnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CorrelationId);

        var projectId = command.ProjectId == Guid.Empty ? idGenerator.NewId() : command.ProjectId;
        session.ApplyEventMetadata(new EventMetadata(
            command.OwnerId,
            command.CorrelationId,
            command.CausationId,
            projectId,
            TenantId: null,
            Source: "projects.create"));
        var created = new ProjectCreated(
            projectId,
            command.Name.Trim(),
            command.OwnerId.Trim(),
            command.OwnerId.Trim(),
            command.CorrelationId.Trim(),
            clock.UtcNow);

        session.Events.StartStream<ProjectAggregate>(projectId, created);
        return new ProjectCommandResult(projectId, 1);
    }

    public static async Task<ProjectCommandResult> Handle(
        RenameProject command,
        IDocumentSession session,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ActorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CorrelationId);

        session.ApplyEventMetadata(new EventMetadata(
            command.ActorId,
            command.CorrelationId,
            command.CausationId,
            command.ProjectId,
            TenantId: null,
            Source: "projects.rename"));

        var stream = await session.Events.FetchForWriting<ProjectAggregate>(
            command.ProjectId,
            command.ExpectedVersion,
            cancellationToken).ConfigureAwait(false);
        if (stream.Aggregate is null)
        {
            throw new KeyNotFoundException($"Project '{command.ProjectId}' was not found.");
        }

        var renamed = stream.Aggregate.Rename(
            command.Name,
            command.ActorId.Trim(),
            command.CorrelationId.Trim(),
            clock.UtcNow);
        stream.AppendOne(renamed);
        return new ProjectCommandResult(command.ProjectId, command.ExpectedVersion + 1);
    }
}
