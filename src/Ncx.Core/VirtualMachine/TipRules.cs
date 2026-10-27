using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The tip of the tool derived from the measurement point (D374): the position store keeps the programmed point
/// (D369), and the tip is that point less TIP_OFFSET along the tool axis of the active WORKPLANE. A shift along one
/// known machine axis needs no kinematics (virtual machine 9, levels 0 and 1); under TILT, TILT_AXIS or TCPM=ON the
/// tool axis is no machine axis and no tip is derived.
/// </summary>
internal static class TipRules
{
    // The units_default of the tool table against the UNITS of the program (machine-config 1, D376).
    private const decimal MillimetresPerInch = 25.4m;

    /// <summary>
    /// Step 5 of a block: the point words take effect with the next tool-axis motion (language 4.4, D372), a RETRACT,
    /// a CYCLE_CALL, or a RAPID, LINE, ARC or HOME with a word on the tool axis of the active WORKPLANE; the holder
    /// called last then takes its point, TIP_OFFSET and POINT_RADIUS into effect, an UNKNOWN value as null.
    /// </summary>
    public static void Activate(BlockContext context)
    {
        ChannelState state = context.State;
        if (!MotionRules.IsMotion(context.Block)
            || context.Block.Verb is not Word verb
            || state.LastHolder is not string id
            || !state.Holders.TryGetValue(id, out HolderState? holder)
            || !MovesToolAxis(context, verb.Key))
        {
            return;
        }

        holder.ActiveToolPoint = holder.ToolPoint;
        holder.ActiveTipOffset = state.Unknown.Contains("TIP_OFFSET:" + id) ? null : holder.TipOffset;
        holder.ActivePointRadius = state.Unknown.Contains("POINT_RADIUS:" + id) ? null : holder.PointRadius;
    }

    /// <summary>
    /// A length of the tool table in the units of the program: every number of the table is in the machine's
    /// units_default (machine-config 1, D376), and the wear joins a radius in the program's UNITS (D374).
    /// </summary>
    /// <param name="length">The length from the tool table.</param>
    /// <param name="unitsDefault">[machine] units_default, MM or INCH; null when the file names none.</param>
    /// <param name="programUnits">The UNITS of the program.</param>
    public static decimal InProgramUnits(decimal length, string? unitsDefault, Units programUnits)
    {
        return (unitsDefault, programUnits) switch
        {
            ("MM", Units.Inch) => length / MillimetresPerInch,
            ("INCH", Units.Mm) => length * MillimetresPerInch,
            _ => length,
        };
    }

    /// <summary>
    /// The tool axis of a working plane: Z of XY, Y of ZX, X of YZ (language 4.2, WORKPLANE).
    /// </summary>
    public static string ToolAxis(Workplane workplane)
    {
        return workplane switch
        {
            Workplane.ZX => "Y",
            Workplane.YZ => "X",
            _ => "Z",
        };
    }

    /// <summary>
    /// True when the tool axis is no machine axis: a TILT or TILT_AXIS in the frame chain, or TCPM=ON (D374).
    /// </summary>
    public static bool ToolAxisTilted(IReadOnlyList<TransformEntry> chain, bool tcpm)
    {
        if (tcpm)
        {
            return true;
        }

        foreach (TransformEntry entry in chain)
        {
            if (entry.Kind is TransformKind.Tilt or TransformKind.TiltAxis)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The holder of the last TOOL, whose point the motions of the channel are written in (virtual machine 2.3).
    /// </summary>
    public static HolderSnapshot? ActiveHolder(ChannelSnapshot snapshot)
    {
        return snapshot.LastHolder is string id && snapshot.Holders.TryGetValue(id, out HolderSnapshot? holder)
            ? holder
            : null;
    }

    /// <summary>
    /// The tip for the positions of a snapshot: every axis as programmed, the tool axis less the tip offset; null
    /// where no tip is derived, under a tilted tool axis or with a tip offset from an expression (D374).
    /// </summary>
    public static IReadOnlyDictionary<string, AxisPosition>? Tip(ChannelSnapshot snapshot,
        IReadOnlyDictionary<string, AxisPosition> positions)
    {
        // The TIP_OFFSET in effect since the last tool-axis motion (language 4.4, D372); null while UNKNOWN.
        HolderSnapshot? holder = ActiveHolder(snapshot);
        if (ToolAxisTilted(snapshot.Frame.Chain, snapshot.Frame.Tcpm)
            || (holder is not null && holder.ActiveTipOffset is null))
        {
            return null;
        }

        decimal offset = holder?.ActiveTipOffset ?? 0m;

        // A point at the tip has the tip at the programmed point.
        string axis = ToolAxis(snapshot.Frame.Workplane);
        if (offset == 0m || !positions.TryGetValue(axis, out AxisPosition programmed) || !programmed.Known)
        {
            return positions;
        }

        var tip = new Dictionary<string, AxisPosition>(positions, StringComparer.Ordinal)
        {
            [axis] = programmed with { Value = programmed.Value - offset },
        };
        return tip.AsReadOnly();
    }

    // A RETRACT and a CYCLE_CALL move the tool axis; any other motion when one of its axis words names it.
    private static bool MovesToolAxis(BlockContext context, string verb)
    {
        if (verb is "RETRACT" or "CYCLE_CALL")
        {
            return true;
        }

        string name = ToolAxis(context.State.Frame.Workplane);
        string toolAxis = context.Resources.KeyOfAxis(name, context.State) ?? name;
        return context.AxisOf.Values.Contains(toolAxis, StringComparer.Ordinal);
    }
}
