using System.Text;
using System.Text.RegularExpressions;

namespace VietAIS.TCFlow.Analyzers.AspNet;

internal static partial class CSharpTextParsing
{
    public static int LineNumber(string source, int index)
    {
        var line = 1;
        for (var position = 0; position < Math.Min(index, source.Length); position++)
        {
            if (source[position] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    public static string ExtractBalanced(string source, int openIndex, char open, char close)
    {
        var closeIndex = FindBalancedClose(source, openIndex, open, close);
        return closeIndex < 0 ? string.Empty : source[(openIndex + 1)..closeIndex];
    }

    public static int FindBalancedClose(string source, int openIndex, char open, char close)
    {
        if (openIndex < 0 || openIndex >= source.Length || source[openIndex] != open)
        {
            return -1;
        }

        var depth = 0;
        var quote = '\0';
        var escaped = false;
        for (var index = openIndex; index < source.Length; index++)
        {
            var character = source[index];
            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (character == open)
            {
                depth++;
            }
            else if (character == close && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    public static IReadOnlyList<string> SplitTopLevel(string source, char separator = ',')
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var round = 0;
        var curly = 0;
        var square = 0;
        var angle = 0;
        var quote = '\0';
        var escaped = false;
        foreach (var character in source)
        {
            if (quote != '\0')
            {
                current.Append(character);
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                current.Append(character);
                continue;
            }

            switch (character)
            {
                case '(':
                    round++;
                    break;
                case ')':
                    round--;
                    break;
                case '{':
                    curly++;
                    break;
                case '}':
                    curly--;
                    break;
                case '[':
                    square++;
                    break;
                case ']':
                    square--;
                    break;
                case '<':
                    angle++;
                    break;
                case '>':
                    angle--;
                    break;
            }

            if (character == separator && round == 0 && curly == 0 && square == 0 && angle == 0)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        if (current.Length > 0)
        {
            values.Add(current.ToString().Trim());
        }

        return values;
    }

    public static bool TryReadRoute(string expression, out string route)
    {
        var value = expression.Trim();
        if (value == "string.Empty")
        {
            route = string.Empty;
            return true;
        }

        if (value.Length >= 2 && value[0] is '\'' or '"' && value[^1] == value[0])
        {
            route = value[1..^1];
            return true;
        }

        route = string.Empty;
        return false;
    }

    public static int FindStatementEnd(string source, int startIndex)
    {
        var round = 0;
        var curly = 0;
        var square = 0;
        var quote = '\0';
        var escaped = false;
        for (var index = Math.Max(0, startIndex); index < source.Length; index++)
        {
            var character = source[index];
            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            switch (character)
            {
                case '(':
                    round++;
                    break;
                case ')':
                    round--;
                    break;
                case '{':
                    curly++;
                    break;
                case '}':
                    curly--;
                    break;
                case '[':
                    square++;
                    break;
                case ']':
                    square--;
                    break;
            }

            if (character == ';' && round == 0 && curly == 0 && square == 0)
            {
                return index;
            }
        }

        return source.Length;
    }

    public static string CombineRoute(params string?[] segments)
    {
        var values = segments
            .Where(segment => !string.IsNullOrWhiteSpace(segment) && segment != "/")
            .Select(segment => segment!.Trim().Trim('/'))
            .Where(segment => segment.Length > 0)
            .ToArray();
        return values.Length == 0 ? "/" : $"/{string.Join('/', values)}";
    }

    public static string NormalizeType(string value)
    {
        var type = value.Trim();
        while (type.EndsWith('?'))
        {
            type = type[..^1].TrimEnd();
        }

        return type.Replace("global::", string.Empty, StringComparison.Ordinal);
    }

    public static string SimpleTypeName(string value)
    {
        var type = NormalizeType(value);
        var generic = type.IndexOf('<');
        if (generic >= 0)
        {
            type = type[..generic];
        }

        var separator = type.LastIndexOf('.');
        return separator >= 0 ? type[(separator + 1)..] : type;
    }

    public static string RemoveAttributes(string value) => AttributeRegex().Replace(value, string.Empty).Trim();

    [GeneratedRegex(@"\[[^\]]+\]")]
    private static partial Regex AttributeRegex();
}
