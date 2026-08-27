using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Paging;
using Marten;
using MediatR;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record CreateEngineeringTaskCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid? RepositoryId,
    Guid? ComponentId,
    Guid? FeatureId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid? SourceChangeId,
    Guid[] ArtifactIds,
    Guid[] ImpactIds,
    string[] AffectedArtifacts,
    string[] Inputs,
    string[] Outputs,
    string[] BusinessRules,
    Guid[] Dependencies)
    : IRequest<EngineeringTask>;

public sealed record SearchEngineeringTasksQuery(
    Guid ActorId,
    Guid ProjectId,
    int PageNumber,
    int PageSize,
    string? Keyword,
    TaskLifecycleStatus? Status,
    TaskPriority? Priority,
    Guid? RepositoryId,
    Guid? FeatureId,
    Guid? AssigneeId)
    : IRequest<PagedList<EngineeringTask>>;

public sealed record GetEngineeringTaskQuery(Guid ActorId, Guid ProjectId, Guid TaskId)
    : IRequest<EngineeringTaskDetails>;

public sealed record EngineeringTaskDetails(
    EngineeringTask Task,
    TaskAssignment? Assignment,
    IReadOnlyList<TaskReview> Reviews,
    IReadOnlyList<TaskEvidence> Evidence);

public sealed record TransitionEngineeringTaskCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid TaskId,
    TaskLifecycleStatus Status,
    string? Reason)
    : IRequest<EngineeringTask>;

public sealed record AssignEngineeringTaskCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid TaskId,
    Guid AssigneeId)
    : IRequest<TaskAssignment>;

public sealed record ReviewEngineeringTaskCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid TaskId,
    TaskReviewDecision Decision,
    string? Comment)
    : IRequest<TaskReview>;

public sealed record AddTaskEvidenceCommand(
    Guid ActorId,
    TaskActorType ActorType,
    Guid ProjectId,
    Guid TaskId,
    TaskEvidenceKind Kind,
    string Summary,
    string? Location,
    Guid? SourceChangeId,
    Guid? ArtifactId,
    Guid? ImpactId,
    decimal? Confidence)
    : IRequest<TaskEvidence>;

public sealed record RecordTaskAiVerificationCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid TaskId,
    AiVerificationStatus Status,
    string Summary)
    : IRequest<EngineeringTask>;

public sealed record GetTaskHistoryQuery(Guid ActorId, Guid ProjectId, Guid TaskId)
    : IRequest<IReadOnlyList<TaskVersion>>;

public sealed class CreateEngineeringTaskHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<CreateEngineeringTaskCommand, EngineeringTask>
{
    public async Task<EngineeringTask> Handle(
        CreateEngineeringTaskCommand request,
        CancellationToken cancellationToken)
    {
        var references = await TaskReferences.LoadAsync(
            session,
            request.ProjectId,
            request.RepositoryId,
            request.ComponentId,
            request.FeatureId,
            cancellationToken);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.TaskCreate,
            new AuthorizationResourceContext(
                request.ProjectId,
                request.RepositoryId,
                references.Component?.Scope),
            cancellationToken);

        await TaskReferences.ValidateSourceTraceAsync(
            session,
            request.ProjectId,
            request.SourceChangeId,
            request.ArtifactIds ?? [],
            request.ImpactIds ?? [],
            cancellationToken);

        var title = ValidateTitle(request.Title);
        var now = timeProvider.GetUtcNow();
        var task = new EngineeringTask(
            Guid.NewGuid(),
            request.ProjectId,
            request.RepositoryId,
            request.ComponentId,
            references.Component?.Scope,
            request.FeatureId,
            title,
            Normalize(request.Description),
            TaskLifecycleStatus.Upcoming,
            request.Priority,
            new TaskSourceTrace(
                request.SourceChangeId,
                Distinct(request.ArtifactIds),
                [],
                Distinct(request.ImpactIds)),
            Normalize(request.AffectedArtifacts),
            Normalize(request.Inputs),
            Normalize(request.Outputs),
            Normalize(request.BusinessRules),
            Distinct(request.Dependencies),
            request.ActorId,
            TaskActorType.User,
            now,
            now,
            CurrentVersion: 1,
            AiVerificationStatus.NotRun,
            HumanApprovalStatus.Pending);

