using System.Text.RegularExpressions;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Contracts;

internal sealed record ContractRouteCandidate(Contract Contract, bool Exact);

internal static partial class ContractRouteMatcher
{
    public static IReadOnlyList<ContractRouteCandidate> FindCandidates(
        Contract frontend,
        IEnumerable<Contract> backendContracts)
    {
        var normalizedFrontend = NormalizePath(frontend.Route);
        var exact = backendContracts
            .Where(contract => NormalizePath(contract.Route) == normalizedFrontend)
            .Select(contract => new ContractRouteCandidate(contract, true))
            .OrderBy(candidate => candidate.Contract.Id, StringComparer.Ordinal)
            .ToArray();
        if (exact.Length > 0)
        {
            return exact;
        }

        var frontendSegments = ComparableSegments(frontend.Route);
        return backendContracts
            .Where(contract => HasSuffixRelationship(frontendSegments, ComparableSegments(contract.Route)))
            .Select(contract => new ContractRouteCandidate(contract, false))
            .OrderBy(candidate => candidate.Contract.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool AreEquivalent(string frontendRoute, string backendRoute) =>
        NormalizePath(frontendRoute) == NormalizePath(backendRoute);

    private static string NormalizePath(string route) => '/' + string.Join('/', ParseSegments(route));

    private static IReadOnlyList<string> ComparableSegments(string route) => ParseSegments(route)
        .Where(segment => segment != "api" && segment != "v{version}")
        .ToArray();

    private static IReadOnlyList<string> ParseSegments(string route)
    {
        var path = route.Split(['?', '#'], 2)[0];
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeSegment)
            .ToArray();
    }

    private static string NormalizeSegment(string segment)
    {
        if (ParameterSegmentRegex().IsMatch(segment))
        {
            return "{}";
        }

        return VersionSegmentRegex().IsMatch(segment)
            ? "v{version}"
            : segment.ToLowerInvariant();
    }

    private static bool HasSuffixRelationship(
        IReadOnlyList<string> frontend,
        IReadOnlyList<string> backend)
    {
        if (frontend.Count == 0 || backend.Count == 0)
        {
            return false;
        }

        var suffixLength = Math.Min(frontend.Count, backend.Count);
        for (var index = 1; index <= suffixLength; index++)
        {
            if (!string.Equals(frontend[^index], backend[^index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex(@"^(?:\{[^}]+\}|:[A-Za-z_$][\w$]*|\$\{[^}]+\})$")]
    private static partial Regex ParameterSegmentRegex();

    [GeneratedRegex(@"^v(?:\d+(?:\.\d+)?|\{version(?::[^}]+)?\})$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionSegmentRegex();
}
