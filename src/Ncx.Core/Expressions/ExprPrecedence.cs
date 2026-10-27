namespace Ncx.Core.Expressions;

/// <summary>
/// The binding strength of every node, from the grammar of language 4.12, and the operand text with the parentheses
/// precedence needs. A parsed tree holds its parentheses as ParenthesesNode, so it never needs one more; where a tree
/// built in code lacks them, the printer adds them (D112).
/// </summary>
internal static class ExprPrecedence
{
    // The levels of the grammar, weakest first: orexpr, andexpr, notexpr, cmpexpr, sum, product, unary, power,
    // primary (language 4.12).
    private const int Or = 1;
    private const int And = 2;
    private const int Not = 3;
    private const int Comparison = 4;
    private const int Sum = 5;
    private const int Product = 6;
    private const int Unary = 7;
    private const int Power = 8;
    private const int Primary = 9;

    /// <summary>
    /// The text of the left operand of a binary operator, in parentheses where the grammar would not read it there.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="operand">The left operand.</param>
    public static string Left(BinaryOperator op, ExprNode operand)
    {
        // OR, AND, sums and products bind to the left, so an operand of the same level stands bare on the left; a
        // comparison does not chain, and the left of ^ is a primary (language 4.12, cmpexpr, power).
        int level = LevelOf(op);
        int needed = op switch
        {
            BinaryOperator.Power => Primary,
            _ when level == Comparison => Sum,
            _ => level,
        };
        return Text(operand, needed);
    }

    /// <summary>
    /// The text of the right operand of a binary operator, in parentheses where the grammar would not read it there.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="operand">The right operand.</param>
    public static string Right(BinaryOperator op, ExprNode operand)
    {
        // The right of a left-binding operator is one level stronger; the right of ^ is a unary (language 4.12,
        // power = primary [ "^" unary ]).
        int needed = op == BinaryOperator.Power ? Unary : LevelOf(op) + 1;
        return Text(operand, needed);
    }

    /// <summary>
    /// The text of the operand of a unary operator: a power after the minus, a comparison after NOT (language 4.12,
    /// unary, notexpr).
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="operand">The operand.</param>
    public static string Operand(UnaryOperator op, ExprNode operand)
    {
        return Text(operand, op == UnaryOperator.Minus ? Power : Comparison);
    }

    // The operand as it prints, in parentheses when it binds weaker than its place needs.
    private static string Text(ExprNode operand, int needed)
    {
        string text = operand.ToCanonical();
        return LevelOf(operand) < needed ? "(" + text + ")" : text;
    }

    private static int LevelOf(ExprNode node)
    {
        return node switch
        {
            BinaryNode binary => LevelOf(binary.Operator),
            UnaryNode { Operator: UnaryOperator.Minus } => Unary,
            UnaryNode => Not,
            _ => Primary,
        };
    }

    private static int LevelOf(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Or => Or,
            BinaryOperator.And => And,
            BinaryOperator.Add or BinaryOperator.Subtract => Sum,
            BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Mod => Product,
            BinaryOperator.Power => Power,
            _ => Comparison,
        };
    }
}
