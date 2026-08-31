using Marten;
using VietAIS.TCFlow.Modules.Integrations.Webhooks;

namespace VietAIS.TCFlow.Modules.Integrations.Configuration;

public static class IntegrationsMartenConfiguration
{
    public static void Configure(StoreOptions options)
    {
        options.Schema.For<WebhookReceipt>().DatabaseSchemaName("integrations");
        options.Schema.For<GitHubOperationalMigrationDocument>().DatabaseSchemaName("integrations");
    }
}

/// <summary>
/// Redacted operational metadata retained during the v0.1 → GOAL2 migration.
/// It is deliberately not a credential store and never contains raw payloads
/// or secret material.
/// </summary>
public sealed class GitHubOperationalMigrationDocument
{
    public Guid Id { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
    public DateTimeOffset ImportedAtUtc { get; set; }
}
