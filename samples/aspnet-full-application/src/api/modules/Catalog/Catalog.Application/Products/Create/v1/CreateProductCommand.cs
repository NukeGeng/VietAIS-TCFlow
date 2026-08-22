using MediatR;

namespace VietAIS.TCFlow.WebApi.Catalog.Application.Products.Create.v1;

public sealed record CreateProductCommand(
    string? Name,
    decimal Price,
    string? Description = null,
    Guid? BrandId = null) : IRequest<CreateProductResponse>;
