using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.Expressions;

/// <summary>
/// Reads the text of an expression, the part between the braces of a word value, into the tree of language 4.12 by
/// recursive descent: one method per production of the grammar, named after it. A text that is not an expression is
/// one PAR ERROR on the line of its block, with the position inside the expression, and gives no tree
/// (code-guidelines 6).
/// </summary>
public sealed class ExprParser
{
    // The operators of the productions that chain them, as the grammar lists them (language 4.12).
    private static readonly BinaryOperator[] s_orOperators = [BinaryOperator.Or];
    private static readonly BinaryOperator[] s_andOperators = [BinaryOperator.And];
    private static readonly BinaryOperator[] s_sumOperators = [BinaryOperator.Add, BinaryOperator.Subtract];
    private static readonly BinaryOperator[] s_productOperators =
        [BinaryOperator.Multiply, BinaryOperator.Divide, BinaryOperator.Mod];

    // The comparisons of cmpexpr (language 4.12).
    private static readonly BinaryOperator[] s_comparisons =
    [
        BinaryOperator.Equal,
        BinaryOperator.NotEqual,
        BinaryOperator.Less,
        BinaryOperator.LessOrEqual,
        BinaryOperator.Greater,
        BinaryOperator.GreaterOrEqual,
    ];

    private readonly string _text;
    private readonly int _line;
    private readonly Diagnostics _diagnostics;
    private readonly List<ExprToken> _tokens;
    private int _next;

    private ExprParser(string text, int line, Diagnostics diagnostics, List<ExprToken> tokens)
    {
        _text = text;
        _line = line;
        _diagnostics = diagnostics;
        _tokens = tokens;
    }

    // The part the parser looks at; the list ends with an End part, which is never taken.
    private ExprToken Current => _tokens[_next];

    // The expression in its braces, as the messages quote it.
    private string Braced => "{" + _text + "}";

    /// <summary>
    /// Parses the text between the braces of an expression value (language 3, expression; 4.12). The parser stops at
    /// the first problem, so one expression gives at most one ERROR.
    /// </summary>
    /// <param name="text">The expression without its braces: "$Q1 + 20".</param>
    /// <param name="line">The 1-based line of the block the word stands in, which the diagnostic carries (D98).</param>
    /// <param name="diagnostics">The diagnostics of the file; the ERROR is added to them.</param>
    /// <returns>The tree, or null when the text is not an expression of language 4.12.</returns>
    public static ExprNode? Parse(string text, int line, Diagnostics diagnostics)
    {
        List<ExprToken>? tokens = ExprTokenizer.Tokenize(text, line, diagnostics);
        if (tokens is null)
        {
            return null;
        }

        var parser = new ExprParser(text, line, diagnostics, tokens);
        return parser.ParseExpression();
    }

    // expression = "{" expr "}": the braces hold one expr and nothing after it (language 3, 4.12).
    private ExprNode? ParseExpression()
    {
        if (Current.Kind == ExprTokenKind.End)
        {
            Report(DiagnosticCodes.ExpressionEmpty,
                $"The expression {Braced} is empty (language 3, expression; 4.12).");
            return null;
        }

        ExprNode? expr = ParseExpr();
        if (expr is null)
        {
            return null;
        }

        if (Current.Kind != ExprTokenKind.End)
        {
            ReportLeftOver();
            return null;
        }

        return expr;
    }

    // expr = orexpr (language 4.12).
    private ExprNode? ParseExpr()
    {
        return ParseOrExpr();
    }

    // orexpr = andexpr { "OR" andexpr }: OR binds weakest (language 4.12).
    private ExprNode? ParseOrExpr()
    {
        return ParseChain(ParseAndExpr, s_orOperators);
    }

    // andexpr = notexpr { "AND" notexpr } (language 4.12).
    private ExprNode? ParseAndExpr()
    {
        return ParseChain(ParseNotExpr, s_andOperators);
    }

    // notexpr = [ "NOT" ] cmpexpr: NOT stands at most once, before a comparison, and negates all of it
    // (language 4.12).
    private ExprNode? ParseNotExpr()
    {
        if (!Take(ExprSymbols.Of(UnaryOperator.Not)))
        {
            return ParseCmpExpr();
        }

        ExprNode? comparison = ParseCmpExpr();
        if (comparison is null)
        {
            return null;
        }

        return new UnaryNode(UnaryOperator.Not, comparison);
    }

