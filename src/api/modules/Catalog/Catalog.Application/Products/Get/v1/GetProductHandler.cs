using Microsoft.Extensions.DependencyInjection;
using VietAIS.TCFlow.WebApi.Catalog.Domain.Exceptions;
using FSH.Framework.Core.Persistence;
using FSH.Framework.Core.Caching;
using VietAIS.TCFlow.WebApi.Catalog.Domain;
using MediatR;

namespace VietAIS.TCFlow.WebApi.Catalog.Application.Products.Get.v1;
public sealed class GetProductHandler(
    [FromKeyedServices("catalog:products")] IReadRepository<Product> repository,
    ICacheService cache)
    : IRequestHandler<GetProductRequest, ProductResponse>
{
    public async Task<ProductResponse> Handle(GetProductRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await cache.GetOrSetAsync(
            $"product:{request.Id}",
            async () =>
            {
                var spec = new GetProductSpecs(request.Id);
                var productItem = await repository.FirstOrDefaultAsync(spec, cancellationToken);
                if (productItem == null) throw new ProductNotFoundException(request.Id);
                return productItem;
            },
            cancellationToken: cancellationToken);
        return item!;
    }
}
