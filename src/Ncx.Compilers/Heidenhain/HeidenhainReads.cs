using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The variables a block reads in its expressions (language 4.9, 4.12): every name of a VariableNode in the values of
/// its words, the indexes included.
/// </summary>
internal static class HeidenhainReads
{
    /// <summary>
    /// The names the words of the block read, uppercase as the parser keeps them: Q1, QL5.
    /// </summary>
    public static HashSet<string> NamesOf(Block block)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Word word in block.Words)
        {
            if (word.Value is ExprValue { Tree: ExprNode tree })
            {
                Collect(tree, names);
            }
        }

        return names;
    }

    /// <summary>
    /// The names an expression reads, uppercase as the parser keeps them: Q1, SYS_POS_X.
    /// </summary>
    public static HashSet<string> NamesOf(ExprNode tree)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        Collect(tree, names);
        return names;
    }

    private static void Collect(ExprNode node, HashSet<string> names)
    {
        switch (node)
        {
            case VariableNode variable:
                names.Add(variable.Name);
                if (variable.Index is ExprNode index)
                {
                    Collect(index, names);
                }

                break;
            case ParenthesesNode group:
                Collect(group.Inner, names);
                break;
            case UnaryNode unary:
                Collect(unary.Operand, names);
                break;
            case BinaryNode binary:
                Collect(binary.Left, names);
                Collect(binary.Right, names);
                break;
            case CallNode call:
                foreach (ExprNode argument in call.Arguments)
                {
                    Collect(argument, names);
                }

                break;
        }
    }
}