    // cmpexpr = sum [ ( "==" | "!=" | "<" | "<=" | ">" | ">=" ) sum ]: at most one comparison of two sums, so a
    // second comparison behind the first is an ERROR, not a chain (language 4.12).
    private ExprNode? ParseCmpExpr()
    {
        ExprNode? left = ParseSum();
        if (left is null)
        {
            return null;
        }

        if (TakeOperator(s_comparisons) is not BinaryOperator comparison)
        {
            return left;
        }

        ExprNode? right = ParseSum();
        if (right is null)
        {
            return null;
        }

        if (PeekOperator(s_comparisons) is not null)
        {
            Report(DiagnosticCodes.ExpressionChainedComparison,
                $"The comparison {Place(Current)} of {Braced} follows another comparison; comparisons do not chain, "
                + "join two of them with AND (language 4.12, cmpexpr).");
            return null;
        }

        return new BinaryNode(comparison, left, right);
    }

    // sum = product { ( "+" | "-" ) product } (language 4.12).
    private ExprNode? ParseSum()
    {
        return ParseChain(ParseProduct, s_sumOperators);
    }

    // product = unary { ( "*" | "/" | "MOD" ) unary } (language 4.12).
    private ExprNode? ParseProduct()
    {
        return ParseChain(ParseUnary, s_productOperators);
    }

    // unary = [ "-" ] power: the minus applies to the whole power behind it, so -2 ^ 2 is the minus of 2 ^ 2
    // (language 4.12).
    private ExprNode? ParseUnary()
    {
        if (!Take(ExprSymbols.Of(UnaryOperator.Minus)))
        {
            return ParsePower();
        }

        ExprNode? power = ParsePower();
        if (power is null)
        {
            return null;
        }

        return new UnaryNode(UnaryOperator.Minus, power);
    }

    // power = primary [ "^" unary ]: the exponent is a unary, which holds the next power, so 2 ^ 3 ^ 2 is 2 ^ (3 ^ 2)
    // and the exponent may carry its own minus (language 4.12).
    private ExprNode? ParsePower()
    {
        ExprNode? primary = ParsePrimary();
        if (primary is null || !Take(ExprSymbols.Of(BinaryOperator.Power)))
        {
            return primary;
        }

        ExprNode? exponent = ParseUnary();
        if (exponent is null)
        {
            return null;
        }

        return new BinaryNode(BinaryOperator.Power, primary, exponent);
    }

    // primary = number | variable | function "(" expr { "," expr } ")" | "(" expr ")" (language 4.12).
    private ExprNode? ParsePrimary()
    {
        ExprToken token = Current;
        if (token.Kind == ExprTokenKind.Number)
        {
            _next++;
            return new NumberNode(token.Text);
        }

        if (token.Is("$"))
        {
            return ParseVariable();
        }

        if (token.Is("("))
        {
            return ParseParentheses();
        }

        // A name that is none of the operator words AND, OR, NOT and MOD can only start a function call
        // (language 4.12).
        if (token.Kind == ExprTokenKind.Name && !ExprSymbols.IsOperatorWord(token.Text))
        {
            return ParseCall();
        }

        Report(DiagnosticCodes.ExpressionMissingOperand,
            $"An operand is missing {Place(token)} of {Braced}: a number, a $variable, a function call or an "
            + "expression in parentheses (language 4.12, primary).");
        return null;
    }

