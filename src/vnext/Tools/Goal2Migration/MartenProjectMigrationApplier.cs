using JasperFx.Events;
using Marten;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Configuration;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.Modules.AccessControl.Configuration;
using VietAIS.TCFlow.Modules.AccessControl.Domain;
using VietAIS.TCFlow.Modules.Planning.Configuration;
using VietAIS.TCFlow.Modules.Planning.Domain;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Configuration;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Domain;
using VietAIS.TCFlow.Modules.TaskFlow.Configuration;
using VietAIS.TCFlow.Modules.TaskFlow.Domain;
using VietAIS.TCFlow.Modules.Projects.Configuration;
using VietAIS.TCFlow.Modules.Projects.Domain;

namespace VietAIS.TCFlow.Tools.Migration;

/// <summary>
/// Applies the model-level GOAL2 migration slices that have typed mappers. The
/// writer fails closed for every bounded-context row without an explicit event
/// mapping instead of serializing it into an untyped event stream.
/// </summary>
internal static class MartenProjectMigrationApplier
{
    private const string ProjectKind = "Project";
    private const string ProjectStateKind = "ProjectState";
    private const string ProjectRoleKind = "ProjectRole";
    private const string ProjectMembershipKind = "ProjectMembership";
    private const string PlanKind = "Plan";
    private const string RequirementKind = "Requirement";
    private const string MilestoneKind = "Milestone";
    private const string EngineeringTaskKind = "EngineeringTask";
    private const string TaskVersionKind = "TaskVersion";
    private const string TaskEvidenceKind = "TaskEvidence";
    private const string AnalysisRunKind = "AnalysisRun";
    private const string SourceArtifactKind = "SourceArtifact";
    private const string SourceImpactKind = "SourceImpact";

