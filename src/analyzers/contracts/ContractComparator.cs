using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Contracts;

public sealed class ContractComparator
{
    public ContractComparisonResult Compare(
        IReadOnlyCollection<Contract> frontendExpected,
        IReadOnlyCollection<Contract> backendActual)
    {
        ArgumentNullException.ThrowIfNull(frontendExpected);
        ArgumentNullException.ThrowIfNull(backendActual);
        EnsureDirections(frontendExpected, backendActual);

        var backend = backendActual
            .OrderBy(contract => contract.Route, StringComparer.Ordinal)
            .ThenBy(contract => contract.HttpMethod, StringComparer.Ordinal)
            .ThenBy(contract => contract.Id, StringComparer.Ordinal)
            .ToArray();
        var unmatchedBackend = backend.Select(contract => contract.Id).ToHashSet(StringComparer.Ordinal);
        var pairs = new List<ContractPair>();
        var mismatches = new List<ContractMismatch>();

        foreach (var frontend in frontendExpected
                     .OrderBy(contract => contract.Route, StringComparer.Ordinal)
                     .ThenBy(contract => contract.HttpMethod, StringComparer.Ordinal)
                     .ThenBy(contract => contract.Id, StringComparer.Ordinal))
        {
            var pair = Pair(frontend, backend);
            pairs.Add(pair);
            if (pair.Status != ContractPairStatus.Matched || pair.BackendContractId is null)
            {
                continue;
            }

            var actual = backend.Single(contract => contract.Id == pair.BackendContractId);
            unmatchedBackend.Remove(actual.Id);
            ComparePair(frontend, actual, pair, mismatches);
        }

        return new ContractComparisonResult(
            pairs.OrderBy(pair => pair.FrontendContractId, StringComparer.Ordinal).ToArray(),
            mismatches.OrderBy(mismatch => mismatch.ContractPairId, StringComparer.Ordinal)
                .ThenBy(mismatch => mismatch.Kind)
                .ThenBy(mismatch => mismatch.Subject, StringComparer.Ordinal)
                .ToArray(),
            unmatchedBackend.Order(StringComparer.Ordinal).ToArray());
    }

