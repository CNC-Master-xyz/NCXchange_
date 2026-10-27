using System.Text;
using System.Text.RegularExpressions;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// Translates a SINUMERIK expression into an NCX expression (controllers siemens.md 8, 11 rule 9; controller-mapping
/// 6; language 4.9, 4.12): R1 and R[1] to $R1, a DEF or GUD name to $NAME, a system variable through the
/// [system_variables] of the machine (D51), == and &lt;&gt; to == and !=, AND OR NOT MOD as they are, SIN COS TAN
/// ASIN ACOS ATAN2 SQRT ABS ROUND LN EXP to the NCX functions, TRUNC to INT, MINVAL and MAXVAL to MIN and MAX, POT to
/// the square, DIV of two number literals of 0 or more to INT of the division (D346). What NCX cannot express, a DIV
/// with an operand that is not a number literal of 0 or more, an indirect R[R1], a string, XOR, the bit
/// operators, the concatenation &lt;&lt;,
/// an unmapped system variable, is a problem that keeps the block as RAW.
/// </summary>
internal static partial class SiemensExpression
{
    // The operators of the translated text that bind less tightly than * / MOD, before which a product chain ends
    // (language 4.12).
    private static readonly string[] s_belowProduct =
        ["+", "-", "==", "!=", "<", "<=", ">", ">=", "AND", "OR", "NOT"];

    // The functions of controller-mapping 6 and their NCX names (language 4.12).
    private static readonly Dictionary<string, string> s_functions = new(StringComparer.Ordinal)
    {
        ["SIN"] = "SIN",
        ["COS"] = "COS",
        ["TAN"] = "TAN",
        ["ASIN"] = "ASIN",
        ["ACOS"] = "ACOS",
        ["ATAN2"] = "ATAN2",
        ["SQRT"] = "SQRT",
        ["ABS"] = "ABS",
        ["TRUNC"] = "INT",
        ["ROUND"] = "ROUND",
        ["LN"] = "LN",
        ["EXP"] = "EXP",
        ["MINVAL"] = "MIN",
        ["MAXVAL"] = "MAX",
    };

    /// <summary>
    /// The NCX value of a SINUMERIK value: a number as written, any other value an NCX expression; null, with the
    /// problem, where NCX cannot express it.
    /// </summary>
    /// <param name="block">The block, with the machine and its system variables.</param>
    /// <param name="text">The value as the source writes it, "R1*2", "-.5".</param>
    /// <param name="problem">Why NCX cannot express it; null when it can.</param>
    public static Value? ValueOf(SiemensBlock block, string text, out string? problem)
    {
        if (SiemensNumbers.Parse(text) is Value number)
        {
            problem = null;
            return number;
        }

        string? ncx = ToText(block, text, out problem);
        return ncx is null ? null : Parse(block, ncx, out problem);
    }

    /// <summary>
    /// An NCX expression value from its text, in the canonical form the parser gives it, so that the program formats to
    /// itself (language 4.12; D91).
    /// </summary>
    /// <param name="block">The block, whose line a problem cites.</param>
    /// <param name="text">The NCX expression without its braces.</param>
    /// <param name="problem">Why the text is no expression of language 4.12; null when it is one.</param>
    public static ExprValue? Parse(SiemensBlock block, string text, out string? problem)
    {
        var scratch = new Diagnostics(block.Diagnostics.File);
        ExprNode? tree = ExprParser.Parse(text, block.Line, scratch);
        if (tree is null)
        {
            problem = $"{{{text}}} is no expression of language 4.12";
            return null;
        }

        problem = null;
        return new ExprValue(tree.ToCanonical(), tree);
    }

    /// <summary>
    /// The NCX text of a SINUMERIK expression, before the expression parser checks it; null, with the problem, where
    /// NCX cannot express it.
    /// </summary>
    /// <param name="block">The block, with the machine and its system variables.</param>
    /// <param name="siemens">The expression as the source writes it.</param>
    /// <param name="problem">Why NCX cannot express it; null when it can.</param>
    public static string? ToText(SiemensBlock block, string siemens, out string? problem)
    {
        problem = null;
        string text = Translate(block, siemens, ref problem);
        return problem is null ? text.Trim() : null;
    }

