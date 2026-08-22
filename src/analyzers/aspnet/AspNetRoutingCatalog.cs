using System.Text.RegularExpressions;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.AspNet;

internal sealed record AspNetRoutePrefix(
    string Prefix,
    bool RequiresAuthorization,
    RepositoryFile File,
    int Index);

internal sealed partial class AspNetRoutingCatalog
{
    private readonly Dictionary<string, Dictionary<string, AspNetRoutePrefix>> _groupsByFile =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, AspNetRoutePrefix> _extensionPrefixes = new(StringComparer.Ordinal);

    public string CarterRootPrefix { get; private init; } = string.Empty;

    public AspNetRoutePrefix? ResolvePrefix(RepositoryFile file, string receiver, int endpointIndex)
    {
        if (_groupsByFile.TryGetValue(file.RelativePath, out var groups) &&
            groups.TryGetValue(receiver, out var group))
        {
            return group;
        }

        var declaration = ExtensionMethodRegex().Matches(file.Content)
            .Where(match => match.Index < endpointIndex && match.Groups["receiver"].Value == receiver)
            .LastOrDefault();
        if (declaration is null)
        {
            return null;
        }

        return _extensionPrefixes.GetValueOrDefault(declaration.Groups["name"].Value);
    }

    public static AspNetRoutingCatalog Create(IEnumerable<RepositoryFile> files)
    {
        var ordered = files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
        var rootPrefix = FindCarterRootPrefix(ordered);
        var catalog = new AspNetRoutingCatalog { CarterRootPrefix = rootPrefix };

        foreach (var file in ordered)
        {
            var groups = new Dictionary<string, AspNetRoutePrefix>(StringComparer.Ordinal);
            var isCarterModule = file.Content.Contains("CarterModule", StringComparison.Ordinal);
            var modulePrefixMatch = CarterModulePrefixRegex().Match(file.Content);
            var modulePrefix = modulePrefixMatch.Success
                ? modulePrefixMatch.Groups["prefix"].Value
                : string.Empty;

            foreach (Match match in RouteGroupRegex().Matches(file.Content))
            {
                var owner = match.Groups["owner"].Value;
                var path = match.Groups["path"].Value;
                var ownerPrefix = groups.GetValueOrDefault(owner)?.Prefix;
                var prefix = ownerPrefix is not null
                    ? CSharpTextParsing.CombineRoute(ownerPrefix, path)
                    : isCarterModule
                        ? CSharpTextParsing.CombineRoute(rootPrefix, modulePrefix, path)
                        : CSharpTextParsing.CombineRoute(path);
                var statementEnd = CSharpTextParsing.FindStatementEnd(file.Content, match.Index + match.Length);
                var statement = file.Content[match.Index..Math.Min(file.Content.Length, statementEnd + 1)];
                var parent = groups.GetValueOrDefault(owner);
                var inheritedAuthorization = parent?.RequiresAuthorization == true;
                var directAuthorizationIndex = statement.IndexOf("RequireAuthorization", StringComparison.Ordinal);
                groups[match.Groups["variable"].Value] = new AspNetRoutePrefix(
                    prefix,
                    inheritedAuthorization || directAuthorizationIndex >= 0,
                    directAuthorizationIndex >= 0 ? file : parent?.File ?? file,
                    directAuthorizationIndex >= 0 ? match.Index + directAuthorizationIndex : parent?.Index ?? match.Index);
            }

            catalog._groupsByFile[file.RelativePath] = groups;
            foreach (Match registration in ExtensionRegistrationRegex().Matches(file.Content))
            {
                var receiver = registration.Groups["receiver"].Value;
                var method = registration.Groups["method"].Value;
                if (method == "MapCarter" || !groups.TryGetValue(receiver, out var group))
                {
                    continue;
                }

                catalog._extensionPrefixes[method] = group;
            }
        }

        return catalog;
    }

    private static string FindCarterRootPrefix(IEnumerable<RepositoryFile> files)
    {
        foreach (var file in files)
        {
            foreach (Match group in RouteGroupRegex().Matches(file.Content))
            {
                var variable = group.Groups["variable"].Value;
                if (Regex.IsMatch(
                    file.Content,
                    $@"\b{Regex.Escape(variable)}\s*\.\s*MapCarter\s*\(",
                    RegexOptions.CultureInvariant))
                {
                    return group.Groups["path"].Value;
                }
            }
        }

        return string.Empty;
    }

    [GeneratedRegex("""\bvar\s+(?<variable>[A-Za-z_$][\w$]*)\s*=\s*(?<owner>[A-Za-z_$][\w$]*)\s*\.\s*MapGroup\s*\(\s*"(?<path>[^"]*)"\s*\)""")]
    private static partial Regex RouteGroupRegex();

    [GeneratedRegex(""":\s*base\s*\(\s*"(?<prefix>[^"]*)"\s*\)""")]
    private static partial Regex CarterModulePrefixRegex();

    [GeneratedRegex(@"\b(?<receiver>[A-Za-z_$][\w$]*)\s*\.\s*(?<method>Map[A-Za-z_$][\w$]*)\s*\(\s*\)\s*;")]
    private static partial Regex ExtensionRegistrationRegex();

    [GeneratedRegex(@"\b(?<name>Map[A-Za-z_$][\w$]*)\s*\(\s*this\s+IEndpointRouteBuilder\s+(?<receiver>[A-Za-z_$][\w$]*)")]
    private static partial Regex ExtensionMethodRegex();
}
