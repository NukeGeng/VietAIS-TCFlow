using System.Text.Json;
using Marten;
using VietAIS.TCFlow.Modules.Integrations.Configuration;

namespace VietAIS.TCFlow.Tools.Migration;

/// <summary>
/// Migrates the Integrations operational-document slice without turning
/// credentials or delivery receipts into business events. Only whitelisted,
/// non-secret metadata is retained.
/// </summary>
internal static class MartenOperationalMigrationApplier
{
    private const string CredentialKind = "GitHubCredential";
    private const string DeliveryKind = "GitHubDelivery";

    private static readonly string[] SensitivePropertyFragments =
    [
        "ACCESS_TOKEN",
        "ACCESSTOKEN",
        "REFRESH_TOKEN",
        "REFRESHTOKEN",
        "PRIVATE_KEY",
        "PRIVATEKEY",
        "CLIENT_SECRET",
        "CLIENTSECRET",
        "WEBHOOK_SECRET",
        "WEBHOOKSECRET",
        "PASSWORD",
        "SIGNATURE",
        "TOKEN",
        "SECRET",
        "CREDENTIAL"
    ];

    private static readonly Dictionary<string, string[]> AllowedMetadata =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [CredentialKind] =
            [
                "installationId",
                "accountId",
                "accountLogin",
                "accountKind",
                "repositorySelection",
                "status",
                "connectedAtUtc",
                "updatedAtUtc"
            ],
            [DeliveryKind] =
            [
                "deliveryId",
                "event",
                "action",
                "payloadSha256",
                "receivedAtUtc",
                "installationId",
                "githubRepositoryId",
                "projectRepositoryId"
            ]
        };

    public static async Task<MigrationOperationalApplyReport> ApplyAsync(
        MigrationPlan plan,
        LegacyExport export,
        string connectionString,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(export);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var operations = plan.Operations
            .Where(operation => operation.Disposition == MigrationDisposition.OperationalDocument)
            .ToArray();
        EnsureSupported(operations);
        var records = export.Records
            .GroupBy(
                record => Goal2MigrationPlanner.BuildSourceReference(record.Kind, record.SourceId),
                StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        // Map and validate every append candidate before opening a write session.
        // This guarantees secret-bearing exports fail closed before any
        // operational document is stored.
        var mapped = operations
            .Where(operation => operation.Action == MigrationAction.Append)
            .Select(operation => Map(operation, FindRecord(operation, records)))
            .ToArray();

        await using var store = DocumentStore.For(options =>
        {
            options.Connection(connectionString);
            IntegrationsMartenConfiguration.Configure(options);
        });
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync().ConfigureAwait(false);

        await using var query = store.QuerySession();
        var existing = new Dictionary<Guid, GitHubOperationalMigrationDocument?>();
        foreach (var operation in operations.Select(operation => operation.TargetId).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            existing[operation] = await query.LoadAsync<GitHubOperationalMigrationDocument>(
                operation,
                cancellationToken).ConfigureAwait(false);
        }

        var pending = new Dictionary<Guid, GitHubOperationalMigrationDocument>();
        var skipped = 0;
        foreach (var operation in operations)
        {
            if (existing[operation.TargetId] is { } document)
            {
                EnsureExistingMatches(operation, document);
                skipped++;
                continue;
            }

            if (operation.Action == MigrationAction.Skip)
            {
                if (string.Equals(operation.SkipReason, "already-applied", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"The migration ledger marks '{operation.SourceReference}' as applied, but the operational document is missing.");
                }

                // duplicate-in-export is satisfied by the first append candidate.
                skipped++;
                continue;
            }

            var documentToStore = mapped.Single(item => item.Id == operation.TargetId);
            if (!pending.TryAdd(operation.TargetId, documentToStore))
            {
                EnsureExistingMatches(operation, pending[operation.TargetId]);
                skipped++;
            }
        }

        if (pending.Count == 0)
        {
            return new MigrationOperationalApplyReport(
                operations.Length,
                0,
                skipped,
                []);
        }

        await using var session = store.LightweightSession();
        foreach (var document in pending.Values)
        {
            session.Store(document);
        }

        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new MigrationOperationalApplyReport(
            operations.Length,
            pending.Count,
            skipped,
            []);
    }

    private static void EnsureSupported(IReadOnlyList<MigrationOperation> operations)
    {
        var unsupported = operations
            .Where(operation => operation.Kind is not (CredentialKind or DeliveryKind))
            .Select(operation => $"{operation.Kind}:{operation.SourceId}")
            .ToArray();
        if (unsupported.Length > 0)
        {
            throw new InvalidOperationException(
                $"Integrations operational migration does not support: {string.Join(", ", unsupported)}.");
        }
    }

    private static GitHubOperationalMigrationDocument Map(
        MigrationOperation operation,
        LegacyRecord record)
    {
        if (!string.Equals(record.Kind, operation.Kind, StringComparison.Ordinal) ||
            !string.Equals(record.SourceId, operation.SourceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Migration record '{operation.SourceReference}' does not match its planned operational operation.");
        }

        if (!string.Equals(record.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Migration record '{operation.SourceReference}' payload hash does not match its plan.");
        }

        EnsureNoSensitiveProperties(record.Payload);
        var metadata = ReadAllowedMetadata(record.Payload, operation.Kind);
        var externalId = operation.Kind switch
        {
            CredentialKind => RequiredExternalIdentifier(
                metadata,
                operation,
                "installationId",
                "accountId",
                "accountLogin"),
            DeliveryKind => RequiredMetadata(metadata, operation, "deliveryId"),
            _ => throw new InvalidOperationException($"No operational mapper exists for '{operation.Kind}'.")
        };

        var projectId = operation.ProjectSourceId is null
            ? (Guid?)null
            : Goal2MigrationPlanner.CreateDeterministicId("Project", operation.ProjectSourceId);
        return new GitHubOperationalMigrationDocument
        {
            Id = operation.TargetId,
            SourceReference = operation.SourceReference,
            Kind = operation.Kind,
            PayloadHash = operation.PayloadHash,
            ProjectId = projectId,
            ExternalId = externalId,
            Metadata = metadata,
            ImportedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static LegacyRecord FindRecord(
        MigrationOperation operation,
        Dictionary<string, LegacyRecord> records)
    {
        if (!records.TryGetValue(operation.SourceReference, out var record))
        {
            throw new InvalidOperationException(
                $"Migration plan operation '{operation.SourceReference}' has no matching input record.");
        }

        return record;
    }

    private static Dictionary<string, string> ReadAllowedMetadata(JsonElement payload, string kind)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Migration payload for '{kind}' must be a JSON object.");
        }

        var allowed = AllowedMetadata[kind];
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in payload.EnumerateObject())
        {
            var name = allowed.FirstOrDefault(
                candidate => string.Equals(candidate, property.Name, StringComparison.OrdinalIgnoreCase));
            if (name is null)
            {
                continue;
            }

            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (value.Length > 512)
            {
                throw new InvalidOperationException(
                    $"Migration metadata property '{property.Name}' is too long.");
            }

            result[name] = value.Trim();
        }

        return result;
    }

    private static string RequiredExternalIdentifier(
        Dictionary<string, string> metadata,
        MigrationOperation operation,
        params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (metadata.TryGetValue(candidate, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"Migration payload '{operation.SourceReference}' must contain one of: {string.Join(", ", candidates)}.");
    }

    private static string RequiredMetadata(
        Dictionary<string, string> metadata,
        MigrationOperation operation,
        string name) =>
        metadata.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(
                $"Migration payload '{operation.SourceReference}' is missing '{name}'.");

    private static void EnsureNoSensitiveProperties(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var normalized = property.Name.Replace("-", string.Empty, StringComparison.Ordinal)
                        .Replace("_", string.Empty, StringComparison.Ordinal)
                        .ToUpperInvariant();
                    if (SensitivePropertyFragments.Any(fragment =>
                            normalized.Contains(
                                fragment.Replace("_", string.Empty, StringComparison.Ordinal),
                                StringComparison.Ordinal)))
                    {
                        throw new InvalidOperationException(
                            $"Migration payload contains forbidden sensitive property '{property.Name}'.");
                    }

                    EnsureNoSensitiveProperties(property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    EnsureNoSensitiveProperties(item);
                }

                break;
        }
    }

    private static void EnsureExistingMatches(
        MigrationOperation operation,
        GitHubOperationalMigrationDocument document)
    {
        if (!string.Equals(document.SourceReference, operation.SourceReference, StringComparison.Ordinal) ||
            !string.Equals(document.Kind, operation.Kind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Operational document identity conflict for '{operation.SourceReference}'.");
        }

        if (!string.Equals(document.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Operational document '{operation.SourceReference}' has payload hash '{document.PayloadHash}', " +
                $"but the input has '{operation.PayloadHash}'.");
        }
    }
}
