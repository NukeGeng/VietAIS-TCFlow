namespace VietAIS.TCFlow.Modules.PlatformAdministration.Domain;

/// <summary>
/// Event-sourced platform-scoped AI provider configuration. Provider
/// configuration is system state, not a project aggregate and must not be
/// copied into a project stream during migration.
/// </summary>
public sealed class GlobalAiProvider
{
    public Guid Id { get; private set; }
    public int Kind { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    public void Apply(GlobalAiProviderImported e)
    {
        Id = e.ProviderId;
        Kind = e.Kind;
        DisplayName = e.DisplayName;
        IsEnabled = e.IsEnabled;
        UpdatedAtUtc = e.UpdatedAtUtc;
        UpdatedBy = e.UpdatedBy;
    }
}
