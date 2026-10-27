using Ncx.Core.Machine;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The values of the machine file the runtime estimate uses (virtual machine 8, D64): rapid, max_feed and
/// acceleration of every [[axis]], linear and rotary (machine-config 4), block_time, the start value path_mode,
/// corner_speed and rapid_exact_stop of [dynamics] (machine-config 4; D286, D350), accel_time and rpm_max of every
/// spindle table (machine-config 5, D323). The report lists every one of
/// them, so that the maintainer corrects the file instead of the code (implementation 14, risks).
/// </summary>
internal sealed class MachineDynamics
{
    private const double SecondsPerMinute = 60d;

    private readonly MachineConfig _machine;

    /// <summary>
    /// The dynamics of one machine file, or of the built-in default machine of D103, which has none.
    /// </summary>
    public MachineDynamics(MachineConfig machine)
    {
        _machine = machine;
    }

    /// <summary>
    /// True with [dynamics] in the machine file: the trapezoidal profile; without it the estimate falls back to
    /// distance over feed (virtual machine 8).
    /// </summary>
    public bool HasProfile => _machine.Dynamics is not null;

    /// <summary>
    /// block_time: the seconds the control needs per block; null when [dynamics] gives none.
    /// </summary>
    public double? BlockTime => _machine.Dynamics?.BlockTime is decimal seconds ? (double)seconds : null;

    /// <summary>
    /// The start value of the path-control state of virtual machine 2.1 that [dynamics] path_mode gives; a [dynamics]
    /// without path_mode starts at exact stop (D350). The estimate reads the state of each motion, not this value.
    /// </summary>
    public PathMode StartPathMode => _machine.Dynamics?.PathMode ?? PathMode.ExactStop;

    /// <summary>
    /// True when the end of a RAPID is a rest point of the path: [dynamics] rapid_exact_stop, else the family default,
    /// a rest point on a Fanuc-family machine (G00 in-position check) and a corner otherwise (D286).
    /// </summary>
    public bool RapidRests => _machine.Dynamics?.RapidExactStop ?? _machine.Machine.Controller == Controller.Fanuc;

    /// <summary>
    /// corner_speed in mm/s, from the mm/min of the file (machine-config 4); 0 without one, which stops in every
    /// corner.
    /// </summary>
    public double CornerSpeed =>
        _machine.Dynamics?.CornerSpeed is decimal perMinute ? (double)perMinute / SecondsPerMinute : 0;

    /// <summary>
    /// The [[axis]] of an axis name of the program (virtual machine 3.8 rule 3); null for an axis the machine does not
    /// declare.
    /// </summary>
    public AxisDef? Axis(string ncxName)
    {
        return _machine.ResolveAxis(ncxName);
    }

    /// <summary>
    /// accel_time of a spindle, from the [spindle.ROLE] table of a role that names it: the seconds from stop to rpm_max
    /// (machine-config 5); null when no table gives one.
    /// </summary>
    /// <param name="spindle">The resource id of the spindle, S1.</param>
    public double? AccelTime(string spindle)
    {
        return SpindleValue(spindle, table => table.AccelTime);
    }

    /// <summary>
    /// rpm_max of a spindle, from the [spindle.ROLE] table of a role that names it (machine-config 5), the speed its
    /// accel_time reaches (D323); null when no table gives one.
    /// </summary>
    /// <param name="spindle">The resource id of the spindle, S1.</param>
    public double? RpmMax(string spindle)
    {
        return SpindleValue(spindle, table => table.RpmMax);
    }

    /// <summary>
    /// True when the machine file gives a [transform] template for the path-control mode, the form a compiler writes
    /// for PATH_MODE (D349); the estimate still follows a mode without one, and the report says so (D350).
    /// </summary>
    public bool CanExpress(PathMode mode)
    {
        return mode == PathMode.Continuous
            ? _machine.Transform?.PathModeContinuous is not null
            : _machine.Transform?.PathModeExactStop is not null;
    }

    /// <summary>
    /// The line of the report that says how the motions are timed.
    /// </summary>
    public string ProfileLine()
    {
        if (_machine.Dynamics is not DynamicsConfig dynamics)
        {
            return "Motions: distance over feed, RAPID, HOME and RETRACT distance over the rapid rate; the machine has "
                + "no [dynamics] (virtual machine 8).";
        }

        // D350: path_mode is the start value of PATH_MODE; without it the state starts at exact stop.
        string pathMode = dynamics.PathMode switch
        {
            PathMode.Continuous => "continuous",
            PathMode.ExactStop => "exact_stop",
            _ => "none, so exact_stop",
        };
        return $"Motions: trapezoidal profile (virtual machine 8, D64), path_mode {pathMode} (the start value of "
            + $"PATH_MODE, D350), corner_speed {ReportText.Number(dynamics.CornerSpeed)} mm/min, "
            + $"block_time {ReportText.Number(dynamics.BlockTime)} s.";
    }

