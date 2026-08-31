using System.Text.Json;
using System.Text.Json.Serialization;
using VietAIS.TCFlow.Tools.Migration;

namespace VietAIS.TCFlow.Tools.Migration.Tests;

public sealed class Goal2MigrationPlannerTests
{
    [Fact]
    public void PlansAreDeterministicAndDuplicateSafe()
    {
        var export = new LegacyExport(
            1,
            [
                Record("EngineeringTask", "task-2", "project-1"),
                Record("Project", "project-1"),
                Record("Project", "project-1")
            ]);

        var first = Goal2MigrationPlanner.Plan(export);
        var second = Goal2MigrationPlanner.Plan(export);

        Assert.Equal(first.ToolVersion, second.ToolVersion);
        Assert.Equal(first.InputSchemaVersion, second.InputSchemaVersion);
        Assert.Equal(first.Operations, second.Operations);
        Assert.Equal(3, first.Operations.Count);
        Assert.Equal("sha256:test", first.Operations.Single(operation => operation.Kind == "Project" && operation.Action == MigrationAction.Append).PayloadHash);
        Assert.Equal("project-1", first.Operations.Single(operation => operation.Kind == "EngineeringTask").ProjectSourceId);
        Assert.Equal(
            MigrationAction.Append,
            first.Operations.Single(operation => operation.Kind == "Project" && operation.Action == MigrationAction.Append).Action);
        Assert.Equal(
            1,
            first.Operations.Count(operation => operation.Kind == "Project" && operation.Action == MigrationAction.Skip));
    }

    [Fact]
    public void AppliedSourceReferencesBecomeSkips()
    {
        var reference = Goal2MigrationPlanner.BuildSourceReference("Project", "project-1");
        var plan = Goal2MigrationPlanner.Plan(
            new LegacyExport(1, [Record("Project", "project-1")]),
            new HashSet<string>([reference], StringComparer.Ordinal));

        var operation = Assert.Single(plan.Operations);
        Assert.Equal(MigrationAction.Skip, operation.Action);
        Assert.Equal("already-applied", operation.SkipReason);
    }

    [Fact]
    public void UnknownLegacyKindsFailClosed()
    {
        var export = new LegacyExport(1, [Record("Unknown", "legacy-1")]);

        Assert.Throws<InvalidOperationException>(() => Goal2MigrationPlanner.Plan(export));
    }

    [Fact]
    public void MissingPayloadHashFailsClosed()
    {
        using var document = JsonDocument.Parse("{}");
        var export = new LegacyExport(
            1,
            [new LegacyRecord("Project", "project-1", null, "", document.RootElement.Clone())]);

        Assert.Throws<ArgumentException>(() => Goal2MigrationPlanner.Plan(export));
    }

