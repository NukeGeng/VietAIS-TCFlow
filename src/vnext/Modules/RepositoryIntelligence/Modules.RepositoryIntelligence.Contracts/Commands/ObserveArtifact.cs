namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;

public enum SourceFactKind { File, HttpEndpoint, Contract, DomainEvent, Aggregate, Projection, Message, Dependency }
public sealed record ObserveArtifact(Guid AnalysisRunId, long ExpectedVersion, string Path, SourceFactKind Kind, string Symbol, string? Details, string ActorId, string CorrelationId, string? CausationId = null);
