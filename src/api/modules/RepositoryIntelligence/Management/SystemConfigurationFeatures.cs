using FSH.Framework.Core.Exceptions;
using Marten;
using MediatR;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record GetGlobalAiProvidersQuery(Guid ActorId)
    : IRequest<IReadOnlyList<GlobalAiProviderConfiguration>>;

public sealed record UpdateGlobalAiProviderCommand(
    Guid ActorId,
    Guid ProviderId,
    string DisplayName,
    bool IsEnabled,
    string? Model)
    : IRequest<GlobalAiProviderConfiguration>;

public sealed record GetGlobalSystemSettingsQuery(Guid ActorId)
    : IRequest<GlobalSystemSettings>;

public sealed record UpdateGlobalSystemSettingsCommand(
    Guid ActorId,
    string PlatformName,
    string DefaultTimeZone,
    Uri? SupportUrl)
    : IRequest<GlobalSystemSettings>;

public sealed record GetPlatformPolicyQuery(Guid ActorId)
    : IRequest<PlatformPolicy>;

public sealed record UpdatePlatformPolicyCommand(
    Guid ActorId,
    bool ProjectCreationEnabled,
    bool RepositoryConnectionsEnabled,
    int MaximumRepositoriesPerProject)
    : IRequest<PlatformPolicy>;

public sealed record GetSystemUsageQuery(Guid ActorId)
    : IRequest<SystemUsageSummary>;

public sealed class GetGlobalAiProvidersHandler(
    IQuerySession session,
    ISystemPermissionEvaluator systemPermissions,
    TimeProvider timeProvider)
    : IRequestHandler<GetGlobalAiProvidersQuery, IReadOnlyList<GlobalAiProviderConfiguration>>
{
    public async Task<IReadOnlyList<GlobalAiProviderConfiguration>> Handle(
        GetGlobalAiProvidersQuery request,
        CancellationToken cancellationToken)
    {
        await systemPermissions.EnsureAuthorizedAsync(
            request.ActorId,
            SystemPermissionCodes.AiProviderManage,
            cancellationToken);
        var provider = await session.LoadAsync<GlobalAiProviderConfiguration>(
            SystemConfigurationIds.CodexAppServerProvider,
            cancellationToken);
        return [provider ?? SystemConfigurationDefaults.AiProvider(timeProvider.GetUtcNow())];
    }
}

