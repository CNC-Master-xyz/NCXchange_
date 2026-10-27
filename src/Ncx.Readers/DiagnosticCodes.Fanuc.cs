namespace Ncx.Readers;

// The codes of the Fanuc reader, RDR100-RDR299 (the ranges in DiagnosticCodes.cs, D98). What the reader keeps as RAW
// carries RDR001 with the reason in its message; the codes here are the other findings of reading a Fanuc source.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// RDR100, a WARNING: an M code that neither the controller nor a table of the machine names is written as
    /// MFUNC=n (machine-config 5, language 4.6).
    /// </summary>
    public const string FanucMCodeNotNamed = "RDR100";

    /// <summary>
    /// RDR101, an ERROR: M6 changes to the preloaded tool, and nothing is preloaded; an error in the source
    /// (controller-mapping 3, controllers fanuc.md 5).
    /// </summary>
    public const string FanucChangeWithoutPreload = "RDR101";

    /// <summary>
    /// RDR102, an ERROR: a T word after the M6 of the same block, T4 M6 T5, which is illegal on most controls; the
    /// block is kept as RAW (controller-mapping 3, controllers fanuc.md 5).
    /// </summary>
    public const string FanucToolAfterChange = "RDR102";

    /// <summary>
    /// RDR103, an ERROR: an END of a WHILE loop that no DO with its number opened; the block is kept as RAW
    /// (controllers fanuc.md 7, controller-mapping 6).
    /// </summary>
    public const string FanucLoopEndWithoutDo = "RDR103";

    /// <summary>
    /// RDR104, a WARNING: a DO of a WHILE loop that no END with its number closes before the end of its program or
    /// subprogram; the loop head is written, the jump back is missing (controllers fanuc.md 7).
    /// </summary>
    public const string FanucLoopNotClosed = "RDR104";

    /// <summary>
    /// RDR105, a WARNING: a drilling cycle under G98 returns to the initial level, and the reader does not know where
    /// the drilling axis stood; CYCLE_RETRACT=SAFE is written without SAFE (controller-mapping 5, language 4.7).
    /// </summary>
    public const string FanucInitialLevelUnknown = "RDR105";

    /// <summary>
    /// RDR106, a WARNING: an M98 subprogram writes one of the caller's local variables #1..#33 back for the caller,
    /// which it shares with it because a subprogram call opens no local-variable level; a CALL of NCX restores the
    /// caller's V1..V33 at the return, so the write does not reach the caller (D148; virtual machine 3.6).
    /// </summary>
    public const string FanucSharedLocalWritten = "RDR106";

    /// <summary>
    /// RDR120, a WARNING: a drilling cycle of a mill outside G17, whose drilling axis the reader derives from the plane;
    /// on the control it depends on parameter 5101#0 FXY, 0 always Z, 1 by the plane and the axis address (D157).
    /// </summary>
    public const string FanucDrillingAxisByParameter = "RDR120";

    /// <summary>
    /// RDR121, a WARNING: a block with a group-01 code and a drilling canned-cycle code is not a cycle: the control
    /// executes the group-01 code and cancels the cycle as G80 does (B-64484EN-2 p.105); the block stays RAW (D386).
    /// </summary>
    public const string FanucGroupOneCancelsTheCycle = "RDR121";
}
