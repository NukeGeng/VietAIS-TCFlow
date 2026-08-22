using System.Text.RegularExpressions;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.AspNet;

internal sealed record CSharpFieldDefinition(
    string Name,
    string Type,
    bool Required,
    SourceLocation Location);

internal sealed record CSharpTypeDefinition(
    string Name,
    string DeclarationKind,
    RepositoryFile File,
    int Index,
    string BaseList,
    string Body,
    IReadOnlyList<CSharpFieldDefinition> Fields,
    IReadOnlyList<CSharpFieldDefinition> ConstructorParameters);

internal sealed record CSharpMethodDefinition(
    string Name,
    RepositoryFile File,
    int Index,
    string Body,
    IReadOnlyList<CSharpFieldDefinition> Parameters);

internal sealed partial class CSharpSourceCatalog
{
    private readonly Dictionary<string, CSharpTypeDefinition> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _constants = new(StringComparer.Ordinal);

    public IEnumerable<CSharpTypeDefinition> Types => _types.Values
        .OrderBy(type => type.File.RelativePath, StringComparer.Ordinal)
        .ThenBy(type => type.Index);

    public bool TryGetType(string name, out CSharpTypeDefinition definition) =>
        _types.TryGetValue(CSharpTextParsing.SimpleTypeName(name), out definition!);

