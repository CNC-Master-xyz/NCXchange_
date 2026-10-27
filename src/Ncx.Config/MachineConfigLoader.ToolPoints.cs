using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config;

// Machine-config 3: [tool_points] and the [[tool_point]] rows, the registers of a tool's measurement points (D373).
public static partial class MachineConfigLoader
{
    private static readonly string[] s_toolPointsKeys =
    [
        "form", "point_change", "edge_numbering", "offset_memory", "managed", "length_register_step",
        "radius_register_step", "point_comment",
    ];

    private static readonly string[] s_toolPointRowKeys = ["tool", "point", "edge", "length", "radius"];
    private static readonly string[] s_toolPointForms = ["indexed_tool", "edge", "registers"];
    private static readonly string[] s_pointChanges = ["call"];
    private static readonly string[] s_edgeNumberings = ["flat"];
    private static readonly string[] s_offsetMemories = ["A", "B", "C"];

    // [tool_points]: the form of the control and the keys of that form, with the [[tool_point]] rows that win over its
    // rule (D373); null without the table, and then a point other than 1 writes nothing (CMP016). A form the loader
    // does not know is the ERROR of every choice of the file (CFG005).
    private static ToolPointsTable? ReadToolPoints(ConfigTable root)
    {
        ConfigTable? table = root.Table("tool_points", section: "machine-config 3");
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_toolPointsKeys);
        ToolPointForm? form = table.RequiredChoice("form", s_toolPointForms) switch
        {
            "indexed_tool" => ToolPointForm.IndexedTool,
            "edge" => ToolPointForm.Edge,
            "registers" => ToolPointForm.Registers,
            _ => null,
        };
        if (form is not ToolPointForm known)
        {
            return null;
        }

        var points = new ToolPointsTable
        {
            Form = known,
            PointChangeByCall = table.Choice("point_change", s_pointChanges) is not null,
            FlatEdges = table.Choice("edge_numbering", s_edgeNumberings) is not null,
            OffsetMemory = table.Choice("offset_memory", s_offsetMemories),
            Managed = table.Flag("managed"),
            LengthRegisterStep = table.Integer("length_register_step"),
            RadiusRegisterStep = table.Integer("radius_register_step"),
            PointComment = table.Flag("point_comment"),
            Rows = ReadToolPointRows(root),
        };
        CheckSharedRegisters(points, root);
        return points;
    }

    // [[tool_point]]: tool and point are what a row is found by, so a row without either is CFG010 (D373).
    private static List<ToolPointRow> ReadToolPointRows(ConfigTable root)
    {
        var rows = new List<ToolPointRow>();
        foreach (ConfigTable row in root.Tables("tool_point", "[[tool_point]]", "machine-config 3"))
        {
            row.WarnUnknownKeys(s_toolPointRowKeys);
            int? tool = row.Integer("tool");
            int? point = row.Integer("point");
            if (tool is not int toolNumber || point is not int pointNumber)
            {
                // A key that stands with a value of the wrong type was reported by Integer as CFG003, and that is the
                // whole mistake of the row; only a missing key is CFG010.
                if ((tool is null && row.Has("tool")) || (point is null && row.Has("point")))
                {
                    continue;
                }

                row.Diagnostics.Error(row.Line, DiagnosticCodes.ToolPointConflict,
                    $"{row.Name} has no {(tool is null ? "tool" : "point")}, by which the row is found "
                    + "(machine-config 3, D373).");
                continue;
            }

            rows.Add(new ToolPointRow
            {
                Tool = toolNumber,
                Point = pointNumber,
                Edge = row.Integer("edge"),
                Length = row.Integer("length"),
                Radius = row.Integer("radius"),
            });
        }

        return rows;
    }

    // Under offset memory A or B the H and D codes share one pool, so a register number that serves as a length for
    // one tool and point and as a radius for another, or for the same, is CFG010; under C, and without the key, it is
    // not checked (D373 improvement 3; Fanuc 30i-B B-64484EN-2 p. 262-263).
    private static void CheckSharedRegisters(ToolPointsTable points, ConfigTable root)
    {
        if (points.OffsetMemory is not ("A" or "B"))
        {
            return;
        }

        var lengths = new HashSet<int>();
        foreach (ToolPointRow row in points.Rows)
        {
            if (row.Length is int length)
            {
                lengths.Add(length);
            }
        }

        foreach (ToolPointRow row in points.Rows)
        {
            if (row.Radius is int radius && lengths.Contains(radius))
            {
                root.Diagnostics.Error(root.Line, DiagnosticCodes.ToolPointConflict,
                    $"Register {radius} serves as a length and as a radius, and under offset_memory = "
                    + $"\"{points.OffsetMemory}\" H and D share one pool (machine-config 3, D373).");
            }
        }
    }
}
