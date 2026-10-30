using System.Text;
using Ncx.Config.Templates;
using Ncx.Core.Expressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// Translates an expression of custom macro B into an NCX expression (controllers fanuc.md 7; controller-mapping 6;
/// language 4.9, 4.12): [ ] to ( ), #n to $Vn (D33), a system variable through the [system_variables] of the machine
/// (D51), EQ NE GT GE LT LE to the NCX comparisons, SIN COS TAN ASIN ACOS ATAN SQRT ABS ROUND LN EXP to the NCX
/// functions, FIX to INT and FUP to the rounding away from zero. What NCX cannot express, an indirect variable, the
/// vacant #0, an unmapped system variable, XOR, is a problem that keeps the block as RAW.
/// </summary>
internal sealed class FanucExpression
{
    // Custom macro B nests brackets five deep; the reader follows far more and stops before a runaway nesting.
    private const int MaxNesting = 64;

    private readonly string _text;
    private readonly FanucBlock _block;
    private int _position;
    private int _nesting;
    private string? _problem;

    private FanucExpression(string text, FanucBlock block)
    {
        _text = text;
        _block = block;
    }

    /// <summary>
    /// The NCX text of a Fanuc expression, before the expression parser checks it.
    /// </summary>
    /// <param name="fanuc">The expression as the source writes it, "[#502+#11099]", "-#501".</param>
    /// <param name="block">The block, with the machine and its system variables.</param>
    /// <param name="problem">Why NCX cannot express it; null when it can.</param>
    public static string? ToText(string fanuc, FanucBlock block, out string? problem)
    {
        var expression = new FanucExpression(fanuc, block);
        string text = expression.Sequence('\0');
        problem = expression._problem;
        return problem is null ? text.Trim() : null;
    }

    /// <summary>
    /// An NCX expression value from its text, in the canonical form the parser gives it, so that the program formats to
    /// itself (language 4.12; D91).
    /// </summary>
    /// <param name="text">The NCX expression without its braces.</param>
    /// <param name="block">The block, whose line a problem cites.</param>
    /// <param name="problem">Why the text is no expression of language 4.12; null when it is one.</param>
    public static ExprValue? Parse(string text, FanucBlock block, out string? problem)
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

    // The expression up to the closing bracket, which it reads, or to the end; the bracket pairs of Fanuc become the
    // parentheses of NCX.
    private string Sequence(char closer)
    {
        var text = new StringBuilder();
        if (++_nesting > MaxNesting)
        {
            _problem = $"[{_text}] nests its brackets deeper than {MaxNesting}";
        }

        while (_problem is null && _position < _text.Length)
        {
            char character = _text[_position];
            if (character == closer)
            {
                _position++;
                return text.ToString();
            }

            if (character is ' ' or '\t')
            {
                _position++;
            }
            else if (character == '[')
            {
                _position++;
                text.Append('(').Append(Sequence(']')).Append(')');
            }
            else if (character == '#')
            {
                _position++;
                text.Append(Variable());
            }
            else if (char.IsAsciiDigit(character) || character == '.')
            {
                text.Append(Number());
            }
            else if (char.IsAsciiLetter(character))
            {
                text.Append(Word());
            }
            else if (character is '+' or '-' or '*' or '/')
            {
                _position++;
                text.Append(' ').Append(character).Append(' ');
            }
            else if (character == ',')
            {
                _position++;
                text.Append(", ");
            }
            else
            {
                _problem = $"the character {character} of [{_text}] has no NCX form";
            }
        }

        if (closer != '\0')
        {
            _problem ??= $"a [ of [{_text}] is not closed";
        }

        return text.ToString();
    }

    // #n: local, common and permanent variables are Vn (D33, language 4.9); #1000 and above are the system variables of
    // the control, which the machine maps to SYS_ names (D51, language 4.12).
    private string Variable()
    {
        if (_position < _text.Length && _text[_position] == '[')
        {
            _problem = $"the indirect variable of [{_text}] has no NCX name (language 4.9)";
            return "";
        }

        int start = _position;
        while (_position < _text.Length && char.IsAsciiDigit(_text[_position]))
        {
            _position++;
        }

        string digits = _text.Substring(start, _position - start).TrimStart('0');
        if (digits.Length == 0)
        {
            _problem = "#0, the vacant variable, has no NCX form (language 4.9)";
            return "";
        }

        if (digits.Length <= 3)
        {
            return "$V" + digits;
        }

        return SystemVariable("#" + digits) ?? "";
    }

    // A system variable is the SYS_ name whose native template the machine writes for it, with its index:
    // SYS_WEAR_Z = "#11{index:03}" reads #11099 as $SYS_WEAR_Z[99]; an unmapped one becomes RAW (language 4.12, D51,
    // machine-config 7).
    private string? SystemVariable(string native)
    {
        foreach (KeyValuePair<string, string> entry in _block.Machine.SystemVariables.Entries)
        {
            if (_block.Templates.For(entry.Value) is Template template
                && template.Matches(native, out TemplateValues values))
            {
                WarnOfADiameterPosition(entry.Key, native);
                return values.TryGetNumber("index", out decimal index)
                    ? "$" + entry.Key + "[" + FanucNumbers.Of(index).ToCanonical() + "]"
                    : "$" + entry.Key;
            }
        }

        _problem = $"{native} is a system variable the machine does not map in [system_variables] (language 4.12, D51)";
        return null;
    }

