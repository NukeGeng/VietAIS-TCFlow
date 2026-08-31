using System.Text.Json;
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

    private static LegacyRecord Record(string kind, string sourceId, string? projectSourceId = null)
    {
        using var document = JsonDocument.Parse("{}");
        return new(kind, sourceId, projectSourceId, "sha256:test", document.RootElement.Clone());
    }
}
