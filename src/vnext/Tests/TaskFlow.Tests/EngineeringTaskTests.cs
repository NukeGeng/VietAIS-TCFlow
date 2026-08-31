using VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries;
using VietAIS.TCFlow.Modules.TaskFlow.Domain;
using TaskStatus = VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries.TaskStatus;

namespace VietAIS.TCFlow.Modules.TaskFlow.Tests;

public sealed class EngineeringTaskTests
{
    [Fact]
    public void LifecycleRequiresAssignmentAiVerificationAndHumanApproval()
    {
        var task = new EngineeringTask();
        task.Apply(new TaskProposed(Guid.NewGuid(), Guid.NewGuid(), "Implement feature", null, "sha:abc", "system", "corr-1", DateTimeOffset.UtcNow));
        Should.Throw<InvalidOperationException>(() => task.Start("user", "corr-2", DateTimeOffset.UtcNow));
        task.Apply(task.Accept("user", "corr-3", DateTimeOffset.UtcNow));
        task.Apply(task.Assign("developer", "user", "corr-4", DateTimeOffset.UtcNow));
        task.Apply(task.Start("developer", "corr-5", DateTimeOffset.UtcNow));
        Should.Throw<InvalidOperationException>(() => task.RequestReview("user", "corr-6", DateTimeOffset.UtcNow));
        task.Apply(task.CompleteAiVerification(true, "tests passed", "ai", "corr-7", DateTimeOffset.UtcNow));
        task.Apply(task.RequestReview("user", "corr-8", DateTimeOffset.UtcNow));
        task.Apply(task.ApproveReview("reviewer", "corr-9", DateTimeOffset.UtcNow));
        task.Apply(task.Complete("reviewer", "corr-10", DateTimeOffset.UtcNow));
        task.Status.ShouldBe(TaskStatus.Completed);
    }

    [Fact]
    public void InvalidTransitionDoesNotMutateState()
    {
        var task = new EngineeringTask();
        task.Apply(new TaskProposed(Guid.NewGuid(), Guid.NewGuid(), "Implement feature", null, null, "system", "corr-1", DateTimeOffset.UtcNow));
        Should.Throw<InvalidOperationException>(() => task.Complete("user", "corr-2", DateTimeOffset.UtcNow));
        task.Status.ShouldBe(TaskStatus.Suggested);
        task.AiVerificationPassed.ShouldBeFalse();
    }

    [Fact]
    public void SourceChangeUpdateKeepsSameTaskIdentity()
    {
        var id = Guid.NewGuid();
        var task = new EngineeringTask();
        task.Apply(new TaskProposed(id, Guid.NewGuid(), "Old title", "old", "sha:abc", "system", "corr-1", DateTimeOffset.UtcNow));
        var update = task.UpdateFromSourceChange("New title", "new", "sha:abc", "analyzer", "corr-2", DateTimeOffset.UtcNow);
        task.Apply(update);
        task.Id.ShouldBe(id);
        task.Title.ShouldBe("New title");
        update.Title.ShouldBe("New title");
    }
}
