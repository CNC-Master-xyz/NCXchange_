namespace Ncx.Readers;

// The codes of the Siemens reader, RDR500-RDR699 (the ranges in DiagnosticCodes.cs, D98). What the reader keeps as a
// whole RAW block carries RDR001 with the reason in its message, a program without an end the RDR010 of the structure
// pass; the codes here are the other findings of reading a SINUMERIK source.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// RDR500, a WARNING: an M function that neither the controller nor a table of the machine names is written as
    /// MFUNC=n (machine-config 5, language 4.6).
    /// </summary>
    public const string SiemensMCodeNotNamed = "RDR500";

    /// <summary>
    /// RDR501, a WARNING: words of a block that NCX has no meaning for, the path control and orientation words of the
    /// G groups, the direction of DC(), ACP() and ACN(), are kept as a RAW:SIEMENS word of the block, verbatim, and
    /// compile only to Siemens (controllers siemens.md 2, 11 rule 8; controller-mapping 2 and 9; D5).
    /// </summary>
    public const string SiemensWordsKeptAsRaw = "RDR501";

    /// <summary>
    /// RDR502, a WARNING: a value of a cycle call or of CYCLE800 that no NCX word carries is not written (controllers
    /// siemens.md 4, 7; machine-config 6).
    /// </summary>
    public const string SiemensValueNotCarried = "RDR502";

    /// <summary>
    /// RDR503, a WARNING: tool vector words stand under ORIMKS, where the vector means the machine axes, while the NCX
    /// vector is meant in the workpiece frame (controller-mapping 2, tool vectors; D81).
    /// </summary>
    public const string SiemensVectorInMachineSystem = "RDR503";

    /// <summary>
    /// RDR504, an ERROR: the end of a structure, ELSE, ENDIF, ENDWHILE, ENDFOR, ENDLOOP or UNTIL, that no structure of
    /// its section opened; the block is kept as RAW (controllers siemens.md 8; controller-mapping 6).
    /// </summary>
    public const string SiemensStructureEndWithoutBegin = "RDR504";

    /// <summary>
    /// RDR505, a WARNING: an IF, WHILE, FOR, LOOP or REPEAT that no end closes before the end of its section; its jump
    /// back or its end label is missing (controllers siemens.md 8).
    /// </summary>
    public const string SiemensStructureNotClosed = "RDR505";

    /// <summary>
    /// RDR506, a WARNING: a DIV with an operand that is no number literal, or with a negative literal whose quotient is
    /// not exact, keeps its block as RAW, since how the control rounds the integer division of a negative operand,
    /// towards zero or towards minus infinity, is not documented; integer-valued literals whose quotient is exact
    /// convert whatever their sign (D346, D382; manual audit 2026-10-26).
    /// </summary>
    public const string SiemensDivisionRoundingUnknown = "RDR506";

    /// <summary>
    /// RDR507, a WARNING: a feed under G70 or G71 whose unit differs from the basic system of the machine (base_units,
    /// MD10240) stays in the basic system on the control, so the reader converts it into the unit UNITS names and names
    /// the code (D292).
    /// </summary>
    public const string SiemensFeedInTheBasicSystem = "RDR507";

    /// <summary>
    /// RDR509, a WARNING: G75 FP=1, or G75 without FP, which the control reads as FP=1, approaches fixed point 1, and
    /// NCX has no POINT for it, since POINT=1 is the reference point G74 approaches; the block is kept as RAW (D260).
    /// </summary>
    public const string SiemensFixedPoint1HasNoPoint = "RDR509";

    /// <summary>
    /// RDR510, a WARNING: $AA_IM[X], the machine position, reads a radius on the control by default (MD20360 bits 6/7
    /// off), half the diameter NCX $SYS_MPOS_X reads, so under DIAMON the reader writes the NCX name halved; the factor
    /// is not yet measured on a machine, and frames_in_diameter on the X axis turns it off (D240).
    /// </summary>
    public const string SiemensMachinePositionDoubled = "RDR510";

    /// <summary>
    /// RDR511, a WARNING: G973 switches the constant surface speed off without the speed limit, which NCX has no form
    /// for, so it stays a RAW word written back as the source wrote it, and NCX holds the surface speed of the code
    /// before it (D342, D424).
    /// </summary>
    public const string SiemensG973HasNoForm = "RDR511";
}
