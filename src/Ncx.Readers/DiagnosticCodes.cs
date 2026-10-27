namespace Ncx.Readers;

// The diagnostic codes of Ncx.Readers (D98, code-guidelines 6, implementation 00-method 4).
//
// A code is the area prefix RDR and three digits; the codes of Ncx.Core (PAR, VM) and Ncx.Config (CFG) live in the
// DiagnosticCodes class of their own project. A diagnostic renders as "file(line): WARNING RDR001: message" with the
// severities ERROR, WARNING and INFO. A constant is named after its rule, and a code is never renumbered or reused, so
// that tests and users can rely on it.
//
// Each component keeps its codes in a part of this class of its own, a file DiagnosticCodes.<Component>.cs next to
// its code, and takes its codes from its own range:
//
//   RDR001-RDR099  reader framework (P3-01): RDR001-RDR009 RAW, RDR010-RDR019 the file structure, RDR020-RDR029
//                  the states of the function tables (D154, D155)
//
// The readers of the controller families take ranges of their own in parts next to their folders.

/// <summary>
/// The diagnostic codes of Ncx.Readers: one constant per rule, named after the rule, the area prefix RDR and three
/// digits (D98).
/// </summary>
public static partial class DiagnosticCodes
{
    /// <summary>
    /// RDR020, a WARNING: a native code switches on two resources of one word, M3 of MAIN and TOOL on the Mori Seiki;
    /// the reader writes the first state in file order and names the others (D155).
    /// </summary>
    public const string FunctionCodeOfSeveralResources = "RDR020";

    /// <summary>
    /// RDR021, a WARNING: a native code of ORIENT or PHASE carries no angle, M19, M219, M92; the reader writes
    /// ORIENT:R=0 or PHASE=0 and names the code, the orientation angle set by the machine (D154).
    /// </summary>
    public const string FunctionAngleSetByTheMachine = "RDR021";
}
