using Marten;

namespace VietAIS.TCFlow.WebApi.Catalog;

public static class CatalogStorage
{
    public static void Configure(StoreOptions options)
    {
        options.Schema.For<Product>().UseOptimisticConcurrency(true);
    }
}