    private static ContractPair Pair(Contract frontend, IReadOnlyList<Contract> backend)
    {
        var candidates = ContractRouteMatcher.FindCandidates(frontend, backend);
        var methodMatches = candidates.Where(candidate => string.Equals(
            candidate.Contract.HttpMethod,
            frontend.HttpMethod,
            StringComparison.OrdinalIgnoreCase)).ToArray();
        var viable = methodMatches.Length > 0 ? methodMatches : candidates.ToArray();
        if (viable.Length == 1)
        {
            var candidate = viable[0];
            var exact = candidate.Exact;
            var inputLevel = LeastCertain(frontend.EvidenceLevel, candidate.Contract.EvidenceLevel);
            var level = exact || inputLevel == EvidenceLevel.Proposed
                ? inputLevel
                : EvidenceLevel.Inferred;
            var confidence = Math.Min(EvidenceConfidence(frontend.EvidenceLevel),
                EvidenceConfidence(candidate.Contract.EvidenceLevel));
            if (!exact)
            {
                confidence = Math.Min(confidence, 0.78m);
            }

            return new ContractPair(
                StableIdentity.Create("contract-pair", frontend.Id, candidate.Contract.Id),
                frontend.Id,
                candidate.Contract.Id,
                [candidate.Contract.Id],
                ContractPairStatus.Matched,
                level,
                confidence,
                PairReason(frontend, candidate.Contract, exact),
                EvidenceIds(frontend, candidate.Contract));
        }

        if (viable.Length > 1)
        {
            var ids = viable.Select(candidate => candidate.Contract.Id).Order(StringComparer.Ordinal).ToArray();
            var level = frontend.EvidenceLevel == EvidenceLevel.Proposed ||
                viable.Any(candidate => candidate.Contract.EvidenceLevel == EvidenceLevel.Proposed)
                    ? EvidenceLevel.Proposed
                    : EvidenceLevel.Inferred;
            return new ContractPair(
                StableIdentity.Create("contract-pair", frontend.Id, "ambiguous", string.Join(',', ids)),
                frontend.Id,
                null,
                ids,
                ContractPairStatus.Ambiguous,
                level,
                0.55m,
                $"{ids.Length} backend contracts have equally plausible route and method evidence.",
                frontend.EvidenceIds.Concat(viable.SelectMany(candidate => candidate.Contract.EvidenceIds))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
        }

        return new ContractPair(
            StableIdentity.Create("contract-pair", frontend.Id, "unmatched"),
            frontend.Id,
            null,
            [],
            ContractPairStatus.Unmatched,
            EvidenceLevel.Proposed,
            0.4m,
            "No backend contract has compatible route evidence.",
            frontend.EvidenceIds.Order(StringComparer.Ordinal).ToArray());
    }

    private static void ComparePair(
        Contract frontend,
        Contract backend,
        ContractPair pair,
        ICollection<ContractMismatch> mismatches)
    {
        if (!string.Equals(frontend.HttpMethod, backend.HttpMethod, StringComparison.OrdinalIgnoreCase))
        {
            AddMismatch(mismatches, pair, ContractMismatchKind.Method, "method", frontend.HttpMethod,
                backend.HttpMethod, "Frontend and backend use different HTTP methods.", [], frontend, backend);
        }

        if (!ContractRouteMatcher.AreEquivalent(frontend.Route, backend.Route))
        {
            AddMismatch(mismatches, pair, ContractMismatchKind.Route, "route", frontend.Route, backend.Route,
                "Frontend and backend routes are not structurally identical.", [], frontend, backend);
        }

        CompareFields(frontend.RequestFields, backend.RequestFields, true, frontend, backend, pair, mismatches);
        CompareFields(frontend.ResponseFields, backend.ResponseFields, false, frontend, backend, pair, mismatches);

        var frontendErrors = ContractValueNormalization.Errors(frontend.ErrorStates);
        var backendErrors = ContractValueNormalization.Errors(backend.ErrorStates);
        if (!frontendErrors.SequenceEqual(backendErrors, StringComparer.Ordinal))
        {
            AddMismatch(mismatches, pair, ContractMismatchKind.ErrorStates, "errors",
                Display(frontendErrors), Display(backendErrors), "Documented error states differ.", [], frontend,
                backend);
        }

        if (frontend.HasPagination != backend.HasPagination)
        {
            AddMismatch(mismatches, pair, ContractMismatchKind.Pagination, "pagination",
                frontend.HasPagination.ToString().ToLowerInvariant(), backend.HasPagination.ToString().ToLowerInvariant(),
                "Pagination expectations differ.", [], frontend, backend);
        }

        var frontendPermissions = ContractValueNormalization.Permissions(frontend.Permissions);
        var backendPermissions = ContractValueNormalization.Permissions(backend.Permissions);
        if (!frontendPermissions.SequenceEqual(backendPermissions, StringComparer.Ordinal))
        {
            AddMismatch(mismatches, pair, ContractMismatchKind.Authorization, "authorization",
                Display(frontendPermissions), Display(backendPermissions), "Authorization requirements differ.", [],
                frontend, backend);
        }
    }

    private static void CompareFields(
        IReadOnlyList<ContractField> frontendFields,
        IReadOnlyList<ContractField> backendFields,
        bool request,
        Contract frontendContract,
        Contract backendContract,
        ContractPair pair,
        ICollection<ContractMismatch> mismatches)
    {
        var frontend = frontendFields.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
        var backend = backendFields.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var expected in frontend.Values.OrderBy(field => field.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!backend.TryGetValue(expected.Name, out var actual))
            {
                AddMismatch(
                    mismatches,
                    pair,
                    request ? ContractMismatchKind.RequestFieldMissingBackend :
                        ContractMismatchKind.ResponseFieldMissingBackend,
                    expected.Name,
                    FieldValue(expected),
                    "<missing>",
                    request
                        ? $"Frontend sends request field '{expected.Name}' but the backend does not accept it."
                        : $"Frontend expects response field '{expected.Name}' but the backend does not expose it.",
                    [expected.Location],
                    frontendContract,
                    backendContract);
                continue;
            }

            CompareField(expected, actual, request, frontendContract, backendContract, pair, mismatches);
        }

        if (!request)
        {
            return;
        }

        foreach (var actual in backend.Values
                     .Where(field => field.Required && !frontend.ContainsKey(field.Name))
                     .OrderBy(field => field.Name, StringComparer.OrdinalIgnoreCase))
        {
            AddMismatch(
                mismatches,
                pair,
                ContractMismatchKind.RequestFieldMissingFrontend,
                actual.Name,
                "<missing>",
                FieldValue(actual),
                $"Backend requires request field '{actual.Name}' but the frontend does not send it.",
                [actual.Location],
                frontendContract,
                backendContract);
        }
    }

