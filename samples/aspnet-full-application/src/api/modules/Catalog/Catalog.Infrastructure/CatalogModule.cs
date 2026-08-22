using Carter;
using Microsoft.AspNetCore.Routing;
using VietAIS.TCFlow.WebApi.Catalog.Infrastructure.Endpoints.v1;

namespace VietAIS.TCFlow.WebApi.Catalog.Infrastructure;

public static class CatalogModule
{
    public sealed class Endpoints : CarterModule
    {
        public Endpoints() : base("catalog") { }

        public override void AddRoutes(IEndpointRouteBuilder app)
        {
            var productGroup = app.MapGroup("products").WithTags("products");
            productGroup.MapProductCreationEndpoint();
        }
    }
}
