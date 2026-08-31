using VietAIS.TCFlow.Modules.PlatformAdministration.Domain;

namespace VietAIS.TCFlow.Modules.PlatformAdministration.Tests;

public sealed class PlatformPolicyTests
{
    [Fact]
    public void PolicyKeepsAiMutationGuardAndAuditMetadata()
    {
        var policy = new PlatformPolicy();
        policy.Apply(new PlatformPolicyCreated(Guid.NewGuid(), "admin", "c1", DateTimeOffset.UtcNow));
        Should.Throw<InvalidOperationException>(() => policy.Update(true, false, true, "admin", "c2", DateTimeOffset.UtcNow));
        var update = policy.Update(true, true, true, "admin", "c3", DateTimeOffset.UtcNow);
        update.AllowAiTaskMutations.ShouldBeTrue();
        var audit = policy.Audit("platform.policy.update", "admin", "c3", DateTimeOffset.UtcNow);
        audit.ActorId.ShouldBe("admin");
    }
}
