using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

// The measurement points of a tool (D372 to D376, D393): what selects a point is the register of the [tool_points]
// mapping, and a compiler never writes TIP_OFFSET or POINT_RADIUS as a control value (D375).
public abstract partial class CompilerBase
{
    // The tool axis of each working plane, whose decimals give the output step of CMP032 (D376).
    private static readonly Dictionary<Workplane, string> s_toolAxes = new()
    {
        [Workplane.XY] = "Z",
        [Workplane.ZX] = "Y",
        [Workplane.YZ] = "X",
    };

    // CMP016 comes once per program (D373).
    private bool _pointWithoutFormReported;

    /// <summary>
    /// What selects the point of the tool of the holder of the last TOOL after a step: the index, the edge or the
    /// registers of the [tool_points] mapping; null where the program named no point since the TOOL (D393), the machine
    /// has no mapping (CMP016) or the mapping cannot map the point (the ERROR CheckPoint reports).
    /// </summary>
    /// <param name="snapshot">The state after the step.</param>
    protected ToolPointRegisters? PointRegistersOf(ChannelSnapshot snapshot)
    {
        if (Machine.ToolPoints is not ToolPointsTable table
            || snapshot.LastHolder is not string id
            || !snapshot.Holders.TryGetValue(id, out HolderSnapshot? holder)
            || !holder.PointNamed)
        {
            return null;
        }

        return table.Map(holder.SpindleTool.Number, holder.ToolPoint);
    }

    // The point of a step, before its lines (D373, D376): a point other than 1 on a machine without [tool_points] is
    // the INFO CMP016 once per program; a point the form cannot map is the ERROR of the family, CMP123, CMP341 or
    // CMP514, naming the tool and k; a TIP_OFFSET or POINT_RADIUS that differs from the row of the tool table by more
    // than one output step of the tool axis is the WARNING CMP032, and no coordinate is corrected.
    private void CheckPoint(BlockStep step)
    {
        Block block = step.Block;
        if (block.Has("PROGRAM", null, "BEGIN"))
        {
            _pointWithoutFormReported = false;
        }

        if (!HasPointWord(block) && !block.Has("TOOL") && !block.Has("OFFSET"))
        {
            return;
        }

        if (step.After.LastHolder is not string id || !step.After.Holders.TryGetValue(id, out HolderSnapshot? holder))
        {
            return;
        }

        if (block.Has("TOOL_POINT") && holder.PointNamed)
        {
            CheckMapping(block, holder);
        }

        CheckOffsetAfterPoint(step, id);
        CompareWithToolTable(step, holder);
    }

    // D393: an OFFSET word is the hand-written form of the registers; on a holder whose tool named its point it
    // replaces the register of the mapping, which it writes, and says so.
    private void CheckOffsetAfterPoint(BlockStep step, string id)
    {
        Block block = step.Block;
        if (block.Find("OFFSET") is not Word offset)
        {
            return;
        }

        bool namedBefore = !block.Has("TOOL")
            && step.Before.Holders.TryGetValue(id, out HolderSnapshot? before) && before.PointNamed;
        if (!namedBefore && !block.Has("TOOL_POINT"))
        {
            return;
        }

        HolderSnapshot holder = step.After.Holders[id];
        Diagnostics.Warning(block, DiagnosticCodes.OffsetReplacesPointRegister,
            string.Create(CultureInfo.InvariantCulture,
                $"{offset.ToCanonical()} on tool {holder.SpindleTool}, whose program named TOOL_POINT="
                + $"{(namedBefore ? step.Before.Holders[id].ToolPoint : holder.ToolPoint)}, replaces the register of "
                + $"[tool_points] with its own (D393)."));
    }

