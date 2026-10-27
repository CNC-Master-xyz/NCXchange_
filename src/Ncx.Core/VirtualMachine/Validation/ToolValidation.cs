using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The tool change and the offsets (language 4.4; virtual machine 2.3, 3.5, 5): the rows of the tool change table
/// (raised by ToolChangeRules) and the offsets given in one form per program. All of them depend on the caller's state
/// and are suppressed inside a subprogram that no program of the file calls (3.9, D99).
/// </summary>
internal static class ToolValidation
{
    // The two forms of the offsets: the combined register, and length and radius apart (language 4.4).
    private const string CombinedForm = "OFFSET";
    private const string SeparateForm = "OFFSET:LEN and OFFSET:RAD";

    // Tool 0 is the empty spindle (language 4.4).
    private static readonly ToolRef s_emptySpindle = new(0);

    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Tool",
        Summary = "The tool change of the two-word model and the offsets (language 4.4; VM 2.3, 3.5, 5; D42).",
        Rules =
        [
            ValidationRule.Warning(DiagnosticCodes.ToolAlreadyInSpindle,
                "Preload of the tool already in the spindle, a no-op on the machine.", "VM 3.5, 5")
                with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.NothingPreloaded, "TOOL without preload.",
                "language 4.4; VM 3.5, 5") with { SuppressedInUncalledSub = true },
            ValidationRule.Warning(DiagnosticCodes.PreloadMismatch,
                "A different tool preloaded than called; the magazine has to cycle twice.", "VM 3.5, 5; D42")
                with { SuppressedInUncalledSub = true },
            ValidationRule.Warning(DiagnosticCodes.ToolChangeWhileCycleActive,
                "TOOL while a cycle is active; the change ends the cycle.", "VM 3.5, 4, 5")
                with { SuppressedInUncalledSub = true },
            ValidationRule.Warning(DiagnosticCodes.ToolChangeWithCompensationOn, "TOOL while compensation is on.",
                "VM 3.5, 5") with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.OffsetFormsMixed,
                "OFFSET mixed with OFFSET:LEN or OFFSET:RAD in one program.", "language 4.4; VM 5")
                with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.PointWordWithoutTool,
                "TOOL_POINT, TIP_OFFSET or POINT_RADIUS on a holder that holds no tool.", "language 4.4; VM 5; D372")
                with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.NegativePointValue, "A negative TIP_OFFSET or POINT_RADIUS.",
                "language 4.4; VM 5; D372"),
            ValidationRule.Error(DiagnosticCodes.ToolPointNotPositive, "TOOL_POINT of 0 or less.",
                "language 4.4; VM 5; D372"),
            ValidationRule.Warning(DiagnosticCodes.TipOffsetUnderTilt,
                "A motion with a TIP_OFFSET under TILT, TILT_AXIS or TCPM=ON: no tip is derived; once per program.",
                "VM 5, 9; D374"),
        ],
    };

    /// <summary>
    /// A motion with a tip offset while TILT, TILT_AXIS or TCPM=ON leaves the tool axis no machine axis: the VM derives
    /// no tip and says so with the WARNING VM484, which the caller reports once per program (D374).
    /// </summary>
    /// <returns>True when the WARNING was reported.</returns>
    public static bool CheckTipUnderTilt(BlockContext context)
    {
        ChannelState state = context.State;
        if (!MotionRules.IsMotion(context.Block)
            || state.LastHolder is not string holderId
            || !state.Holders.TryGetValue(holderId, out HolderState? holder)
            || holder.TipOffset == 0m
            || !TipRules.ToolAxisTilted(state.Frame.Chain, state.Frame.Tcpm))
        {
            return false;
        }

        context.Diagnostics.Warning(context.Block, DiagnosticCodes.TipOffsetUnderTilt,
            string.Create(CultureInfo.InvariantCulture,
                $"TIP_OFFSET={holder.TipOffset} under a tilted tool axis (TILT, TILT_AXIS or TCPM=ON): the tool axis "
                + $"is no machine axis, so the virtual machine derives no tip in this program (D374)."));
        return true;
    }

    /// <summary>
    /// The point words of D372 after the block: a point word on a holder that holds no tool is VM481, a negative
    /// TIP_OFFSET or POINT_RADIUS VM482, a TOOL_POINT of 0 or less VM483. Only VM481 depends on the caller's state and
    /// is suppressed inside a subprogram that no program of the file calls (3.9, D99); a value from an expression is
    /// checked when INTERPRETED mode has resolved it.
    /// </summary>
    public static void CheckPointWords(BlockContext context)
    {
        foreach (Word word in context.Block.Words)
        {
            if (word.Key is not ("TOOL_POINT" or "TIP_OFFSET" or "POINT_RADIUS"))
            {
                continue;
            }

            if (context.ResourceOf.TryGetValue(word, out string? holderId)
                && context.State.Holders[holderId].SpindleTool == s_emptySpindle)
            {
                context.CallerRuleDiagnostics.Error(context.Block, DiagnosticCodes.PointWordWithoutTool,
                    $"{word.ToCanonical()} names a point of the tool on {holderId}, which holds no tool (D372).");
            }

            decimal? value = word.Value switch
            {
                IntegerValue integer => integer.Number,
                DecimalValue number => number.Number,
                _ => null,
            };
            if (word.Key == "TOOL_POINT" && value <= 0)
            {
                context.Diagnostics.Error(context.Block, DiagnosticCodes.ToolPointNotPositive,
                    $"{word.ToCanonical()}: the points of a tool count from 1 (D372).");
            }
            else if (word.Key != "TOOL_POINT" && value < 0)
            {
                context.Diagnostics.Error(context.Block, DiagnosticCodes.NegativePointValue,
                    $"{word.ToCanonical()} is negative; the tip offset and the register radius are not (D372).");
            }
        }
    }

    /// <summary>
    /// A program gives its offsets combined, OFFSET, or as length and radius, OFFSET:LEN and OFFSET:RAD (language 4.4);
    /// the two mixed in one program are an ERROR (virtual machine 5). A subprogram a program calls belongs to it. The
    /// rule depends on the caller's state and is suppressed inside a subprogram that no program of the file calls (3.9,
    /// D99).
    /// </summary>
    /// <param name="context">The block.</param>
    /// <param name="firstLineOfForm">The line where the program gave each form first, by form; the caller empties it
    /// at every PROGRAM=BEGIN.</param>
    public static void CheckOffsetForms(BlockContext context, Dictionary<string, int> firstLineOfForm)
    {
        foreach (Word word in context.Block.Words)
        {
            if (word.Key != "OFFSET")
            {
                continue;
            }

            string form = word.Addr is null ? CombinedForm : SeparateForm;
            string otherForm = word.Addr is null ? SeparateForm : CombinedForm;
            if (firstLineOfForm.TryGetValue(otherForm, out int otherLine))
            {
                context.CallerRuleDiagnostics.Error(context.Block, DiagnosticCodes.OffsetFormsMixed,
                    string.Create(CultureInfo.InvariantCulture,
                        $"{word.ToCanonical()} gives the offset as {form}, and the program gives it as {otherForm} "
                        + $"since line {otherLine}; one program uses one of the two forms (language 4.4, virtual "
                        + $"machine 5)."));
            }

            firstLineOfForm.TryAdd(form, context.Block.Line);
        }
    }
}
