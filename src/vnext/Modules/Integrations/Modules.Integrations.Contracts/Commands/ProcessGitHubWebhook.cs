namespace VietAIS.TCFlow.Modules.Integrations.Contracts.Commands;

public sealed record ProcessGitHubWebhook(string DeliveryId, string EventType, string Signature, string Body, string CorrelationId);
