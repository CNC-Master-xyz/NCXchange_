using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The tool rows of virtual machine 2.3 for one holder as they stood when the snapshot was taken; immutable, part of
/// the Before and After of every event (architecture 5.3).
/// </summary>
public sealed record HolderSnapshot
{
    /// <summary>
    /// spindleTool: the tool in the spindle, a number or a name (language 4.4); tool 0 is the empty spindle.
    /// </summary>
    public required ToolRef SpindleTool { get; init; }

    /// <summary>
    /// preloaded: the tool PRELOAD prepared; null for none.
    /// </summary>
    public required ToolRef? Preloaded { get; init; }

    /// <summary>
    /// offset.length: the register of OFFSET:LEN.
    /// </summary>
    public required int OffsetLen { get; init; }

    /// <summary>
    /// offset.radius: the register of OFFSET:RAD.
    /// </summary>
    public required int OffsetRad { get; init; }

    /// <summary>
    /// offset.combined: the register of OFFSET.
    /// </summary>
    public required int OffsetCombined { get; init; }

    /// <summary>
    /// toolPoint: the measurement point k of TOOL_POINT, 1 by default (D372).
    /// </summary>
    public int ToolPoint { get; init; } = 1;

    /// <summary>
    /// tipOffset: the axial offset a of TIP_OFFSET from the tool tip, 0 by default (D372).
    /// </summary>
    public decimal TipOffset { get; init; }

    /// <summary>
    /// pointRadius: the register radius r of POINT_RADIUS, 0 by default (D372).
    /// </summary>
    public decimal PointRadius { get; init; }

    /// <summary>
    /// True when the program named the point with TOOL_POINT since the TOOL of the holder and no OFFSET word replaced
    /// the registers since (D393).
    /// </summary>
    public bool PointNamed { get; init; }

    /// <summary>
    /// Where the holder stood in the tool change states of architecture 5.2.
    /// </summary>
    public required ToolChangeState ToolChange { get; init; }
}
