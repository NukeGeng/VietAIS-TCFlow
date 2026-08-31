using Marten;
using VietAIS.TCFlow.Modules.TaskFlow.Contracts.Queries;
using VietAIS.TCFlow.Modules.TaskFlow.Projections;

namespace VietAIS.TCFlow.Modules.TaskFlow.Features;

public static class TaskFlowQueries
{
    public static async Task<TaskView?> Handle(GetTask query, IQuerySession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var current = await session.LoadAsync<TaskCurrent>(query.TaskId, cancellationToken).ConfigureAwait(false);
        return current is null ? null : new TaskView(current.Id, current.ProjectId, current.Title, current.Description, current.Status, current.AssigneeId, current.AiVerificationPassed, current.HumanReviewRequested, current.HumanReviewApproved, current.SourceChangeKey, current.Version, current.LastChangedAtUtc);
    }
}
