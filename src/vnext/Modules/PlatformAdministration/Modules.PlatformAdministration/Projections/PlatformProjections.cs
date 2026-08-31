using Marten.Events.Aggregation;
using VietAIS.TCFlow.Modules.PlatformAdministration.Domain;

namespace VietAIS.TCFlow.Modules.PlatformAdministration.Projections;

public static class PlatformProjectionNames
{
    public const string Current = "platform-policy-current";
}

public sealed class PlatformPolicyCurrent
{
    public Guid Id { get; set; }
    public bool AllowAiAnalysis { get; set; }
    public bool AllowAiTaskSuggestions { get; set; }
    public bool AllowAiTaskMutations { get; set; }
    public string? ProviderName { get; set; }
    public bool ProviderEnabled { get; set; }
    public long Version { get; set; }
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class PlatformPolicyCurrentProjection : SingleStreamProjection<PlatformPolicyCurrent, Guid>
{
    public PlatformPolicyCurrentProjection() => Name = PlatformProjectionNames.Current;
    public static PlatformPolicyCurrent Create(PlatformPolicyCreated e) => new() { Id = e.PolicyId, Version = 1, LastChangedAtUtc = e.OccurredAtUtc };
    public static void Apply(PlatformPolicyUpdated e, PlatformPolicyCurrent x) { x.AllowAiAnalysis = e.AllowAiAnalysis; x.AllowAiTaskSuggestions = e.AllowAiTaskSuggestions; x.AllowAiTaskMutations = e.AllowAiTaskMutations; Set(x, e.OccurredAtUtc); }
    public static void Apply(AiProviderConfigured e, PlatformPolicyCurrent x) { x.ProviderName = e.ProviderName; x.ProviderEnabled = e.Enabled; Set(x, e.OccurredAtUtc); }
    public static void Apply(PlatformAdminActionAudited e, PlatformPolicyCurrent x) => Set(x, e.OccurredAtUtc);
    private static void Set(PlatformPolicyCurrent x, DateTimeOffset at) { x.Version++; x.LastChangedAtUtc = at; }
}
