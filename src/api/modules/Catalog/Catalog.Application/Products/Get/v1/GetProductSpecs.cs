using Ardalis.Specification;
using VietAIS.TCFlow.WebApi.Catalog.Domain;

namespace VietAIS.TCFlow.WebApi.Catalog.Application.Products.Get.v1;

public class GetProductSpecs : Specification<Product, ProductResponse>
{
    public GetProductSpecs(Guid id)
    {
        Query
            .Where(p => p.Id == id)
            .Include(p => p.Brand);
    }
}
