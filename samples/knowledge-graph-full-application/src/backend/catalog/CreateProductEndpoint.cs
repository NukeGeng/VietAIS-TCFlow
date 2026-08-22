using FSH.Framework.Infrastructure.Auth.Policy;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace VietAIS.TCFlow.WebApi.Catalog;

public static class CreateProductEndpoint
{
    public static RouteHandlerBuilder MapProductCreationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/", async (CreateProductCommand request, ISender mediator) =>
            Results.Ok(await mediator.Send(request)))
            .WithName(nameof(CreateProductEndpoint))
            .Produces<CreateProductResponse>()
            .RequirePermission("Permissions.Products.Create")
            .MapToApiVersion(1);
    }
}