    public static async Task<MigrationBusinessApplyReport> ApplyAsync(
        MigrationPlan plan,
        LegacyExport export,
        string connectionString,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(export);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var operations = plan.Operations
            .Where(operation => operation.Disposition == MigrationDisposition.EventStream)
            .ToArray();
        var unsupported = operations
            .Where(operation => operation.Kind is not (
                ProjectKind or ProjectStateKind or ProjectRoleKind or ProjectMembershipKind or
                PlanKind or RequirementKind or MilestoneKind or EngineeringTaskKind or
                TaskVersionKind or TaskEvidenceKind or AnalysisRunKind or SourceArtifactKind or
                SourceImpactKind))
            .Select(operation => $"{operation.Kind}:{operation.SourceId}")
            .ToArray();
        if (unsupported.Length > 0)
        {
            throw new InvalidOperationException(
                "Marten apply currently supports Projects, AccessControl, Planning, TaskFlow, and RepositoryIntelligence records. " +
                $"Create a bounded-context mapper before applying: {string.Join(", ", unsupported)}.");
        }

        var records = export.Records.ToDictionary(
            record => Goal2MigrationPlanner.BuildSourceReference(record.Kind, record.SourceId),
            StringComparer.Ordinal);
        var projectRecords = export.Records
            .Where(record => string.Equals(record.Kind, ProjectKind, StringComparison.Ordinal))
            .ToDictionary(
                record => Goal2MigrationPlanner.BuildSourceReference(record.Kind, record.SourceId),
                StringComparer.Ordinal);
        var mapped = operations
            .Where(operation => operation.Action == MigrationAction.Append)
            .Select(operation => Map(operation, FindRecord(operation, records), projectRecords))
            .ToArray();
        var ledgerApplied = operations
            .Where(operation => operation.Action == MigrationAction.Skip &&
                                string.Equals(operation.SkipReason, "already-applied", StringComparison.Ordinal))
            .ToArray();

        await using var store = DocumentStore.For(options =>
        {
            options.Connection(connectionString);
            TcFlowEventStoreConfiguration.Configure(options);
            AccessControlMartenConfiguration.Configure(options);
            PlanningMartenConfiguration.Configure(options);
            TaskFlowMartenConfiguration.Configure(options);
            RepositoryMartenConfiguration.Configure(options);
            ProjectsMartenConfiguration.Configure(options);
        });
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync().ConfigureAwait(false);

        var streamEvents = await ReadStreamsAsync(
            store,
            operations.Select(operation => operation.TargetId).Distinct().ToArray(),
            cancellationToken).ConfigureAwait(false);
        ValidateLedgerAppliedMarkers(ledgerApplied, streamEvents);

        var effective = new List<MappedEvent>(mapped.Length);
        foreach (var item in mapped)
        {
            if (TryFindMarker(streamEvents, item.Operation, out var existingHash))
            {
                if (!string.Equals(existingHash, item.Operation.PayloadHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Marten event marker '{item.Operation.SourceReference}' has payload hash '{existingHash}', " +
                        $"but the input has '{item.Operation.PayloadHash}'.");
                }

                continue;
            }

            if (streamEvents.TryGetValue(item.Operation.TargetId, out var existing) && existing.Count > 0 &&
                item.Events.Any(@event => @event is ProjectCreated or ProjectAccessInitialized or PlanCreated or TaskProposed or AnalysisStarted))
            {
                throw new InvalidOperationException(
                    $"Aggregate stream '{item.Operation.TargetId}' already exists without a migration marker for " +
                    $"'{item.Operation.SourceReference}'. Refusing to append a second aggregate initializer.");
            }

            effective.Add(item);
        }

        // Validate stream shape before opening a write session so a malformed
        // export cannot partially mutate a database.
        ValidateStreamInputs(effective, streamEvents);

        if (effective.Count == 0)
        {
            return new MigrationBusinessApplyReport(
                operations.Length,
                0,
                operations.Length,
                streamEvents.Count(pair => pair.Value.Count > 0),
                []);
        }

        await using var session = store.LightweightSession();
        var expectedVersions = streamEvents.ToDictionary(pair => pair.Key, pair => pair.Value.Count);
        foreach (var group in effective.GroupBy(item => item.Operation.TargetId))
        {
            var items = group.ToArray();
            var ordered = items
                .SelectMany(item => item.Events.Select(@event => new MappedEventData(item, @event)))
                .OrderBy(item => EventOrder(item.Event))
                .ToArray();
            var initializer = ordered.FirstOrDefault(item => item.Event is ProjectCreated or ProjectAccessInitialized or PlanCreated or TaskProposed or AnalysisStarted);
            if (initializer is not null)
            {
                ApplyMetadata(session, initializer.Item.Operation);
                var actionEvents = ordered.Select(item => item.Event).ToArray();
                if (initializer.Event is ProjectCreated)
                {
                    var action = session.Events.StartStream<ProjectAggregate>(
                        initializer.Item.Operation.TargetId,
                        actionEvents);
                    SetActionEventMetadata(action.Events, ordered);
                }
                else if (initializer.Event is PlanCreated)
                {
                    var action = session.Events.StartStream<PlanAggregate>(
                        initializer.Item.Operation.TargetId,
                        actionEvents);
                    SetActionEventMetadata(action.Events, ordered);
                }
                else if (initializer.Event is TaskProposed)
                {
                    var action = session.Events.StartStream<EngineeringTask>(
                        initializer.Item.Operation.TargetId,
                        actionEvents);
                    SetActionEventMetadata(action.Events, ordered);
                }
                else if (initializer.Event is AnalysisStarted)
                {
                    var action = session.Events.StartStream<AnalysisRun>(
                        initializer.Item.Operation.TargetId,
                        actionEvents);
                    SetActionEventMetadata(action.Events, ordered);
                }
                else
                {
                    var action = session.Events.StartStream<ProjectAccessAggregate>(
                        initializer.Item.Operation.TargetId,
                        actionEvents);
                    SetActionEventMetadata(action.Events, ordered);
                }

                expectedVersions[initializer.Item.Operation.TargetId] = actionEvents.Length;
                continue;
            }

            if (ordered.Any(item => item.Event is ProjectRoleCreated or ProjectRolePermissionsUpdated or ProjectMemberAdded or ProjectMemberRolesAssigned or ProjectMemberRemoved))
            {
                var first = ordered[0];
                ApplyMetadata(session, first.Item.Operation);
                if (!expectedVersions.TryGetValue(first.Item.Operation.TargetId, out var expectedVersion) ||
                    expectedVersion == 0)
                {
                    throw new InvalidOperationException(
                        $"AccessControl operation '{first.Item.Operation.SourceReference}' has no existing stream and no " +
                        "ProjectAccessInitialized operation in this batch.");
                }

                var stream = await session.Events.FetchForWriting<ProjectAccessAggregate>(
                    first.Item.Operation.TargetId,
                    expectedVersion,
                    cancellationToken).ConfigureAwait(false);
                if (stream.Aggregate is null)
                {
                    throw new InvalidOperationException(
                        $"AccessControl stream '{first.Item.Operation.TargetId}' could not be reconstructed for migration.");
                }

                foreach (var item in ordered)
                {
                    stream.AppendOne(item.Event);
                    SetEventMetadata(stream.Events[^1], item.Item.Operation);
                    expectedVersions[item.Item.Operation.TargetId]++;
                }
                continue;
            }

            if (ordered.Any(item => item.Event is RequirementAdded or MilestoneAdded))
            {
                var first = ordered[0];
                ApplyMetadata(session, first.Item.Operation);
                if (!expectedVersions.TryGetValue(first.Item.Operation.TargetId, out var expectedVersion) ||
                    expectedVersion == 0)
                {
                    throw new InvalidOperationException(
                        $"Planning operation '{first.Item.Operation.SourceReference}' has no existing Plan stream.");
                }

                var stream = await session.Events.FetchForWriting<PlanAggregate>(
                    first.Item.Operation.TargetId,
                    expectedVersion,
                    cancellationToken).ConfigureAwait(false);
                if (stream.Aggregate is null)
                {
                    throw new InvalidOperationException(
                        $"Plan stream '{first.Item.Operation.TargetId}' could not be reconstructed for migration.");
                }

                foreach (var item in ordered)
                {
                    stream.AppendOne(item.Event);
                    SetEventMetadata(stream.Events[^1], item.Item.Operation);
                    expectedVersions[item.Item.Operation.TargetId]++;
                }

                continue;
            }

            if (ordered.Any(item => item.Event is TaskLifecycleReconciled or TaskVersionImported or TaskEvidenceImported))
            {
                var first = ordered[0];
                ApplyMetadata(session, first.Item.Operation);
                if (!expectedVersions.TryGetValue(first.Item.Operation.TargetId, out var expectedVersion) ||
                    expectedVersion == 0)
                {
                    throw new InvalidOperationException(
                        $"TaskFlow operation '{first.Item.Operation.SourceReference}' has no existing task stream.");
                }

                var stream = await session.Events.FetchForWriting<EngineeringTask>(
                    first.Item.Operation.TargetId,
                    expectedVersion,
                    cancellationToken).ConfigureAwait(false);
                if (stream.Aggregate is null)
                {
                    throw new InvalidOperationException(
                        $"Task stream '{first.Item.Operation.TargetId}' could not be reconstructed for migration.");
                }

                foreach (var item in ordered)
                {
                    stream.AppendOne(item.Event);
                    SetEventMetadata(stream.Events[^1], item.Item.Operation);
                    expectedVersions[item.Item.Operation.TargetId]++;
                }

                continue;
            }

            if (ordered.Any(item => item.Event is ArtifactObserved or ImpactRecorded or SourceChangeDetected or EvidenceRecorded or AnalysisCompleted))
            {
                var first = ordered[0];
                ApplyMetadata(session, first.Item.Operation);
                if (!expectedVersions.TryGetValue(first.Item.Operation.TargetId, out var expectedVersion) ||
                    expectedVersion == 0)
                {
                    throw new InvalidOperationException(
                        $"RepositoryIntelligence operation '{first.Item.Operation.SourceReference}' has no existing analysis stream.");
                }

                var stream = await session.Events.FetchForWriting<AnalysisRun>(
                    first.Item.Operation.TargetId,
                    expectedVersion,
                    cancellationToken).ConfigureAwait(false);
                if (stream.Aggregate is null)
                {
                    throw new InvalidOperationException(
                        $"Analysis stream '{first.Item.Operation.TargetId}' could not be reconstructed for migration.");
                }

                foreach (var item in ordered)
                {
                    stream.AppendOne(item.Event);
                    SetEventMetadata(stream.Events[^1], item.Item.Operation);
                    expectedVersions[item.Item.Operation.TargetId]++;
                }

                continue;
            }

            foreach (var item in ordered)
            {
                ApplyMetadata(session, item.Item.Operation);
                if (!expectedVersions.TryGetValue(item.Item.Operation.TargetId, out var expectedVersion) ||
                    expectedVersion == 0)
                {
                    throw new InvalidOperationException(
                        $"ProjectState '{item.Item.Operation.SourceReference}' has no existing Project stream and no " +
                        "ProjectCreated operation in this batch.");
                }

                var stream = await session.Events.FetchForWriting<ProjectAggregate>(
                    item.Item.Operation.TargetId,
                    expectedVersion,
                    cancellationToken).ConfigureAwait(false);
                if (stream.Aggregate is null)
                {
                    throw new InvalidOperationException(
                        $"Project stream '{item.Item.Operation.TargetId}' could not be reconstructed for migration.");
                }

                stream.AppendOne(item.Event);
                SetEventMetadata(stream.Events[^1], item.Item.Operation);
                expectedVersions[item.Item.Operation.TargetId]++;
            }
        }

        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new MigrationBusinessApplyReport(
            operations.Length,
            effective.Count,
            operations.Length - effective.Count,
            effective.Select(item => item.Operation.TargetId).Distinct().Count(),
            []);
    }

