using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// Linear motion and the machine limits of a motion (virtual machine 3.1, 5; D64, D100): UNITS before the first motion,
/// a feed for LINE, IX from a known position, one form per axis (raised by MotionRules), F in a RAPID block, F above an
/// axis max_feed, a target beyond the axis limits.
/// </summary>
internal static class MotionValidation
{
    // Millimetres per inch: max_feed is a feed per minute in mm/min (machine-config 4), and F under UNITS=INCH is one
    // in inches per minute (language 4.3).
    private const decimal MillimetresPerInch = 25.4m;

    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Motion",
        Summary = "Linear motion and the machine limits of a motion (language 4.3; VM 3.1, 5; machine-config 4; D64, "
            + "D100).",
        Rules =
        [
            ValidationRule.Error(DiagnosticCodes.MotionBeforeUnits, "Motion before UNITS.",
                "language 4.1; VM 3.1, 5") with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.LineWithoutFeed, "LINE without feed.", "VM 3.1, 5")
                with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.IncrementalFromUnknownPosition,
                "IX from an unknown position. In a subprogram nothing calls the position becomes unknown instead.",
                "VM 3.1, 5; D99") with { SuppressedInUncalledSub = true },
            ValidationRule.Error(DiagnosticCodes.TwoFormsOfOneAxis,
                "Two forms of one axis in one block (X=10 IX=5).", "language 4.3"),
            ValidationRule.Warning(DiagnosticCodes.FeedInRapid, "F in a RAPID block.", "VM 5"),
            ValidationRule.Warning(DiagnosticCodes.FeedAboveMaxFeed, "F above an axis max_feed.",
                "VM 5; machine-config 4; D64"),
            ValidationRule.Warning(DiagnosticCodes.TargetBeyondLimits,
                "A target beyond the axis limits, compared in the MACHINE frame and not checked while the machine "
                + "position is unknown.", "VM 5; machine-config 4; D64, D100"),
            ValidationRule.Warning(DiagnosticCodes.LimitClamped,
                "With limits = \"clamp\" in the configuration: RPM, F or a target beyond a machine limit is rewritten "
                + "to the limit, and the WARNING says so.", "VM 5; machine-config 1; D64")
                with { RaisedBy = "the expander" },
        ],
    };

    /// <summary>
    /// F in a RAPID block is a WARNING (virtual machine 5): RAPID moves at the rapid rate (language 4.3), and the F
    /// only sets the feed of the blocks that follow.
    /// </summary>
    public static void CheckFeedInRapid(Block block, Diagnostics diagnostics)
    {
        if (block.Verb?.Key != "RAPID" || block.Find("F") is not Word feed)
        {
            return;
        }

        diagnostics.Warning(block, DiagnosticCodes.FeedInRapid,
            $"RAPID moves at the rapid rate and does not use {feed.ToCanonical()}, which sets the feed of the blocks "
            + "that follow (language 4.3, virtual machine 5).");
    }

    /// <summary>
    /// F above an axis max_feed is a WARNING (virtual machine 5, D64): max_feed is the feed in mm/min that the axis can
    /// follow (machine-config 4). With limits = "clamp" the expander rewrites the value before the block runs and says
    /// so itself (D64).
    /// </summary>
    // TODO(question): virtual machine 5 compares "F above an axis max_feed" without saying which axes (those the block
    // moves, those of the machine), nor how a feed per revolution compares with a feed per minute; F per minute is
    // compared where it is written with the max_feed of every linear axis of the machine, and a feed per revolution is
    // not compared, until D192 is answered.
    public static void CheckFeedAboveMaxFeed(BlockContext context)
    {
        ChannelState state = context.State;
        if (context.Block.Find("F") is not Word feedWord
            || MotionRules.NumberOf(feedWord) is not decimal feed
            || state.Motion.FeedMode != FeedMode.PerMin)
        {
            return;
        }

        decimal feedPerMinute = state.Frame.Units == Units.Inch ? feed * MillimetresPerInch : feed;
        var exceeded = new List<string>();
        foreach (AxisDef axis in context.Machine.Axes)
        {
            if (axis.Kind == AxisKind.Linear && axis.MaxFeed is decimal maxFeed && feedPerMinute > maxFeed)
            {
                exceeded.Add($"{axis.NcxName} ({maxFeed.ToString(CultureInfo.InvariantCulture)} mm/min)");
            }
        }

        if (exceeded.Count == 0)
        {
            return;
        }

        context.Diagnostics.Warning(context.Block, DiagnosticCodes.FeedAboveMaxFeed,
            $"{feedWord.ToCanonical()} is above the max_feed of {string.Join(", ", exceeded)}, the feed the axis can "
            + "follow (virtual machine 5, D64).");
    }

    /// <summary>
    /// A target beyond the axis limits is a WARNING, compared in the MACHINE frame and not checked while the machine
    /// position is unknown (virtual machine 5, D100): the limits are machine coordinates (machine-config 4). Every axis
    /// whose position the motion block changed is compared where it ended. With limits = "clamp" the expander rewrites
    /// the value before the block runs and says so itself (D64).
    /// </summary>
    /// <param name="context">The motion block, after its verb moved the axes.</param>
    /// <param name="before">The position store before the block.</param>
    // D193: the limits of a rotary axis are travel limits like a linear axis's and are compared, unless its [[axis]]
    // carries modulo = true, which makes them its display range (decisions.md; machine-config 4). Whether the machine
    // coordinates of an X axis programmed in diameters are diameters is D137: the stored radius is compared with the
    // limits as written.
    public static void CheckLimits(BlockContext context, IReadOnlyDictionary<string, AxisPosition> before)
    {
        ChannelState state = context.State;
        foreach (KeyValuePair<string, AxisPosition> axisPosition in state.Motion.Position)
        {
            if (before.TryGetValue(axisPosition.Key, out AxisPosition was) && was == axisPosition.Value)
            {
                continue;
            }

            if (context.Machine.ResolveAxis(axisPosition.Key) is not AxisDef axis
                || !HasTravelLimits(axis)
                || FrameRules.MachineCoordinate(state, axisPosition.Key) is not decimal machine)
            {
                continue;
            }

            if (axis.Max is decimal max && machine > max)
            {
                context.Diagnostics.Warning(context.Block, DiagnosticCodes.TargetBeyondLimits,
                    $"{axis.NcxName} ends at {Text(machine)} in the MACHINE frame, beyond its upper limit {Text(max)} "
                    + "(virtual machine 5, D100).");
            }
            else if (axis.Min is decimal min && machine < min)
            {
                context.Diagnostics.Warning(context.Block, DiagnosticCodes.TargetBeyondLimits,
                    $"{axis.NcxName} ends at {Text(machine)} in the MACHINE frame, beyond its lower limit {Text(min)} "
                    + "(virtual machine 5, D100).");
            }
        }
    }

    /// <summary>
    /// Tells whether the machine has an axis with travel limits, the only axes whose targets are compared: every
    /// linear axis, and a rotary axis that is not a modulo axis (D193).
    /// </summary>
    public static bool HasComparedLimits(MachineConfig machine)
    {
        foreach (AxisDef axis in machine.Axes)
        {
            if (HasTravelLimits(axis))
            {
                return true;
            }
        }

        return false;
    }

    // An axis whose limits are travel limits and carry a value: every linear axis, and a rotary axis without
    // modulo = true, whose limits are its display range (machine-config 4, D193).
    private static bool HasTravelLimits(AxisDef axis)
    {
        return !(axis.Kind == AxisKind.Rotary && axis.Modulo) && (axis.Min is not null || axis.Max is not null);
    }

    private static string Text(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
