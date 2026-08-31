namespace VietAIS.TCFlow.Modules.PlatformAdministration.Contracts.Queries;

public sealed record GlobalAiProviderView(
    Guid Id,
    int Kind,
    string DisplayName,
    bool IsEnabled,
    DateTimeOffset UpdatedAtUtc,
    string UpdatedBy);
