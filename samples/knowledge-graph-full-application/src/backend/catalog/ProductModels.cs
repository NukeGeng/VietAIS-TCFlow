using MediatR;

namespace VietAIS.TCFlow.WebApi.Catalog;

public sealed record CreateProductCommand(string Name, decimal Price, Guid CategoryId)
    : IRequest<CreateProductResponse>;

public sealed record CreateProductResponse(Guid Id, string Name, decimal Price, Guid CategoryId);

public sealed record Product(Guid Id, string Name, decimal Price, Guid CategoryId);
