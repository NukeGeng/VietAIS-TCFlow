using System.Text;

namespace VietAIS.TCFlow.Analyzers.Vue;

internal static class TextParsing
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
        if (openIndex < 0 || openIndex >= source.Length || source[openIndex] != open)
        {
            return string.Empty;
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
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"' or '`')
            {
                quote = character;
                continue;
            }

            if (character == open)
            {
                depth++;
            }
            else if (character == close && --depth == 0)
            {
                return source[(openIndex + 1)..index];
            }
        }

        return string.Empty;
    }

    public static IReadOnlyList<string> SplitTopLevel(string source, char separator = ',')
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var round = 0;
        var curly = 0;
        var square = 0;
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

            if (character is '\'' or '"' or '`')
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
            }

            if (character == separator && round == 0 && curly == 0 && square == 0)
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

    public static bool TryReadLiteral(string expression, out string value, out bool interpolated)
    {
        value = string.Empty;
        interpolated = false;
        var trimmed = expression.Trim();
        if (trimmed.Length < 2 || trimmed[0] is not ('\'' or '"' or '`') || trimmed[^1] != trimmed[0])
        {
            return false;
        }

        value = trimmed[1..^1];
        interpolated = trimmed[0] == '`' && value.Contains("${", StringComparison.Ordinal);
        return true;
    }
}
