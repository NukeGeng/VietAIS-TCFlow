using System.Text;

namespace VietAIS.TCFlow.Analyzers.Marten;

internal static class MartenTextParsing
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

    public static IReadOnlyList<string> SplitTopLevel(string source)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var round = 0;
        var curly = 0;
        var square = 0;
        var angle = 0;
        var quote = '\0';
        foreach (var character in source)
        {
            if (quote != '\0')
            {
                current.Append(character);
                if (character == quote)
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

            if (character == ',' && round == 0 && curly == 0 && square == 0 && angle == 0)
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

    public static int FindStatementEnd(string source, int startIndex)
    {
        var round = 0;
        var quote = '\0';
        for (var index = startIndex; index < source.Length; index++)
        {
            var character = source[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (character == '(')
            {
                round++;
            }
            else if (character == ')')
            {
                round--;
            }
            else if (character == ';' && round == 0)
            {
                return index;
            }
        }

        return source.Length;
    }
}
