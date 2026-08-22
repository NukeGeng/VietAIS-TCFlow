using MediatR;

namespace VietAIS.TCFlow.WebApi.Catalog.Application.Products.Create.v1;

public interface IProductWriter
{
    Task<Guid> AddAsync(CreateProductCommand request, CancellationToken cancellationToken);
}

public sealed class CreateProductHandler(IProductWriter writer)
    : IRequestHandler<CreateProductCommand, CreateProductResponse>
{
    public async Task<CreateProductResponse> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        var id = await writer.AddAsync(request, cancellationToken);
        return new CreateProductResponse(id);
    }
}
