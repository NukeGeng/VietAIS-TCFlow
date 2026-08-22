using System.Collections.Concurrent;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Knowledge;

namespace VietAIS.TCFlow.Analyzers.Monitoring;

public enum IncrementalMonitoringStatus
{
    Duplicate,
    Ignored,
    FastPathCompleted,
    DeepReasoningQueued
}

public sealed record IncrementalChangeSet(
    IReadOnlyList<SourceFileChange> Changes,
    IReadOnlyList<RepositoryFile> AnalysisFiles);

public sealed record DeepReasoningWorkItem(
    string Id,
    string RequestId,
    string ProjectId,
    string RepositoryId,
    string CorrelationId,
    long GraphRevision,
    IReadOnlyList<string> SourceChangeIds,
    IReadOnlyList<string> ContractMismatchIds,
    IReadOnlyList<string> RevertedSourceChangeIds,
    DateTimeOffset QueuedAt);

public sealed record IncrementalMonitoringResult(
    string RequestId,
    IncrementalMonitoringStatus Status,
    RepositoryKnowledgeGraph Graph,
    IReadOnlyList<ChangeFilterResult> Changes,
    IReadOnlyList<Impact> Impacts,
    DeepReasoningWorkItem? DeepReasoning,
    TimeSpan Elapsed,
    string Reason);

public interface IIncrementalChangeSource
{
    Task<IncrementalChangeSet> LoadAsync(
        RepositoryAnalysisWorkItem workItem,
        CancellationToken cancellationToken = default);
}

public interface IIncrementalDeliveryRegistry
{
    ValueTask<bool> TryBeginAsync(string deliveryKey, CancellationToken cancellationToken = default);

    ValueTask MarkCompletedAsync(string deliveryKey, CancellationToken cancellationToken = default);

    ValueTask MarkFailedAsync(string deliveryKey, CancellationToken cancellationToken = default);
}

public interface IDeepReasoningQueue
{
    ValueTask EnqueueAsync(DeepReasoningWorkItem workItem, CancellationToken cancellationToken = default);
}

public sealed class InMemoryIncrementalDeliveryRegistry : IIncrementalDeliveryRegistry
{
    private readonly ConcurrentDictionary<string, DeliveryState> _deliveries = new(StringComparer.Ordinal);

    public ValueTask<bool> TryBeginAsync(string deliveryKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_deliveries.TryAdd(deliveryKey, DeliveryState.Processing));
    }

    public ValueTask MarkCompletedAsync(string deliveryKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_deliveries.TryUpdate(deliveryKey, DeliveryState.Completed, DeliveryState.Processing))
        {
            throw new InvalidOperationException($"Delivery '{deliveryKey}' is not processing.");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask MarkFailedAsync(string deliveryKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _deliveries.TryRemove(new KeyValuePair<string, DeliveryState>(deliveryKey, DeliveryState.Processing));
        return ValueTask.CompletedTask;
    }

    private enum DeliveryState
    {
        Processing,
        Completed
    }
}

public sealed class InMemoryDeepReasoningQueue : IDeepReasoningQueue
{
    private readonly ConcurrentDictionary<string, DeepReasoningWorkItem> _workItems = new(StringComparer.Ordinal);

    public IReadOnlyList<DeepReasoningWorkItem> WorkItems => _workItems.Values
        .OrderBy(item => item.QueuedAt)
        .ThenBy(item => item.Id, StringComparer.Ordinal)
        .ToArray();

    public ValueTask EnqueueAsync(
        DeepReasoningWorkItem workItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        cancellationToken.ThrowIfCancellationRequested();
        _workItems.TryAdd(workItem.Id, workItem);
        return ValueTask.CompletedTask;
    }
}
