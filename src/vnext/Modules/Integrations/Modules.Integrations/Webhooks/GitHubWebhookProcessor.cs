using System.Security.Cryptography;
using System.Text;
using FSH.Framework.Eventing.Abstractions;
using Marten;
using Microsoft.Extensions.Options;
using VietAIS.TCFlow.Modules.Integrations.Configuration;
using VietAIS.TCFlow.Modules.Integrations.Contracts.Events;

namespace VietAIS.TCFlow.Modules.Integrations.Webhooks;

public sealed class WebhookReceipt
{
    public string Id { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string PayloadSha256 { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public bool Published { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
}

public sealed record WebhookProcessingResult(bool Accepted, bool Duplicate, bool InvalidSignature, string DeliveryId);

public sealed class GitHubWebhookProcessor
{
    private readonly IDocumentSession _session;
    private readonly IEventBus _bus;
    private readonly GitHubWebhookOptions _options;
    private readonly TimeProvider _time;

    public GitHubWebhookProcessor(IDocumentSession session, IEventBus bus, IOptions<GitHubWebhookOptions> options, TimeProvider time)
    {
        _session = session;
        _bus = bus;
        _options = options.Value;
        _time = time;
    }

    public async Task<WebhookProcessingResult> ProcessAsync(string deliveryId, string eventType, string signature, string body, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deliveryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        if (string.IsNullOrWhiteSpace(_options.Secret)) return new(false, false, true, deliveryId);
        if (!VerifySignature(body, signature, _options.Secret)) return new(false, false, true, deliveryId);

        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        var receipt = await _session.LoadAsync<WebhookReceipt>(deliveryId, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            if (!receipt.Published) await PublishAsync(receipt, cancellationToken).ConfigureAwait(false);
            return new(true, true, false, deliveryId);
        }

        receipt = new WebhookReceipt { Id = deliveryId, EventType = eventType.Trim(), PayloadSha256 = payloadHash, CorrelationId = correlationId.Trim(), ReceivedAtUtc = _time.GetUtcNow() };
        _session.Store(receipt);
        await _session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await PublishAsync(receipt, cancellationToken).ConfigureAwait(false);
        return new(true, false, false, deliveryId);
    }

    private async Task PublishAsync(WebhookReceipt receipt, CancellationToken cancellationToken)
    {
        await _bus.PublishAsync(new GitHubWebhookDelivery(StableGuid(receipt.Id), receipt.Id, receipt.EventType, receipt.PayloadSha256, receipt.CorrelationId, receipt.ReceivedAtUtc.UtcDateTime), cancellationToken).ConfigureAwait(false);
        receipt.Published = true;
        await _session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static bool VerifySignature(string body, string signature, string secret)
    {
        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var provided = Convert.FromHexString(signature[7..]);
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
            return CryptographicOperations.FixedTimeEquals(provided, expected);
        }
        catch (FormatException) { return false; }
    }

    private static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