    private static MappedEvent Map(
        MigrationOperation operation,
        LegacyRecord record,
        Dictionary<string, LegacyRecord> projectRecords)
    {
        IReadOnlyList<object> events = operation.Kind switch
        {
            ProjectKind => [ProjectMigrationMapper.ToProjectCreated(operation, record)],
            ProjectStateKind => [ProjectMigrationMapper.ToLifecycleReconciled(operation, record)],
            ProjectRoleKind or ProjectMembershipKind =>
                ProjectAccessMigrationMapper.ToEvents(
                    operation,
                    record,
                    FindProjectRecord(operation, projectRecords)),
            PlanKind or RequirementKind or MilestoneKind =>
                PlanningMigrationMapper.ToEvents(operation, record),
            EngineeringTaskKind =>
                TaskFlowMigrationMapper.ToEvents(operation, record),
            TaskVersionKind =>
                [TaskFlowMigrationMapper.ToVersionEvent(operation, record)],
            TaskEvidenceKind =>
                [TaskFlowMigrationMapper.ToEvidenceEvent(operation, record)],
            AnalysisRunKind =>
                [RepositoryIntelligenceMigrationMapper.ToAnalysisStarted(operation, record)],
            SourceArtifactKind =>
                [RepositoryIntelligenceMigrationMapper.ToArtifactObserved(operation, record)],
            SourceImpactKind =>
                [RepositoryIntelligenceMigrationMapper.ToImpactRecorded(operation, record)],
            _ => throw new InvalidOperationException($"No typed mapper exists for migration kind '{operation.Kind}'.")
        };

        return new MappedEvent(operation, events);
    }

