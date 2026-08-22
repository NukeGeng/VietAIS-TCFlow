using Marten;
using MediatR;

namespace VietAIS.TCFlow.WebApi.Catalog;

public sealed class CreateProductHandler(IDocumentSession session)
    : IRequestHandler<CreateProductCommand, CreateProductResponse>
{
    public async Task<CreateProductResponse> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        var product = new Product(Guid.NewGuid(), request.Name, request.Price, request.CategoryId);
        session.Store(product);
        await session.SaveChangesAsync(cancellationToken);
        return new CreateProductResponse(product.Id, product.Name, product.Price, product.CategoryId);
    }
}
