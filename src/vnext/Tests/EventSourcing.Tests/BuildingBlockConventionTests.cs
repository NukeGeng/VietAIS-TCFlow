using VietAIS.TCFlow.BuildingBlocks.Application.Identity;
using VietAIS.TCFlow.BuildingBlocks.Application.Results;
using VietAIS.TCFlow.BuildingBlocks.Application.Time;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.BuildingBlocks.Messaging;
using Wolverine;

namespace VietAIS.TCFlow.BuildingBlocks.EventSourcing.Tests;

public sealed class BuildingBlockConventionTests
{
    [Fact]
    public void ResultExposesValueOnlyForSuccessfulResult()
    {
        var success = Result.Success("ready");
        var failure = Result.Failure<string>(
            ResultError.Validation("project.name.required", "Project name is required."));

        success.IsSuccess.ShouldBeTrue();
        success.Value.ShouldBe("ready");
        failure.IsFailure.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => _ = failure.Value);
    }

    [Fact]
    public void EventMetadataNormalizesRequiredAndOptionalValues()
    {
        var projectId = Guid.NewGuid();
        var metadata = new EventMetadata(
            " actor ",
            " correlation ",
            "  ",
            projectId,
            " tenant ",
            " api ").Normalize();

        metadata.ActorId.ShouldBe("actor");
        metadata.CorrelationId.ShouldBe("correlation");
        metadata.CausationId.ShouldBeNull();
        metadata.ProjectId.ShouldBe(projectId);
        metadata.TenantId.ShouldBe("tenant");
        metadata.Source.ShouldBe("api");
    }

    [Fact]
    public void ClockAndIdentityUseInjectedTimeProviderAndUuidVersion7()
    {
        var timestamp = new DateTimeOffset(2026, 8, 30, 12, 30, 0, TimeSpan.Zero);
        var clock = new SystemClock(new FixedTimeProvider(timestamp));
        var id = new UuidV7IdGenerator().NewId();

        clock.UtcNow.ShouldBe(timestamp);
        id.Version.ShouldBe(7);
    }

    [Fact]
    public async Task MessagingUsesCloudSafeUuidVersion7EnvelopeIdentity()
    {
        await using var options = new WolverineOptions();

        TcFlowMessagingConfiguration.Configure(options);

        options.EnvelopeIdGeneration.ShouldBe(EnvelopeIdGeneration.GuidV7);
    }

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }
}
