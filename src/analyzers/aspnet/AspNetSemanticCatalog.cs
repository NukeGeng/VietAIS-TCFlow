using System.Globalization;
using System.Text.RegularExpressions;

namespace VietAIS.TCFlow.Analyzers.AspNet;

internal sealed record AspNetValidationRule(
    string Field,
    IReadOnlyList<string> Rules,
    int Index);

internal sealed record AspNetValidatorDefinition(
    CSharpTypeDefinition Type,
    string RequestType,
    IReadOnlyDictionary<string, AspNetValidationRule> Rules);

internal sealed record AspNetHandlerDefinition(
    CSharpTypeDefinition Type,
    string RequestType,
    string ResponseType,
    IReadOnlyList<string> Permissions);

internal sealed partial class AspNetSemanticCatalog
{
    private readonly Dictionary<string, AspNetValidatorDefinition> _validators = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AspNetHandlerDefinition> _handlers = new(StringComparer.Ordinal);

    public bool TryGetValidator(string requestType, out AspNetValidatorDefinition validator) =>
        _validators.TryGetValue(CSharpTextParsing.SimpleTypeName(requestType), out validator!);

    public bool TryGetHandler(string requestType, out AspNetHandlerDefinition handler) =>
        _handlers.TryGetValue(CSharpTextParsing.SimpleTypeName(requestType), out handler!);

    public static AspNetSemanticCatalog Create(CSharpSourceCatalog source)
    {
        var catalog = new AspNetSemanticCatalog();
        foreach (var type in source.Types)
        {
            if (CSharpSourceCatalog.TryGetGenericArguments(
                    type.BaseList,
                    "AbstractValidator",
                    out var validatorArguments) &&
                validatorArguments.Count == 1)
            {
                var requestType = CSharpTextParsing.SimpleTypeName(validatorArguments[0]);
                catalog._validators[requestType] = new AspNetValidatorDefinition(
                    type,
                    requestType,
                    ParseValidationRules(type));
            }

            if (CSharpSourceCatalog.TryGetGenericArguments(
                    type.BaseList,
                    "IRequestHandler",
                    out var handlerArguments) &&
                handlerArguments.Count == 2)
            {
                var requestType = CSharpTextParsing.SimpleTypeName(handlerArguments[0]);
                catalog._handlers[requestType] = new AspNetHandlerDefinition(
                    type,
                    requestType,
                    handlerArguments[1].Trim(),
                    ParsePermissions(type, source));
            }
        }

        return catalog;
    }

    public static string? ResolveResponseType(CSharpSourceCatalog source, string requestType)
    {
        if (!source.TryGetType(requestType, out var definition) ||
            !CSharpSourceCatalog.TryGetGenericArguments(definition.BaseList, "IRequest", out var arguments) ||
            arguments.Count != 1)
        {
            return null;
        }

        return arguments[0].Trim();
    }

    private static IReadOnlyDictionary<string, AspNetValidationRule> ParseValidationRules(
        CSharpTypeDefinition type)
    {
        var rules = new SortedDictionary<string, AspNetValidationRule>(StringComparer.Ordinal);
        foreach (Match match in RuleForRegex().Matches(type.Body))
        {
            var field = match.Groups["field"].Value;
            var values = RuleCallRegex().Matches(match.Groups["chain"].Value)
                .Select(rule => FormatRule(rule.Groups["name"].Value, rule.Groups["argument"].Value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            rules[field] = new AspNetValidationRule(field, values, type.Index + match.Index);
        }

        return rules;
    }

    private static IReadOnlyList<string> ParsePermissions(
        CSharpTypeDefinition type,
        CSharpSourceCatalog source)
    {
        var permissions = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match match in EnsureAuthorizedRegex().Matches(type.Body))
        {
            var openIndex = type.Body.IndexOf('(', match.Index);
            var arguments = CSharpTextParsing.ExtractBalanced(type.Body, openIndex, '(', ')');
            var values = CSharpTextParsing.SplitTopLevel(arguments);
            if (values.Count >= 2)
            {
                permissions.Add(source.ResolveConstant(values[1]));
            }
        }

        return permissions.ToArray();
    }

    private static string FormatRule(string name, string argument)
    {
        var normalized = char.ToLower(name[0], CultureInfo.InvariantCulture) + name[1..];
        var value = argument.Trim();
        return value.Length == 0 ? normalized : $"{normalized}:{value}";
    }

    [GeneratedRegex(@"RuleFor\s*\(\s*[A-Za-z_$][\w$]*\s*=>\s*[A-Za-z_$][\w$]*\.(?<field>[A-Za-z_$][\w$]*)\s*\)(?<chain>[^;]*);")]
    private static partial Regex RuleForRegex();

    [GeneratedRegex(@"\.(?<name>NotEmpty|NotNull|MinimumLength|MaximumLength|Length|GreaterThan|GreaterThanOrEqualTo|LessThan|LessThanOrEqualTo|Matches|EmailAddress)\s*\((?<argument>[^)]*)\)")]
    private static partial Regex RuleCallRegex();

    [GeneratedRegex(@"EnsureAuthorizedAsync\s*\(")]
    private static partial Regex EnsureAuthorizedRegex();
}
