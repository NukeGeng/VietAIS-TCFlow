namespace VietAIS.TCFlow.Modules.PlatformAdministration.Domain;

/// <summary>
/// Event-sourced platform-scoped settings imported from the v0.1 system
/// configuration document.
/// </summary>
public sealed class GlobalSystemSettings
{
    public Guid Id { get; private set; }
    public string PlatformName { get; private set; } = string.Empty;
    public string DefaultTimeZone { get; private set; } = string.Empty;
    public Uri? SupportUrl { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    public void Apply(GlobalSystemSettingsImported e)
    {
        Id = e.SettingsId;
        PlatformName = e.PlatformName;
        DefaultTimeZone = e.DefaultTimeZone;
        SupportUrl = e.SupportUrl;
        UpdatedAtUtc = e.UpdatedAtUtc;
        UpdatedBy = e.UpdatedBy;
    }
}
