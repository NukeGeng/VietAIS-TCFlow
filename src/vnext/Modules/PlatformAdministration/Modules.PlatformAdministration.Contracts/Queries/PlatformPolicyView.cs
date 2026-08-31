namespace VietAIS.TCFlow.Modules.PlatformAdministration.Contracts.Queries;

public sealed record PlatformPolicyView(
    Guid Id,
    bool AllowAiAnalysis,
    bool AllowAiTaskSuggestions,
    bool AllowAiTaskMutations,
    string? ProviderName,
    bool ProviderEnabled,
    long Version,
    DateTimeOffset LastChangedAtUtc,
    bool ProjectCreationEnabled = false,
    bool RepositoryConnectionsEnabled = false,
    int MaximumRepositoriesPerProject = 0);
