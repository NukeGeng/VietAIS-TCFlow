namespace VietAIS.TCFlow.Modules.PlatformAdministration.Contracts.Commands;

public sealed record ConfigureAiProvider(Guid PolicyId, long ExpectedVersion, string ProviderName, bool Enabled, string ActorId, string CorrelationId, string? CausationId = null);
