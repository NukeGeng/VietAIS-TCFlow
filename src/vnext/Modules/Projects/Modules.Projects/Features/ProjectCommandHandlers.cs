using Marten;
using VietAIS.TCFlow.Modules.Projects.Contracts.Commands;
using VietAIS.TCFlow.Modules.Projects.Domain;
using Wolverine.Attributes;

namespace VietAIS.TCFlow.Modules.Projects.Features;

public sealed record ProjectCommandResult(Guid ProjectId, long ExpectedVersion);

[WolverineHandler]
public static class ProjectCommandHandlers
{
    public static async Task<ProjectCommandResult> Handle(
        CreateProject command,
        IDocumentSession session,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.OwnerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CorrelationId);

        var projectId = command.ProjectId == Guid.Empty ? Guid.CreateVersion7() : command.ProjectId;
        var created = new ProjectCreated(
            projectId,
            command.Name.Trim(),
            command.OwnerId.Trim(),
            command.OwnerId.Trim(),
            command.CorrelationId.Trim(),
            timeProvider.GetUtcNow());

        session.Events.StartStream<ProjectAggregate>(projectId, created);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ProjectCommandResult(projectId, 1);
    }

    public static async Task<ProjectCommandResult> Handle(
        RenameProject command,
        IDocumentSession session,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CorrelationId);

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
            "system",
            command.CorrelationId.Trim(),
            timeProvider.GetUtcNow());
        stream.AppendOne(renamed);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ProjectCommandResult(command.ProjectId, command.ExpectedVersion + 1);
    }
}
