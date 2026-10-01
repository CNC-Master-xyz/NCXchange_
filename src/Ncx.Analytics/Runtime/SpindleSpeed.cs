using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The speed of the spindle a motion's feed per revolution follows (virtual machine 8): the rpm of RPM, or under CSS
/// the rpm that VC gives at the current X radius, capped by RPM_MAX.
/// </summary>
internal static class SpindleSpeed
{
    private const double MetresPerFoot = 0.3048;
    private const double MillimetresPerMetre = 1000d;

    /// <summary>
    /// The spindle whose revolutions a feed per revolution and CSS follow (virtual machine 8, D288): the spindle of
    /// the holder of the last TOOL, the default spindle for a holder without one, while that spindle turns; while it
    /// stands, the workpiece holder's spindle if that one turns in SPINDLE mode, the spindle a turning tool cuts on.
    /// Null when the machine has none.
    /// </summary>
    /// <param name="vmEvent">The event with the state after its block.</param>
    /// <param name="machine">The machine file, or the default machine of D103.</param>
    public static string? SpindleOf(VmEvent vmEvent, MachineConfig machine)
    {
        ChannelSnapshot state = vmEvent.After;
        string? holderSpindle = vmEvent.After.LastHolder is string holder
            ? machine.FindResource(holder)?.Spindle
            : null;
        holderSpindle ??= machine.ResolveDefaultSpindle()?.Id;
        if (Turns(state, holderSpindle))
        {
            return holderSpindle;
        }

        // D288: on a turret with a driven tool spindle the holder's spindle stands while a turning tool cuts, and the
        // feed per revolution is that of the spindle that turns the workpiece (the master spindle of the controls).
        string? workpieceSpindle = state.Frame.WorkpieceHolder is string workpiece
            ? machine.FindResource(workpiece)?.Spindle ?? workpiece
            : null;
        return Turns(state, workpieceSpindle)
            && state.Spindles[workpieceSpindle!].Mode == SpindleMode.Spindle
                ? workpieceSpindle
                : holderSpindle;
    }

    // A spindle of the machine that turns: a direction other than OFF (virtual machine 2.4).
    private static bool Turns(ChannelSnapshot state, string? spindle)
    {
        return spindle is not null
            && state.Spindles.TryGetValue(spindle, out SpindleSnapshot? speed)
            && speed.Direction != SpindleDirection.Off;
    }

    /// <summary>
    /// The rpm of the spindle a feed per revolution follows during a motion (D288): 0 while it stands, the rpm of RPM
    /// while it turns, under CSS the rpm from VC and the X radius capped by RPM_MAX (virtual machine 8); null when a
    /// value it needs is unknown. Along a motion that changes X the radius is the mean of the start and the end X
    /// radius (D289), which is the one speed per motion the runtime estimate's profile wants.
    /// </summary>
    public static double? RpmOf(MotionEvent motion, MachineConfig machine)
    {
        return RpmOf(motion, machine, Radius.Mean);
    }

    /// <summary>
    /// The lowest and the highest rpm of the tool's spindle along a motion: the rpm itself twice where it does not
    /// change along the motion, under CSS the rpm at the start and at the end X radius, each capped by RPM_MAX
    /// (virtual machine 8, D289). The rpm range of the tool list reads this and not the mean, so that a facing cut
    /// from radius 50 to the centre reports the low rpm it starts at and the capped high rpm it ends at; null when a
    /// value it needs is unknown.
    /// </summary>
    public static (double Low, double High)? RpmRangeOf(MotionEvent motion, MachineConfig machine)
    {
        if (RpmOf(motion, machine, Radius.Start) is not double start
            || RpmOf(motion, machine, Radius.End) is not double end)
        {
            return null;
        }

        return (Math.Min(start, end), Math.Max(start, end));
    }