    /// <summary>
    /// The line of the report that names where the path comes to rest under CONTINUOUS (D286).
    /// </summary>
    public string RestPointLine()
    {
        string rapid = _machine.Dynamics?.RapidExactStop switch
        {
            true => "a rest point ([dynamics] rapid_exact_stop = true)",
            false => "a corner ([dynamics] rapid_exact_stop = false)",
            _ when RapidRests => "a rest point (Fanuc family, G00 in-position check)",
            _ => "a corner (no Fanuc family)",
        };
        return "Rest points under CONTINUOUS (D286): DWELL, a spindle start or stop, TOOL_BEGIN and TOOL_END, STOP, "
            + "program and file events, a motion not timed, a direction reversal, CYCLE_DWELL, a cycle call not timed, "
            + "every SYNC and every block without path motion except comments, variable computations, LABEL, JUMP "
            + $"and CALL; the end of a RAPID is {rapid}.";
    }

    /// <summary>
    /// The values of every linear axis of the machine, which the profile limits and accelerates by (virtual machine 8),
    /// in the units of machine-config 4.
    /// </summary>
    public TextTable LinearAxisTable()
    {
        return AxisTable(AxisKind.Linear, "rapid (mm/min)", "max_feed (mm/min)", "acceleration (mm/s^2)");
    }

    /// <summary>
    /// The line of the report above the table of the rotary axes: where the estimate uses their values and how it
    /// converts them (D290).
    /// </summary>
    public static string RotaryAxisLine()
    {
        return "Rotary axes: deg/min and deg/s^2 (machine-config 4); they limit and accelerate a motion only in the "
            + "polar or cylinder plane, where their word is a length (virtual machine 3.1, D102): under CYLINDER=r at "
            + "r, 1 deg = pi r / 180 mm, under POLAR=ON at L, the closest approach of the block to the centre of "
            + "rotation; a RAPID there takes the rapid of the linear axes (D290).";
    }

    /// <summary>
    /// The values of every rotary axis of the machine in deg/min and deg/s^2 (machine-config 4, D290): a motion in the
    /// polar or cylinder plane limits and accelerates by them, converted to the length of the plane (MovingAxes).
    /// </summary>
    public TextTable RotaryAxisTable()
    {
        // The report carries every value the estimate used (implementation 14, risks), and a rotary axis sets the
        // limits of a motion in the polar and the cylinder plane (virtual machine 3.1, 8, D102). Every rotary axis is
        // listed, because a motion in that plane can name any of them, C or the C2 of a sub spindle (virtual machine
        // 3.8 rule 3).
        return AxisTable(AxisKind.Rotary, "rapid (deg/min)", "max_feed (deg/min)", "acceleration (deg/s^2)");
    }

    /// <summary>
    /// accel_time and rpm_max of every spindle of the machine, which a spindle start, stop or speed change adds
    /// (machine-config 5, D323).
    /// </summary>
    public TextTable SpindleTable()
    {
        var table = new TextTable("spindle", "accel_time (s)", "rpm_max");
        foreach (ResourceDef resource in _machine.Resources)
        {
            if (resource.IsSpindle)
            {
                decimal? accelTime = AccelTime(resource.Id) is double seconds ? (decimal)seconds : null;
                decimal? rpmMax = RpmMax(resource.Id) is double rpm ? (decimal)rpm : null;
                table.Add(resource.Id, ReportText.Number(accelTime), ReportText.Number(rpmMax));
            }
        }

        return table;
    }

    // The value of the first [spindle.ROLE] table of a role that names the spindle and gives it.
    private double? SpindleValue(string spindle, Func<FunctionTable, decimal?> value)
    {
        foreach (KeyValuePair<string, string> role in _machine.Roles)
        {
            if (role.Value == spindle
                && _machine.SpindleTables.TryGetValue(role.Key, out FunctionTable? table)
                && value(table) is decimal number)
            {
                return (double)number;
            }
        }

        return null;
    }

    // The axes of one kind in the order of the machine file, each with its rapid, max_feed and acceleration as the
    // file gives them, none where it gives none.
    private TextTable AxisTable(AxisKind kind, string rapid, string maxFeed, string acceleration)
    {
        var table = new TextTable("axis", rapid, maxFeed, acceleration);
        foreach (AxisDef axis in _machine.Axes)
        {
            if (axis.Kind == kind)
            {
                table.Add(axis.NcxName, ReportText.Number(axis.Rapid), ReportText.Number(axis.MaxFeed),
                    ReportText.Number(axis.Acceleration));
            }
        }

        return table;
    }
}
