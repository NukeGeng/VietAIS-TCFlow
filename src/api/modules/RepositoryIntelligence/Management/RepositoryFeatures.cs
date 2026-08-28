using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Paging;
using Marten;
using MediatR;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record CreateProjectRepositoryCommand(
    Guid ActorId,
    Guid ProjectId,
    string Name,
    RepositoryProviderKind Provider,
    string? LocalPath,
    string? RemoteUrl,
    string DefaultBranch)
    : IRequest<ProjectRepository>;

public sealed record SearchProjectRepositoriesQuery(
    Guid ActorId,
    Guid ProjectId,
    int PageNumber,
    int PageSize,
    string? Keyword,
    RepositoryLifecycleStatus? Status)
    : IRequest<PagedList<ProjectRepository>>;

public sealed record UpdateProjectRepositoryCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid RepositoryId,
    string Name,
    string? LocalPath,
    string? RemoteUrl,
    string DefaultBranch,
    RepositoryLifecycleStatus Status)
    : IRequest<ProjectRepository>;

public sealed record DisableProjectRepositoryCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid RepositoryId)
    : IRequest<ProjectRepository>;

public sealed record CreateProjectComponentCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid RepositoryId,
    string Name,
    ComponentScopeKind Scope,
    string? RootPath)
    : IRequest<ProjectComponent>;

public sealed record UpdateProjectComponentCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid ComponentId,
    string Name,
    ComponentScopeKind Scope,
    string? RootPath)
    : IRequest<ProjectComponent>;

public sealed record DeleteProjectComponentCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid ComponentId)
    : IRequest;

public sealed record SearchProjectComponentsQuery(
    Guid ActorId,
    Guid ProjectId,
    int PageNumber,
    int PageSize,
    string? Keyword,
    Guid? RepositoryId,
    ComponentScopeKind? Scope)
    : IRequest<PagedList<ProjectComponent>>;

public sealed record CreateProjectFeatureCommand(
    Guid ActorId,
    Guid ProjectId,
    string Name,
    string? Description)
    : IRequest<ProjectFeature>;

public sealed record UpdateProjectFeatureCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid FeatureId,
    string Name,
    string? Description)
    : IRequest<ProjectFeature>;

public sealed record DeleteProjectFeatureCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid FeatureId)
    : IRequest;

public sealed record SearchProjectFeaturesQuery(
    Guid ActorId,
    Guid ProjectId,
    int PageNumber,
    int PageSize,
    string? Keyword)
    : IRequest<PagedList<ProjectFeature>>;

public sealed class CreateProjectRepositoryHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<CreateProjectRepositoryCommand, ProjectRepository>
{
    public async Task<ProjectRepository> Handle(
        CreateProjectRepositoryCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RepositoryCreate,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        var platformPolicy = await session.LoadAsync<PlatformPolicy>(
            SystemConfigurationIds.PlatformPolicy,
            cancellationToken) ?? SystemConfigurationDefaults.Policy(timeProvider.GetUtcNow());
        if (!platformPolicy.RepositoryConnectionsEnabled)
        {
            throw new ProjectManagementValidationException(
                "Repository connections are disabled by the platform policy.");
        }

        var repositoryCount = await session.Query<ProjectRepository>()
            .CountAsync(
                repository => repository.ProjectId == request.ProjectId &&
                    repository.Status != RepositoryLifecycleStatus.Disabled,
                cancellationToken);
        if (repositoryCount >= platformPolicy.MaximumRepositoriesPerProject)
        {
            throw new ProjectManagementValidationException(
                $"The platform policy allows at most {platformPolicy.MaximumRepositoriesPerProject} repositories per project.");
        }

        var name = ValidateName(request.Name, "Repository");
        var branch = ValidateName(request.DefaultBranch, "Default branch");
        ValidateLocation(request.Provider, request.LocalPath, request.RemoteUrl);

        var duplicate = await session.Query<ProjectRepository>()
            .AnyAsync(
                repository => repository.ProjectId == request.ProjectId && repository.Name == name,
                cancellationToken);
        if (duplicate)
        {
            throw new ProjectManagementValidationException(
                "A repository with the same name already exists in this project.");
        }

        var repository = new ProjectRepository(
            Guid.NewGuid(),
            request.ProjectId,
            name,
            request.Provider,
            Normalize(request.LocalPath),
            Normalize(request.RemoteUrl),
            branch,
            RepositoryLifecycleStatus.Pending,
            timeProvider.GetUtcNow(),
            request.ActorId);
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "repository.create",
            nameof(ProjectRepository),
            repository.Id.ToString(),
            null,
            repository,
            timeProvider);

