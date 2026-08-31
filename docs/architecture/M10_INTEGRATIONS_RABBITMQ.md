# M10 Integrations and RabbitMQ boundary

Status: `CONFIRMED` for the normalized GitHub webhook/delivery boundary.

The webhook endpoint verifies `X-Hub-Signature-256` with a configured secret,
deduplicates `X-GitHub-Delivery` receipts in Marten, and publishes only a
sanitized `GitHubWebhookDelivery` integration contract containing event type
and payload hash. Raw payloads, signatures, tokens, and private keys never
enter events, logs, or projections. The existing eventing composition selects
InMemory locally or RabbitMQ when `EventingOptions:Provider=RabbitMQ`, with
persistent delivery metadata and retry behavior delegated to that adapter.

This boundary intentionally keeps GitHub provider mechanics outside
RepositoryIntelligence; downstream consumers receive normalized contracts.