    // The rpm at one radius of the motion, the mean for the runtime estimate and the two ends for the rpm range of
    // the tool list (D289); the radius matters under CSS only.
    private static double? RpmOf(MotionEvent motion, MachineConfig machine, Radius which)
    {
        ChannelSnapshot state = motion.After;
        if (SpindleOf(motion, machine) is not string spindle
            || !state.Spindles.TryGetValue(spindle, out SpindleSnapshot? speed))
        {
            return null;
        }

        if (speed.Direction == SpindleDirection.Off)
        {
            return 0;
        }

        if (!speed.Css)
        {
            return state.Unknown.Contains("RPM:" + spindle) ? null : (double)speed.Rpm;
        }

        return CssRpm(motion, spindle, speed, which);
    }

    // Under CSS the rpm follows from VC and the current X radius, capped by RPM_MAX (virtual machine 8): VC in m/min,
    // or ft/min under UNITS=INCH (language 4.11), over the circumference at the radius, which the virtual machine
    // stores (D28, D60).
    // D289: along a motion that changes X the runtime estimate takes the mean of the start and the end X radius,
    // because the control regulates the rpm continuously from the current X; for a motion at constant VC that stays
    // below the cap the mean radius gives the exact time, while the start radius alone nearly doubles a facing cut to
    // the centre. The rpm range of the tool list takes the two extremes instead, the rpm at the start and at the end
    // radius, each capped by RPM_MAX: a range reports what the spindle turned at, not an average.
    // TODO(question): D289 gives one rule for every motion, and the mean is what the code takes for a RAPID too.
    // Fanuc computes the G96 rpm of a G00 block from the end position, because nothing is cut on the way; whether
    // a RAPID under CSS should take the end radius instead of the mean is not settled, and nothing of the estimate
    // depends on it today (virtual machine 8 times a RAPID at the rapid rate, and the tool list counts the rpm of
    // a LINE or an ARC only).
    private static double? CssRpm(MotionEvent motion, string spindle, SpindleSnapshot speed, Radius which)
    {
        ChannelSnapshot state = motion.After;
        if (speed.Vc is not decimal cuttingSpeed || state.Unknown.Contains("VC:" + spindle))
        {
            return null;
        }

        double? limit = speed.RpmMax is decimal rpmMax && !state.Unknown.Contains("RPM_MAX:" + spindle)
            ? (double)rpmMax
            : null;
        if (RadiusOf(motion, motion.From) is not double startRadius
            || RadiusOf(motion, motion.To) is not double endRadius)
        {
            return null;
        }

        double radius = which switch
        {
            Radius.Start => startRadius,
            Radius.End => endRadius,
            _ => (startRadius + endRadius) / 2,
        };
        if (radius <= 0)
        {
            return limit;
        }

        double metresPerMinute = state.Frame.Units == Units.Inch
            ? (double)cuttingSpeed * MetresPerFoot
            : (double)cuttingSpeed;
        double rpm = metresPerMinute * MillimetresPerMetre / (2 * Math.PI * radius);
        return limit is double cap ? Math.Min(rpm, cap) : rpm;
    }

    // The X radius at one end of a motion, in millimetres (D28, D60): the X the virtual machine stores, which is a
    // radius, as an absolute value; null when X is not known there in a frame whose X is a workpiece radius (D289).
    private static double? RadiusOf(MotionEvent motion, IReadOnlyDictionary<string, AxisPosition> at)
    {
        if (!at.TryGetValue("X", out AxisPosition x)
            || !x.Known
            || x.Frame is not (PositionFrame.Workpiece or PositionFrame.Polar))
        {
            return null;
        }

        return Math.Abs((double)x.Value) * CommandedFeed.MillimetresPerUnit(motion);
    }

    // Which X radius of a motion a CSS rpm follows (D289): the mean for the one speed per motion of the runtime
    // estimate, the two ends for the extremes of the tool list's rpm range.
    private enum Radius
    {
        Start,
        Mean,
        End,
    }
}