        await TaskReferences.ValidateDependenciesAsync(session, task, cancellationToken);
        var version = TaskVersionFactory.Create(
            task,
            request.ActorId,
            TaskActorType.User,
            "task created",
            timeProvider);
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "task.create",
            nameof(EngineeringTask),
            task.Id.ToString(),
            null,
            task,
            timeProvider);

        session.Store(task);
        session.Store(version);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return task;
    }

    private static string ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ProjectManagementValidationException("Task title is required.");
        }

        var normalized = title.Trim();
        if (normalized.Length > 240)
        {
            throw new ProjectManagementValidationException("Task title cannot exceed 240 characters.");
        }

        return normalized;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] Normalize(string[]? values) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static Guid[] Distinct(Guid[]? values) => (values ?? []).Distinct().ToArray();
}

public sealed class SearchEngineeringTasksHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<SearchEngineeringTasksQuery, PagedList<EngineeringTask>>
{
    public async Task<PagedList<EngineeringTask>> Handle(
        SearchEngineeringTasksQuery request,
        CancellationToken cancellationToken)
    {
        var (pageNumber, pageSize) = PageRequest.Validate(request.PageNumber, request.PageSize);
        var grants = await evaluator.GetProjectPermissionGrantsAsync(
            request.ActorId,
            request.ProjectId,
            cancellationToken);
        if (!grants.Any(grant => grant.PermissionCode == ProjectPermissionCodes.TaskView))
        {
            throw new ForbiddenException(
                $"Permission '{ProjectPermissionCodes.TaskView}' is not granted for this project.");
        }

        var query = session.Query<EngineeringTask>()
            .Where(task => task.ProjectId == request.ProjectId);
        if (request.Status is not null)
        {
            query = query.Where(task => task.Status == request.Status);
        }

        if (request.Priority is not null)
        {
            query = query.Where(task => task.Priority == request.Priority);
        }

        if (request.RepositoryId is not null)
        {
            query = query.Where(task => task.RepositoryId == request.RepositoryId);
        }

        if (request.FeatureId is not null)
        {
            query = query.Where(task => task.FeatureId == request.FeatureId);
        }

        var candidates = await query
            .OrderByDescending(task => task.UpdatedAt)
            .ToListAsync(cancellationToken);
        var assignments = await session.Query<TaskAssignment>()
            .Where(assignment => assignment.ProjectId == request.ProjectId)
            .ToListAsync(cancellationToken);
        var assignmentByTask = assignments.ToDictionary(assignment => assignment.TaskId);

        var visible = new List<EngineeringTask>();
        foreach (var task in candidates)
        {
            if (!MatchesFilters(task, request, assignmentByTask))
            {
                continue;
            }

            var effective = await evaluator.GetEffectivePermissionsAsync(
                request.ActorId,
                TaskReferences.AuthorizationContext(
                    task,
                    assignmentByTask.GetValueOrDefault(task.Id)),
                cancellationToken);
            if (effective.HasPermission(ProjectPermissionCodes.TaskView))
            {
                visible.Add(task);
            }
        }

        return new PagedList<EngineeringTask>(
            visible.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(),
            pageNumber,
            pageSize,
            visible.Count);
    }

    private static bool MatchesFilters(
        EngineeringTask task,
        SearchEngineeringTasksQuery request,
        IReadOnlyDictionary<Guid, TaskAssignment> assignmentByTask)
    {
        if (request.AssigneeId is not null &&
            (!assignmentByTask.TryGetValue(task.Id, out var assignment) ||
             assignment.AssigneeId != request.AssigneeId))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(request.Keyword) ||
            task.Title.Contains(request.Keyword.Trim(), StringComparison.OrdinalIgnoreCase) ||
            task.Description?.Contains(request.Keyword.Trim(), StringComparison.OrdinalIgnoreCase) is true;
    }
}

public sealed class GetEngineeringTaskHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<GetEngineeringTaskQuery, EngineeringTaskDetails>
{
    public async Task<EngineeringTaskDetails> Handle(
        GetEngineeringTaskQuery request,
        CancellationToken cancellationToken)
    {
        var task = await TaskReferences.LoadTaskAsync(
            session,
            request.ProjectId,
            request.TaskId,
            cancellationToken);
        var assignment = await session.Query<TaskAssignment>()
            .SingleOrDefaultAsync(item => item.TaskId == task.Id, cancellationToken);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.TaskView,
            TaskReferences.AuthorizationContext(task, assignment),
            cancellationToken);

        var reviews = await session.Query<TaskReview>()
            .Where(review => review.TaskId == task.Id)
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync(cancellationToken);
        var evidence = await session.Query<TaskEvidence>()
            .Where(item => item.TaskId == task.Id)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return new EngineeringTaskDetails(task, assignment, reviews, evidence);
    }
}

public sealed class TransitionEngineeringTaskHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<TransitionEngineeringTaskCommand, EngineeringTask>
{
    public async Task<EngineeringTask> Handle(
        TransitionEngineeringTaskCommand request,
        CancellationToken cancellationToken)
    {
        var task = await TaskReferences.LoadTaskAsync(
            session,
            request.ProjectId,
            request.TaskId,
            cancellationToken);
        var assignment = await session.Query<TaskAssignment>()
            .SingleOrDefaultAsync(item => item.TaskId == task.Id, cancellationToken);
        var permission = request.Status switch
        {
            TaskLifecycleStatus.Upcoming when task.Status == TaskLifecycleStatus.Suggested =>
                ProjectPermissionCodes.TaskCreate,
            TaskLifecycleStatus.Completed => ProjectPermissionCodes.TaskApprove,
            TaskLifecycleStatus.Rejected => ProjectPermissionCodes.TaskReject,
            _ => ProjectPermissionCodes.TaskStatusUpdate
        };
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            permission,
            TaskReferences.AuthorizationContext(task, assignment),
            cancellationToken);

        if (!TaskLifecycle.CanTransition(task.Status, request.Status))
        {
            throw new ProjectManagementValidationException(
                $"Task cannot transition from '{task.Status}' to '{request.Status}'.");
        }

        if (request.Status == TaskLifecycleStatus.Completed &&
            task.HumanApproval != HumanApprovalStatus.Approved)
        {
            throw new ProjectManagementValidationException(
                "A task requires explicit human approval before completion.");
        }

        var humanApproval = request.Status switch
        {
            TaskLifecycleStatus.ReadyForReview => HumanApprovalStatus.Pending,
            TaskLifecycleStatus.InProgress when task.Status == TaskLifecycleStatus.Rejected =>
                HumanApprovalStatus.Pending,
            _ => task.HumanApproval
        };
        var updated = task with
        {
            Status = request.Status,
            HumanApproval = humanApproval,
            UpdatedAt = timeProvider.GetUtcNow(),
            CurrentVersion = task.CurrentVersion + 1
        };
        TaskMutation.Store(
            session,
            task,
            updated,
            request.ActorId,
            TaskActorType.User,
            NormalizeReason(request.Reason, $"status changed to {request.Status}"),
            "task.status.update",
            timeProvider);
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }

    private static string NormalizeReason(string? reason, string fallback) =>
        string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
}

public sealed class AssignEngineeringTaskHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<AssignEngineeringTaskCommand, TaskAssignment>
{
    public async Task<TaskAssignment> Handle(
        AssignEngineeringTaskCommand request,
        CancellationToken cancellationToken)
    {
        var task = await TaskReferences.LoadTaskAsync(
            session,
            request.ProjectId,
            request.TaskId,
            cancellationToken);
        var existing = await session.Query<TaskAssignment>()
            .SingleOrDefaultAsync(item => item.TaskId == task.Id, cancellationToken);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.TaskAssign,
            TaskReferences.AuthorizationContext(task, existing),
            cancellationToken);

        var membership = await session.Query<ProjectMembership>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == request.ProjectId &&
                    item.UserId == request.AssigneeId &&
                    item.IsActive,
                cancellationToken);
        if (membership is null)
        {
            throw new ProjectManagementValidationException(
                "Tasks can only be assigned to active project members.");
        }

        var assignment = new TaskAssignment(
            existing?.Id ?? Guid.NewGuid(),
            request.ProjectId,
            task.Id,
            request.AssigneeId,
            request.ActorId,
            timeProvider.GetUtcNow());
        var updated = task with
        {
            UpdatedAt = timeProvider.GetUtcNow(),
            CurrentVersion = task.CurrentVersion + 1
        };
        session.Store(assignment);
        TaskMutation.Store(
            session,
            task,
            updated,
            request.ActorId,
            TaskActorType.User,
            "task assignment changed",
            "task.assign",
            timeProvider,
            beforeOverride: existing,
            afterOverride: assignment,
            assignment: assignment,
            targetType: nameof(TaskAssignment),
            targetId: assignment.Id.ToString());
        await session.SaveChangesAsync(cancellationToken);
        return assignment;
    }
}

public sealed class ReviewEngineeringTaskHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<ReviewEngineeringTaskCommand, TaskReview>
{
    public async Task<TaskReview> Handle(
        ReviewEngineeringTaskCommand request,
        CancellationToken cancellationToken)
    {
        var task = await TaskReferences.LoadTaskAsync(
            session,
            request.ProjectId,
            request.TaskId,
            cancellationToken);
        var assignment = await session.Query<TaskAssignment>()
            .SingleOrDefaultAsync(item => item.TaskId == task.Id, cancellationToken);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.TaskReview,
            TaskReferences.AuthorizationContext(task, assignment),
            cancellationToken);

        if (task.Status != TaskLifecycleStatus.ReadyForReview)
        {
            throw new ProjectManagementValidationException(
                "A task can only be reviewed while Ready For Review.");
        }

        var review = new TaskReview(
            Guid.NewGuid(),
            request.ProjectId,
            task.Id,
            request.ActorId,
            request.Decision,
            string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            timeProvider.GetUtcNow());
        var approval = request.Decision switch
        {
            TaskReviewDecision.Approve => HumanApprovalStatus.Approved,
            TaskReviewDecision.Reject => HumanApprovalStatus.Rejected,
            _ => HumanApprovalStatus.ChangesRequested
        };
        var updated = task with
        {
            HumanApproval = approval,
            UpdatedAt = timeProvider.GetUtcNow(),
            CurrentVersion = task.CurrentVersion + 1
        };
        session.Store(review);
        TaskMutation.Store(
            session,
            task,
            updated,
            request.ActorId,
            TaskActorType.User,
            $"human review: {request.Decision}",
            "task.review",
            timeProvider,
            afterOverride: review,
            review: review,
            targetType: nameof(TaskReview),
            targetId: review.Id.ToString());
        await session.SaveChangesAsync(cancellationToken);
        return review;
    }
}

public sealed class AddTaskEvidenceHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<AddTaskEvidenceCommand, TaskEvidence>
{
    public async Task<TaskEvidence> Handle(
        AddTaskEvidenceCommand request,
        CancellationToken cancellationToken)
    {
        var task = await TaskReferences.LoadTaskAsync(
            session,
            request.ProjectId,
            request.TaskId,
            cancellationToken);
        var assignment = await session.Query<TaskAssignment>()
            .SingleOrDefaultAsync(item => item.TaskId == task.Id, cancellationToken);
        if (request.ActorType == TaskActorType.User)
        {
            await evaluator.EnsureAuthorizedAsync(
                request.ActorId,
                ProjectPermissionCodes.TaskUpdate,
                TaskReferences.AuthorizationContext(task, assignment),
                cancellationToken);
        }
        else if (request.ActorType == TaskActorType.Ai)
        {
            await TaskReferences.EnsureAiPermissionAsync(
                session,
                request.ProjectId,
                ProjectPermissionCodes.AiTaskUpdate,
                cancellationToken);
        }
        else
        {
            throw new ForbiddenException("System-authored evidence requires a dedicated system policy.");
        }

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            throw new ProjectManagementValidationException("Evidence summary is required.");
        }

        if (request.Confidence is < 0 or > 1)
        {
            throw new ProjectManagementValidationException("Evidence confidence must be between 0 and 1.");
        }

        await TaskReferences.ValidateSourceTraceAsync(
            session,
            request.ProjectId,
            request.SourceChangeId,
            request.ArtifactId is null ? [] : [request.ArtifactId.Value],
            request.ImpactId is null ? [] : [request.ImpactId.Value],
            cancellationToken);
        if (task.SourceTrace.SourceChangeId is not null &&
            request.SourceChangeId is not null &&
            task.SourceTrace.SourceChangeId != request.SourceChangeId)
        {
            throw new ProjectManagementValidationException(
                "Evidence source change conflicts with the task's existing source trace.");
        }

