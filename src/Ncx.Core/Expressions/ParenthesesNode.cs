namespace Ncx.Core.Expressions;

/// <summary>
/// An expression in parentheses, ($Q1 + 1) * 2 (language 4.12, primary = "(" expr ")"). The tree keeps the
/// parentheses where the program wrote them, so that the canonical text never changes the grouping.
/// </summary>
/// <param name="Inner">The expression between the parentheses.</param>
public sealed record ParenthesesNode(ExprNode Inner) : ExprNode
{
    /// <summary>
    /// The inner expression in parentheses without spaces inside them.
    /// </summary>
    public override string ToCanonical()
    {
        // Written parentheses are kept, also where the grammar does not need them, ((1)) (D112; language 2 rule 8).
        return "(" + Inner.ToCanonical() + ")";
    }
}
