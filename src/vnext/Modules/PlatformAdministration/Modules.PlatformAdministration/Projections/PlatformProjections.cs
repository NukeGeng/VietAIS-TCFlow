using Marten.Events.Aggregation;
using VietAIS.TCFlow.Modules.PlatformAdministration.Domain;

namespace VietAIS.TCFlow.Modules.PlatformAdministration.Projections;

public static class PlatformProjectionNames
{
    public const string Current = "platform-policy-current";
    public const string AiProviderCurrent = "global-ai-provider-current";
    public const string GlobalSettingsCurrent = "global-system-settings-current";
}

public sealed class PlatformPolicyCurrent
{
    public Guid Id { get; set; }
    public bool AllowAiAnalysis { get; set; }
    public bool AllowAiTaskSuggestions { get; set; }
    public bool AllowAiTaskMutations { get; set; }
    public bool ProjectCreationEnabled { get; set; }
    public bool RepositoryConnectionsEnabled { get; set; }
    public int MaximumRepositoriesPerProject { get; set; }
    public string? ProviderName { get; set; }
    public bool ProviderEnabled { get; set; }
    public long Version { get; set; }
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class PlatformPolicyCurrentProjection : SingleStreamProjection<PlatformPolicyCurrent, Guid>
{
    public PlatformPolicyCurrentProjection() => Name = PlatformProjectionNames.Current;
    public static PlatformPolicyCurrent Create(PlatformPolicyCreated e) => new()
    {
        Id = e.PolicyId,
        ProjectCreationEnabled = true,
        RepositoryConnectionsEnabled = true,
        MaximumRepositoriesPerProject = 20,
        Version = 1,
        LastChangedAtUtc = e.OccurredAtUtc
    };
    public static void Apply(PlatformPolicyUpdated e, PlatformPolicyCurrent x) { x.AllowAiAnalysis = e.AllowAiAnalysis; x.AllowAiTaskSuggestions = e.AllowAiTaskSuggestions; x.AllowAiTaskMutations = e.AllowAiTaskMutations; Set(x, e.OccurredAtUtc); }
    public static void Apply(PlatformPolicyImported e, PlatformPolicyCurrent x) { x.ProjectCreationEnabled = e.ProjectCreationEnabled; x.RepositoryConnectionsEnabled = e.RepositoryConnectionsEnabled; x.MaximumRepositoriesPerProject = e.MaximumRepositoriesPerProject; Set(x, e.OccurredAtUtc); }
    public static void Apply(AiProviderConfigured e, PlatformPolicyCurrent x) { x.ProviderName = e.ProviderName; x.ProviderEnabled = e.Enabled; Set(x, e.OccurredAtUtc); }
    public static void Apply(PlatformAdminActionAudited e, PlatformPolicyCurrent x) => Set(x, e.OccurredAtUtc);
    private static void Set(PlatformPolicyCurrent x, DateTimeOffset at) { x.Version++; x.LastChangedAtUtc = at; }
}

public sealed class GlobalAiProviderCurrent
{
    public Guid Id { get; set; }
    public int Kind { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class GlobalAiProviderCurrentProjection : SingleStreamProjection<GlobalAiProviderCurrent, Guid>
{
    public GlobalAiProviderCurrentProjection() => Name = PlatformProjectionNames.AiProviderCurrent;
    public static GlobalAiProviderCurrent Create(GlobalAiProviderImported e) => new()
    {
        Id = e.ProviderId,
        Kind = e.Kind,
        DisplayName = e.DisplayName,
        IsEnabled = e.IsEnabled,
        UpdatedAtUtc = e.UpdatedAtUtc,
        UpdatedBy = e.UpdatedBy
    };
}

public sealed class GlobalSystemSettingsCurrent
{
    public Guid Id { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public string DefaultTimeZone { get; set; } = string.Empty;
    public Uri? SupportUrl { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class GlobalSystemSettingsCurrentProjection : SingleStreamProjection<GlobalSystemSettingsCurrent, Guid>
{
    public GlobalSystemSettingsCurrentProjection() => Name = PlatformProjectionNames.GlobalSettingsCurrent;
    public static GlobalSystemSettingsCurrent Create(GlobalSystemSettingsImported e) => new()
    {
        Id = e.SettingsId,
        PlatformName = e.PlatformName,
        DefaultTimeZone = e.DefaultTimeZone,
        SupportUrl = e.SupportUrl,
        UpdatedAtUtc = e.UpdatedAtUtc,
        UpdatedBy = e.UpdatedBy
    };
}
