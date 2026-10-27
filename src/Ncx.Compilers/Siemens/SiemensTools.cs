using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The tool words of a SINUMERIK block (controllers siemens.md 5, 12 rule 3; controller-mapping 3; machine-config 3):
/// T and M6 per [tool_change], which the framework writes, with {offset} the D of the holder; D from OFFSET where the
/// change does not carry it, the one register of the cutting edge for length and radius.
/// </summary>
internal static class SiemensTools
{
    // The target key of the active D (controllers siemens.md 5).
    private const string Edge = "D";

    /// <summary>
    /// The values the controller gives the change template itself: {offset}, the D of the holder after the block
    /// (machine-config 3; controller-mapping 3, OFFSET:LEN).
    /// </summary>
    public static TemplateValues ChangeValues(SiemensBlock write)
    {
        var values = new TemplateValues();
        if (EdgeOf(write) is int edge)
        {
            values.Set("offset", edge);
        }

        return values;
    }

    /// <summary>
    /// After the change: the D the template wrote is active, and without {offset} the control selects the edge of the
    /// new tool itself, which the compiler does not know (controllers siemens.md 5).
    /// </summary>
    public static void AfterChange(SiemensBlock write, string? template)
    {
        if (write.HasPlaceholder(template, "offset") && EdgeOf(write) is int edge)
        {
            write.Target.Set(Edge, edge.ToString(CultureInfo.InvariantCulture));
            return;
        }

        write.MakeUnknown(Edge);
    }

    /// <summary>
    /// D from OFFSET in the main line where the control has another edge active and the change of the block did not
    /// write it: D0 in the retract block G0 G53 Z360 D0 (controllers siemens.md 12 rule 3; controller-mapping 1,
    /// FRAME=MACHINE, and 3, OFFSET:LEN).
    /// </summary>
    public static void WriteOffset(SiemensBlock write)
    {
        // D375: TIP_OFFSET and POINT_RADIUS are never written to the control, no TOFFL, TOFF, TOFFR and no DL; a named
        // point selects its edge, which the mapping of [tool_points] gives (D373, D393).
        write.Written("TOOL_POINT");
        write.Written("TIP_OFFSET");
        write.Written("POINT_RADIUS");
        bool selectsPoint = write.Block.Has("TOOL_POINT") && write.PointRegisters?.Edge is not null;
        if (!write.Block.Has("OFFSET") && !selectsPoint)
        {
            return;
        }

        write.Written("OFFSET");
        if (EdgeOf(write) is int edge && write.Target.Changes(Edge, edge.ToString(CultureInfo.InvariantCulture)))
        {
            write.Main.Word(SiemensLine.OffsetRank, "D" + edge.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// The D of the holder of the last TOOL after the block: OFFSET, the combined register, where the program writes
    /// it; else OFFSET:LEN and OFFSET:RAD, which on this control are one register (controller-mapping 3, OFFSET:LEN;
    /// language 4.4, OFFSET).
    /// </summary>
    // D301: equal registers, or the single register that is set, give that D; two different registers give the
    // length register with the WARNING of ReportTwoRegisters, since one D cannot carry two. A block that sets
    // OFFSET:LEN=0 writes D0, because a D of this control always carries the length, and D0 drops the radius with it
    // (decisions.md D301; controller-mapping 3, OFFSET:LEN).
    public static int? EdgeOf(SiemensBlock write)
    {
        ChannelSnapshot after = write.After;
        if (after.LastHolder is not string holder || !after.Holders.TryGetValue(holder, out HolderSnapshot? state))
        {
            return null;
        }

        if (CancelsLength(write))
        {
            return 0;
        }

        // A named point selects the edge of the mapping, D k or the edge of its row (D373, D393).
        if (write.PointRegisters?.Edge is int pointEdge)
        {
            return pointEdge;
        }

        if (write.Block.Find("OFFSET", null) is not null || state.OffsetCombined != 0)
        {
            return state.OffsetCombined;
        }

        return state.OffsetLen != 0 ? state.OffsetLen : state.OffsetRad;
    }

    // True where the block itself cancels the length register, OFFSET:LEN=0 (language 4.4; D301).
    private static bool CancelsLength(SiemensBlock write)
    {
        return write.Block.Find("OFFSET", "LEN") is Word length && length.Value.ToCanonical() == "0";
    }

    /// <summary>
    /// A WARNING where OFFSET:LEN and OFFSET:RAD of the block leave the holder with two registers, of which the one D
    /// of the control carries the length, and where a block that sets OFFSET:LEN=0 writes the D0 that drops the radius
    /// too while the compensation is still on (controller-mapping 3, OFFSET:LEN; D301).
    /// </summary>
    public static void ReportTwoRegisters(SiemensBlock write)
    {
        ChannelSnapshot after = write.After;
        if (CancelsLength(write) && after.Motion.Comp != Compensation.Off)
        {
            write.Warning(DiagnosticCodes.SiemensOffsetRegistersDiffer,
                "OFFSET:LEN=0 is written as D0, which cancels the length and the radius of the active tool together, "
                + $"and COMP={after.Motion.Comp.ToString().ToUpperInvariant()} is still on after the block "
                + "(controller-mapping 3, OFFSET:LEN; controllers siemens.md 5; D301).");
        }

        if ((write.Block.Find("OFFSET", "LEN") is null && write.Block.Find("OFFSET", "RAD") is null)
            || after.LastHolder is not string holder || !after.Holders.TryGetValue(holder, out HolderSnapshot? state)
            || state.OffsetCombined != 0 || state.OffsetLen == state.OffsetRad || state.OffsetLen == 0
            || state.OffsetRad == 0)
        {
            return;
        }

        write.Warning(DiagnosticCodes.SiemensOffsetRegistersDiffer,
            $"OFFSET:LEN={state.OffsetLen.ToString(CultureInfo.InvariantCulture)} and OFFSET:RAD="
            + $"{state.OffsetRad.ToString(CultureInfo.InvariantCulture)} name two registers, and the D of the control "
            + $"is one register for length and radius; D{state.OffsetLen.ToString(CultureInfo.InvariantCulture)} is "
            + "written (controller-mapping 3, OFFSET:LEN; controllers siemens.md 5).");
    }
}