public sealed class UpdateGlobalAiProviderHandler(
    IDocumentSession session,
    ISystemPermissionEvaluator systemPermissions,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateGlobalAiProviderCommand, GlobalAiProviderConfiguration>
{
    public async Task<GlobalAiProviderConfiguration> Handle(
        UpdateGlobalAiProviderCommand request,
        CancellationToken cancellationToken)
    {
        await systemPermissions.EnsureAuthorizedAsync(
            request.ActorId,
            SystemPermissionCodes.AiProviderManage,
            cancellationToken);
        if (request.ProviderId != SystemConfigurationIds.CodexAppServerProvider)
        {
            throw new NotFoundException("AI provider not found.");
        }

        var now = timeProvider.GetUtcNow();
        var current = await session.LoadAsync<GlobalAiProviderConfiguration>(
            request.ProviderId,
            cancellationToken) ?? SystemConfigurationDefaults.AiProvider(now);
        var updated = current with
        {
            DisplayName = SystemConfigurationValidation.Required(
                request.DisplayName,
                "AI provider display name",
                100),
            IsEnabled = request.IsEnabled,
            Model = SystemConfigurationValidation.Optional(request.Model, "AI provider model", 100),
            UpdatedAt = now,
            UpdatedBy = request.ActorId
        };
        session.Store(updated);
        session.Store(AuditRecordFactory.Create(
            projectId: null,
            request.ActorId,
            "system-admin",
            "ai-provider.update",
            nameof(GlobalAiProviderConfiguration),
            updated.Id.ToString(),
            current,
            updated,
            timeProvider));
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }
}

public sealed class GetGlobalSystemSettingsHandler(
    IQuerySession session,
    ISystemPermissionEvaluator systemPermissions,
    TimeProvider timeProvider)
    : IRequestHandler<GetGlobalSystemSettingsQuery, GlobalSystemSettings>
{
    public async Task<GlobalSystemSettings> Handle(
        GetGlobalSystemSettingsQuery request,
        CancellationToken cancellationToken)
    {
        await systemPermissions.EnsureAuthorizedAsync(
            request.ActorId,
            SystemPermissionCodes.SystemSettingsManage,
            cancellationToken);
        return await session.LoadAsync<GlobalSystemSettings>(
            SystemConfigurationIds.GlobalSettings,
            cancellationToken) ?? SystemConfigurationDefaults.Settings(timeProvider.GetUtcNow());
    }
}

public sealed class UpdateGlobalSystemSettingsHandler(
    IDocumentSession session,
    ISystemPermissionEvaluator systemPermissions,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateGlobalSystemSettingsCommand, GlobalSystemSettings>
{
    public async Task<GlobalSystemSettings> Handle(
        UpdateGlobalSystemSettingsCommand request,
        CancellationToken cancellationToken)
    {
        await systemPermissions.EnsureAuthorizedAsync(
            request.ActorId,
            SystemPermissionCodes.SystemSettingsManage,
            cancellationToken);
        var timeZone = SystemConfigurationValidation.Required(
            request.DefaultTimeZone,
            "Default time zone",
            100);
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ProjectManagementValidationException("Default time zone is not recognized.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new ProjectManagementValidationException("Default time zone is invalid.");
        }

        var now = timeProvider.GetUtcNow();
        var current = await session.LoadAsync<GlobalSystemSettings>(
            SystemConfigurationIds.GlobalSettings,
            cancellationToken) ?? SystemConfigurationDefaults.Settings(now);
        var updated = current with
        {
            PlatformName = SystemConfigurationValidation.Required(
                request.PlatformName,
                "Platform name",
                100),
            DefaultTimeZone = timeZone,
            SupportUrl = SystemConfigurationValidation.OptionalAbsoluteHttpUrl(request.SupportUrl),
            UpdatedAt = now,
            UpdatedBy = request.ActorId
        };
        session.Store(updated);
        session.Store(AuditRecordFactory.Create(
            projectId: null,
            request.ActorId,
            "system-admin",
            "system-settings.update",
            nameof(GlobalSystemSettings),
            updated.Id.ToString(),
            current,
            updated,
            timeProvider));
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }
}

public sealed class GetPlatformPolicyHandler(
    IQuerySession session,
    ISystemPermissionEvaluator systemPermissions,
    TimeProvider timeProvider)
    : IRequestHandler<GetPlatformPolicyQuery, PlatformPolicy>
{
    public async Task<PlatformPolicy> Handle(
        GetPlatformPolicyQuery request,
        CancellationToken cancellationToken)
    {
        await systemPermissions.EnsureAuthorizedAsync(
            request.ActorId,
            SystemPermissionCodes.PlatformPolicyManage,
            cancellationToken);
        return await session.LoadAsync<PlatformPolicy>(
            SystemConfigurationIds.PlatformPolicy,
            cancellationToken) ?? SystemConfigurationDefaults.Policy(timeProvider.GetUtcNow());
    }
}

public sealed class UpdatePlatformPolicyHandler(
    IDocumentSession session,
    ISystemPermissionEvaluator systemPermissions,
    TimeProvider timeProvider)
    : IRequestHandler<UpdatePlatformPolicyCommand, PlatformPolicy>
{
    public async Task<PlatformPolicy> Handle(
        UpdatePlatformPolicyCommand request,
        CancellationToken cancellationToken)
    {
        await systemPermissions.EnsureAuthorizedAsync(
            request.ActorId,
            SystemPermissionCodes.PlatformPolicyManage,
            cancellationToken);
        if (request.MaximumRepositoriesPerProject is < 1 or > 100)
        {
            throw new ProjectManagementValidationException(
                "Maximum repositories per project must be between 1 and 100.");
        }

        var now = timeProvider.GetUtcNow();
        var current = await session.LoadAsync<PlatformPolicy>(
            SystemConfigurationIds.PlatformPolicy,
            cancellationToken) ?? SystemConfigurationDefaults.Policy(now);
        var updated = current with
        {
            ProjectCreationEnabled = request.ProjectCreationEnabled,
            RepositoryConnectionsEnabled = request.RepositoryConnectionsEnabled,
            MaximumRepositoriesPerProject = request.MaximumRepositoriesPerProject,
            UpdatedAt = now,
            UpdatedBy = request.ActorId
        };
        session.Store(updated);
        session.Store(AuditRecordFactory.Create(
            projectId: null,
            request.ActorId,
            "system-admin",
            "platform-policy.update",
            nameof(PlatformPolicy),
            updated.Id.ToString(),
            current,
            updated,
            timeProvider));
        await session.SaveChangesAsync(cancellationToken);
        return updated;
    }
}

public sealed class GetSystemUsageHandler(
    IQuerySession session,
    ISystemPermissionEvaluator systemPermissions)
    : IRequestHandler<GetSystemUsageQuery, SystemUsageSummary>
{
    public async Task<SystemUsageSummary> Handle(
        GetSystemUsageQuery request,
        CancellationToken cancellationToken)
    {
        await systemPermissions.EnsureAuthorizedAsync(
            request.ActorId,
            SystemPermissionCodes.PlatformUsageView,
            cancellationToken);
        var projects = await session.Query<Project>().ToListAsync(cancellationToken);
        var states = await session.Query<ProjectState>().ToListAsync(cancellationToken);
        var repositories = await session.Query<ProjectRepository>().ToListAsync(cancellationToken);
        var tasks = await session.Query<EngineeringTask>().ToListAsync(cancellationToken);
        var audits = await session.Query<AuditRecord>().ToListAsync(cancellationToken);
        return new SystemUsageSummary(
            projects.Count,
            states.Count(state => state.Status == ProjectLifecycleStatus.Active),
            states.Count(state => state.Status == ProjectLifecycleStatus.Suspended),
            repositories.Count,
            repositories.Count(repository => repository.Status == RepositoryLifecycleStatus.Active),
            tasks.Count,
            tasks.Count(task => task.CreatedByType == TaskActorType.Ai),
            audits.Count);
    }
}

internal static class SystemConfigurationValidation
{
    public static string Required(string value, string label, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProjectManagementValidationException($"{label} is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ProjectManagementValidationException(
                $"{label} cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    public static string? Optional(string? value, string label, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ProjectManagementValidationException(
                $"{label} cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    public static Uri? OptionalAbsoluteHttpUrl(Uri? value)
    {
        if (value is null)
        {
            return null;
        }

        if (!value.IsAbsoluteUri ||
            (value.Scheme != Uri.UriSchemeHttp && value.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(value.UserInfo) ||
            value.AbsoluteUri.Length > 500)
        {
            throw new ProjectManagementValidationException(
                "Support URL must be an absolute HTTP or HTTPS URL without embedded credentials.");
        }

        return value;
    }
}
