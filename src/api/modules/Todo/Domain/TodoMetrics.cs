using System.Diagnostics.Metrics;
using VietAIS.TCFlow.Aspire.ServiceDefaults;

namespace VietAIS.TCFlow.WebApi.Todo.Domain;

public static class TodoMetrics
{
    private static readonly Meter Meter = new Meter(MetricsConstants.Todos, "1.0.0");
    public static readonly Counter<int> Created = Meter.CreateCounter<int>("items.created");
    public static readonly Counter<int> Updated = Meter.CreateCounter<int>("items.updated");
    public static readonly Counter<int> Deleted = Meter.CreateCounter<int>("items.deleted");
}