    [Fact]
    public void ConflictingHashesForTheSameSourceReferenceFailClosed()
    {
        var export = new LegacyExport(
            1,
            [
                Record("Project", "project-1"),
                new LegacyRecord(
                    "Project",
                    "project-1",
                    null,
                    "sha256:changed",
                    JsonDocument.Parse("{}").RootElement.Clone())
            ]);

        var failure = Assert.Throws<InvalidOperationException>(() => Goal2MigrationPlanner.Plan(export));
        Assert.Contains("Conflicting payload hashes", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectStateUsesTheOwningProjectStreamIdentity()
    {
        var plan = Goal2MigrationPlanner.Plan(
            new LegacyExport(
                1,
                [Record("Project", "project-1"), Record("ProjectState", "state-1", "project-1")]));

        var project = plan.Operations.Single(operation => operation.Kind == "Project");
        var state = plan.Operations.Single(operation => operation.Kind == "ProjectState");

        Assert.Equal(project.TargetId, state.TargetId);
        Assert.Equal(
            Goal2MigrationPlanner.CreateDeterministicId("Project", "project-1"),
            state.TargetId);
    }

    [Fact]
    public void ProjectStateWithoutOwningProjectFailsClosed()
    {
        var failure = Assert.Throws<InvalidOperationException>(() =>
            Goal2MigrationPlanner.Plan(
                new LegacyExport(1, [Record("ProjectState", "state-1")])));

        Assert.Contains("must identify its Project source record", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanningChildrenUseTheOwningPlanStreamIdentity()
    {
        using var payload = JsonDocument.Parse("{\"planSourceId\":\"plan-1\"}");
        var plan = Goal2MigrationPlanner.Plan(
            new LegacyExport(
                1,
                [
                    Record("Plan", "plan-1", "project-1"),
                    new LegacyRecord(
                        "Requirement",
                        "requirement-1",
                        "project-1",
                        "sha256:requirement",
                        payload.RootElement.Clone())
                ]));

        var planOperation = plan.Operations.Single(operation => operation.Kind == "Plan");
        var requirement = plan.Operations.Single(operation => operation.Kind == "Requirement");

        Assert.Equal(planOperation.TargetId, requirement.TargetId);
        Assert.Equal("plan-1", requirement.AggregateSourceId);
    }

    [Fact]
    public void PlanningChildWithoutOwningPlanFailsClosed()
    {
        var failure = Assert.Throws<InvalidOperationException>(() =>
            Goal2MigrationPlanner.Plan(
                new LegacyExport(1, [Record("Requirement", "requirement-1", "project-1")])));

        Assert.Contains("must identify its Plan source", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskHistoryUsesTheOwningTaskStreamIdentity()
    {
        using var payload = JsonDocument.Parse("{\"taskSourceId\":\"task-1\"}");
        var plan = Goal2MigrationPlanner.Plan(
            new LegacyExport(
                1,
                [
                    Record("EngineeringTask", "task-1", "project-1"),
                    new LegacyRecord(
                        "TaskVersion",
                        "version-1",
                        "project-1",
                        "sha256:version",
                        payload.RootElement.Clone()),
                    new LegacyRecord(
                        "TaskEvidence",
                        "evidence-1",
                        "project-1",
                        "sha256:evidence",
                        payload.RootElement.Clone())
                ]));

        var task = plan.Operations.Single(operation => operation.Kind == "EngineeringTask");
        var version = plan.Operations.Single(operation => operation.Kind == "TaskVersion");
        var evidence = plan.Operations.Single(operation => operation.Kind == "TaskEvidence");

        Assert.Equal(task.TargetId, version.TargetId);
        Assert.Equal(task.TargetId, evidence.TargetId);
        Assert.Equal("task-1", version.AggregateSourceId);
        Assert.Equal("task-1", evidence.AggregateSourceId);
    }

    [Fact]
    public void TaskHistoryWithoutOwningTaskFailsClosed()
    {
        var failure = Assert.Throws<InvalidOperationException>(() =>
            Goal2MigrationPlanner.Plan(
                new LegacyExport(1, [Record("TaskEvidence", "evidence-1", "project-1")])));

        Assert.Contains("must identify its Task source", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryFactsUseTheOwningAnalysisStreamIdentity()
    {
        using var artifactPayload = JsonDocument.Parse("{\"analysisRunSourceId\":\"analysis-1\"}");
        using var impactPayload = JsonDocument.Parse("{\"analysisRunSourceId\":\"analysis-1\"}");
        var plan = Goal2MigrationPlanner.Plan(
            new LegacyExport(
                1,
                [
                    Record("AnalysisRun", "analysis-1", "project-1"),
                    new LegacyRecord(
                        "SourceArtifact",
                        "artifact-1",
                        "project-1",
                        "sha256:artifact",
                        artifactPayload.RootElement.Clone()),
                    new LegacyRecord(
                        "SourceImpact",
                        "impact-1",
                        "project-1",
                        "sha256:impact",
                        impactPayload.RootElement.Clone())
                ]));

        var analysis = plan.Operations.Single(operation => operation.Kind == "AnalysisRun");
        var artifact = plan.Operations.Single(operation => operation.Kind == "SourceArtifact");
        var impact = plan.Operations.Single(operation => operation.Kind == "SourceImpact");

        Assert.Equal(analysis.TargetId, artifact.TargetId);
        Assert.Equal(analysis.TargetId, impact.TargetId);
        Assert.Equal("analysis-1", artifact.AggregateSourceId);
        Assert.Equal("analysis-1", impact.AggregateSourceId);
    }

    [Fact]
    public void RepositoryFactWithoutOwningAnalysisFailsClosed()
    {
        var failure = Assert.Throws<InvalidOperationException>(() =>
            Goal2MigrationPlanner.Plan(
                new LegacyExport(1, [Record("SourceArtifact", "artifact-1", "project-1")] )));

        Assert.Contains("must identify its aggregate source", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EventStormingAndArchitectureChildrenUseOwningAggregateStreams()
    {
        using var boardPayload = JsonDocument.Parse("{\"boardSourceId\":\"board-1\"}");
        using var modulePayload = JsonDocument.Parse("{\"modelSourceId\":\"model-1\"}");
        var plan = Goal2MigrationPlanner.Plan(
            new LegacyExport(
                1,
                [
                    Record("StormingBoard", "board-1", "project-1"),
                    new LegacyRecord("StormingNode", "node-1", "project-1", "sha256:node", boardPayload.RootElement.Clone()),
                    Record("ArchitectureModel", "model-1", "project-1"),
                    new LegacyRecord("ArchitectureModule", "module-1", "project-1", "sha256:module", modulePayload.RootElement.Clone())
                ]));

        var board = plan.Operations.Single(operation => operation.Kind == "StormingBoard");
        var node = plan.Operations.Single(operation => operation.Kind == "StormingNode");
        var model = plan.Operations.Single(operation => operation.Kind == "ArchitectureModel");
        var module = plan.Operations.Single(operation => operation.Kind == "ArchitectureModule");

        Assert.Equal(board.TargetId, node.TargetId);
        Assert.Equal(model.TargetId, module.TargetId);
        Assert.Equal("board-1", node.AggregateSourceId);
        Assert.Equal("model-1", module.AggregateSourceId);
    }

    [Fact]
    public void EventStormingChildWithoutOwningBoardFailsClosed()
    {
        var failure = Assert.Throws<InvalidOperationException>(() =>
            Goal2MigrationPlanner.Plan(
                new LegacyExport(1, [Record("StormingNode", "node-1", "project-1")])));

        Assert.Contains("must identify its Board source", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchitectureChildWithoutOwningModelFailsClosed()
    {
        var failure = Assert.Throws<InvalidOperationException>(() =>
            Goal2MigrationPlanner.Plan(
                new LegacyExport(1, [Record("ArchitectureModule", "module-1", "project-1")])));

        Assert.Contains("must identify its Model source", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyingTheLedgerIsRepeatableAndDoesNotAppendDuplicateEntries()
    {
        var export = new LegacyExport(
            1,
            [
                Record("Project", "project-1"),
                Record("EngineeringTask", "task-1", "project-1"),
                Record("Project", "project-1")
            ]);
        var path = Path.Combine(
            AppContext.BaseDirectory,
            $"migration-ledger-{Guid.CreateVersion7():N}.json");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());

        try
        {
            var initial = MigrationLedgerStore.Load(path, 1, 1, options);
            var firstPlan = Goal2MigrationPlanner.Plan(
                export,
                initial.Entries.Select(entry => entry.SourceReference).ToHashSet(StringComparer.Ordinal));
            var (firstLedger, firstReport) = MigrationLedgerStore.Apply(
                firstPlan,
                initial,
                DateTimeOffset.UtcNow);
            MigrationLedgerStore.SaveAtomic(path, firstLedger, options);

            var persisted = MigrationLedgerStore.Load(path, 1, 1, options);
            var secondPlan = Goal2MigrationPlanner.Plan(
                export,
                persisted.Entries.Select(entry => entry.SourceReference).ToHashSet(StringComparer.Ordinal));
            var (secondLedger, secondReport) = MigrationLedgerStore.Apply(
                secondPlan,
                persisted,
                DateTimeOffset.UtcNow);

            Assert.Equal(2, firstReport.AppendCount);
            Assert.Equal(1, firstReport.SkipCount);
            Assert.Equal(2, persisted.Entries.Count);
            Assert.Equal(0, secondReport.AppendCount);
            Assert.Equal(3, secondReport.SkipCount);
            Assert.True(secondReport.Idempotent);
            Assert.Equal(persisted.Entries, secondLedger.Entries);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LedgerVersionMustMatchThePlannerVersion()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            $"migration-ledger-{Guid.CreateVersion7():N}.json");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new MigrationLedger(99, 1, []),
                options));

        try
        {
            Assert.Throws<InvalidOperationException>(
                () => MigrationLedgerStore.Load(path, 1, 1, options));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static LegacyRecord Record(string kind, string sourceId, string? projectSourceId = null)
    {
        using var document = JsonDocument.Parse("{}");
        return new(kind, sourceId, projectSourceId, "sha256:test", document.RootElement.Clone());
    }
}
