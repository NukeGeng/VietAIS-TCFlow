using VietAIS.TCFlow.Modules.Architecture.Contracts.Commands;
using VietAIS.TCFlow.Modules.Architecture.Domain;

namespace VietAIS.TCFlow.Modules.Architecture.Tests;

public sealed class ArchitectureModelTests
{
    [Fact]
    public void ModelRequiresNodesBeforeRelationshipsAndRejectsDuplicateDrift()
    {
        var model = new ArchitectureModel();
        model.Apply(new ArchitectureModelCreated(Guid.NewGuid(), Guid.NewGuid(), "Checkout", "user", "c1", DateTimeOffset.UtcNow));
        var api = Guid.NewGuid();
        var data = Guid.NewGuid();
        model.Apply(model.AddModule(api, "API", null, "user", "c2", DateTimeOffset.UtcNow));
        Should.Throw<InvalidOperationException>(() => model.ConnectModules(api, data, "depends-on", "user", "c3", DateTimeOffset.UtcNow));
        model.Apply(model.AddEntity(data, "Order", null, "user", "c4", DateTimeOffset.UtcNow));
        var drift = model.RecordDrift("route-mismatch", "Route differs from architecture", "source:api/orders", "analyzer", "c5", DateTimeOffset.UtcNow);
        model.Apply(drift);
        Should.Throw<InvalidOperationException>(() => model.RecordDrift("route-mismatch", "duplicate", "source", "analyzer", "c6", DateTimeOffset.UtcNow));
    }
}
