namespace VietAIS.TCFlow.Modules.PlatformAdministration.Domain;

public sealed class PlatformPolicy
{
    public Guid Id { get; private set; }
    public bool AllowAiAnalysis { get; private set; }
    public bool AllowAiTaskSuggestions { get; private set; }
    public bool AllowAiTaskMutations { get; private set; }
    public bool ProjectCreationEnabled { get; private set; } = true;
    public bool RepositoryConnectionsEnabled { get; private set; } = true;
    public int MaximumRepositoriesPerProject { get; private set; } = 20;
    public string? ProviderName { get; private set; }
    public bool ProviderEnabled { get; private set; }
    public string? LastAuditAction { get; private set; }
    public void Apply(PlatformPolicyCreated e) => Id = e.PolicyId;
    public void Apply(PlatformPolicyUpdated e) { AllowAiAnalysis = e.AllowAiAnalysis; AllowAiTaskSuggestions = e.AllowAiTaskSuggestions; AllowAiTaskMutations = e.AllowAiTaskMutations; }
    public void Apply(PlatformPolicyImported e) { ProjectCreationEnabled = e.ProjectCreationEnabled; RepositoryConnectionsEnabled = e.RepositoryConnectionsEnabled; MaximumRepositoriesPerProject = e.MaximumRepositoriesPerProject; }
    public void Apply(AiProviderConfigured e) { ProviderName = e.ProviderName; ProviderEnabled = e.Enabled; }
    public void Apply(PlatformAdminActionAudited e) => LastAuditAction = e.Action;

    public PlatformPolicyUpdated Update(bool analysis, bool suggestions, bool mutations, string actor, string correlation, DateTimeOffset now)
    { Identity(actor, correlation); if (mutations && !suggestions) throw new InvalidOperationException("AI mutations require AI task suggestions to be enabled."); return new(Id, analysis, suggestions, mutations, actor.Trim(), correlation.Trim(), now); }
    public AiProviderConfigured ConfigureProvider(string name, bool enabled, string actor, string correlation, DateTimeOffset now)
    { Identity(actor, correlation); ArgumentException.ThrowIfNullOrWhiteSpace(name); var n = name.Trim(); if (n.Length > 120) throw new ArgumentException("Provider name cannot exceed 120 characters.", nameof(name)); return new(Id, n, enabled, actor.Trim(), correlation.Trim(), now); }
    public PlatformAdminActionAudited Audit(string action, string actor, string correlation, DateTimeOffset now)
    { Identity(actor, correlation); ArgumentException.ThrowIfNullOrWhiteSpace(action); return new(Id, action.Trim(), actor.Trim(), correlation.Trim(), now); }
    private static void Identity(string actor, string correlation) { ArgumentException.ThrowIfNullOrWhiteSpace(actor); ArgumentException.ThrowIfNullOrWhiteSpace(correlation); }
}