    private void CheckMapping(Block block, HolderSnapshot holder)
    {
        string point = holder.ToolPoint.ToString(CultureInfo.InvariantCulture);
        if (Machine.ToolPoints is not ToolPointsTable table)
        {
            if (holder.ToolPoint != 1 && !_pointWithoutFormReported)
            {
                _pointWithoutFormReported = true;
                Diagnostics.Info(block, DiagnosticCodes.ToolPointWithoutForm,
                    $"TOOL_POINT={point} of tool {holder.SpindleTool} writes nothing: the machine "
                    + $"\"{Machine.Machine.Name}\" has no [tool_points], so the control selects no point; the program "
                    + "runs in the registers of the tool as it is (machine-config 3, D373).");
            }

            return;
        }

        if (table.Map(holder.SpindleTool.Number, holder.ToolPoint) is not null)
        {
            return;
        }

        string code = Machine.Machine.Controller switch
        {
            Controller.Heidenhain => DiagnosticCodes.HeidenhainToolPoint,
            Controller.Siemens => DiagnosticCodes.SiemensToolPointNotMapped,
            _ => DiagnosticCodes.FanucToolPointNotMapped,
        };
        string why = table.Form switch
        {
            ToolPointForm.IndexedTool => "a Klartext tool has at most nine indices, so points 1 to 10",
            ToolPointForm.Edge => "under edge_numbering = \"flat\" every point needs a [[tool_point]] row",
            _ => "the registers come from a [[tool_point]] row, the step rule, or tool management for point 1 only",
        };
        Diagnostics.Error(block, code,
            $"Point {point} of tool {holder.SpindleTool} has no register in [tool_points]: {why}; another point's "
            + "registers would cut with the wrong offsets (machine-config 3, D373).");
    }

    // D376: the row of the point in the tool table, a tool without rows being point 1 at the tip; silent without a
    // table, without a row, and for a value from an expression, which the program does not state as a number.
    private void CompareWithToolTable(BlockStep step, HolderSnapshot holder)
    {
        if (Options.ToolTable?.Find(holder.SpindleTool)?.FindPoint(holder.ToolPoint) is not ToolPointData row
            || step.After.LastHolder is not string id
            || step.After.Unknown.Contains("TIP_OFFSET:" + id)
            || step.After.Unknown.Contains("POINT_RADIUS:" + id))
        {
            return;
        }

        string axis = s_toolAxes.TryGetValue(step.After.Frame.Workplane, out string? toolAxis) ? toolAxis : "Z";
        decimal outputStep = Numbers.DecimalsOf(axis) is int decimals ? OutputStep(decimals) : 0m;
        string tool = holder.SpindleTool.ToString();
        string point = holder.ToolPoint.ToString(CultureInfo.InvariantCulture);
        ReportDifference(step.Block, "TIP_OFFSET", "tip_offset", holder.TipOffset, row.TipOffset, outputStep, tool,
            point);
        ReportDifference(step.Block, "POINT_RADIUS", "radius", holder.PointRadius, row.Radius, outputStep, tool,
            point);
    }

    private void ReportDifference(Block block, string word, string key, decimal program, decimal table,
        decimal outputStep, string tool, string point)
    {
        if (Math.Abs(program - table) <= outputStep)
        {
            return;
        }

        Diagnostics.Warning(block, DiagnosticCodes.ToolPointDiffersFromTable,
            string.Create(CultureInfo.InvariantCulture,
                $"{word}={program} of point {point} of tool {tool} differs from {key} = {table} of the tool table "
                + $"{Options.ToolTableFile}; the program is written as it stands and no coordinate is corrected "
                + $"(machine-config 1, D376)."));
    }

    // point_comment = true: one comment line with the point, a and r where a point word selects a point other than
    // point 1 at the tip; point 1 with a = r = 0 writes nothing, so a program without point words compiles as before
    // (machine-config 3, D375).
    private void WritePointComment(BlockStep step)
    {
        if (Machine.ToolPoints is not { PointComment: true }
            || !HasPointWord(step.Block)
            || step.After.LastHolder is not string id
            || !step.After.Holders.TryGetValue(id, out HolderSnapshot? holder)
            || (holder.ToolPoint == 1 && holder.TipOffset == 0m && holder.PointRadius == 0m))
        {
            return;
        }

        string text = string.Create(CultureInfo.InvariantCulture,
            $"TOOL {holder.SpindleTool} POINT {holder.ToolPoint} TIP_OFFSET {holder.TipOffset} "
            + $"POINT_RADIUS {holder.PointRadius}");
        Line(CommentLine(CommentText(text)));
    }

    private static bool HasPointWord(Block block)
    {
        return block.Has("TOOL_POINT") || block.Has("TIP_OFFSET") || block.Has("POINT_RADIUS");
    }

    // One output step of an address with these decimals: 0.001 for three; a machine file without decimals for the
    // tool axis writes every digit, and then any difference counts (D376).
    private static decimal OutputStep(int decimals)
    {
        decimal step = 1m;
        for (int index = 0; index < decimals; index++)
        {
            step /= 10m;
        }

        return step;
    }
}
