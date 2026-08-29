using MediatR;

namespace VietAIS.TCFlow.WebApi.LiveAcceptance;

public sealed record CreateLiveAcceptanceOrderCommand(string Name)
    : IRequest<CreateLiveAcceptanceOrderResponse>;

public sealed record CreateLiveAcceptanceOrderResponse(Guid Id, string Name);

public sealed record LiveAcceptanceOrder(Guid Id, string Name);
