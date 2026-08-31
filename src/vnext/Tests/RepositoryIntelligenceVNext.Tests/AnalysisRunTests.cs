using VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Domain;

namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Tests;

public sealed class AnalysisRunTests
{
    [Fact]
    public void AnalysisRunDeduplicatesSourceFactsAndClosesExplicitly()
    {
        var run = new AnalysisRun();
        run.Apply(new AnalysisStarted(Guid.NewGuid(), Guid.NewGuid(), "repo", "abc123", "analyzer", "c1", DateTimeOffset.UtcNow));
        var artifact = run.Observe("src/Orders.cs", SourceFactKind.Aggregate, "Order", "aggregate", "analyzer", "c2", DateTimeOffset.UtcNow);
        run.Apply(artifact);
        Should.Throw<InvalidOperationException>(() => run.Observe("src/Orders.cs", SourceFactKind.Aggregate, "Order", "duplicate", "analyzer", "c3", DateTimeOffset.UtcNow));
        var completed = run.Complete("analyzer", "c4", DateTimeOffset.UtcNow);
        run.Apply(completed);
        Should.Throw<InvalidOperationException>(() => run.RecordEvidence("e1", "src/Orders.cs", "claim", "CONFIRMED", "ai", "c5", DateTimeOffset.UtcNow));
    }
}
