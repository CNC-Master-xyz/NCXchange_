namespace Ncx.Core.Machine;

/// <summary>
/// [machine] gcode_system: the G code system of a Fanuc lathe (machine-config 1).
/// </summary>
public enum GcodeSystem
{
    /// <summary>
    /// gcode_system = "A": G98/G99 feed, U/W incremental, G90 is the turning cycle.
    /// </summary>
    A,

    /// <summary>
    /// gcode_system = "B": G94/G95 feed, G90/G91 absolute and incremental.
    /// </summary>
    B,

    /// <summary>
    /// gcode_system = "C": as B, but G70/G71 are inch and millimetre input, the simple turning cycles are
    /// G20/G21/G24 and the multiple repetitive cycles G72..G78 (controllers fanuc.md 3, D174).
    /// </summary>
    C,
}