    private static string Translate(SiemensBlock block, string text, ref string? problem)
    {
        var output = new StringBuilder();
        int divisions = 0;
        int position = 0;
        while (problem is null && position < text.Length)
        {
            char character = text[position];
            if (character is ' ' or '\t')
            {
                position++;
            }
            else if (char.IsAsciiDigit(character) || character == '.')
            {
                int end = SiemensScanner.ReadNumber(text, position);
                Value? number = end < 0 ? null : SiemensNumbers.Parse(text.Substring(position, end - position));
                if (number is null)
                {
                    problem = $"{text.Substring(position)} is no number";
                    break;
                }

                output.Append(number.ToCanonical());
                position = end;
            }
            else if (character == '(')
            {
                int end = SiemensScanner.ReadGroup(text, position);
                if (end < 0)
                {
                    problem = $"the parenthesis of {text} does not close";
                    break;
                }

                output.Append('(').Append(Translate(block, text.Substring(position + 1, end - position - 2),
                    ref problem)).Append(')');
                position = end;
            }
            else if (character == ',')
            {
                CloseDivisions(output, ref divisions);
                output.Append(", ");
                position++;
            }
            else if (SiemensScanner.StartsIdentifier(character))
            {
                position = Identifier(block, text, position, output, ref divisions, ref problem);
            }
            else
            {
                position = Operator(text, position, output, ref divisions, ref problem);
            }
        }

        CloseDivisions(output, ref divisions);
        return output.ToString();
    }

    // Closes the INT( of every integer division of the product chain that ends here: DIV takes the operands it stands
    // between, so the next operator of the chain works on its result (D346).
    private static void CloseDivisions(StringBuilder output, ref int divisions)
    {
        output.Append(')', divisions);
        divisions = 0;
    }

    // a DIV b is the quotient truncated towards zero, which language 4.12 writes as INT(a / b) (D346): the INT( opens
    // in front of the product chain the left operand belongs to, and CloseDivisions closes it where that chain ends.
    private static void OpenDivision(StringBuilder output, ref int divisions)
    {
        CloseDivisions(output, ref divisions);
        output.Insert(ProductStart(output.ToString()), "INT(");
        output.Append(" / ");
        divisions = 1;
    }

    // The manuals show DIV only for operands that are not negative (840D sl NC-Programmierung SW4.92 p.449: 7 DIV 4.1
    // = 1; HLL training 2010 p.242: 3 DIV 4 = 0), and none says whether -7 DIV 2 is -3 or -4 (manual audit
    // 2026-10-26, D346). D382 keeps that: DIV converts where both operands are number literals of 0 or more, or
    // integer-valued literals of either sign whose quotient is exact, where both roundings agree; any other DIV keeps
    // the block RAW with the WARNING RDR506 (D5), a visible RAW rather than a guess at the rounding (D382).
    private static string DivisionRounding(SiemensBlock block, string text)
    {
        var warning = new SiemensWarning(DiagnosticCodes.SiemensDivisionRoundingUnknown,
            $"{text.Trim()} divides with DIV an operand that is not a number literal of 0 or more, and how the control "
            + "rounds DIV for a negative operand, towards zero or towards minus infinity, is not documented; the block "
            + "is kept as RAW (D346, D5).");
        if (!block.Draft.ReadingWarnings.Contains(warning))
        {
            block.Draft.ReadingWarnings.Add(warning);
        }

        return $"the rounding of DIV for a negative operand is not documented, so {text.Trim()} has no NCX form (D346)";
    }

    // True when a DIV converts (D346, D382): both operands are number literals, the left one the product chain at
    // the end of the translated text, the right one the number that follows DIV in the source, and either both are
    // of 0 or more, or a unary minus stands in front of one or both, both are integer-valued and the divisor divides
    // the dividend exactly.
    private static bool DivisionConverts(string translated, string text, int afterDiv)
    {
        int start = ProductStart(translated);
        string left = translated.Substring(start).Trim();
        if (!NonNegativeLiteral().IsMatch(left))
        {
            return false;
        }

        bool negative = false;
        string before = translated.Substring(0, start).TrimEnd();
        if (before.EndsWith('-'))
        {
            string rest = before.Substring(0, before.Length - 1).TrimEnd();
            string last = rest.Length == 0 ? "" : rest.Substring(rest.LastIndexOf(' ') + 1);
            negative = !(last.Length > 0 && Array.IndexOf(s_belowProduct, last) < 0
                && last is not ("*" or "/" or "MOD") && !last.EndsWith('(') && !last.EndsWith(','));
        }

        int minus = DivisorMinus(text, afterDiv);
        int right = SiemensScanner.SkipBlanks(text, minus < 0 ? afterDiv : minus + 1);
        if (right >= text.Length || !(char.IsAsciiDigit(text[right]) || text[right] == '.'))
        {
            return false;
        }

        int end = SiemensScanner.ReadNumber(text, right);
        decimal? divisor = end > right ? SiemensNumbers.NumberOf(SiemensNumbers.Parse(text[right..end])) : null;
        if (divisor is null)
        {
            return false;
        }

        if (!negative && minus < 0)
        {
            return true;
        }

        decimal? dividend = SiemensNumbers.NumberOf(SiemensNumbers.Parse(left));
        decimal by = divisor.Value;
        return dividend is { } whole && whole == decimal.Truncate(whole) && by == decimal.Truncate(by) && by != 0
            && whole % by == 0;
    }

