using Marten;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence;

public static class RepositoryIntelligenceModule
{
    public static void Configure(StoreOptions options)
    {
        options.DatabaseSchemaName = "repository_intelligence";
        options.Schema.For<Project>().UseOptimisticConcurrency(true);
        options.Schema.For<ProjectRepository>().UseOptimisticConcurrency(true);
    }
}
