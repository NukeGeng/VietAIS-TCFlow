using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;

namespace VietAIS.TCFlow.Modules.Planning.Contracts.Queries;

public sealed record GetPlan(Guid PlanId) : IQuery;
