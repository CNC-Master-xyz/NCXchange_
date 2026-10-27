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
        HolderSnapshot? holder = ActiveHolder(snapshot);
        decimal offset = holder?.TipOffset ?? 0m;
        if (ToolAxisTilted(snapshot.Frame.Chain, snapshot.Frame.Tcpm)
            || (snapshot.LastHolder is string id && snapshot.Unknown.Contains("TIP_OFFSET:" + id)))
        {
            return null;
        }

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
}