        session.Store(repository);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return repository;
    }

    internal static void ValidateLocation(
        RepositoryProviderKind provider,
        string? localPath,
        string? remoteUrl)
    {
        if (provider == RepositoryProviderKind.Local && string.IsNullOrWhiteSpace(localPath))
        {
            throw new ProjectManagementValidationException("Local repositories require a local path.");
        }

        if (provider == RepositoryProviderKind.GitHub)
        {
            if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new ProjectManagementValidationException(
                    "GitHub repositories require an HTTPS remote URL without embedded credentials.");
            }
        }
    }

    internal static string ValidateName(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProjectManagementValidationException($"{label} is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > 150)
        {
            throw new ProjectManagementValidationException($"{label} cannot exceed 150 characters.");
        }

        return normalized;
    }

    internal static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class UpdateProjectRepositoryHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateProjectRepositoryCommand, ProjectRepository>
{
    public async Task<ProjectRepository> Handle(
        UpdateProjectRepositoryCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RepositoryUpdate,
            new AuthorizationResourceContext(request.ProjectId, request.RepositoryId),
            cancellationToken);
        var current = await FindRepository(session, request.ProjectId, request.RepositoryId, cancellationToken);
        if (!Enum.IsDefined(request.Status))
        {
            throw new ProjectManagementValidationException("Repository status is invalid.");
        }

        if (request.Status == RepositoryLifecycleStatus.Disabled &&
            current.Status != RepositoryLifecycleStatus.Disabled)
        {
            throw new ProjectManagementValidationException(
                "Use the repository delete operation to disable a repository.");
        }

        if (current.Status == RepositoryLifecycleStatus.Disabled &&
            request.Status != RepositoryLifecycleStatus.Disabled)
        {
            throw new ProjectManagementValidationException(
                "A disabled repository cannot be reactivated through the update operation.");
        }

        var name = CreateProjectRepositoryHandler.ValidateName(request.Name, "Repository");
        var branch = CreateProjectRepositoryHandler.ValidateName(request.DefaultBranch, "Default branch");
        CreateProjectRepositoryHandler.ValidateLocation(
            current.Provider,
            request.LocalPath,
            request.RemoteUrl);
        var duplicate = await session.Query<ProjectRepository>().AnyAsync(
            repository => repository.ProjectId == request.ProjectId &&
                repository.Id != request.RepositoryId &&
                repository.Name == name,
            cancellationToken);
        if (duplicate)
        {
            throw new ProjectManagementValidationException(
                "A repository with the same name already exists in this project.");
        }

        var updated = current with
        {
            Name = name,
            LocalPath = CreateProjectRepositoryHandler.Normalize(request.LocalPath),
            RemoteUrl = CreateProjectRepositoryHandler.Normalize(request.RemoteUrl),
            DefaultBranch = branch,
            Status = request.Status
        };
        StoreMutation(session, request.ActorId, request.ProjectId, "repository.update", current, updated, timeProvider);
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }

    internal static async Task<ProjectRepository> FindRepository(
        IQuerySession session,
        Guid projectId,
        Guid repositoryId,
        CancellationToken cancellationToken)
    {
        var repository = await session.LoadAsync<ProjectRepository>(repositoryId, cancellationToken);
        return repository is not null && repository.ProjectId == projectId
            ? repository
            : throw new NotFoundException("Project repository not found.");
    }

    internal static void StoreMutation<T>(
        IDocumentSession session,
        Guid actorId,
        Guid projectId,
        string action,
        T current,
        T updated,
        TimeProvider timeProvider)
        where T : class
    {
        switch (updated)
        {
            case ProjectRepository repository:
                session.Store(repository);
                break;
            case ProjectComponent component:
                session.Store(component);
                break;
            case ProjectFeature feature:
                session.Store(feature);
                break;
            default:
                throw new InvalidOperationException("Unsupported resource mutation type.");
        }

        session.Store(AuditRecordFactory.Create(
            projectId,
            actorId,
            "user",
            action,
            typeof(T).Name,
            GetId(updated),
            current,
            updated,
            timeProvider));
    }

    private static string GetId<T>(T value) =>
        value switch
        {
            ProjectRepository repository => repository.Id.ToString(),
            ProjectComponent component => component.Id.ToString(),
            ProjectFeature feature => feature.Id.ToString(),
            _ => throw new InvalidOperationException("Unsupported resource mutation type.")
        };
}

