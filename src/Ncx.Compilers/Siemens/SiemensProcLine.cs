namespace Ncx.Compilers.Siemens;

/// <summary>
/// Scans a RAW PROC line as the Siemens reader reads one (SiemensUnits.ParseProc): an optional block number, PROC, the
/// name, a parameter group in balanced parentheses that may hold nested parentheses, brackets and strings, then the
/// attributes split on blanks up to a comment (840D sl NC-Programmierung SW4.92, p.510-518; controllers siemens.md 8).
/// </summary>
internal static class SiemensProcLine
{
    /// <summary>
    /// The parts of a PROC line; null for a line that is no PROC line.
    /// </summary>
    public static ProcLineParts? Parse(string raw)
    {
        int position = SkipBlanks(raw, 0);
        if (position + 1 < raw.Length && raw[position] is 'N' or 'n' && char.IsAsciiDigit(raw[position + 1]))
        {
            position++;
            while (position < raw.Length && char.IsAsciiDigit(raw[position]))
            {
                position++;
            }

            int afterNumber = SkipBlanks(raw, position);
            if (afterNumber == position)
            {
                return null;
            }

            position = afterNumber;
        }

        if (!raw.AsSpan(position).StartsWith("PROC", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        position += "PROC".Length;
        int nameStart = SkipBlanks(raw, position);
        if (nameStart == position || nameStart >= raw.Length || !IsNameStart(raw[nameStart]))
        {
            return null;
        }

        int nameEnd = nameStart + 1;
        while (nameEnd < raw.Length && (IsNameStart(raw[nameEnd]) || char.IsAsciiDigit(raw[nameEnd])))
        {
            nameEnd++;
        }

        string name = raw.Substring(nameStart, nameEnd - nameStart);
        position = SkipBlanks(raw, nameEnd);
        string? parameters = null;
        if (position < raw.Length && raw[position] == '(')
        {
            int close = ClosingParenthesis(raw, position);
            if (close < 0)
            {
                return new ProcLineParts(name, null, [], false);
            }

            parameters = raw.Substring(position + 1, close - position - 1);
            position = close + 1;
        }
        else if (position == nameEnd && position < raw.Length && raw[position] != ';')
        {
            // A character after the name that neither ends it nor opens the parameters: no PROC line NCX reads.
            return null;
        }

        string rest = raw.Substring(position);
        int comment = rest.IndexOf(';', StringComparison.Ordinal);
        if (comment >= 0)
        {
            rest = rest.Substring(0, comment);
        }

        string[] attributes = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return new ProcLineParts(name, parameters, attributes, true);
    }

    /// <summary>
    /// The parameters of a parameter group split at the commas that stand outside nested parentheses, brackets and
    /// strings: "REAL A=POT(2), REAL B" is two.
    /// </summary>
    public static List<string> SplitParameters(string parameters)
    {
        var parts = new List<string>();
        int depth = 0;
        bool inString = false;
        int start = 0;
        for (int index = 0; index < parameters.Length; index++)
        {
            char character = parameters[index];
            if (character == '"')
            {
                inString = !inString;
            }
            else if (!inString && character is '(' or '[')
            {
                depth++;
            }
            else if (!inString && character is ')' or ']')
            {
                depth--;
            }
            else if (!inString && depth == 0 && character == ',')
            {
                parts.Add(parameters.Substring(start, index - start));
                start = index + 1;
            }
        }

        parts.Add(parameters.Substring(start));
        return parts;
    }

    // The index of the parenthesis that closes the one at position, past nested parentheses, brackets and strings; -1
    // where it does not close.
    private static int ClosingParenthesis(string text, int position)
    {
        int depth = 0;
        bool inString = false;
        for (int index = position; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '"')
            {
                inString = !inString;
            }
            else if (!inString && character is '(' or '[')
            {
                depth++;
            }
            else if (!inString && character is ')' or ']')
            {
                depth--;
                if (depth == 0)
                {
                    return character == ')' ? index : -1;
                }
            }
        }

        return -1;
    }

    private static int SkipBlanks(string text, int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t')
        {
            position++;
        }

        return position;
    }

    private static bool IsNameStart(char character)
    {
        return char.IsAsciiLetter(character) || character == '_';
    }
}
