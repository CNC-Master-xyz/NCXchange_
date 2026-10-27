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
    /// RDR107, an ERROR: the channel of the file, from ncx convert --channel or from the suffix .P-n of its name, is
    /// not a channel of the machine file; CHANNEL=n is written as given (D223; controller-mapping 7).
    /// </summary>
    public const string FanucChannelNotOfTheMachine = "RDR107";

    /// <summary>
    /// RDR108, a WARNING: a position variable, #5021.. or #5041.., is mapped 1:1 onto $SYS_MPOS_ or $SYS_POS_ of an
    /// axis programmed in diameters or switchable, while whether the control gives a diameter or a radius there is not
    /// measured yet (D240; language 4.12).
    /// </summary>
    public const string FanucPositionOfADiameterAxis = "RDR108";

    /// <summary>
    /// RDR109, a WARNING: a G2 or G3 carries both its radius R and its centre I J K; the control takes R and ignores
    /// I J K, and the reader writes the radius form (D186; controllers fanuc.md 4).
    /// </summary>
    public const string FanucArcRadiusOverCentre = "RDR109";

    /// <summary>
    /// RDR110, a WARNING: a helix moves with the F along the arc in the plane ([format] helix_feed = "arc", parameter
    /// 1403#5), and the reader does not know its geometry or its F, so the F is not scaled back to the feed along the
    /// path (D354; machine-config 2).
    /// </summary>
    public const string FanucHelixFeedNotScaled = "RDR110";
}