public sealed class DisableProjectRepositoryHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<DisableProjectRepositoryCommand, ProjectRepository>
{
    public async Task<ProjectRepository> Handle(
        DisableProjectRepositoryCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.RepositoryDelete,
            new AuthorizationResourceContext(request.ProjectId, request.RepositoryId),
            cancellationToken);
        var current = await UpdateProjectRepositoryHandler.FindRepository(
            session,
            request.ProjectId,
            request.RepositoryId,
            cancellationToken);
        if (current.Status == RepositoryLifecycleStatus.Disabled)
        {
            return current;
        }

        var updated = current with { Status = RepositoryLifecycleStatus.Disabled };
        UpdateProjectRepositoryHandler.StoreMutation(
            session,
            request.ActorId,
            request.ProjectId,
            "repository.disable",
            current,
            updated,
            timeProvider);
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }
}

public sealed class SearchProjectRepositoriesHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<SearchProjectRepositoriesQuery, PagedList<ProjectRepository>>
{
    public async Task<PagedList<ProjectRepository>> Handle(
        SearchProjectRepositoriesQuery request,
        CancellationToken cancellationToken)
    {
        var (pageNumber, pageSize) = PageRequest.Validate(request.PageNumber, request.PageSize);
        var grants = await evaluator.GetProjectPermissionGrantsAsync(
            request.ActorId,
            request.ProjectId,
            cancellationToken);
        if (!grants.Any(grant => grant.PermissionCode == ProjectPermissionCodes.RepositoryView))
        {
            throw new ForbiddenException(
                $"Permission '{ProjectPermissionCodes.RepositoryView}' is not granted for this project.");
        }

        var query = session.Query<ProjectRepository>()
            .Where(repository => repository.ProjectId == request.ProjectId);
        if (request.Status is not null)
        {
            query = query.Where(repository => repository.Status == request.Status);
        }

        var candidates = await query
            .OrderBy(repository => repository.Name)
            .ToListAsync(cancellationToken);

        var visible = new List<ProjectRepository>();
        foreach (var repository in candidates)
        {
            if (!string.IsNullOrWhiteSpace(request.Keyword) &&
                !repository.Name.Contains(request.Keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var effective = await evaluator.GetEffectivePermissionsAsync(
                request.ActorId,
                new AuthorizationResourceContext(request.ProjectId, repository.Id),
                cancellationToken);
            if (effective.HasPermission(ProjectPermissionCodes.RepositoryView))
            {
                visible.Add(repository);
            }
        }

        return new PagedList<ProjectRepository>(
            visible.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(),
            pageNumber,
            pageSize,
            visible.Count);
    }
}

public sealed class CreateProjectComponentHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<CreateProjectComponentCommand, ProjectComponent>
{
    public async Task<ProjectComponent> Handle(
        CreateProjectComponentCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.ComponentCreate,
            new AuthorizationResourceContext(request.ProjectId, request.RepositoryId, request.Scope),
            cancellationToken);

        var repository = await session.LoadAsync<ProjectRepository>(request.RepositoryId, cancellationToken);
        if (repository is null || repository.ProjectId != request.ProjectId)
        {
            throw new NotFoundException("Project repository not found.");
        }

        var component = new ProjectComponent(
            Guid.NewGuid(),
            request.ProjectId,
            request.RepositoryId,
            CreateProjectRepositoryHandler.ValidateName(request.Name, "Component name"),
            request.Scope,
            string.IsNullOrWhiteSpace(request.RootPath) ? null : request.RootPath.Trim(),
            timeProvider.GetUtcNow(),
            request.ActorId);
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "component.create",
            nameof(ProjectComponent),
            component.Id.ToString(),
            null,
            component,
            timeProvider);

        session.Store(component);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return component;
    }
}

