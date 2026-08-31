namespace VietAIS.TCFlow.BuildingBlocks.Application.Identity;

public sealed class UuidV7IdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}
