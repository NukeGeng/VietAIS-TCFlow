using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Contracts;

public enum ContractPairStatus
{
    Matched,
    Ambiguous,
    Unmatched
}

public enum ContractMismatchKind
{
    Method,
    Route,
    RequestFieldMissingBackend,
    RequestFieldMissingFrontend,
    RequestFieldType,
    RequestFieldOptionality,
    RequestFieldValidation,
    ResponseFieldMissingBackend,
    ResponseFieldType,
    ResponseFieldOptionality,
    ErrorStates,
    Pagination,
    Authorization
}

public sealed record ContractPair(
    string Id,
    string FrontendContractId,
    string? BackendContractId,
    IReadOnlyList<string> CandidateBackendContractIds,
    ContractPairStatus Status,
    EvidenceLevel EvidenceLevel,
    decimal Confidence,
    string Reason,
    IReadOnlyList<string> EvidenceIds);

public sealed record ContractMismatch(
    string Id,
    string ContractPairId,
    ContractMismatchKind Kind,
    string Subject,
    string FrontendValue,
    string BackendValue,
    EvidenceLevel EvidenceLevel,
    decimal Confidence,
    string Explanation,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<SourceLocation> Locations);

public sealed record ContractComparisonResult(
    IReadOnlyList<ContractPair> Pairs,
    IReadOnlyList<ContractMismatch> Mismatches,
    IReadOnlyList<string> UnmatchedBackendContractIds);