    // variable = "$" addr [ "[" expr "]" ]: the name of the variable after the dollar sign, and for a register or a
    // table row the index in brackets, itself an expression (language 4.9, 4.12, D51).
    private VariableNode? ParseVariable()
    {
        ExprToken dollar = Current;
        _next++;
        ExprToken name = Current;
        if (name.Kind != ExprTokenKind.Name)
        {
            Report(DiagnosticCodes.ExpressionMissingVariableName,
                $"The $ {Place(dollar)} of {Braced} is not followed by a variable name such as $Q1 "
                + "(language 4.9, 4.12, variable).");
            return null;
        }

        _next++;
        ExprToken openBracket = Current;
        if (!Take("["))
        {
            return new VariableNode(name.Text, null);
        }

        // Only a SYS_ name takes an index, which selects a register or table row; an index on any other variable is
        // plainly wrong text and an ERROR of the parser, so that every command reports it (D145; language 4.12).
        if (!SystemNames.IsSystem(name.Text))
        {
            Report(DiagnosticCodes.ExpressionIndexNeedsSystemVariable,
                $"${name.Text} {Place(name)} of {Braced} has an index, and only a SYS_ name has registers or table "
                + "rows for an index to select (language 4.12, D145).");
            return null;
        }

        ExprNode? index = ParseExpr();
        if (index is null)
        {
            return null;
        }

        if (!Take("]"))
        {
            Report(DiagnosticCodes.ExpressionUnbalancedBracket,
                $"The bracket {Place(openBracket)} of {Braced} is not closed: ] is expected {Place(Current)} "
                + "(language 4.12, variable).");
            return null;
        }

        // An index written as a literal number is a whole number of 0 or more; $SYS_WEAR_Z[-1] and
        // $SYS_WEAR_Z[99.5] are ERRORs, since no writer emits them (D145). A computed index is rounded when it is
        // evaluated.
        if (LiteralIndexOf(index) is decimal literal && (literal < 0 || literal != decimal.Truncate(literal)))
        {
            Report(DiagnosticCodes.ExpressionIndexNotAWholeNumber,
                $"The index {index} {Place(openBracket)} of {Braced} is no whole number of 0 or more; a literal index "
                + "selects a register or table row by its number (language 4.12, D145).");
            return null;
        }

        return new VariableNode(name.Text, index);
    }

    // The number an index writes as a literal, a number with or without a minus and parentheses around it; null for
    // a computed index (D145).
    private static decimal? LiteralIndexOf(ExprNode index)
    {
        return index switch
        {
            ParenthesesNode parentheses => LiteralIndexOf(parentheses.Inner),
            UnaryNode { Operator: UnaryOperator.Minus } minus => -LiteralIndexOf(minus.Operand),
            NumberNode number when decimal.TryParse(number.Text, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal value) => value,
            _ => null,
        };
    }

    // function "(" expr { "," expr } ")": one of the seventeen names, then at least one argument, the arguments
    // separated by commas and closed by a parenthesis (language 4.12, primary and function).
    private CallNode? ParseCall()
    {
        ExprToken name = Current;
        _next++;
        ExprFunction? function = ExprSymbols.FunctionNamed(name.Text);
        ExprToken openParenthesis = Current;
        if (!Take("("))
        {
            ReportBareName(name, function);
            return null;
        }

        if (function is null)
        {
            Report(DiagnosticCodes.ExpressionUnknownFunction,
                $"{name.Text} {Place(name)} of {Braced} is not a function of language 4.12, which knows "
                + $"{ExprSymbols.FunctionList()}.");
            return null;
        }

        var arguments = new List<ExprNode>();
        do
        {
            ExprNode? argument = ParseExpr();
            if (argument is null)
            {
                return null;
            }

            arguments.Add(argument);
        }
        while (Take(","));

        if (!Take(")"))
        {
            Report(DiagnosticCodes.ExpressionUnbalancedParenthesis,
                $"The parenthesis {Place(openParenthesis)} of {Braced} is not closed: , or ) is expected "
                + $"{Place(Current)} (language 4.12, primary).");
            return null;
        }

        // D118: ATAN2 takes two arguments, MIN and MAX two or more, every other function one. A wrong count is an
        // ERROR of the parser, like an unknown function, so format, convert, check and compile all report it.
        return ArgumentCountFits(function.Value, arguments.Count, name)
            ? new CallNode(function.Value, arguments)
            : null;
    }

    // The number of arguments each function of language 4.12 takes (D118). False after the ERROR.
    private bool ArgumentCountFits(ExprFunction function, int given, ExprToken name)
    {
        bool atLeastTwo = function is ExprFunction.Min or ExprFunction.Max;
        int expected = function == ExprFunction.Atan2 ? 2 : 1;
        if (atLeastTwo ? given >= 2 : given == expected)
        {
            return true;
        }

        string takes = atLeastTwo
            ? "two arguments or more"
            : expected == 1 ? "one argument" : "two arguments";
        Report(DiagnosticCodes.ExpressionArgumentCount,
            $"{name.Text} {Place(name)} of {Braced} is given "
            + $"{given.ToString(CultureInfo.InvariantCulture)} arguments, and {name.Text} takes {takes} "
            + "(language 4.12, D118).");
        return false;
    }