        var evidence = new TaskEvidence(
            Guid.NewGuid(),
            request.ProjectId,
            task.Id,
            request.Kind,
            request.Summary.Trim(),
            string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            request.SourceChangeId,
            request.ArtifactId,
            request.ImpactId,
            request.Confidence,
            timeProvider.GetUtcNow(),
            request.ActorId,
            request.ActorType);
        var updatedTrace = task.SourceTrace with
        {
            SourceChangeId = task.SourceTrace.SourceChangeId ?? request.SourceChangeId,
            ArtifactIds = task.SourceTrace.ArtifactIds
                .Concat(request.ArtifactId is null ? [] : [request.ArtifactId.Value])
                .Distinct()
                .ToArray(),
            EvidenceIds = task.SourceTrace.EvidenceIds.Append(evidence.Id).Distinct().ToArray(),
            ImpactIds = task.SourceTrace.ImpactIds
                .Concat(request.ImpactId is null ? [] : [request.ImpactId.Value])
                .Distinct()
                .ToArray()
        };
        var updated = task with
        {
            SourceTrace = updatedTrace,
            UpdatedAt = timeProvider.GetUtcNow(),
            CurrentVersion = task.CurrentVersion + 1
        };
        session.Store(evidence);
        TaskMutation.Store(
            session,
            task,
            updated,
            request.ActorId,
            request.ActorType,
            "task evidence added",
            "task.evidence.add",
            timeProvider,
            afterOverride: evidence,
            evidence: evidence,
            targetType: nameof(TaskEvidence),
            targetId: evidence.Id.ToString());
        await session.SaveChangesAsync(cancellationToken);
        return evidence;
    }
}

public sealed class RecordTaskAiVerificationHandler(
    IDocumentSession session,
    TimeProvider timeProvider)
    : IRequestHandler<RecordTaskAiVerificationCommand, EngineeringTask>
{
    public async Task<EngineeringTask> Handle(
        RecordTaskAiVerificationCommand request,
        CancellationToken cancellationToken)
    {
        var task = await TaskReferences.LoadTaskAsync(
            session,
            request.ProjectId,
            request.TaskId,
            cancellationToken);
        await TaskReferences.EnsureAiPermissionAsync(
            session,
            request.ProjectId,
            ProjectPermissionCodes.AiTaskUpdate,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            throw new ProjectManagementValidationException("AI verification summary is required.");
        }

        var updated = task with
        {
            AiVerification = request.Status,
            UpdatedAt = timeProvider.GetUtcNow(),
            CurrentVersion = task.CurrentVersion + 1
        };
        TaskMutation.Store(
            session,
            task,
            updated,
            request.ActorId,
            TaskActorType.Ai,
            request.Summary.Trim(),
            "task.ai.verify",
            timeProvider);
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }
}

public sealed class GetTaskHistoryHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<GetTaskHistoryQuery, IReadOnlyList<TaskVersion>>
{
    public async Task<IReadOnlyList<TaskVersion>> Handle(
        GetTaskHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var task = await TaskReferences.LoadTaskAsync(
            session,
            request.ProjectId,
            request.TaskId,
            cancellationToken);
        var assignment = await session.Query<TaskAssignment>()
            .SingleOrDefaultAsync(item => item.TaskId == task.Id, cancellationToken);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.TaskView,
            TaskReferences.AuthorizationContext(task, assignment),
            cancellationToken);
        return await session.Query<TaskVersion>()
            .Where(version => version.TaskId == task.Id)
            .OrderBy(version => version.Version)
            .ToListAsync(cancellationToken);
    }
}

internal static class TaskMutation
{
    public static void Store(
        IDocumentSession session,
        EngineeringTask before,
        EngineeringTask after,
        Guid actorId,
        TaskActorType actorType,
        string reason,
        string action,
        TimeProvider timeProvider,
        object? beforeOverride = null,
        object? afterOverride = null,
        TaskAssignment? assignment = null,
        TaskReview? review = null,
        TaskEvidence? evidence = null,
        string? targetType = null,
        string? targetId = null)
    {
        session.Store(after);
        session.Store(TaskVersionFactory.Create(
            after,
            actorId,
            actorType,
            reason,
            timeProvider,
            assignment,
            review,
            evidence));
        session.Store(AuditRecordFactory.Create(
            after.ProjectId,
            actorId,
            actorType.ToString().ToLowerInvariant(),
            action,
            targetType ?? nameof(EngineeringTask),
            targetId ?? after.Id.ToString(),
            beforeOverride ?? before,
            afterOverride ?? after,
            timeProvider));
    }
}

internal static class TaskVersionFactory
{
    public static TaskVersion Create(
        EngineeringTask task,
        Guid actorId,
        TaskActorType actorType,
        string reason,
        TimeProvider timeProvider,
        TaskAssignment? assignment = null,
        TaskReview? review = null,
        TaskEvidence? evidence = null) =>
        new(
            Guid.NewGuid(),
            task.ProjectId,
            task.Id,
            task.CurrentVersion,
            new EngineeringTaskSnapshot(
                task.Title,
                task.Description,
                task.Status,
                task.Priority,
                task.SourceTrace,
                task.AffectedArtifacts,
                task.Inputs,
                task.Outputs,
                task.BusinessRules,
                task.Dependencies,
                task.CurrentVersion,
                task.AiVerification,
                task.HumanApproval),
            assignment,
            review,
            evidence,
            actorId,
            actorType,
            reason,
            timeProvider.GetUtcNow());
}

