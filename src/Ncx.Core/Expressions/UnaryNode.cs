namespace Ncx.Core.Expressions;

/// <summary>
/// An operator with one operand: the unary minus before a power, -$A (language 4.12, unary), or NOT before a
/// comparison, NOT $Q3 (language 4.12, notexpr).
/// </summary>
/// <param name="Operator">The operator.</param>
/// <param name="Operand">What the operator applies to: the power behind the minus, the comparison behind NOT.</param>
public sealed record UnaryNode(UnaryOperator Operator, ExprNode Operand) : ExprNode
{
    /// <summary>
    /// The minus directly before its operand; NOT with one space before its operand; the operand in parentheses where
    /// a tree built in code needs them (D112; language 4.12).
    /// </summary>
    public override string ToCanonical()
    {
        // The unary minus stands directly before its operand, NOT is followed by one space, so that NOT before a
        // function name never reads back as one name (D112).
        string operand = ExprPrecedence.Operand(Operator, Operand);
        return Operator == UnaryOperator.Minus
            ? ExprSymbols.Of(Operator) + operand
            : ExprSymbols.Of(Operator) + " " + operand;
    }
}
