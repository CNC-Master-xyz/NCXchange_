namespace Ncx.Config;

// The codes of the cycle catalogs (P2-03) take CFG150-CFG199 of the CFG area of Ncx.Config (D98); a code is never
// renumbered or reused. A catalog file and the [[cycle]] entries of a machine file report the codes of loading as
// well: a file that is not TOML, an unknown key, a wrong type, a missing name or native (CFG001-CFG004).
//
// D141: an entry that no program or reader can use is an ERROR (CFG150 a name written twice in one file, CFG151 a name
// or a parameter word NCX cannot write, CFG154 a contour of more than two words); an entry that can be used but does
// not hold together is a WARNING (CFG152 a word of absolute_from_surface without its parameter, CFG153 one without
// SURFACE, CFG155 a key that the entries of its controller family leave out), like the unknown keys of P2-01.
// Machine-config 6 lists the checks and their severities.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// ERROR: a second [[cycle]] of one file names a cycle an earlier entry of the file names already; the catalog
    /// finds an entry by its name (machine-config 6).
    /// </summary>
    public const string CycleNameTwice = "CFG150";

    /// <summary>
    /// ERROR: the name of a catalog entry or a word of its params is not written as NCX writes an identifier or a key,
    /// a capital followed by capitals, digits and underscores, so no program can name it (language 3, 4.7.1).
    /// </summary>
    public const string CycleWordMalformed = "CFG151";

    /// <summary>
    /// WARNING: absolute_from_surface names a word that params does not map, so no native value converts to it
    /// (machine-config 6).
    /// </summary>
    public const string CycleAbsoluteWordUnmapped = "CFG152";

    /// <summary>
    /// WARNING: an entry with absolute_from_surface maps no SURFACE, the value its words are relative to, NCX = SURFACE
    /// + native (machine-config 6).
    /// </summary>
    public const string CycleAbsoluteWithoutSurface = "CFG153";

    /// <summary>
    /// ERROR: contour lists more than two words; two are the first and the last block of the contour range, one names
    /// the contour subprogram (machine-config 6, D65).
    /// </summary>
    public const string CycleContourWordCount = "CFG154";

    /// <summary>
    /// WARNING: a key that the entries of the controller family leave out: modal = true on Heidenhain or Siemens, whose
    /// calls are words of their own, a signature on Fanuc, whose parameters are address words (machine-config 6).
    /// </summary>
    public const string CycleKeyOutsideItsFamily = "CFG155";

    /// <summary>
    /// WARNING: a [cycles] key of the machine holds a distance the control keeps, chip_break_lift or peck_clearance,
    /// while the defaults of a catalog entry name the same native parameter; the key is the one source the compiler
    /// writes, and the message names both places (D358).
    /// </summary>
    public const string CycleDefaultBesideMachineKey = "CFG156";

    /// <summary>
    /// WARNING: a catalog file writes channel or available, keys of the [[cycle]] tables of a machine file; the
    /// loader ignores them (D361, D377).
    /// </summary>
    public const string CycleMachineKeyInCatalog = "CFG157";

    /// <summary>
    /// ERROR: the catalog of a Heidenhain machine writes Q256, the chip-break lift the control holds, and the machine
    /// sets no [cycles] chip_break_lift, its one source; the loader does not guess a lift (D164, D358).
    /// </summary>
    public const string CycleChipBreakLiftMissing = "CFG158";

    /// <summary>
    /// ERROR: a params word of a catalog entry has the form of a machine axis name, which a program reads as a machine
    /// axis word of D93 (D114).
    /// </summary>
    public const string CycleParameterAxisForm = "CFG159";
}