    private static void CompareField(
        ContractField frontend,
        ContractField backend,
        bool request,
        Contract frontendContract,
        Contract backendContract,
        ContractPair pair,
        ICollection<ContractMismatch> mismatches)
    {
        var locations = new[] { frontend.Location, backend.Location };
        if (!ContractValueNormalization.TypesAreCompatible(frontend.Type, backend.Type))
        {
            AddMismatch(mismatches, pair,
                request ? ContractMismatchKind.RequestFieldType : ContractMismatchKind.ResponseFieldType,
                frontend.Name, frontend.Type, backend.Type, $"Field '{frontend.Name}' has incompatible types.",
                locations, frontendContract, backendContract);
        }

        if (frontend.Required != backend.Required)
        {
            AddMismatch(mismatches, pair,
                request ? ContractMismatchKind.RequestFieldOptionality :
                    ContractMismatchKind.ResponseFieldOptionality,
                frontend.Name,
                frontend.Required ? "required" : "optional",
                backend.Required ? "required" : "optional",
                $"Field '{frontend.Name}' has different optionality.", locations, frontendContract, backendContract);
        }

        if (!request)
        {
            return;
        }

        var frontendValidations = ContractValueNormalization.Validations(frontend);
        var backendValidations = ContractValueNormalization.Validations(backend);
        if (!frontendValidations.SequenceEqual(backendValidations, StringComparer.Ordinal))
        {
            AddMismatch(mismatches, pair, ContractMismatchKind.RequestFieldValidation, frontend.Name,
                Display(frontendValidations), Display(backendValidations),
                $"Request field '{frontend.Name}' has different validation constraints.", locations,
                frontendContract, backendContract);
        }
    }

    private static void AddMismatch(
        ICollection<ContractMismatch> mismatches,
        ContractPair pair,
        ContractMismatchKind kind,
        string subject,
        string frontendValue,
        string backendValue,
        string explanation,
        IEnumerable<SourceLocation> locations,
        Contract frontend,
        Contract backend)
    {
        var evidenceIds = EvidenceIds(frontend, backend);
        mismatches.Add(new ContractMismatch(
            StableIdentity.Create("contract-mismatch", pair.Id, kind.ToString(), subject, frontendValue, backendValue),
            pair.Id,
            kind,
            subject,
            frontendValue,
            backendValue,
            pair.EvidenceLevel,
            pair.Confidence,
            explanation,
            evidenceIds,
            locations.Distinct().OrderBy(location => location.Path, StringComparer.Ordinal)
                .ThenBy(location => location.StartLine)
                .ThenBy(location => location.Symbol, StringComparer.Ordinal)
                .ToArray()));
    }

    private static string[] EvidenceIds(Contract frontend, Contract backend) => frontend.EvidenceIds
        .Concat(backend.EvidenceIds)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string FieldValue(ContractField field) =>
        $"{field.Type} ({(field.Required ? "required" : "optional")})";

    private static string PairReason(Contract frontend, Contract backend, bool exact)
    {
        var methodMatches = string.Equals(
            frontend.HttpMethod,
            backend.HttpMethod,
            StringComparison.OrdinalIgnoreCase);
        return (exact, methodMatches) switch
        {
            (true, true) => "Method-compatible contracts have the same normalized route.",
            (true, false) => "A unique backend contract has the same normalized route.",
            (false, true) => "A unique method-compatible backend contract has a suffix-compatible route.",
            _ => "A unique backend contract has a suffix-compatible route."
        };
    }

    private static string Display(IEnumerable<string> values)
    {
        var items = values.ToArray();
        return items.Length == 0 ? "<none>" : string.Join(',', items);
    }

    private static EvidenceLevel LeastCertain(EvidenceLevel first, EvidenceLevel second) =>
        (EvidenceLevel)Math.Max((int)first, (int)second);

    private static decimal EvidenceConfidence(EvidenceLevel level) => level switch
    {
        EvidenceLevel.Confirmed => 1m,
        EvidenceLevel.Inferred => 0.72m,
        _ => 0.5m
    };

    private static void EnsureDirections(
        IEnumerable<Contract> frontend,
        IEnumerable<Contract> backend)
    {
        if (frontend.Any(contract => contract.Direction != ContractDirection.FrontendExpected))
        {
            throw new ArgumentException("All frontend contracts must use FrontendExpected direction.",
                nameof(frontend));
        }

        if (backend.Any(contract => contract.Direction != ContractDirection.BackendActual))
        {
            throw new ArgumentException("All backend contracts must use BackendActual direction.", nameof(backend));
        }
    }
}
