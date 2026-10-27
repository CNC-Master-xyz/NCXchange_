using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The tool rows of virtual machine 2.3 for one tool holder: the tool in the spindle, the preloaded tool and the
/// offset registers. Mutable; only the virtual machine changes it, through the tool change rules of virtual machine
/// 3.5 (code-guidelines 7).
/// </summary>
internal sealed class HolderState
{
    // Tool 0 is the empty spindle: TOOL=0 empties it (language 4.4).
    private static readonly ToolRef s_emptySpindle = new(0);

    // spindleTool starts 0, preloaded none, and the offsets length, radius and combined 0 (virtual machine 2.3); the
    // measurement point starts at point 1 with a = 0 and r = 0 (D372).
    public HolderState()
    {
        SpindleTool = s_emptySpindle;
        Preloaded = null;
        OffsetLen = 0;
        OffsetRad = 0;
        OffsetCombined = 0;
        ResetPoint();
    }

    /// <summary>
    /// spindleTool: a number or a name (language 4.4); tool 0 is the empty spindle.
    /// </summary>
    public ToolRef SpindleTool { get; set; }

    /// <summary>
    /// preloaded; null for none. PRELOAD=0 clears it (language 4.4).
    /// </summary>
    public ToolRef? Preloaded { get; set; }

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
    /// Point 1 with a = 0 and r = 0: the start of a holder and what a TOOL without point words leaves (D372).
    /// </summary>
    public void ResetPoint()
    {
        ToolPoint = 1;
        TipOffset = 0m;
        PointRadius = 0m;
    }

    /// <summary>
    /// Where the holder stands in the tool change states of architecture 5.2.
    /// </summary>
    public ToolChangeState ToolChange
    {
        get
        {
            // A preload makes the holder Pending. Pending remembers whether the spindle held a tool, because PRELOAD
            // never changes the spindle (virtual machine 3.5): when PRELOAD=0 drops the preload, the holder returns
            // to Loaded with the tool still in the spindle, or to Empty with tool 0 (architecture 5.2).
            if (Preloaded is not null)
            {
                return ToolChangeState.Pending;
            }

            return SpindleTool == s_emptySpindle ? ToolChangeState.Empty : ToolChangeState.Loaded;
        }
    }

    /// <summary>
    /// An immutable copy of the holder as it is now.
    /// </summary>
    public HolderSnapshot Snapshot()
    {
        return new HolderSnapshot
        {
            SpindleTool = SpindleTool,
            Preloaded = Preloaded,
            OffsetLen = OffsetLen,
            OffsetRad = OffsetRad,
            OffsetCombined = OffsetCombined,
            ToolPoint = ToolPoint,
            TipOffset = TipOffset,
            PointRadius = PointRadius,
            ToolChange = ToolChange,
        };
    }
}