    // D240: the position variables #5021 and #5041 map 1:1 onto $SYS_MPOS_X and $SYS_POS_X, which read a diameter
    // under DIAMETER=ON (D216); whether the control gives a diameter or a radius there is not measured yet, so the
    // reader warns, RDR108, where it maps one on an axis programmed in diameters or switchable.
    private void WarnOfADiameterPosition(string name, string native)
    {
        string? axis = name.StartsWith("SYS_POS_", StringComparison.Ordinal) ? name["SYS_POS_".Length..]
            : name.StartsWith("SYS_MPOS_", StringComparison.Ordinal) ? name["SYS_MPOS_".Length..]
            : null;
        if (axis is not null
            && _block.Machine.ResolveAxis(axis)?.Programming is Programming.Diameter or Programming.Switchable)
        {
            _block.Diagnostics.Warning(_block.Line, DiagnosticCodes.FanucPositionOfADiameterAxis,
                $"{native} is read 1:1 as ${name} of the {axis} axis programmed in diameters, which NCX reads as a "
                + "diameter; whether the control gives the diameter or the radius there is not measured yet (D240, "
                + "D216).");
        }
    }

    // A number with a leading or a trailing dot, 1. and .5, in the form of language 3.
    private string Number()
    {
        int start = _position;
        while (_position < _text.Length && (char.IsAsciiDigit(_text[_position]) || _text[_position] == '.'))
        {
            _position++;
        }

        string written = _text.Substring(start, _position - start);
        Value? number = new SourceWord { Address = "", Text = written }.ToNcxNumber();
        if (number is null)
        {
            _problem = $"{written} of [{_text}] is no number";
            return "";
        }

        return number.ToCanonical();
    }

    // An operator word or a function of custom macro B (controllers fanuc.md 7).
    private string Word()
    {
        int start = _position;
        while (_position < _text.Length && char.IsAsciiLetter(_text[_position]))
        {
            _position++;
        }

        string name = _text.Substring(start, _position - start).ToUpperInvariant();
        switch (name)
        {
            case "EQ":
                return " == ";
            case "NE":
                return " != ";
            case "GT":
                return " > ";
            case "GE":
                return " >= ";
            case "LT":
                return " < ";
            case "LE":
                return " <= ";
            case "AND" or "OR" or "MOD":
                return " " + name + " ";
            case "SIN" or "COS" or "TAN" or "ACOS" or "SQRT" or "ABS" or "ROUND" or "LN" or "EXP":
                return name + "(" + Argument(name) + ")";
            case "ASIN":
                return InTheRangeOfTheControl("ASIN(" + Argument(name) + ")");
            case "FIX":
                // FIX discards the fraction, INT truncates toward zero (language 4.12).
                return "INT(" + Argument(name) + ")";
            case "FUP":
                return RoundedUp();
            case "ATAN":
                return ArcTangent();
            default:
                _problem = $"{name} of [{_text}] has no NCX form (language 4.12)";
                return "";
        }
    }

    // The bracketed argument of a function, SIN[#3].
    private string Argument(string function)
    {
        while (_position < _text.Length && _text[_position] == ' ')
        {
            _position++;
        }

        if (_position >= _text.Length || _text[_position] != '[')
        {
            _problem = $"{function} of [{_text}] has no [ ] argument";
            return "";
        }

        _position++;
        return Sequence(']');
    }

    // FUP raises the fraction to the next integer away from zero, INT plus the sign of the fraction.
    // D119: FRAC(x) = x - INT(x) keeps the sign of x, as the TNC's FRAC beside its truncating INT does, so this form
    // gives FUP[-1.2] = -2, the rounding away from zero (decisions.md; wave-1 question #85).
    private string RoundedUp()
    {
        string argument = Argument("FUP");
        return "(INT(" + argument + ") + SGN(FRAC(" + argument + ")))";
    }

    // ATAN[a]/[b] and ATAN[a,b] are the arc tangent of a over b, ATAN[a] the one of a single argument.
    // D119: ATAN2(y, x) takes the ordinate first, which is the argument order of Fanuc's two-argument ATAN too, so
    // ATAN[a]/[b] reads as ATAN2(a, b) (decisions.md; wave-1 question #86). The two-argument form is converted to the
    // range of the control (D404).
    private string ArcTangent()
    {
        string first = Argument("ATAN");
        int afterFirst = _position;
        while (_position < _text.Length && _text[_position] == ' ')
        {
            _position++;
        }

        if (_position + 1 < _text.Length && _text[_position] == '/' && _text[_position + 1] == '[')
        {
            _position += 2;
            string second = Sequence(']');
            return InTheRangeOfTheControl("ATAN2(" + first + ", " + second + ")");
        }

        _position = afterFirst;
        return first.Contains(',', StringComparison.Ordinal)
            ? InTheRangeOfTheControl("ATAN2(" + first + ")")
            : "ATAN(" + first + ")";
    }

    // D404: under the factory setting of parameter 6004#0 NAT, [variables] atan_nat = false, the two-argument ATAN of
    // the control gives 0 to 360 and ASIN 270 to 0 to 90 (B-64490EN-05 p.340-341), while NCX ATAN2 runs above -180 up
    // to 180 and ASIN -90 to 90; the reader reads the value of the control, (f + 360) MOD 360. Under NAT = 1 the
    // ranges agree and the function is read plain. The parentheses keep the form one operand, 2 * ATAN[a]/[b].
    private string InTheRangeOfTheControl(string function)
    {
        return _block.Machine.Variables.AtanNat ? function : "((" + function + " + 360) MOD 360)";
    }
}