    private static LegacyRecord FindProjectRecord(
        MigrationOperation operation,
        Dictionary<string, LegacyRecord> projectRecords)
    {
        var projectSourceId = operation.ProjectSourceId
            ?? throw new InvalidOperationException(
                $"Migration operation '{operation.SourceReference}' has no project source id.");
        var reference = Goal2MigrationPlanner.BuildSourceReference(ProjectKind, projectSourceId);
        if (!projectRecords.TryGetValue(reference, out var projectRecord))
        {
            throw new InvalidOperationException(
                $"Migration operation '{operation.SourceReference}' has no matching Project record '{reference}'.");
        }

        return projectRecord;
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

    private static async Task<Dictionary<Guid, IReadOnlyList<IEvent>>> ReadStreamsAsync(
        DocumentStore store,
        IReadOnlyList<Guid> streamIds,
        CancellationToken cancellationToken)
    {
        await using var query = store.QuerySession();
        var result = new Dictionary<Guid, IReadOnlyList<IEvent>>();
        foreach (var streamId in streamIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[streamId] = await query.Events.FetchStreamAsync(
                streamId,
                long.MaxValue,
                timestamp: null,
                fromVersion: 0,
                token: cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private static void ValidateLedgerAppliedMarkers(
        IReadOnlyList<MigrationOperation> ledgerApplied,
        Dictionary<Guid, IReadOnlyList<IEvent>> streamEvents)
    {
        foreach (var operation in ledgerApplied)
        {
            if (!TryFindMarker(streamEvents, operation, out var hash))
            {
                throw new InvalidOperationException(
                    $"The ledger marks '{operation.SourceReference}' as applied, but no matching Marten event marker exists.");
            }

            if (!string.Equals(hash, operation.PayloadHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The ledger marker '{operation.SourceReference}' has payload hash '{hash}', " +
                    $"but the input has '{operation.PayloadHash}'.");
            }
        }
    }

    private static bool TryFindMarker(
        Dictionary<Guid, IReadOnlyList<IEvent>> streamEvents,
        MigrationOperation operation,
        out string? payloadHash)
    {
        payloadHash = null;
        if (!streamEvents.TryGetValue(operation.TargetId, out var events))
        {
            return false;
        }

        var matching = events.FirstOrDefault(@event =>
            @event.Headers is not null &&
            @event.Headers.TryGetValue(EventMetadataHeaders.MigrationSourceReference, out var sourceReference) &&
            string.Equals(
                Convert.ToString(sourceReference, System.Globalization.CultureInfo.InvariantCulture),
                operation.SourceReference,
                StringComparison.Ordinal));
        if (matching?.Headers is null)
        {
            return false;
        }

        matching.Headers.TryGetValue(EventMetadataHeaders.MigrationPayloadHash, out var hash);
        payloadHash = Convert.ToString(hash, System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }

    private static void ValidateStreamInputs(
        IReadOnlyList<MappedEvent> effective,
        Dictionary<Guid, IReadOnlyList<IEvent>> streamEvents)
    {
        foreach (var group in effective.GroupBy(item => item.Operation.TargetId))
        {
            var events = group.SelectMany(item => item.Events).ToArray();
            var hasInitializer = events.Any(@event => @event is ProjectCreated or ProjectAccessInitialized or PlanCreated or TaskProposed or AnalysisStarted);
            if (!hasInitializer && (!streamEvents.TryGetValue(group.Key, out var existing) || existing.Count == 0))
            {
                var kind = "AccessControl";
                if (events.Any(@event => @event is ProjectLifecycleReconciled))
                {
                    kind = "ProjectState";
                }
                else if (events.Any(@event => @event is RequirementAdded or MilestoneAdded))
                {
                    kind = "Planning";
                }
                else if (events.Any(@event => @event is TaskLifecycleReconciled or TaskVersionImported or TaskEvidenceImported))
                {
                    kind = "TaskFlow";
                }
                else if (events.Any(@event => @event is ArtifactObserved or ImpactRecorded or SourceChangeDetected or EvidenceRecorded or AnalysisCompleted))
                {
                    kind = "RepositoryIntelligence";
                }
                throw new InvalidOperationException(
                    $"{kind} stream '{group.Key}' is required before applying this migration.");
            }

            if (events.Count(@event => @event is ProjectCreated or ProjectAccessInitialized or PlanCreated or TaskProposed or AnalysisStarted) > 1)
            {
                throw new InvalidOperationException(
                    $"More than one stream initializer is planned for stream '{group.Key}'.");
            }

            if (events.Count(@event => @event is ProjectLifecycleReconciled) > 1)
            {
                throw new InvalidOperationException(
                    $"More than one ProjectLifecycleReconciled event is planned for stream '{group.Key}'.");
            }

            if (events.Count(@event => @event is ProjectCreated) > 1 ||
                events.Count(@event => @event is ProjectAccessInitialized) > 1 ||
                events.Count(@event => @event is PlanCreated) > 1 ||
                events.Count(@event => @event is TaskProposed) > 1 ||
                events.Count(@event => @event is AnalysisStarted) > 1)
            {
                throw new InvalidOperationException(
                    $"More than one typed initializer is planned for stream '{group.Key}'.");
            }
        }
    }

    private static int EventOrder(object @event) => @event switch
    {
        ProjectCreated or ProjectAccessInitialized => 0,
        PlanCreated => 0,
        TaskProposed => 0,
        AnalysisStarted => 0,
        ProjectRoleCreated => 10,
        ProjectRolePermissionsUpdated => 20,
        ProjectMemberAdded => 30,
        ProjectMemberRolesAssigned => 40,
        ProjectMemberRemoved => 50,
        RequirementAdded => 10,
        MilestoneAdded => 20,
        TaskLifecycleReconciled => 60,
        TaskVersionImported => 70,
        TaskEvidenceImported => 80,
        ProjectLifecycleReconciled => 60,
        ArtifactObserved => 10,
        SourceChangeDetected => 20,
        EvidenceRecorded => 30,
        ImpactRecorded => 40,
        AnalysisCompleted => 50,
        _ => 100
    };

    private static void SetActionEventMetadata(
        IReadOnlyList<IEvent> actionEvents,
        IReadOnlyList<MappedEventData> mappedEvents)
    {
        foreach (var actionEvent in actionEvents)
        {
            var mapped = mappedEvents.Single(item => ReferenceEquals(item.Event, actionEvent.Data));
            SetEventMetadata(actionEvent, mapped.Item.Operation);
        }
    }

    private static void ApplyMetadata(IDocumentSession session, MigrationOperation operation)
    {
        session.ApplyEventMetadata(new EventMetadata(
            ProjectMigrationMapper.MigrationActor,
            ProjectMigrationMapper.CorrelationId(operation),
            operation.SourceReference,
            operation.TargetId,
            TenantId: null,
            MigrationSource(operation)));
    }

    private static void SetEventMetadata(IEvent @event, MigrationOperation operation)
    {
        var headers = @event.Headers is null
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : new Dictionary<string, object>(@event.Headers, StringComparer.Ordinal);
        headers[EventMetadataHeaders.MigrationSourceReference] = operation.SourceReference;
        headers[EventMetadataHeaders.MigrationPayloadHash] = operation.PayloadHash;
        @event.Headers = headers;
        @event.UserName = ProjectMigrationMapper.MigrationActor;
        @event.CorrelationId = ProjectMigrationMapper.CorrelationId(operation);
        @event.CausationId = operation.SourceReference;
    }

    private static string MigrationSource(MigrationOperation operation)
    {
        if (operation.Kind is ProjectRoleKind or ProjectMembershipKind)
        {
            return ProjectAccessMigrationMapper.MigrationSource;
        }

        if (operation.Kind is PlanKind or RequirementKind or MilestoneKind)
        {
            return PlanningMigrationMapper.MigrationSource;
        }

        if (operation.Kind is EngineeringTaskKind or TaskVersionKind or TaskEvidenceKind)
        {
            return TaskFlowMigrationMapper.MigrationSource;
        }

        if (operation.Kind is AnalysisRunKind or SourceArtifactKind or SourceImpactKind)
        {
            return RepositoryIntelligenceMigrationMapper.MigrationSource;
        }

        return ProjectMigrationMapper.MigrationSource;
    }

    private sealed record MappedEvent(MigrationOperation Operation, IReadOnlyList<object> Events);

    private sealed record MappedEventData(MappedEvent Item, object Event);
}
