using Marten;
using MediatR;

namespace VietAIS.TCFlow.WebApi.LiveAcceptance;

public sealed class CreateLiveAcceptanceOrderHandler(IDocumentSession session)
    : IRequestHandler<CreateLiveAcceptanceOrderCommand, CreateLiveAcceptanceOrderResponse>
{
    public async Task<CreateLiveAcceptanceOrderResponse> Handle(
        CreateLiveAcceptanceOrderCommand request,
        CancellationToken cancellationToken)
    {
        var order = new LiveAcceptanceOrder(Guid.NewGuid(), request.Name);
        session.Store(order);
        await session.SaveChangesAsync(cancellationToken);
        return new CreateLiveAcceptanceOrderResponse(order.Id, order.Name);
    }
}
