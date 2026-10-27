namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The offset rows of virtual machine 2.3 for one tool holder as one channel sees them: the offset registers and the
/// measurement point of the tool. They stay per channel in a job, as the control's tool-compensation memory is per
/// channel (D308), and the point words TOOL_POINT, TIP_OFFSET and POINT_RADIUS select and size registers, so they are
/// offset state too (D388 (7)). Mutable; only the virtual machine changes it (code-guidelines 7).
/// </summary>
internal sealed class HolderOffsets
{
    // The offsets length, radius and combined start 0 (virtual machine 2.3); the measurement point starts at point 1
    // with a = 0 and r = 0 (D372).
    public HolderOffsets()
    {
        OffsetLen = 0;
        OffsetRad = 0;
        OffsetCombined = 0;
        ResetPoint();
    }

    public int OffsetLen { get; set; }

    public int OffsetRad { get; set; }

    public int OffsetCombined { get; set; }

    /// <summary>
    /// toolPoint: the measurement point k of the tool, from 1 (D372).
    /// </summary>
    public int ToolPoint { get; set; }

    /// <summary>
    /// tipOffset: the axial offset a of the point from the tool tip, in the units of its block (D372).
    /// </summary>
    public decimal TipOffset { get; set; }

    /// <summary>
    /// pointRadius: the register radius r of the point (D372).
    /// </summary>
    public decimal PointRadius { get; set; }

    /// <summary>
    /// The point in effect: the point words take effect with the next tool-axis motion (language 4.4, D372), and the
    /// tip, the reported radius and the tool list follow these; TipRules.Activate takes them over from ToolPoint,
    /// TipOffset and PointRadius. 1 initially.
    /// </summary>
    public int ActiveToolPoint { get; set; } = 1;

    /// <summary>
    /// The TIP_OFFSET in effect since the last tool-axis motion; null while UNKNOWN (virtual machine 1). 0 initially.
    /// </summary>
    public decimal? ActiveTipOffset { get; set; } = 0m;

    /// <summary>
    /// The POINT_RADIUS in effect since the last tool-axis motion; null while UNKNOWN (virtual machine 1). 0 initially.
    /// </summary>
    public decimal? ActivePointRadius { get; set; } = 0m;

    /// <summary>
    /// True when the program named the point of the tool with TOOL_POINT since its TOOL, and no OFFSET word replaced
    /// the registers since; only then does a compiler write the registers of the [tool_points] mapping (D393).
    /// </summary>
    public bool PointNamed { get; set; }

    /// <summary>
    /// Point 1 with a = 0 and r = 0: the start of a holder and what a TOOL without point words leaves (D372).
    /// </summary>
    public void ResetPoint()
    {
        ToolPoint = 1;
        TipOffset = 0m;
        PointRadius = 0m;
        PointNamed = false;
    }
}