    public string ResolveConstant(string expression)
    {
        var value = expression.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1];
        }

        var name = value.Split('.').LastOrDefault() ?? value;
        return _constants.TryGetValue(name, out var resolved) ? resolved : value;
    }

    public CSharpMethodDefinition? FindMethod(RepositoryFile file, string name)
    {
        var pattern = $@"(?m)^\s*(?:public|internal|private|protected)\s+static\s+(?:async\s+)?[A-Za-z_$][\w$<>,?.\[\]\s]*?\s+{Regex.Escape(name)}\s*\(";
        var match = Regex.Match(file.Content, pattern, RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var openIndex = file.Content.IndexOf('(', match.Index + match.Length - 1);
        var closeIndex = CSharpTextParsing.FindBalancedClose(file.Content, openIndex, '(', ')');
        if (closeIndex < 0)
        {
            return null;
        }

        var parameters = ParseParameters(
            file,
            file.Content[(openIndex + 1)..closeIndex],
            openIndex + 1);
        var bodyOpen = file.Content.IndexOf('{', closeIndex + 1);
        if (bodyOpen < 0)
        {
            var expressionEnd = file.Content.IndexOf(';', closeIndex + 1);
            var body = expressionEnd < 0 ? string.Empty : file.Content[(closeIndex + 1)..expressionEnd];
            return new CSharpMethodDefinition(name, file, match.Index, body, parameters);
        }

        var bodyClose = CSharpTextParsing.FindBalancedClose(file.Content, bodyOpen, '{', '}');
        var methodBody = bodyClose < 0 ? string.Empty : file.Content[(bodyOpen + 1)..bodyClose];
        return new CSharpMethodDefinition(name, file, match.Index, methodBody, parameters);
    }

    public static CSharpSourceCatalog Create(IEnumerable<RepositoryFile> files)
    {
        var catalog = new CSharpSourceCatalog();
        foreach (var file in files.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            foreach (Match constant in StringConstantRegex().Matches(file.Content))
            {
                catalog._constants[constant.Groups["name"].Value] = constant.Groups["value"].Value;
            }

            foreach (Match match in TypeDeclarationRegex().Matches(file.Content))
            {
                var name = match.Groups["name"].Value;
                var cursor = match.Index + match.Length;
                while (cursor < file.Content.Length && char.IsWhiteSpace(file.Content[cursor]))
                {
                    cursor++;
                }

                var constructorParameters = Array.Empty<CSharpFieldDefinition>();
                if (cursor < file.Content.Length && file.Content[cursor] == '(')
                {
                    var closeIndex = CSharpTextParsing.FindBalancedClose(file.Content, cursor, '(', ')');
                    if (closeIndex >= 0)
                    {
                        constructorParameters = ParseParameters(
                            file,
                            file.Content[(cursor + 1)..closeIndex],
                            cursor + 1).ToArray();
                        cursor = closeIndex + 1;
                    }
                }

                var terminator = FindDeclarationTerminator(file.Content, cursor);
                var baseList = terminator.Index > cursor
                    ? file.Content[cursor..terminator.Index].Trim().TrimStart(':').Trim()
                    : string.Empty;
                var body = string.Empty;
                var properties = Array.Empty<CSharpFieldDefinition>();
                if (terminator.Character == '{')
                {
                    var bodyClose = CSharpTextParsing.FindBalancedClose(file.Content, terminator.Index, '{', '}');
                    if (bodyClose >= 0)
                    {
                        body = file.Content[(terminator.Index + 1)..bodyClose];
                        properties = ParseProperties(file, body, terminator.Index + 1).ToArray();
                    }
                }

                var fields = constructorParameters
                    .Concat(properties)
                    .DistinctBy(field => field.Name, StringComparer.Ordinal)
                    .ToArray();
                catalog._types[name] = new CSharpTypeDefinition(
                    name,
                    match.Groups["kind"].Value,
                    file,
                    match.Index,
                    baseList,
                    body,
                    fields,
                    constructorParameters);
            }
        }

        return catalog;
    }

    public static bool TryGetGenericArguments(
        string source,
        string genericType,
        out IReadOnlyList<string> arguments)
    {
        var match = Regex.Match(
            source,
            $@"\b{Regex.Escape(genericType)}\s*<",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            arguments = [];
            return false;
        }

        var openIndex = source.IndexOf('<', match.Index);
        var closeIndex = CSharpTextParsing.FindBalancedClose(source, openIndex, '<', '>');
        if (closeIndex < 0)
        {
            arguments = [];
            return false;
        }

        arguments = CSharpTextParsing.SplitTopLevel(source[(openIndex + 1)..closeIndex]);
        return true;
    }

    private static IReadOnlyList<CSharpFieldDefinition> ParseParameters(
        RepositoryFile file,
        string source,
        int sourceIndex)
    {
        var fields = new List<CSharpFieldDefinition>();
        var searchOffset = 0;
        foreach (var item in CSharpTextParsing.SplitTopLevel(source))
        {
            var cleaned = CSharpTextParsing.RemoveAttributes(item);
            var defaultParts = CSharpTextParsing.SplitTopLevel(cleaned, '=');
            var declaration = defaultParts[0].Trim();
            var defaultValue = defaultParts.Count > 1 ? defaultParts[1].Trim() : null;
            declaration = ParameterModifierRegex().Replace(declaration, string.Empty).Trim();
            var separator = declaration.LastIndexOfAny([' ', '\t', '\r', '\n']);
            if (separator <= 0)
            {
                continue;
            }

            var rawType = declaration[..separator].Trim();
            var name = declaration[(separator + 1)..].Trim();
            if (!IdentifierRegex().IsMatch(name))
            {
                continue;
            }

            var itemIndex = source.IndexOf(item, searchOffset, StringComparison.Ordinal);
            searchOffset = itemIndex < 0 ? searchOffset : itemIndex + item.Length;
            var absoluteIndex = sourceIndex + Math.Max(0, itemIndex);
            fields.Add(new CSharpFieldDefinition(
                name,
                CSharpTextParsing.NormalizeType(rawType),
                !rawType.TrimEnd().EndsWith('?') && defaultValue is not "null" and not "default",
                new SourceLocation(
                    file.RelativePath,
                    CSharpTextParsing.LineNumber(file.Content, absoluteIndex),
                    CSharpTextParsing.LineNumber(file.Content, absoluteIndex),
                    name)));
        }

        return fields;
    }

    private static IReadOnlyList<CSharpFieldDefinition> ParseProperties(
        RepositoryFile file,
        string body,
        int bodyIndex) => PropertyRegex().Matches(body)
        .Select(match =>
        {
            var rawType = match.Groups["type"].Value;
            var name = match.Groups["name"].Value;
            return new CSharpFieldDefinition(
                name,
                CSharpTextParsing.NormalizeType(rawType),
                !rawType.EndsWith('?') && !match.Groups["optional"].Success,
                new SourceLocation(
                    file.RelativePath,
                    CSharpTextParsing.LineNumber(file.Content, bodyIndex + match.Index),
                    CSharpTextParsing.LineNumber(file.Content, bodyIndex + match.Index),
                    name));
        })
        .ToArray();

    private static (int Index, char Character) FindDeclarationTerminator(string source, int startIndex)
    {
        var angle = 0;
        for (var index = startIndex; index < source.Length; index++)
        {
            switch (source[index])
            {
                case '<':
                    angle++;
                    break;
                case '>':
                    angle--;
                    break;
                case '{' or ';' when angle == 0:
                    return (index, source[index]);
            }
        }

        return (source.Length, '\0');
    }

    [GeneratedRegex(@"(?m)^\s*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly)\s+)*(?<kind>record(?:\s+(?:class|struct))?|class|interface)\s+(?<name>[A-Za-z_$][\w$]*)")]
    private static partial Regex TypeDeclarationRegex();

    [GeneratedRegex(@"\bconst\s+string\s+(?<name>[A-Za-z_$][\w$]*)\s*=\s*""(?<value>[^""]*)""")]
    private static partial Regex StringConstantRegex();

    [GeneratedRegex(@"(?m)^\s*(?:public|internal|private|protected)\s+(?<type>[A-Za-z_$][\w$<>,?.\[\]\s]*)\s+(?<name>[A-Za-z_$][\w$]*)\s*\{\s*get\s*;[^}]*\}(?<optional>\s*=\s*(?:null|default))?")]
    private static partial Regex PropertyRegex();

    [GeneratedRegex(@"^(?:(?:ref|out|in|params|this|required)\s+)+")]
    private static partial Regex ParameterModifierRegex();

    [GeneratedRegex(@"^[A-Za-z_$][\w$]*$")]
    private static partial Regex IdentifierRegex();
}
