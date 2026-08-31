using VietAIS.TCFlow.Modules.Planning.Domain;

namespace VietAIS.TCFlow.Modules.Planning.Tests;

public sealed class PlanAggregateTests
{
    [Fact]
    public void RequirementsAndMilestonesReplayIntoThePlan()
    {
        var planId = Guid.NewGuid();
        var plan = new PlanAggregate();
        plan.Apply(new PlanCreated(
            planId,
            Guid.NewGuid(),
            "Platform roadmap",
            "Ship the next release",
            "owner-1",
            "c-1",
            DateTimeOffset.UtcNow));

        var requirement = plan.AddRequirement(
            Guid.NewGuid(),
            "Support source traceability",
            null,
            "owner-1",
            "c-2",
            DateTimeOffset.UtcNow);
        plan.Apply(requirement);
        var milestone = plan.AddMilestone(
            Guid.NewGuid(),
            "M5 complete",
            new DateOnly(2026, 9, 15),
            "owner-1",
            "c-3",
            DateTimeOffset.UtcNow);
        plan.Apply(milestone);

        plan.Id.ShouldBe(planId);
        plan.Name.ShouldBe("Platform roadmap");
        requirement.Title.ShouldBe("Support source traceability");
        milestone.Name.ShouldBe("M5 complete");
    }

    [Fact]
    public void DuplicateRequirementAndInvalidTitleAreRejected()
    {
        var plan = new PlanAggregate();
        plan.Apply(new PlanCreated(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Roadmap",
            null,
            "owner-1",
            "c-1",
            DateTimeOffset.UtcNow));
        var requirementId = Guid.NewGuid();
        var requirement = plan.AddRequirement(
            requirementId,
            "Valid requirement",
            null,
            "owner-1",
            "c-2",
            DateTimeOffset.UtcNow);
        plan.Apply(requirement);

        Should.Throw<InvalidOperationException>(() => plan.AddRequirement(
            requirementId,
            "Another title",
            null,
            "owner-1",
            "c-3",
            DateTimeOffset.UtcNow));
        Should.Throw<ArgumentException>(() => plan.AddRequirement(
            Guid.NewGuid(),
            "x",
            null,
            "owner-1",
            "c-4",
            DateTimeOffset.UtcNow));
    }
}