    // Where the unary minus of the divisor stands in the source, the first character after DIV other than a blank;
    // -1 when the divisor has none.
    private static int DivisorMinus(string text, int afterDiv)
    {
        int index = SiemensScanner.SkipBlanks(text, afterDiv);
        return index < text.Length && text[index] == '-' ? index : -1;
    }

    // Where the product chain at the end of the translated text begins: after the last operator of lower precedence
    // than * / MOD that stands outside parentheses, after the last argument comma, or at the start (language 4.12).
    private static int ProductStart(string text)
    {
        int depth = 0;
        for (int index = text.Length - 1; index >= 0; index--)
        {
            char character = text[index];
            if (character == ')')
            {
                depth++;
            }
            else if (character == '(')
            {
                depth--;
            }
            else if (depth != 0)
            {
                continue;
            }
            else if (character == ',')
            {
                int start = index + 1;
                while (start < text.Length && text[start] == ' ')
                {
                    start++;
                }

                return start;
            }
            else if (character == ' ')
            {
                int token = index;
                while (token > 0 && text[token - 1] is not (' ' or ','))
                {
                    token--;
                }

                if (Array.IndexOf(s_belowProduct, text.Substring(token, index - token)) >= 0)
                {
                    return index + 1;
                }

                index = token;
            }
        }

        return 0;
    }

    // The comparisons and the arithmetic of controllers siemens.md 8; <> is != (language 4.12); << concatenates
    // strings, which NCX has no operator for (controller-mapping 6).
    private static int Operator(string text, int position, StringBuilder output, ref int divisions,
        ref string? problem)
    {
        string two = position + 1 < text.Length ? text.Substring(position, 2) : "";
        string? ncx = two switch
        {
            "==" => "==",
            "<>" => "!=",
            "<=" => "<=",
            ">=" => ">=",
            _ => null,
        };
        if (ncx is not null)
        {
            CloseDivisions(output, ref divisions);
            output.Append(' ').Append(ncx).Append(' ');
            return position + 2;
        }

        char character = text[position];
        if (two == "<<" || character is not ('+' or '-' or '*' or '/' or '<' or '>'))
        {
            problem = two == "<<"
                ? "<< concatenates strings, which NCX has no operator for (controller-mapping 6)"
                : $"the character {character} of {text} has no NCX form";
            return position + 1;
        }

        CloseDivisions(output, ref divisions);
        output.Append(' ').Append(character).Append(' ');
        return position + 1;
    }

    // A name: an operator word, a function with its arguments, an R parameter, a system variable, or a variable.
    private static int Identifier(SiemensBlock block, string text, int position, StringBuilder output,
        ref int divisions, ref string? problem)
    {
        int end = SiemensScanner.ReadIdentifier(text, position);
        string name = text.Substring(position, end - position).ToUpperInvariant();
        int next = SiemensScanner.SkipBlanks(text, end);
        switch (name)
        {
            case "AND" or "OR" or "NOT" or "MOD":
                CloseDivisions(output, ref divisions);
                output.Append(' ').Append(name).Append(' ');
                return end;

            // D346: DIV is the integer division, 3 DIV 4 = 0, and NCX writes it INT(a / b), since INT of language
            // 4.12 truncates towards zero (decisions.md; controller-mapping 6, functions).
            case "DIV":
                CloseDivisions(output, ref divisions);
                int divisor = DivisorMinus(text, end);
                if (!DivisionConverts(output.ToString(), text, end))
                {
                    problem = DivisionRounding(block, text);
                    return end;
                }

                OpenDivision(output, ref divisions);
                if (divisor < 0)
                {
                    return end;
                }

                // D382: the minus of a negative divisor belongs to the divisor, not to the chain after it.
                output.Append('-');
                return divisor + 1;

            case "XOR" or "B_AND" or "B_OR" or "B_NOT" or "B_XOR" or "TRUE" or "FALSE":
                problem = $"{name} has no NCX form (language 4.12; controller-mapping 6)";
                return end;
        }

        if (next < text.Length && text[next] == '(')
        {
            return Function(block, text, name, next, output, ref problem);
        }

        string index = "";
        if (end < text.Length && text[end] == '[')
        {
            int indexEnd = SiemensScanner.ReadGroup(text, end);
            if (indexEnd < 0)
            {
                problem = $"the bracket of {text} does not close";
                return text.Length;
            }

            index = text.Substring(end, indexEnd - end).ToUpperInvariant().Replace(" ", "");
            end = indexEnd;
        }

        output.Append(Variable(block, name, index, ref problem));
        return end;
    }

