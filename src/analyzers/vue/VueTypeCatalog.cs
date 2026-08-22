using System.Text.RegularExpressions;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Vue;

internal sealed record VueTypeField(string Name, string Type, bool Required, SourceLocation Location);

internal sealed record VueTypeDefinition(
    string Name,
    string Path,
    int Index,
    IReadOnlyList<VueTypeField> Fields);

internal sealed partial class VueTypeCatalog
{
    private readonly Dictionary<string, VueTypeDefinition> _types = new(StringComparer.Ordinal);

    public IEnumerable<VueTypeDefinition> All => _types.Values
        .OrderBy(type => type.Path, StringComparer.Ordinal)
        .ThenBy(type => type.Name, StringComparer.Ordinal);

    public bool TryGet(string name, out VueTypeDefinition definition) =>
        _types.TryGetValue(CleanTypeName(name), out definition!);

    public static VueTypeCatalog Create(IEnumerable<RepositoryFile> files)
    {
        var catalog = new VueTypeCatalog();
        foreach (var file in files)
        {
            foreach (Match match in InterfaceRegex().Matches(file.Content))
            {
                var fields = new List<VueTypeField>();
                var body = match.Groups["body"];
                foreach (Match field in InterfaceFieldRegex().Matches(body.Value))
                {
                    var index = body.Index + field.Index;
                    fields.Add(new VueTypeField(
                        field.Groups["name"].Value,
                        field.Groups["type"].Value.Trim(),
                        !field.Groups["optional"].Success,
                        new SourceLocation(
                            file.RelativePath,
                            TextParsing.LineNumber(file.Content, index),
                            TextParsing.LineNumber(file.Content, index),
                            field.Groups["name"].Value)));
                }

                var name = match.Groups["name"].Value;
                catalog._types[name] = new VueTypeDefinition(name, file.RelativePath, match.Index, fields);
            }
        }

        return catalog;
    }

    private static string CleanTypeName(string value)
    {
        var cleaned = value.Trim();
        if (cleaned.EndsWith("[]", StringComparison.Ordinal))
        {
            cleaned = cleaned[..^2];
        }

        var genericStart = cleaned.IndexOf('<');
        return genericStart >= 0 ? cleaned[..genericStart].Trim() : cleaned;
    }

    [GeneratedRegex(@"(?:export\s+)?interface\s+(?<name>[A-Za-z_$][\w$]*)\s*\{(?<body>.*?)\}", RegexOptions.Singleline)]
    private static partial Regex InterfaceRegex();

    [GeneratedRegex(@"(?m)^[ \t]*(?<name>[A-Za-z_$][\w$]*)(?<optional>\?)?[ \t]*:[ \t]*(?<type>[^;\r\n]+?)[ \t]*;?[ \t]*$")]
    private static partial Regex InterfaceFieldRegex();
}