public sealed class SearchProjectComponentsHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<SearchProjectComponentsQuery, PagedList<ProjectComponent>>
{
    public async Task<PagedList<ProjectComponent>> Handle(
        SearchProjectComponentsQuery request,
        CancellationToken cancellationToken)
    {
        var (pageNumber, pageSize) = PageRequest.Validate(request.PageNumber, request.PageSize);
        var grants = await evaluator.GetProjectPermissionGrantsAsync(
            request.ActorId,
            request.ProjectId,
            cancellationToken);
        if (!grants.Any(grant => grant.PermissionCode == ProjectPermissionCodes.ComponentView))
        {
            throw new ForbiddenException(
                $"Permission '{ProjectPermissionCodes.ComponentView}' is not granted for this project.");
        }

        var query = session.Query<ProjectComponent>()
            .Where(component => component.ProjectId == request.ProjectId);
        if (request.RepositoryId is not null)
        {
            query = query.Where(component => component.RepositoryId == request.RepositoryId);
        }

        if (request.Scope is not null)
        {
            query = query.Where(component => component.Scope == request.Scope);
        }

        var candidates = await query.OrderBy(component => component.Name).ToListAsync(cancellationToken);
        var visible = new List<ProjectComponent>();
        foreach (var component in candidates)
        {
            if (!string.IsNullOrWhiteSpace(request.Keyword) &&
                !component.Name.Contains(request.Keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var effective = await evaluator.GetEffectivePermissionsAsync(
                request.ActorId,
                new AuthorizationResourceContext(
                    request.ProjectId,
                    component.RepositoryId,
                    component.Scope),
                cancellationToken);
            if (effective.HasPermission(ProjectPermissionCodes.ComponentView))
            {
                visible.Add(component);
            }
        }

        return new PagedList<ProjectComponent>(
            visible.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(),
            pageNumber,
            pageSize,
            visible.Count);
    }
}

public sealed class CreateProjectFeatureHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<CreateProjectFeatureCommand, ProjectFeature>
{
    public async Task<ProjectFeature> Handle(
        CreateProjectFeatureCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.FeatureCreate,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        var feature = new ProjectFeature(
            Guid.NewGuid(),
            request.ProjectId,
            CreateProjectRepositoryHandler.ValidateName(request.Name, "Feature name"),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            timeProvider.GetUtcNow(),
            request.ActorId);
        var audit = AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "feature.create",
            nameof(ProjectFeature),
            feature.Id.ToString(),
            null,
            feature,
            timeProvider);

        session.Store(feature);
        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);
        return feature;
    }
}

public sealed class UpdateProjectComponentHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateProjectComponentCommand, ProjectComponent>
{
    public async Task<ProjectComponent> Handle(
        UpdateProjectComponentCommand request,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionExistsAsync(
            evaluator,
            request.ActorId,
            request.ProjectId,
            ProjectPermissionCodes.ComponentUpdate,
            cancellationToken);
        var current = await FindComponent(session, request.ProjectId, request.ComponentId, cancellationToken);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.ComponentUpdate,
            new AuthorizationResourceContext(request.ProjectId, current.RepositoryId, current.Scope),
            cancellationToken);
        if (current.Scope != request.Scope)
        {
            await evaluator.EnsureAuthorizedAsync(
                request.ActorId,
                ProjectPermissionCodes.ComponentUpdate,
                new AuthorizationResourceContext(request.ProjectId, current.RepositoryId, request.Scope),
                cancellationToken);
        }

        var updated = current with
        {
            Name = CreateProjectRepositoryHandler.ValidateName(request.Name, "Component name"),
            Scope = request.Scope,
            RootPath = CreateProjectRepositoryHandler.Normalize(request.RootPath)
        };
        UpdateProjectRepositoryHandler.StoreMutation(
            session,
            request.ActorId,
            request.ProjectId,
            "component.update",
            current,
            updated,
            timeProvider);
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }

    internal static async Task<ProjectComponent> FindComponent(
        IQuerySession session,
        Guid projectId,
        Guid componentId,
        CancellationToken cancellationToken)
    {
        var component = await session.LoadAsync<ProjectComponent>(componentId, cancellationToken);
        return component is not null && component.ProjectId == projectId
            ? component
            : throw new NotFoundException("Project component not found.");
    }

    internal static async Task EnsurePermissionExistsAsync(
        IProjectPermissionEvaluator evaluator,
        Guid actorId,
        Guid projectId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        var grants = await evaluator.GetProjectPermissionGrantsAsync(actorId, projectId, cancellationToken);
        if (!grants.Any(grant => grant.PermissionCode == permissionCode))
        {
            throw new ForbiddenException(
                $"Permission '{permissionCode}' is not granted for the requested project scope.");
        }
    }
}

