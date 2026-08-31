namespace VietAIS.TCFlow.BuildingBlocks.Application.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
