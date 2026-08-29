using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace VietAIS.TCFlow.WebApi.LiveAcceptance;

public sealed class LiveAcceptanceModule : CarterModule
{
    public LiveAcceptanceModule() : base("live-acceptance") { }

    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("orders", async (
            CreateLiveAcceptanceOrderCommand request,
            ISender mediator,
            CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(request, cancellationToken)))
            .WithName("CreateLiveAcceptanceOrder")
            .Produces<CreateLiveAcceptanceOrderResponse>();
    }
}
