namespace Ncx.Core.Machine;

/// <summary>
/// [cycles] ream_feed_out: the plane up to which REAM feeds out at CYCLE_F under CYCLE_RETRACT=SAFE before the rest of
/// the retract is a rapid (machine-config 6, virtual machine 3.3, D378).
/// </summary>
public enum ReamFeedOut
{
    /// <summary>
    /// ream_feed_out = "CLEARANCE": feed to CLEARANCE, rapid to SAFE; the family default on Fanuc and Heidenhain.
    /// </summary>
    Clearance,

    /// <summary>
    /// ream_feed_out = "SAFE": feed the whole way to SAFE; the family default on Siemens (CYCLE85 retracts at FR to
    /// the retract plane).
    /// </summary>
    Safe,
}
