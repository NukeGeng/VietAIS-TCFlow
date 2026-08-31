using Marten;
using VietAIS.TCFlow.BuildingBlocks.Application.Identity;
using VietAIS.TCFlow.BuildingBlocks.Application.Time;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.Modules.AccessControl.Authorization;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Commands;
using VietAIS.TCFlow.Modules.AccessControl.Contracts.Models;
using VietAIS.TCFlow.Modules.AccessControl.Domain;
using Wolverine.Attributes;

namespace VietAIS.TCFlow.Modules.AccessControl.Features;

public sealed record AccessCommandResult(Guid ProjectId, long ExpectedVersion);

[WolverineHandler]
public static class AccessCommandHandlers
{
    public static async Task<AccessCommandResult> Handle(
        CreateProjectRole command,
        IDocumentSession session,
        IClock clock,
        IIdGenerator idGenerator,
        IProjectOwnerReader ownerReader,
        IProjectPermissionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        ValidateCommand(command.ProjectId, command.ActorId, command.CorrelationId);
        ArgumentNullException.ThrowIfNull(ownerReader);
        ArgumentNullException.ThrowIfNull(idGenerator);
        var ownerId = await ownerReader.GetOwnerIdAsync(command.ProjectId, cancellationToken).ConfigureAwait(false);
        if (ownerId is null)
        {
            throw new KeyNotFoundException($"Project '{command.ProjectId}' was not found.");
        }

        var streamId = ProjectAccessStreamIdentity.ForProject(command.ProjectId);
        session.ApplyEventMetadata(new EventMetadata(
            command.ActorId,
            command.CorrelationId,
            command.CausationId,
            command.ProjectId,
            TenantId: null,
            Source: "access-control.role.create"));

        var stream = await session.Events.FetchForWriting<ProjectAccessAggregate>(
            streamId,
            command.ExpectedVersion,
            cancellationToken).ConfigureAwait(false);
        if (stream.Aggregate is null)
        {
            if (command.ExpectedVersion != 0 ||
                !string.Equals(ownerId, command.ActorId.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Only the project owner may initialize project access control.");
            }

            var ownerRoleId = idGenerator.NewId();
            var aggregate = new ProjectAccessAggregate();
            var initialized = new ProjectAccessInitialized(
                command.ProjectId,
                ownerId,
                ownerRoleId,
                command.ActorId.Trim(),
                command.CorrelationId.Trim(),
                clock.UtcNow);
            aggregate.Apply(initialized);
            var role = aggregate.CreateRole(
                command.Name,
                idGenerator.NewId(),
                command.ActorId,
                command.CorrelationId,
                clock.UtcNow);
            session.Events.StartStream<ProjectAccessAggregate>(streamId, initialized, role);
            return new AccessCommandResult(command.ProjectId, 2);
        }

        await evaluator.EnsureAuthorizedAsync(
            command.ActorId,
            command.ProjectId,
            ProjectPermissionCatalog.RoleManage,
            repositoryId: null,
            component: null,
            cancellationToken).ConfigureAwait(false);
        var created = stream.Aggregate.CreateRole(
            command.Name,
            idGenerator.NewId(),
            command.ActorId,
            command.CorrelationId,
            clock.UtcNow);
        stream.AppendOne(created);
        return new AccessCommandResult(command.ProjectId, command.ExpectedVersion + 1);
    }

    public static async Task<AccessCommandResult> Handle(
        UpdateProjectRolePermissions command,
        IDocumentSession session,
        IClock clock,
        IProjectPermissionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        ValidateCommand(command.ProjectId, command.ActorId, command.CorrelationId);
        await evaluator.EnsureAuthorizedAsync(
            command.ActorId,
            command.ProjectId,
            ProjectPermissionCatalog.RoleManage,
            repositoryId: null,
            component: null,
            cancellationToken).ConfigureAwait(false);
        var stream = await session.Events.FetchForWriting<ProjectAccessAggregate>(
            ProjectAccessStreamIdentity.ForProject(command.ProjectId),
            command.ExpectedVersion,
            cancellationToken).ConfigureAwait(false);
        if (stream.Aggregate is null)
        {
            throw new KeyNotFoundException($"Project access control for '{command.ProjectId}' was not initialized.");
        }
        var updated = stream.Aggregate!.UpdatePermissions(
            command.RoleId,
            command.Grants,
            command.ActorId,
            command.CorrelationId,
            clock.UtcNow);
        ApplyMetadata(session, command.ActorId, command.CorrelationId, command.CausationId, command.ProjectId, "access-control.role.permissions");
        stream.AppendOne(updated);
        return new AccessCommandResult(command.ProjectId, command.ExpectedVersion + 1);
    }

    public static async Task<AccessCommandResult> Handle(
        AddProjectMember command,
        IDocumentSession session,
        IClock clock,
        IProjectPermissionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        ValidateCommand(command.ProjectId, command.ActorId, command.CorrelationId);
        await evaluator.EnsureAuthorizedAsync(
            command.ActorId,
            command.ProjectId,
            ProjectPermissionCatalog.MemberManage,
            repositoryId: null,
            component: null,
            cancellationToken).ConfigureAwait(false);
        var stream = await session.Events.FetchForWriting<ProjectAccessAggregate>(
            ProjectAccessStreamIdentity.ForProject(command.ProjectId),
            command.ExpectedVersion,
            cancellationToken).ConfigureAwait(false);
        if (stream.Aggregate is null)
        {
            throw new KeyNotFoundException($"Project access control for '{command.ProjectId}' was not initialized.");
        }
        var added = stream.Aggregate!.AddMember(
            command.UserId,
            command.ActorId,
            command.CorrelationId,
            clock.UtcNow);
        ApplyMetadata(session, command.ActorId, command.CorrelationId, command.CausationId, command.ProjectId, "access-control.member.add");
        stream.AppendOne(added);
        return new AccessCommandResult(command.ProjectId, command.ExpectedVersion + 1);
    }

    public static async Task<AccessCommandResult> Handle(
        AssignMemberRoles command,
        IDocumentSession session,
        IClock clock,
        IProjectPermissionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        ValidateCommand(command.ProjectId, command.ActorId, command.CorrelationId);
        await evaluator.EnsureAuthorizedAsync(
            command.ActorId,
            command.ProjectId,
            ProjectPermissionCatalog.MemberManage,
            repositoryId: null,
            component: null,
            cancellationToken).ConfigureAwait(false);
        var stream = await session.Events.FetchForWriting<ProjectAccessAggregate>(
            ProjectAccessStreamIdentity.ForProject(command.ProjectId),
            command.ExpectedVersion,
            cancellationToken).ConfigureAwait(false);
        if (stream.Aggregate is null)
        {
            throw new KeyNotFoundException($"Project access control for '{command.ProjectId}' was not initialized.");
        }
        var assigned = stream.Aggregate!.AssignRoles(
            command.UserId,
            command.RoleIds,
            command.ActorId,
            command.CorrelationId,
            clock.UtcNow);
        ApplyMetadata(session, command.ActorId, command.CorrelationId, command.CausationId, command.ProjectId, "access-control.member.roles");
        stream.AppendOne(assigned);
        return new AccessCommandResult(command.ProjectId, command.ExpectedVersion + 1);
    }

    public static async Task<AccessCommandResult> Handle(
        RemoveProjectMember command,
        IDocumentSession session,
        IClock clock,
        IProjectPermissionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        ValidateCommand(command.ProjectId, command.ActorId, command.CorrelationId);
        await evaluator.EnsureAuthorizedAsync(
            command.ActorId,
            command.ProjectId,
            ProjectPermissionCatalog.MemberManage,
            repositoryId: null,
            component: null,
            cancellationToken).ConfigureAwait(false);
        var stream = await session.Events.FetchForWriting<ProjectAccessAggregate>(
            ProjectAccessStreamIdentity.ForProject(command.ProjectId),
            command.ExpectedVersion,
            cancellationToken).ConfigureAwait(false);
        if (stream.Aggregate is null)
        {
            throw new KeyNotFoundException($"Project access control for '{command.ProjectId}' was not initialized.");
        }
        var removed = stream.Aggregate!.RemoveMember(
            command.UserId,
            command.ActorId,
            command.CorrelationId,
            clock.UtcNow);
        ApplyMetadata(session, command.ActorId, command.CorrelationId, command.CausationId, command.ProjectId, "access-control.member.remove");
        stream.AppendOne(removed);
        return new AccessCommandResult(command.ProjectId, command.ExpectedVersion + 1);
    }

    private static void ApplyMetadata(
        IDocumentSession session,
        string actorId,
        string correlationId,
        string? causationId,
        Guid projectId,
        string source) =>
        session.ApplyEventMetadata(new EventMetadata(
            actorId,
            correlationId,
            causationId,
            projectId,
            TenantId: null,
            Source: source));

    private static void ValidateCommand(Guid projectId, string actorId, string correlationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(projectId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
    }
}
