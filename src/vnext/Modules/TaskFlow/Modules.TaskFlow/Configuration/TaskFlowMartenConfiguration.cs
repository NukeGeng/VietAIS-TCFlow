using JasperFx.Events.Projections;
using Marten;
using VietAIS.TCFlow.Modules.TaskFlow.Domain;
using VietAIS.TCFlow.Modules.TaskFlow.Projections;

namespace VietAIS.TCFlow.Modules.TaskFlow.Configuration;

public static class TaskFlowMartenConfiguration
{
    public static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Events.AddEventType<TaskProposed>();
        options.Events.AddEventType<TaskAccepted>();
        options.Events.AddEventType<TaskRejected>();
        options.Events.AddEventType<TaskAssigned>();
        options.Events.AddEventType<TaskStarted>();
        options.Events.AddEventType<TaskBlocked>();
        options.Events.AddEventType<AiVerificationCompleted>();
        options.Events.AddEventType<ReviewRequested>();
        options.Events.AddEventType<ReviewApproved>();
        options.Events.AddEventType<ReviewRejected>();
        options.Events.AddEventType<TaskCompleted>();
        options.Events.AddEventType<TaskReopened>();
        options.Events.AddEventType<TaskUpdatedFromSourceChange>();
        options.Projections.Add<TaskCurrentProjection>(ProjectionLifecycle.Inline);
        options.Projections.Add<TaskBoardProjection>(ProjectionLifecycle.Async);
        options.Projections.Add<TaskAnalyticsProjection>(ProjectionLifecycle.Async);
    }
}