internal sealed record LoadedTaskReferences(
    ProjectRepository? Repository,
    ProjectComponent? Component,
    ProjectFeature? Feature);

internal static class TaskReferences
{
    public static async Task<LoadedTaskReferences> LoadAsync(
        IQuerySession session,
        Guid projectId,
        Guid? repositoryId,
        Guid? componentId,
        Guid? featureId,
        CancellationToken cancellationToken)
    {
        var repository = repositoryId is null
            ? null
            : await session.LoadAsync<ProjectRepository>(repositoryId.Value, cancellationToken);
        if (repositoryId is not null && (repository is null || repository.ProjectId != projectId))
        {
            throw new NotFoundException("Project repository not found.");
        }

        var component = componentId is null
            ? null
            : await session.LoadAsync<ProjectComponent>(componentId.Value, cancellationToken);
        if (componentId is not null &&
            (component is null || component.ProjectId != projectId || component.RepositoryId != repositoryId))
        {
            throw new NotFoundException("Project component not found for the selected repository.");
        }

        var feature = featureId is null
            ? null
            : await session.LoadAsync<ProjectFeature>(featureId.Value, cancellationToken);
        if (featureId is not null && (feature is null || feature.ProjectId != projectId))
        {
            throw new NotFoundException("Project feature not found.");
        }

        return new LoadedTaskReferences(repository, component, feature);
    }

    public static async Task<EngineeringTask> LoadTaskAsync(
        IQuerySession session,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await session.LoadAsync<EngineeringTask>(taskId, cancellationToken);
        return task is not null && task.ProjectId == projectId
            ? task
            : throw new NotFoundException("Engineering task not found.");
    }

    public static AuthorizationResourceContext AuthorizationContext(
        EngineeringTask task,
        TaskAssignment? assignment) =>
        new(
            task.ProjectId,
            task.RepositoryId,
            task.ComponentScope,
            task.CreatedBy,
            assignment is null ? [] : [assignment.AssigneeId]);

    public static async Task ValidateDependenciesAsync(
        IQuerySession session,
        EngineeringTask task,
        CancellationToken cancellationToken)
    {
        if (task.Dependencies.Contains(task.Id))
        {
            throw new ProjectManagementValidationException("A task cannot depend on itself.");
        }

        foreach (var dependencyId in task.Dependencies)
        {
            var dependency = await session.LoadAsync<EngineeringTask>(dependencyId, cancellationToken);
            if (dependency is null || dependency.ProjectId != task.ProjectId)
            {
                throw new ProjectManagementValidationException(
                    "Every task dependency must belong to the same project.");
            }
        }
    }

    public static async Task ValidateSourceTraceAsync(
        IQuerySession session,
        Guid projectId,
        Guid? sourceChangeId,
        IReadOnlyCollection<Guid> artifactIds,
        IReadOnlyCollection<Guid> impactIds,
        CancellationToken cancellationToken)
    {
        if (sourceChangeId is not null)
        {
            var change = await session.LoadAsync<SourceChange>(sourceChangeId.Value, cancellationToken);
            if (change is null || change.ProjectId != projectId)
            {
                throw new ProjectManagementValidationException(
                    "The source change must belong to the same project.");
            }
        }

        foreach (var artifactId in artifactIds)
        {
            var artifact = await session.LoadAsync<SourceArtifact>(artifactId, cancellationToken);
            if (artifact is null || artifact.ProjectId != projectId)
            {
                throw new ProjectManagementValidationException(
                    "Every source artifact must belong to the same project.");
            }
        }

        foreach (var impactId in impactIds)
        {
            var impact = await session.LoadAsync<SourceImpact>(impactId, cancellationToken);
            if (impact is null || impact.ProjectId != projectId)
            {
                throw new ProjectManagementValidationException(
                    "Every source impact must belong to the same project.");
            }
        }
    }

    public static async Task EnsureAiPermissionAsync(
        IQuerySession session,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken)
    {
        var policy = await session.LoadAsync<AiPermissionPolicy>(projectId, cancellationToken);
        if (policy is null || !policy.Allows(permission))
        {
            throw new ForbiddenException(
                $"AI policy does not grant permission '{permission}'.");
        }
    }
}