    // "(" expr ")": parentheses group an expression, and the tree keeps them as written (language 4.12, primary).
    private ParenthesesNode? ParseParentheses()
    {
        ExprToken openParenthesis = Current;
        _next++;
        ExprNode? inner = ParseExpr();
        if (inner is null)
        {
            return null;
        }

        if (!Take(")"))
        {
            Report(DiagnosticCodes.ExpressionUnbalancedParenthesis,
                $"The parenthesis {Place(openParenthesis)} of {Braced} is not closed: ) is expected {Place(Current)} "
                + "(language 4.12, primary).");
            return null;
        }

        return new ParenthesesNode(inner);
    }

    // The productions orexpr, andexpr, sum and product: an operand, then any number of operators of the production,
    // each with the next operand, grouped from the left, so 1 - 2 + 3 is (1 - 2) + 3 (language 4.12).
    private ExprNode? ParseChain(Func<ExprNode?> parseOperand, BinaryOperator[] operators)
    {
        ExprNode? left = parseOperand();
        if (left is null)
        {
            return null;
        }

        while (TakeOperator(operators) is BinaryOperator binaryOperator)
        {
            ExprNode? right = parseOperand();
            if (right is null)
            {
                return null;
            }

            left = new BinaryNode(binaryOperator, left, right);
        }

        return left;
    }

    // The operator of the list that the current part spells, taken; null when it spells none of them.
    private BinaryOperator? TakeOperator(BinaryOperator[] operators)
    {
        BinaryOperator? found = PeekOperator(operators);
        if (found is not null)
        {
            _next++;
        }

        return found;
    }

    // The operator of the list that the current part spells, not taken; null when it spells none of them.
    private BinaryOperator? PeekOperator(BinaryOperator[] operators)
    {
        foreach (BinaryOperator candidate in operators)
        {
            if (Current.Is(ExprSymbols.Of(candidate)))
            {
                return candidate;
            }
        }

        return null;
    }

    // Takes the current part when it is the symbol or the word with this spelling.
    private bool Take(string spelling)
    {
        if (!Current.Is(spelling))
        {
            return false;
        }

        _next++;
        return true;
    }

    // What stands after a complete expression: a parenthesis or a bracket that closes nothing, or a part that does
    // not continue the expression (language 4.12).
    private void ReportLeftOver()
    {
        ExprToken token = Current;
        if (token.Is(")"))
        {
            Report(DiagnosticCodes.ExpressionUnbalancedParenthesis,
                $"The parenthesis {Place(token)} of {Braced} closes nothing (language 4.12, primary).");
        }
        else if (token.Is("]"))
        {
            Report(DiagnosticCodes.ExpressionUnbalancedBracket,
                $"The bracket {Place(token)} of {Braced} closes nothing (language 4.12, variable).");
        }
        else
        {
            Report(DiagnosticCodes.ExpressionUnexpectedToken,
                $"{token.Text} {Place(token)} of {Braced} does not continue the expression: an operator or the end is "
                + "expected (language 4.12).");
        }
    }

    // A name without $ and without parentheses: a function lacks its arguments, anything else most likely the $ of a
    // variable (language 4.9, 4.12, primary).
    private void ReportBareName(ExprToken name, ExprFunction? function)
    {
        if (function is not null)
        {
            Report(DiagnosticCodes.ExpressionBareName,
                $"{name.Text} {Place(name)} of {Braced} is a function and takes its arguments in parentheses, as in "
                + $"{name.Text}(...) (language 4.12, primary).");
            return;
        }

        Report(DiagnosticCodes.ExpressionBareName,
            $"{name.Text} {Place(name)} of {Braced} is neither a variable nor a function call; a variable is read "
            + $"with $, as in ${name.Text} (language 4.9, 4.12).");
    }

    // Every problem is an ERROR on the line of the block the expression stands in (code-guidelines 6, D98).
    private void Report(string code, string message)
    {
        _diagnostics.Error(_line, code, message);
    }

    // Where a part stands in the expression, as the messages say it.
    private string Place(ExprToken token)
    {
        return ExprTokenizer.Place(token.Position, _text);
    }
}
