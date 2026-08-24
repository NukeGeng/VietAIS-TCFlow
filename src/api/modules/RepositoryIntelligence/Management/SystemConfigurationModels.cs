namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public enum GlobalAiProviderKind
{
    CodexAppServer = 0
}

public static class SystemConfigurationIds
{
    public static readonly Guid CodexAppServerProvider =
        Guid.Parse("1d93ad55-f5f9-4c6a-a723-ff02f9c6eae1");

    public static readonly Guid GlobalSettings =
        Guid.Parse("1d93ad55-f5f9-4c6a-a723-ff02f9c6eae2");

    public static readonly Guid PlatformPolicy =
        Guid.Parse("1d93ad55-f5f9-4c6a-a723-ff02f9c6eae3");
}

public sealed record GlobalAiProviderConfiguration(
    Guid Id,
    GlobalAiProviderKind Kind,
    string DisplayName,
    bool IsEnabled,
    string? Model,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record GlobalSystemSettings(
    Guid Id,
    string PlatformName,
    string DefaultTimeZone,
    Uri? SupportUrl,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record PlatformPolicy(
    Guid Id,
    bool ProjectCreationEnabled,
    bool RepositoryConnectionsEnabled,
    int MaximumRepositoriesPerProject,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record SystemUsageSummary(
    int Projects,
    int ActiveProjects,
    int SuspendedProjects,
    int Repositories,
    int ActiveRepositories,
    int Tasks,
    int AiGeneratedTasks,
    int AuditRecords);

internal static class SystemConfigurationDefaults
{
    public static GlobalAiProviderConfiguration AiProvider(
        DateTimeOffset now,
        Guid updatedBy = default) =>
        new(
            SystemConfigurationIds.CodexAppServerProvider,
            GlobalAiProviderKind.CodexAppServer,
            "Codex App Server",
            IsEnabled: true,
            Model: null,
            now,
            updatedBy);

    public static GlobalSystemSettings Settings(
        DateTimeOffset now,
        Guid updatedBy = default) =>
        new(
            SystemConfigurationIds.GlobalSettings,
            "VietAIS TCFlow",
            "UTC",
            SupportUrl: null,
            now,
            updatedBy);

    public static PlatformPolicy Policy(
        DateTimeOffset now,
        Guid updatedBy = default) =>
        new(
            SystemConfigurationIds.PlatformPolicy,
            ProjectCreationEnabled: true,
            RepositoryConnectionsEnabled: true,
            MaximumRepositoriesPerProject: 20,
            now,
            updatedBy);
}
