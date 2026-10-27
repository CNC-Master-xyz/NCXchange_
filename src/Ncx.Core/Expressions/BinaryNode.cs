namespace Ncx.Core.Expressions;

/// <summary>
/// An operator with two operands: OR, AND, a comparison, a sum, a product or a power (language 4.12, orexpr to power).
/// </summary>
/// <param name="Operator">The operator.</param>
/// <param name="Left">The operand before the operator.</param>
/// <param name="Right">The operand after the operator.</param>
public sealed record BinaryNode(BinaryOperator Operator, ExprNode Left, ExprNode Right) : ExprNode
{
    /// <summary>
    /// The two operands with one space on each side of the operator, AND, OR and MOD included. A parsed tree holds
    /// the parentheses of the expression as ParenthesesNode; where a tree built in code lacks the parentheses its
    /// grouping needs, they are added, so that the text reads back into the same tree grouping (D112; language 4.12).
    /// Compilers bracket from the tree, never from this text.
    /// </summary>
    public override string ToCanonical()
    {
        return ExprPrecedence.Left(Operator, Left) + " " + ExprSymbols.Of(Operator) + " "
            + ExprPrecedence.Right(Operator, Right);
    }
}
