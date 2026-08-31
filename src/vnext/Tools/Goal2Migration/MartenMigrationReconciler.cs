using JasperFx.Events;
using Marten;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Configuration;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.Modules.AccessControl.Configuration;
using VietAIS.TCFlow.Modules.Architecture.Configuration;
using VietAIS.TCFlow.Modules.EventStorming.Configuration;
using VietAIS.TCFlow.Modules.Integrations.Configuration;
using VietAIS.TCFlow.Modules.Planning.Configuration;
using VietAIS.TCFlow.Modules.PlatformAdministration.Configuration;
using VietAIS.TCFlow.Modules.Projects.Configuration;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Configuration;
using VietAIS.TCFlow.Modules.TaskFlow.Configuration;

namespace VietAIS.TCFlow.Tools.Migration;

/// <summary>
/// Reconciles a migration export against immutable migration markers already
/// present in Marten. This is deliberately read-only: schema provisioning and
/// business writes belong to the apply command, not the pre/post check.
/// </summary>
internal static class MartenMigrationReconciler
{
    public static async Task<MigrationReconciliationReport> ReconcileAsync(
        MigrationPlan plan,
        string connectionString,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var eventOperations = plan.Operations
            .Where(operation => operation.Disposition == MigrationDisposition.EventStream)
            .ToArray();
        var operationalOperations = plan.Operations
            .Where(operation => operation.Disposition == MigrationDisposition.OperationalDocument)
            .ToArray();
        var expected = eventOperations
            .GroupBy(operation => operation.SourceReference, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        await using var store = DocumentStore.For(options =>
        {
            options.Connection(connectionString);
            TcFlowEventStoreConfiguration.Configure(options);
            AccessControlMartenConfiguration.Configure(options);
            PlanningMartenConfiguration.Configure(options);
            TaskFlowMartenConfiguration.Configure(options);
            RepositoryMartenConfiguration.Configure(options);
            StormingMartenConfiguration.Configure(options);
            ArchitectureMartenConfiguration.Configure(options);
            ProjectsMartenConfiguration.Configure(options);
            PlatformMartenConfiguration.Configure(options);
            IntegrationsMartenConfiguration.Configure(options);
        });

        var streams = new Dictionary<Guid, IReadOnlyList<IEvent>>();
        await using var query = store.QuerySession();
        foreach (var streamId in expected.Select(operation => operation.TargetId).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            streams[streamId] = await query.Events.FetchStreamAsync(
                streamId,
                long.MaxValue,
                timestamp: null,
                fromVersion: 0,
                token: cancellationToken).ConfigureAwait(false);
        }

        var missing = new List<string>();
        var mismatches = new List<string>();
        var duplicateReferences = new List<string>();
        var found = 0;
        var missingStreams = 0;

        foreach (var operation in expected)
        {
            if (!streams.TryGetValue(operation.TargetId, out var stream) || stream.Count == 0)
            {
                missingStreams++;
                missing.Add(operation.SourceReference);
                continue;
            }

            var markers = stream
                .Where(@event => HasSourceReference(@event, operation.SourceReference))
                .ToArray();
            if (markers.Length == 0)
            {
                missing.Add(operation.SourceReference);
                continue;
            }

            found++;
            if (markers.Length > 1)
            {
                duplicateReferences.Add(operation.SourceReference);
            }

            foreach (var marker in markers)
            {
                var hash = Header(marker, EventMetadataHeaders.MigrationPayloadHash);
                if (!string.Equals(hash, operation.PayloadHash, StringComparison.Ordinal))
                {
                    mismatches.Add(
                        $"{operation.SourceReference}: expected '{operation.PayloadHash}', found '{hash ?? "<missing>"}'.");
                }
            }
        }

        var missingOperational = new List<string>();
        var foundOperational = 0;
        foreach (var operation in operationalOperations
                     .GroupBy(item => item.SourceReference, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = await query.LoadAsync<GitHubOperationalMigrationDocument>(
                operation.TargetId,
                cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                missingOperational.Add(operation.SourceReference);
                continue;
            }

            if (!string.Equals(document.SourceReference, operation.SourceReference, StringComparison.Ordinal) ||
                !string.Equals(document.Kind, operation.Kind, StringComparison.Ordinal))
            {
                mismatches.Add(
                    $"{operation.SourceReference}: operational document identity does not match the plan.");
                continue;
            }

            foundOperational++;
            if (!string.Equals(document.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
            {
                mismatches.Add(
                    $"{operation.SourceReference}: expected '{operation.PayloadHash}', found operational document hash '{document.PayloadHash}'.");
            }
        }

        var issues = new List<string>();
        if (missing.Count > 0)
        {
            issues.Add($"Missing migration markers: {string.Join(", ", missing)}.");
        }

        if (missingOperational.Count > 0)
        {
            issues.Add(
                $"Missing operational migration documents: {string.Join(", ", missingOperational)}.");
        }

        if (mismatches.Count > 0)
        {
            issues.Add($"Migration payload hash mismatches: {string.Join("; ", mismatches)}");
        }

        if (duplicateReferences.Count > 0)
        {
            issues.Add($"Duplicate migration markers: {string.Join(", ", duplicateReferences)}.");
        }

        return new MigrationReconciliationReport(
            plan.Operations.Count,
            eventOperations.Length,
            operationalOperations.Length,
            expected.Length,
            found,
            operationalOperations
                .GroupBy(item => item.SourceReference, StringComparer.Ordinal)
                .Count(),
            foundOperational,
            duplicateReferences.Count,
            missingStreams,
            missing,
            missingOperational,
            mismatches,
            duplicateReferences,
            issues,
            issues.Count == 0);
    }

    private static bool HasSourceReference(IEvent @event, string sourceReference) =>
        @event.Headers is not null &&
        @event.Headers.TryGetValue(EventMetadataHeaders.MigrationSourceReference, out var value) &&
        string.Equals(
            Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            sourceReference,
            StringComparison.Ordinal);

    private static string? Header(IEvent @event, string key) =>
        @event.Headers is not null && @event.Headers.TryGetValue(key, out var value)
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            : null;
}
