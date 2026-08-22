using System.Text.RegularExpressions;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Contracts;

internal static partial class ContractValueNormalization
{
    public static bool TypesAreCompatible(string frontendType, string backendType)
    {
        var frontend = NormalizeType(frontendType);
        var backend = NormalizeType(backendType);
        return frontend == "unknown" || backend == "unknown" || frontend == backend;
    }

    public static IReadOnlyList<string> Validations(ContractField field) => field.Validations
        .Select(NormalizeValidation)
        .Where(validation => validation.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static IReadOnlyList<string> Errors(IEnumerable<string> values) => values
        .Select(value => value.Trim().ToLowerInvariant())
        .Where(value => value.Length > 0 && value is not "error" and not "any")
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static IReadOnlyList<string> Permissions(IEnumerable<string> values) => values
        .Select(NormalizePermission)
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string NormalizeType(string value)
    {
        var type = WhitespaceRegex().Replace(value.Trim().TrimEnd('?'), string.Empty);
        if (type.EndsWith("[]", StringComparison.Ordinal))
        {
            return $"collection:{NormalizeType(type[..^2])}";
        }

        var generic = GenericTypeRegex().Match(type);
        if (generic.Success && IsCollection(generic.Groups["container"].Value))
        {
            return $"collection:{NormalizeType(generic.Groups["argument"].Value)}";
        }

        var simple = type.Split('.').LastOrDefault()?.ToLowerInvariant() ?? string.Empty;
        if (simple is "unknown" or "any" or "object")
        {
            return "unknown";
        }

        if (simple is "number" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or
            "long" or "ulong" or "float" or "double" or "decimal")
        {
            return "number";
        }

        if (simple is "string" or "char" or "guid" or "uri" or "datetime" or "datetimeoffset" or "dateonly" or
            "timeonly")
        {
            return "string";
        }

        return simple is "bool" or "boolean" ? "boolean" : simple;
    }

    private static bool IsCollection(string value) => value.Split('.').Last() is
        "Array" or "List" or "IList" or "IReadOnlyList" or "IEnumerable" or "ICollection" or
        "IReadOnlyCollection";

    private static string NormalizeValidation(string value)
    {
        var rule = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (rule.Length == 0 || rule.Equals("required", StringComparison.OrdinalIgnoreCase) ||
            rule.Equals("notEmpty", StringComparison.OrdinalIgnoreCase) ||
            rule.Equals("notNull", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var separator = rule.IndexOf(':');
        var name = (separator < 0 ? rule : rule[..separator]).ToLowerInvariant();
        var argument = separator < 0 ? string.Empty : rule[(separator + 1)..];
        name = name switch
        {
            "maxlength" or "maximumlength" => "maximumLength",
            "minlength" or "minimumlength" => "minimumLength",
            "min" => "minimumInclusive",
            "max" => "maximumInclusive",
            "greaterthan" => "minimumExclusive",
            "lessthan" => "maximumExclusive",
            _ => name
        };
        return argument.Length == 0 ? name : $"{name}:{argument}";
    }

    private static string NormalizePermission(string value)
    {
        var permission = value.Trim();
        if (permission.Length == 0)
        {
            return string.Empty;
        }

        var parts = permission.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 3 && parts[0].Equals("Permissions", StringComparison.OrdinalIgnoreCase))
        {
            parts = parts[^2..];
        }

        if (parts.Length == 2)
        {
            var resource = parts[0].ToLowerInvariant();
            if (resource.EndsWith('s') && !resource.EndsWith("ss", StringComparison.Ordinal))
            {
                resource = resource[..^1];
            }

            return $"{resource}.{parts[1].ToLowerInvariant()}";
        }

        return permission.ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^(?<container>[A-Za-z_$][\w$.]*)<(?<argument>.+)>$")]
    private static partial Regex GenericTypeRegex();
}
