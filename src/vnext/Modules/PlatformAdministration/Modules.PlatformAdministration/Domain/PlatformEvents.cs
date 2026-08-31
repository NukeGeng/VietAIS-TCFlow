namespace VietAIS.TCFlow.Modules.PlatformAdministration.Domain;

public sealed record PlatformPolicyCreated(Guid PolicyId, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record PlatformPolicyUpdated(Guid PolicyId, bool AllowAiAnalysis, bool AllowAiTaskSuggestions, bool AllowAiTaskMutations, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record PlatformPolicyImported(Guid PolicyId, bool ProjectCreationEnabled, bool RepositoryConnectionsEnabled, int MaximumRepositoriesPerProject, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record AiProviderConfigured(Guid PolicyId, string ProviderName, bool Enabled, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record PlatformAdminActionAudited(Guid PolicyId, string Action, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record GlobalAiProviderImported(Guid ProviderId, int Kind, string DisplayName, bool IsEnabled, string UpdatedBy, string CorrelationId, DateTimeOffset UpdatedAtUtc);
public sealed record GlobalSystemSettingsImported(Guid SettingsId, string PlatformName, string DefaultTimeZone, Uri? SupportUrl, string UpdatedBy, string CorrelationId, DateTimeOffset UpdatedAtUtc);
