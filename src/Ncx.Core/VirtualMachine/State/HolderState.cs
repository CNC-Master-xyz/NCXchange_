using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The tool rows of virtual machine 2.3 for one tool holder: the tool in the spindle and the preloaded tool. In a job
/// they are one per job, read and changed by every channel in round order (D308); the offsets stay per channel
/// (HolderOffsets). Mutable; only the virtual machine changes it, through the tool change rules of virtual machine 3.5
/// (code-guidelines 7).
/// </summary>
internal sealed class HolderState
{
    // Tool 0 is the empty spindle: TOOL=0 empties it (language 4.4).
    private static readonly ToolRef s_emptySpindle = new(0);

    // spindleTool starts 0 and preloaded none (virtual machine 2.3).
    public HolderState()
    {
        SpindleTool = s_emptySpindle;
        Preloaded = null;
    }

    /// <summary>
    /// spindleTool: a number or a name (language 4.4); tool 0 is the empty spindle.
    /// </summary>
    public ToolRef SpindleTool { get; set; }

    /// <summary>
    /// preloaded; null for none. PRELOAD=0 clears it (language 4.4).
    /// </summary>
    public ToolRef? Preloaded { get; set; }

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
    /// An immutable copy of the holder as one channel sees it now: the shared tool rows with that channel's offsets.
    /// </summary>
    /// <param name="offsets">The offsets of the holder in the channel of the snapshot (D308).</param>
    public HolderSnapshot Snapshot(HolderOffsets offsets)
    {
        return new HolderSnapshot
        {
            SpindleTool = SpindleTool,
            Preloaded = Preloaded,
            OffsetLen = offsets.OffsetLen,
            OffsetRad = offsets.OffsetRad,
            OffsetCombined = offsets.OffsetCombined,
            ToolPoint = offsets.ToolPoint,
            TipOffset = offsets.TipOffset,
            PointRadius = offsets.PointRadius,
            ActiveToolPoint = offsets.ActiveToolPoint,
            ActiveTipOffset = offsets.ActiveTipOffset,
            ActivePointRadius = offsets.ActivePointRadius,
            PointNamed = offsets.PointNamed,
            ToolChange = ToolChange,
        };
    }
}
