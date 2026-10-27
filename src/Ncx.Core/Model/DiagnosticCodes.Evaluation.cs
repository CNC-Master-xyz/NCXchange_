namespace Ncx.Core.Model;

// The codes of the expression evaluator, VM900-VM949 (the ranges in DiagnosticCodes.cs, D98): one ERROR for each way
// an expression that parsed can fail when INTERPRETED mode evaluates it (language 4.12, virtual machine 3.6, 5). Every
// one stops the run on the line of the block the expression stands in. VM904 was the wrong number of arguments of a
// function; D118 moved that check into the expression parser, where it is PAR111, and the number is not reused.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// VM900: a division by zero, the divisor of / or of MOD, or 0 to a negative power (language 4.12).
    /// </summary>
    public const string DivisionByZero = "VM900";

    /// <summary>
    /// VM901: a variable is read before anything assigned it, and the configuration does not set unassigned = 0
    /// (virtual machine 3.6, D38).
    /// </summary>
    public const string UnassignedVariableRead = "VM901";

    /// <summary>
    /// VM902: a string where a number is required, the string a variable holds as an operand or a function argument
    /// (virtual machine 5).
    /// </summary>
    public const string StringWhereNumberIsRequired = "VM902";

    /// <summary>
    /// VM903: a SYS_ name the configuration does not map, or whose state is unknown, read in INTERPRETED mode (virtual
    /// machine 2.7, 3.6, D51).
    /// </summary>
    public const string SystemVariableUnknown = "VM903";

    /// <summary>
    /// VM905: an operation without a real result, SQRT(-1), LN(0), ASIN(2), TAN(90), ATAN2(0, 0), a negative number to
    /// a fractional power (language 4.12).
    /// </summary>
    public const string ResultUndefined = "VM905";

    /// <summary>
    /// VM906: a number or a result beyond the range of the decimal arithmetic the evaluator computes with, about
    /// 7.9E28 either way (language 4.12).
    /// </summary>
    public const string ResultOutOfRange = "VM906";

    /// <summary>
    /// VM907, retired 2026-10-27 (D145): an index on a variable that is not a SYS_ name is an ERROR of the expression
    /// parser now, PAR112. The number is not reused.
    /// </summary>
    [Obsolete("Retired by D145: the index check moved into the expression parser, PAR112.")]
    public const string IndexNeedsSystemVariable = "VM907";

    /// <summary>
    /// VM908: a computed index that selects no register or table row: negative after rounding half away from zero,
    /// or beyond every register (language 4.12, D51, D145).
    /// </summary>
    public const string IndexSelectsNoRegister = "VM908";
}
