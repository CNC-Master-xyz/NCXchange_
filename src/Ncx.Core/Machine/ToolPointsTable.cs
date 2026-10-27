namespace Ncx.Core.Machine;

/// <summary>
/// [tool_points] with the [[tool_point]] rows: how the machine maps a tool and a measurement point to the control's
/// registers (machine-config 3, D373). NCX derives nothing from a tool number that the machine file does not state.
/// </summary>
public sealed record ToolPointsTable
{
    // The most points a Klartext tool carries: the main line and nine indices (D373 improvement 1).
    private const int MostIndexedPoints = 10;

    // H99 and D99 select the registers that tool management registered for the tool at the spindle (D373 improvement
    // 4; Fanuc 30i-B B-64484EN p. 260, parameter 13265 = 0).
    private const int ManagedRegister = 99;

    /// <summary>
    /// form: how the control selects a point.
    /// </summary>
    public required ToolPointForm Form { get; init; }

    /// <summary>
    /// point_change = "call": the builder accepts a point change of the tool in the spindle by an indexed TOOL CALL,
    /// which silences the WARNING CMP123 (indexed_tool).
    /// </summary>
    public bool PointChangeByCall { get; init; }

    /// <summary>
    /// edge_numbering = "flat": D numbers without relation to the tool (MD18102 = 1), so every point needs a row
    /// (edge).
    /// </summary>
    public bool FlatEdges { get; init; }

    /// <summary>
    /// offset_memory: A, B or C, the Fanuc offset memory type; null when left out, and then no register collision is
    /// checked (registers).
    /// </summary>
    public string? OffsetMemory { get; init; }

    /// <summary>
    /// managed = true: tool management, H99 and D99 for point 1 (registers).
    /// </summary>
    public bool Managed { get; init; }

    /// <summary>
    /// length_register_step: the shop's rule register = tool + (k - 1) * step for the length register; null without
    /// a rule (registers).
    /// </summary>
    public int? LengthRegisterStep { get; init; }

    /// <summary>
    /// radius_register_step: the same rule for the radius register; null without a rule (registers).
    /// </summary>
    public int? RadiusRegisterStep { get; init; }

    /// <summary>
    /// point_comment = true: the compiler writes one comment line with the point, a and r (D375).
    /// </summary>
    public bool PointComment { get; init; }

    /// <summary>
    /// The [[tool_point]] rows, which win over the rule of the form.
    /// </summary>
    public IReadOnlyList<ToolPointRow> Rows { get; init; } = [];

    /// <summary>
    /// What selects point k of a tool, or null where the form cannot map it, which a compiler reports as the ERROR of
    /// its family (CMP124, CMP341, CMP514; D373, D388).
    /// </summary>
    /// <param name="tool">The tool number; null for a tool called by name, which only indexed_tool maps.</param>
    /// <param name="point">The point k, from 1.</param>
    public ToolPointRegisters? Map(int? tool, int point)
    {
        ToolPointRow? row = tool is int number ? FindRow(number, point) : null;
        return Form switch
        {
            ToolPointForm.IndexedTool => MapIndex(point),
            ToolPointForm.Edge => MapEdge(row, point),
            _ => MapRegisters(row, tool, point),
        };
    }

    // indexed_tool: point 1 is the plain call and point k the index k - 1, at most nine indices (D373 improvement 1).
    private static ToolPointRegisters? MapIndex(int point)
    {
        return point <= MostIndexedPoints ? new ToolPointRegisters { Index = point - 1 } : null;
    }

    // edge: a row wins, else D k; with flat numbering only a row maps a point (D373 improvement 2).
    private ToolPointRegisters? MapEdge(ToolPointRow? row, int point)
    {
        if (row?.Edge is int edge)
        {
            return new ToolPointRegisters { Edge = edge };
        }

        return FlatEdges ? null : new ToolPointRegisters { Edge = point };
    }

    // registers: a row wins; tool management maps point 1 to H99 and D99 and nothing else; the step keys are the shop's
    // numbering rule; without any of them nothing is derived from the tool number (D373 improvements 3 to 5).
    private ToolPointRegisters? MapRegisters(ToolPointRow? row, int? tool, int point)
    {
        if (row is not null && (row.Length is not null || row.Radius is not null))
        {
            return new ToolPointRegisters { Length = row.Length, Radius = row.Radius };
        }

        if (Managed)
        {
            return point == 1 ? new ToolPointRegisters { Length = ManagedRegister, Radius = ManagedRegister } : null;
        }

        if (tool is not int number || (LengthRegisterStep is null && RadiusRegisterStep is null))
        {
            return null;
        }

        return new ToolPointRegisters
        {
            Length = number + ((point - 1) * LengthRegisterStep),
            Radius = number + ((point - 1) * RadiusRegisterStep),
        };
    }

    private ToolPointRow? FindRow(int tool, int point)
    {
        foreach (ToolPointRow row in Rows)
        {
            if (row.Tool == tool && row.Point == point)
            {
                return row;
            }
        }

        return null;
    }
}
