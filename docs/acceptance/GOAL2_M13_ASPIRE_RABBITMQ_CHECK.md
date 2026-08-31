# GOAL2 M13 Aspire RabbitMQ composition check

Status: `INFERRED`. The AppHost composition is compiled and reviewed, but this
artifact does not claim a completed Aspire runtime or production deployment.

## Composition

- `FSH.Starter.AppHost` provisions a persistent `rabbitmq:4-management-alpine`
  resource with AMQP and management endpoints.
- Username and password are Aspire parameters; they are not committed to
  application configuration or written to diagnostics.
- The vNext API waits for RabbitMQ and receives the `EventingOptions:RabbitMQ`
  host, port, credentials, and exchange name through environment references.
- Marten remains configured with `AddAsyncDaemon(DaemonMode.HotCold)`; RabbitMQ
  is reserved for integration events and is not the async projection transport.

## Verification

```text
dotnet build Host/FSH.Starter.AppHost/FSH.Starter.AppHost.csproj \
  --configuration Release --no-restore -v:minimal
```

Result: build succeeded with `0 warnings, 0 errors` on .NET 10.

The self-host canary profile also passes both default and `goal2` Compose
configuration validation, Nginx syntax validation, and a local `.NET 10`
`Dockerfile.vnext` image build. These are packaging checks; they do not replace
the required isolated startup and broker-failure transcript.

## Remaining runtime evidence

Run Aspire in an isolated environment and capture redacted evidence for
RabbitMQ health, exchange declaration, publish/retry behavior, broker outage,
and vNext API startup before upgrading this status to `CONFIRMED`.