public sealed class DeleteProjectComponentHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<DeleteProjectComponentCommand>
{
    public async Task Handle(DeleteProjectComponentCommand request, CancellationToken cancellationToken)
    {
        await UpdateProjectComponentHandler.EnsurePermissionExistsAsync(
            evaluator,
            request.ActorId,
            request.ProjectId,
            ProjectPermissionCodes.ComponentDelete,
            cancellationToken);
        var component = await UpdateProjectComponentHandler.FindComponent(
            session,
            request.ProjectId,
            request.ComponentId,
            cancellationToken);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.ComponentDelete,
            new AuthorizationResourceContext(request.ProjectId, component.RepositoryId, component.Scope),
            cancellationToken);
        var referencedByTask = await session.Query<EngineeringTask>().AnyAsync(
            task => task.ProjectId == request.ProjectId && task.ComponentId == request.ComponentId,
            cancellationToken);
        var referencedByArtifact = await session.Query<SourceArtifact>().AnyAsync(
            artifact => artifact.ProjectId == request.ProjectId &&
                artifact.ComponentId == request.ComponentId,
            cancellationToken);
        if (referencedByTask || referencedByArtifact)
        {
            throw new ProjectManagementValidationException(
                "A component referenced by engineering tasks or source artifacts cannot be deleted.");
        }

        session.Delete<ProjectComponent>(request.ComponentId);
        session.Store(AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "component.delete",
            nameof(ProjectComponent),
            request.ComponentId.ToString(),
            component,
            null,
            timeProvider));
        await session.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UpdateProjectFeatureHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateProjectFeatureCommand, ProjectFeature>
{
    public async Task<ProjectFeature> Handle(
        UpdateProjectFeatureCommand request,
        CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.FeatureUpdate,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);
        var current = await FindFeature(session, request.ProjectId, request.FeatureId, cancellationToken);
        var updated = current with
        {
            Name = CreateProjectRepositoryHandler.ValidateName(request.Name, "Feature name"),
            Description = CreateProjectRepositoryHandler.Normalize(request.Description)
        };
        UpdateProjectRepositoryHandler.StoreMutation(
            session,
            request.ActorId,
            request.ProjectId,
            "feature.update",
            current,
            updated,
            timeProvider);
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }

    internal static async Task<ProjectFeature> FindFeature(
        IQuerySession session,
        Guid projectId,
        Guid featureId,
        CancellationToken cancellationToken)
    {
        var feature = await session.LoadAsync<ProjectFeature>(featureId, cancellationToken);
        return feature is not null && feature.ProjectId == projectId
            ? feature
            : throw new NotFoundException("Project feature not found.");
    }
}

public sealed class DeleteProjectFeatureHandler(
    IDocumentSession session,
    IProjectPermissionEvaluator evaluator,
    TimeProvider timeProvider)
    : IRequestHandler<DeleteProjectFeatureCommand>
{
    public async Task Handle(DeleteProjectFeatureCommand request, CancellationToken cancellationToken)
    {
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.FeatureDelete,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);
        var feature = await UpdateProjectFeatureHandler.FindFeature(
            session,
            request.ProjectId,
            request.FeatureId,
            cancellationToken);
        if (await session.Query<EngineeringTask>().AnyAsync(
            task => task.ProjectId == request.ProjectId && task.FeatureId == request.FeatureId,
            cancellationToken))
        {
            throw new ProjectManagementValidationException(
                "A feature referenced by engineering tasks cannot be deleted.");
        }

        session.Delete<ProjectFeature>(request.FeatureId);
        session.Store(AuditRecordFactory.Create(
            request.ProjectId,
            request.ActorId,
            "user",
            "feature.delete",
            nameof(ProjectFeature),
            request.FeatureId.ToString(),
            feature,
            null,
            timeProvider));
        await session.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SearchProjectFeaturesHandler(
    IQuerySession session,
    IProjectPermissionEvaluator evaluator)
    : IRequestHandler<SearchProjectFeaturesQuery, PagedList<ProjectFeature>>
{
    public async Task<PagedList<ProjectFeature>> Handle(
        SearchProjectFeaturesQuery request,
        CancellationToken cancellationToken)
    {
        var (pageNumber, pageSize) = PageRequest.Validate(request.PageNumber, request.PageSize);
        await evaluator.EnsureAuthorizedAsync(
            request.ActorId,
            ProjectPermissionCodes.FeatureView,
            new AuthorizationResourceContext(request.ProjectId),
            cancellationToken);

        var features = await session.Query<ProjectFeature>()
            .Where(feature => feature.ProjectId == request.ProjectId)
            .OrderBy(feature => feature.Name)
            .ToListAsync(cancellationToken);
        var filtered = string.IsNullOrWhiteSpace(request.Keyword)
            ? features
            : features.Where(feature =>
                feature.Name.Contains(request.Keyword.Trim(), StringComparison.OrdinalIgnoreCase) ||
                (feature.Description?.Contains(request.Keyword.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

        return new PagedList<ProjectFeature>(
            filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(),
            pageNumber,
            pageSize,
            filtered.Count);
    }
}
