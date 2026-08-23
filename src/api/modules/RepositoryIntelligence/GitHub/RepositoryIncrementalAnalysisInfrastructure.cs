using JasperFx;
using Marten;
using Microsoft.Extensions.Options;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Monitoring;
using VietAIS.TCFlow.Analyzers.Reasoning;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

internal enum IncrementalDeliveryStatus
{
    Processing,
    Completed
}

internal sealed record IncrementalAnalysisDelivery(
    string Id,
    IncrementalDeliveryStatus Status,
    DateTimeOffset UpdatedAt);

internal enum RepositoryReasoningJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

internal sealed record RepositoryReasoningJob(
    string Id,
    DeepReasoningWorkItem WorkItem,
    RepositoryReasoningJobStatus Status,
    int Attempt,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    int AiRequestCount,
    int ReconciledTaskCount,
    string? ErrorCode,
    string? ErrorMessage);

internal sealed record RepositoryTaskProjection(
    string Id,
    Guid ProjectId,
    Guid RepositoryId,
    Guid EngineeringTaskId,
    int SourceVersion,
    SourceAwareTaskStatus SourceStatus,
    DateTimeOffset UpdatedAt);

internal sealed class NoAnalyzableRepositoryChangesException(string message)
    : InvalidOperationException(message);

internal sealed class MartenIncrementalDeliveryRegistry(
    IDocumentSession session,
    TimeProvider timeProvider,
    IOptions<RepositoryAnalysisWorkerOptions> options) : IIncrementalDeliveryRegistry
{
    private readonly TimeSpan _processingLease = options.Value.ProcessingLease;

    public async ValueTask<bool> TryBeginAsync(
        string deliveryKey,
        CancellationToken cancellationToken = default)
    {
        var existing = await session.LoadAsync<IncrementalAnalysisDelivery>(
            deliveryKey,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (existing?.Status == IncrementalDeliveryStatus.Completed ||
            existing?.UpdatedAt >= now - _processingLease)
        {
            return false;
        }

        var processing = new IncrementalAnalysisDelivery(
            deliveryKey,
            IncrementalDeliveryStatus.Processing,
            now);
        if (existing is null)
        {
            session.Insert(processing);
        }
        else
        {
            session.Store(processing);
        }
        try
        {
            await session.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DocumentAlreadyExistsException)
        {
            return false;
        }
    }

    public async ValueTask MarkCompletedAsync(
        string deliveryKey,
        CancellationToken cancellationToken = default)
    {
        var current = await session.LoadAsync<IncrementalAnalysisDelivery>(
            deliveryKey,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Incremental delivery '{deliveryKey}' is not processing.");
        if (current.Status != IncrementalDeliveryStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Incremental delivery '{deliveryKey}' is already complete.");
        }

        session.Store(current with
        {
            Status = IncrementalDeliveryStatus.Completed,
            UpdatedAt = timeProvider.GetUtcNow()
        });
    }

    public async ValueTask MarkFailedAsync(
        string deliveryKey,
        CancellationToken cancellationToken = default)
    {
        session.Delete<IncrementalAnalysisDelivery>(deliveryKey);
        await session.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class MartenDeepReasoningQueue(IDocumentSession session) : IDeepReasoningQueue
{
    public ValueTask EnqueueAsync(
        DeepReasoningWorkItem workItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        cancellationToken.ThrowIfCancellationRequested();
        session.Insert(new RepositoryReasoningJob(
            workItem.Id,
            workItem,
            RepositoryReasoningJobStatus.Pending,
            Attempt: 0,
            workItem.QueuedAt,
            workItem.QueuedAt,
            CompletedAt: null,
            AiRequestCount: 0,
            ReconciledTaskCount: 0,
            ErrorCode: null,
            ErrorMessage: null));
        return ValueTask.CompletedTask;
    }
}