    // SIN(30) is SIN(30), TRUNC is INT, POT(x) the square of x (controller-mapping 6, functions).
    private static int Function(SiemensBlock block, string text, string name, int open, StringBuilder output,
        ref string? problem)
    {
        int end = SiemensScanner.ReadGroup(text, open);
        if (end < 0)
        {
            problem = $"the parenthesis of {name} does not close";
            return text.Length;
        }

        string arguments = Translate(block, text.Substring(open + 1, end - open - 2), ref problem);
        if (name == "POT")
        {
            output.Append("((").Append(arguments).Append(") ^ 2)");
        }
        else if (s_functions.TryGetValue(name, out string? ncx))
        {
            // D119: NCX ATAN2 is Siemens ATAN2, the angle referred to the second argument over four quadrants, so
            // the arguments stay in the order the source writes them (decisions.md).
            output.Append(ncx).Append('(').Append(arguments).Append(')');
        }
        else
        {
            problem = $"{name}() has no NCX function (language 4.12; controller-mapping 6)";
        }

        return end;
    }

    // $AA_IW[X] reads a diameter under DIAMON and maps one to one, while $AA_IM[X] reads a radius where MD20360 bits
    // 6/7 are off, so under DIAMON and DIAM90 the reader doubles it into the diameter NCX reads under DIAMETER=ON;
    // where [[axis]] frames_in_diameter says the control gives diameters it maps one to one (D240, D388;
    // Funktionshandbuch Grundfunktionen 2012 p.901-903, cnc-manuals).
    private static bool MachinePositionInRadius(SiemensBlock block, string native)
    {
        return string.Equals(native, "$AA_IM[X]", StringComparison.OrdinalIgnoreCase)
            && block.Facts.DiameterMode is "DIAMON" or "DIAM90"
            && block.Machine.ResolveAxis("X") is not { FramesInDiameter: true };
    }

    // R1 and R[1] are $R1; an indirect R[R1] is RAW (controller-mapping 6, VAR); a system variable is the SYS_ name
    // the machine maps it to, and unmapped it is RAW (language 4.12, D51); a DEF or GUD name keeps its name (language
    // 4.9).
    private static string Variable(SiemensBlock block, string name, string index, ref string? problem)
    {
        if (name.StartsWith('$'))
        {
            string native = name + index;
            foreach (KeyValuePair<string, string> entry in block.Machine.SystemVariables.Entries)
            {
                if (string.Equals(entry.Value.Replace(" ", ""), native, StringComparison.OrdinalIgnoreCase))
                {
                    return MachinePositionInRadius(block, native) ? "(2 * $" + entry.Key + ")" : "$" + entry.Key;
                }
            }

            problem = $"the system variable {native} is none the machine maps to a SYS_ name (language 4.12, D51)";
            return "";
        }

        if (name == "R" && index.Length > 2
            && SiemensNumbers.WholeNumber(index.Substring(1, index.Length - 2)) is int register && register >= 0)
        {
            return "$R" + register.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (index.Length > 0 || !VariableName().IsMatch(name))
        {
            problem = $"{name}{index} is no variable NCX can name (controller-mapping 6, VAR)";
            return "";
        }

        return "$" + name;
    }

    // A number literal of 0 or more as the translation writes it, 7, 4.1, .5.
    [GeneratedRegex(@"^([0-9]+(\.[0-9]*)?|\.[0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex NonNegativeLiteral();

    // The name of an NCX variable follows ADDR (language 3, 4.9).
    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex VariableName();
}
