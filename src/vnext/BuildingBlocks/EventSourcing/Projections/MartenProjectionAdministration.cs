using JasperFx.Events.Projections;
using Marten;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Projections;

public sealed class MartenProjectionAdministration(
    IDocumentStore store,
    IOptions<ProjectionAdministrationOptions> options,
    ILogger<MartenProjectionAdministration> logger) : IProjectionAdministration
{
    private static readonly Action<ILogger, string, Exception?> LogRebuildStarting =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(LogRebuildStarting)),
            "Starting rebuild for projection {ProjectionName}");

    private static readonly Action<ILogger, string, Exception?> LogRebuildCompleted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(LogRebuildCompleted)),
            "Completed rebuild for projection {ProjectionName}");

    private readonly IDocumentStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ProjectionAdministrationOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<MartenProjectionAdministration> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<IReadOnlyList<ProjectionStatus>> GetStatusAsync(CancellationToken cancellationToken)
    {
        var rows = await _store.Advanced
            .AllProjectionProgress(tenantId: null, cancellationToken)
            .ConfigureAwait(false);
        var statistics = await _store.Advanced
            .FetchEventStoreStatistics(tenantId: null, cancellationToken)
            .ConfigureAwait(false);

        var highWaterMark = rows
            .Where(row => string.Equals(
                row.ShardName,
                ShardState.HighWaterMark,
                StringComparison.Ordinal))
            .Select(row => row.Sequence)
            .DefaultIfEmpty(statistics.EventSequenceNumber)
            .Max();

        return rows
            .Where(row => !string.Equals(
                row.ShardName,
                ShardState.HighWaterMark,
                StringComparison.Ordinal))
            .Select(row => new ProjectionStatus(
                row.ShardName,
                row.TenantId,
                row.Sequence,
                highWaterMark,
                Math.Max(0, highWaterMark - row.Sequence),
                row.AgentStatus,
                row.LastHeartbeat))
            .OrderBy(status => status.ProjectionName, StringComparer.Ordinal)
            .ThenBy(status => status.TenantId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task RebuildAsync(string projectionName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        var normalizedName = projectionName.Trim();

        if (!_options.AllowedProjectionNames.Contains(normalizedName))
        {
            throw new InvalidOperationException(
                $"Projection '{normalizedName}' is not approved for administrative rebuilds.");
        }

        LogRebuildStarting(_logger, normalizedName, null);
        using var daemon = await _store.BuildProjectionDaemonAsync().ConfigureAwait(false);
        await daemon
            .RebuildProjectionAsync(normalizedName, _options.RebuildTimeout, cancellationToken)
            .ConfigureAwait(false);
        LogRebuildCompleted(_logger, normalizedName, null);
    }
}
