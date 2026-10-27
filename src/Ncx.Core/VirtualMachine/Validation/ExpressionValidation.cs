using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// Expressions and variables (language 4.9, 4.12; virtual machine 1, 2.7, 3.6, 5; D38, D51): the grammar (raised by the
/// expression parser), the assignment of a SYS_ variable, the ERRORs of evaluation in INTERPRETED mode (raised by the
/// evaluator), and the expressions STATIC mode leaves unresolved.
/// </summary>
internal static class ExpressionValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Expression",
        Summary = "Expressions and variables (language 4.9, 4.12; VM 1, 2.7, 3.6, 5; D38, D51). The ERRORs of "
            + "evaluation come in INTERPRETED mode, which evaluates.",
        Rules =
        [
            ValidationRule.Error(DiagnosticCodes.ExpressionUnexpectedCharacter,
                "A character that begins no part of an expression.", "language 4.12"),
            ValidationRule.Error(DiagnosticCodes.ExpressionMalformedNumber,
                "A number without a digit before or after its decimal point.", "language 2 rule 5, 3"),
            ValidationRule.Error(DiagnosticCodes.ExpressionMissingVariableName, "A $ without a variable name.",
                "language 4.9, 4.12"),
            ValidationRule.Error(DiagnosticCodes.ExpressionEmpty, "Nothing between the braces.", "language 3"),
            ValidationRule.Error(DiagnosticCodes.ExpressionMissingOperand, "An operator without its operand.",
                "language 4.12"),
            ValidationRule.Error(DiagnosticCodes.ExpressionUnbalancedParenthesis,
                "A parenthesis opened and not closed, or closed and not opened.", "language 4.12"),
            ValidationRule.Error(DiagnosticCodes.ExpressionUnbalancedBracket,
                "A bracket of an index opened and not closed, or closed and not opened.", "language 4.12"),
            ValidationRule.Error(DiagnosticCodes.ExpressionUnknownFunction,
                "A call of a name that is none of the functions.", "language 4.12"),
            ValidationRule.Error(DiagnosticCodes.ExpressionBareName,
                "A name without $ and without parentheses.", "language 4.9, 4.12"),
            ValidationRule.Error(DiagnosticCodes.ExpressionUnexpectedToken,
                "A part after a complete expression.", "language 4.12"),
            ValidationRule.Error(DiagnosticCodes.ExpressionChainedComparison,
                "A second comparison behind a comparison.", "language 4.12"),
            ValidationRule.Error(DiagnosticCodes.ExpressionArgumentCount,
                "A function with a number of arguments it does not take.", "language 4.12; D118"),
            ValidationRule.Error(DiagnosticCodes.ExpressionIndexNeedsSystemVariable,
                "An index on a variable that is not a SYS_ name.", "language 4.12; D145"),
            ValidationRule.Error(DiagnosticCodes.ExpressionIndexNotAWholeNumber,
                "A literal index that is no whole number of 0 or more.", "language 4.12; D145"),
            ValidationRule.Error(DiagnosticCodes.SystemVariableAssigned, "Assignment to a SYS_ variable.",
                "VM 2.7, 5"),
            ValidationRule.Warning(DiagnosticCodes.UnresolvedExpressions,
                "Unresolved expression in STATIC mode, counted and reported once per run.", "VM 1, 5"),
            ValidationRule.Error(DiagnosticCodes.DivisionByZero, "Division by zero.", "language 4.12; VM 5"),
            ValidationRule.Error(DiagnosticCodes.UnassignedVariableRead,
                "Unassigned variable, unless the configuration sets unassigned = 0.", "VM 3.6, 5; D38"),
            ValidationRule.Error(DiagnosticCodes.StringWhereNumberIsRequired,
                "String where a number is required.", "VM 5"),
            ValidationRule.Error(DiagnosticCodes.SystemVariableUnknown,
                "A SYS_ name the configuration does not map, or an unknown state, read in INTERPRETED mode.",
                "VM 2.7, 3.6; D51"),
            ValidationRule.Error(DiagnosticCodes.ResultUndefined, "An operation without a real result.",
                "language 4.12"),
            ValidationRule.Error(DiagnosticCodes.ResultOutOfRange,
                "A number beyond the range of the decimal arithmetic.", "language 4.12"),
            // VM907 keeps its row as retired, so that the documentation can still cite it (D98, D145).
#pragma warning disable CS0618
            ValidationRule.Error(DiagnosticCodes.IndexNeedsSystemVariable,
                "Retired (D145): an index on a variable that is not a SYS_ name is PAR112.", "language 4.12; D145"),
#pragma warning restore CS0618
            ValidationRule.Error(DiagnosticCodes.IndexSelectsNoRegister,
                "A computed index that selects no register: negative after rounding half away from zero.",
                "language 4.12; D51, D145"),
            ValidationRule.Error(DiagnosticCodes.ValueNotAnInteger,
                "An expression gives a number with decimals where its word takes an integer.", "language 3, 4; VM 5"),
        ],
    };

    /// <summary>
    /// STATIC mode does not evaluate expressions, and a state variable set from one becomes UNKNOWN (virtual machine
    /// 1). Adds the expressions of a block to those of the run; each is counted once, however often the walks of D99
    /// pass its block.
    /// </summary>
    /// <param name="block">An executed block.</param>
    /// <param name="unresolved">The expression words of the run, compared by reference.</param>
    public static void CollectUnresolved(Block block, HashSet<Word> unresolved)
    {
        foreach (Word word in block.Words)
        {
            if (word.Value is ExprValue)
            {
                unresolved.Add(word);
            }
        }
    }

    /// <summary>
    /// An unresolved expression in STATIC mode is a WARNING, counted and reported once, on the block of the first
    /// (virtual machine 5).
    /// </summary>
    public static void ReportUnresolved(int count, Block first, Diagnostics diagnostics)
    {
        string expressions = count == 1
            ? "An expression is"
            : count.ToString(CultureInfo.InvariantCulture) + " expressions are";
        diagnostics.Warning(first, DiagnosticCodes.UnresolvedExpressions,
            $"{expressions} not evaluated in STATIC mode, the first in this block; what they set is UNKNOWN (virtual "
            + "machine 1, 5).");
    }
}
