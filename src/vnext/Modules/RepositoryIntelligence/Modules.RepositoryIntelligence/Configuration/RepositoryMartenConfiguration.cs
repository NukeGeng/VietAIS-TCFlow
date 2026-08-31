using JasperFx.Events.Projections;
using Marten;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Domain;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Projections;

namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Configuration;

public static class RepositoryMartenConfiguration
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Events.AddEventType<AnalysisStarted>();
        options.Events.AddEventType<ArtifactObserved>();
        options.Events.AddEventType<SourceChangeDetected>();
        options.Events.AddEventType<EvidenceRecorded>();
        options.Events.AddEventType<ImpactRecorded>();
        options.Events.AddEventType<AnalysisCompleted>();
        options.Projections.Add<AnalysisCurrentProjection>(ProjectionLifecycle.Inline);
        options.Projections.Add<KnowledgeGraphProjection>(ProjectionLifecycle.Async);
        options.Projections.Add<ImpactGraphProjection>(ProjectionLifecycle.Async);
    }
}
