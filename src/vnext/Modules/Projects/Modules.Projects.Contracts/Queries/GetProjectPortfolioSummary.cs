using VietAIS.TCFlow.BuildingBlocks.Application.Messaging;

namespace VietAIS.TCFlow.Modules.Projects.Contracts.Queries;

public sealed record GetProjectPortfolioSummary(Guid ProjectId) : IQuery;
