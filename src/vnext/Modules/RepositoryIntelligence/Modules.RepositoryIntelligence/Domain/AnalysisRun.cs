using VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;

namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Domain;

public sealed class AnalysisRun
{
    private readonly HashSet<string> _artifacts = [];
    private readonly HashSet<string> _changes = [];
    private readonly HashSet<string> _evidence = [];
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string RepositoryId { get; private set; } = string.Empty;
    public string CommitSha { get; private set; } = string.Empty;
    public bool Completed { get; private set; }

    public void Apply(AnalysisStarted e) { Id = e.AnalysisRunId; ProjectId = e.ProjectId; RepositoryId = e.RepositoryId; CommitSha = e.CommitSha; }
    public void Apply(ArtifactObserved e) => _artifacts.Add(Key(e.Path, e.Symbol));
    public void Apply(SourceChangeDetected e) => _changes.Add(e.ChangeKey);
    public void Apply(EvidenceRecorded e) => _evidence.Add(e.EvidenceKey);
    public void Apply(AnalysisCompleted e) => Completed = true;

    public ArtifactObserved Observe(string path, SourceFactKind kind, string symbol, string? details, string actor, string correlation, DateTimeOffset now)
    {
        Identity(actor, correlation); path = Text(path, 1, 1000, nameof(path)); symbol = Text(symbol, 1, 300, nameof(symbol)); if (!_artifacts.Add(Key(path, symbol))) throw new InvalidOperationException("The source artifact was already observed."); _artifacts.Remove(Key(path, symbol));
        EnsureOpen(); return new(Id, path, kind, symbol, Optional(details), actor.Trim(), correlation.Trim(), now);
    }
    public SourceChangeDetected DetectChange(string key, string path, string type, string summary, string actor, string correlation, DateTimeOffset now)
    {
        Identity(actor, correlation); EnsureOpen(); key = Text(key, 2, 300, nameof(key)); if (!_changes.Add(key)) throw new InvalidOperationException("The source change was already recorded."); _changes.Remove(key);
        return new(Id, key, Text(path, 1, 1000, nameof(path)), Text(type, 2, 80, nameof(type)), Text(summary, 2, 2000, nameof(summary)), actor.Trim(), correlation.Trim(), now);
    }
    public EvidenceRecorded RecordEvidence(string key, string sourcePath, string claim, string confidence, string actor, string correlation, DateTimeOffset now)
    {
        Identity(actor, correlation); EnsureOpen(); key = Text(key, 2, 300, nameof(key)); if (!_evidence.Add(key)) throw new InvalidOperationException("The evidence was already recorded."); _evidence.Remove(key);
        return new(Id, key, Text(sourcePath, 1, 1000, nameof(sourcePath)), Text(claim, 2, 2000, nameof(claim)), Text(confidence, 2, 40, nameof(confidence)), actor.Trim(), correlation.Trim(), now);
    }
    public AnalysisCompleted Complete(string actor, string correlation, DateTimeOffset now) { Identity(actor, correlation); EnsureOpen(); return new(Id, actor.Trim(), correlation.Trim(), now); }
    private void EnsureOpen() { if (Completed) throw new InvalidOperationException("The analysis run is already complete."); }
    private static string Key(string path, string symbol) => $"{path}:{symbol}";
    private static void Identity(string actor, string correlation) { ArgumentException.ThrowIfNullOrWhiteSpace(actor); ArgumentException.ThrowIfNullOrWhiteSpace(correlation); }
    private static string Text(string value, int min, int max, string name) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length < min || v.Length > max) throw new ArgumentException($"Value must contain between {min} and {max} characters.", name); return v; }
    private static string? Optional(string? value, int max = 2000) { if (value is null) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", nameof(value)); return v.Length == 0 ? null : v; }
}
