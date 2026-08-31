namespace VietAIS.TCFlow.Modules.PlatformAdministration.Contracts.Queries;

public sealed record GlobalSystemSettingsView(
    Guid Id,
    string PlatformName,
    string DefaultTimeZone,
    Uri? SupportUrl,
    DateTimeOffset UpdatedAtUtc,
    string UpdatedBy);
