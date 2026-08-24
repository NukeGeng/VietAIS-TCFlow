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

public sealed record CreateProjectComponentCommand(
    Guid ActorId,
    Guid ProjectId,
    Guid RepositoryId,
    string Name,
    ComponentScopeKind Scope,
    string? RootPath)
    : IRequest<ProjectComponent>;

public sealed record CreateProjectFeatureCommand(
    Guid ActorId,
    Guid ProjectId,
    string Name,
    string? Description)
    : IRequest<ProjectFeature>;

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

    private static void ValidateLocation(
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

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
