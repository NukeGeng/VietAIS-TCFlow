using Carter;
using Microsoft.AspNetCore.Routing;

namespace VietAIS.TCFlow.WebApi.Catalog;

public sealed class CatalogModule : CarterModule
{
    public CatalogModule() : base("catalog") { }

    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("products");
        products.MapProductCreationEndpoint();
    }
}
