using FSH.Framework.Eventing.Abstractions;

namespace VietAIS.TCFlow.Modules.Integrations.Contracts.Events;

public sealed record GitHubWebhookDelivery(
    Guid Id,
    string DeliveryId,
    string EventType,
    string PayloadSha256,
    string CorrelationId,
    DateTime OccurredOnUtc) : IIntegrationEvent
{
    public string? TenantId => null;
    public string Source => "github.webhook";
}
