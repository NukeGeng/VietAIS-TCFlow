namespace VietAIS.TCFlow.Modules.PlatformAdministration.Domain;

public sealed record PlatformPolicyCreated(Guid PolicyId, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record PlatformPolicyUpdated(Guid PolicyId, bool AllowAiAnalysis, bool AllowAiTaskSuggestions, bool AllowAiTaskMutations, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record AiProviderConfigured(Guid PolicyId, string ProviderName, bool Enabled, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record PlatformAdminActionAudited(Guid PolicyId, string Action, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
