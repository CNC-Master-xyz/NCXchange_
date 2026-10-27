namespace Ncx.Compilers;

// The diagnostic codes of Ncx.Compilers (D98, code-guidelines 6, implementation 00-method 4).
//
// A code is the area prefix CMP and three digits; the codes of Ncx.Core (PAR, VM) and Ncx.Config (CFG) live in the
// DiagnosticCodes class of their own project. A diagnostic renders as "file(line): ERROR CMP001: message" with the
// severities ERROR, WARNING and INFO. A constant is named after its rule, and a code is never renumbered or reused, so
// that tests and users can rely on it.
//
// Each component keeps its codes in a part of this class of its own and takes its codes from its own range:
//
//   CMP001-CMP099  compiler framework (P3-03): CMP001-CMP009 what a machine takes at all and the subprograms
//                  (D94, D99), CMP010-CMP019 the templates of the machine (machine-config 3), CMP020-CMP029 the
//                  numbers and the lines of the output (machine-config 2), CMP030-CMP039 the rounding of the output
//                  and the tool data of the machine (D364, D365, D370, D376)
//
// The compilers of the controller families take ranges of their own in parts next to their folders.

/// <summary>
/// The diagnostic codes of Ncx.Compilers: one constant per rule, named after the rule, the area prefix CMP and three
/// digits (D98).
/// </summary>
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CMP001: RAW of another controller or builder, or a CYCLE:controller=n block of another controller family, is
    /// compiled; it compiles only to the controller it names, and any other target is an ERROR (language 4.1, 4.7.1;
    /// controller-mapping 9; D5, D94).
    /// </summary>
    public const string NativeTextOfAnotherController = "CMP001";

    /// <summary>
    /// CMP002: two walks of one subprogram write different lines, and the section is written once for every caller
    /// (virtual machine 3.9, D99).
    /// </summary>
    public const string SubprogramWalksDiffer = "CMP002";

    /// <summary>
    /// CMP003, retired 2026-10-27 (D264): a subprogram that no program calls is written once, into the first program's
    /// file under program_layout = "file_per_program". The number is not reused.
    /// </summary>
    [Obsolete("Retired by D264: every SUB is written at least once.")]
    public const string UncalledSubprogramNotWritten = "CMP003";

    /// <summary>
    /// CMP005, a WARNING: under program_layout = "file_per_program" a program name, compared case-insensitively, is
    /// taken already by an earlier program, and the file gets _2, _3 (machine-config 2, D266).
    /// </summary>
    public const string ProgramNameTaken = "CMP005";

    /// <summary>
    /// CMP006, a WARNING: {next} of a tool change finds no other tool to prepare, and the tool changed in is written
    /// (machine-config 3, D267).
    /// </summary>
    public const string NoOtherToolToPrepare = "CMP006";

    /// <summary>
    /// CMP007: ORIENT with an angle other than 0 on a spindle whose ORIENT template has no {angle} (machine-config 5,
    /// D154).
    /// </summary>
    public const string OrientAngleWithoutTemplate = "CMP007";

    /// <summary>
    /// CMP008: a tool called by name on a machine with tool_name_allowed = true whose [tool_change] has no
    /// change_named or preload_named for it (machine-config 3, D172).
    /// </summary>
    public const string NamedToolTemplateMissing = "CMP008";

    /// <summary>
    /// CMP015: the [tolerance] template the compiler writes has no placeholder for a value the program set, {tol},
    /// {rotary} or {mode}, so the control keeps the value it holds; once per distinct value per program, never for
    /// TOLERANCE=OFF, and also for a TOLERANCE:ROTARY on a machine without ON_ROTARY. An INFO when held_value is at
    /// least as tight as the program's TOLERANCE, else a WARNING (machine-config 5, D151, D367).
    /// </summary>
    public const string ToleranceValueNotWritten = "CMP015";

    /// <summary>
    /// CMP017: an ARC with a tool vector TX TY TZ compiled for a controller without a vector arc, Heidenhain (LN has no
    /// arc form) and, until a Siemens vector arc is checked, Siemens; an ERROR at compile time like RAW of another
    /// controller (D185).
    /// </summary>
    public const string VectorArcNotWritable = "CMP017";

    /// <summary>
    /// CMP016, an INFO: the program names a measurement point other than 1 and the machine file has no [tool_points],
    /// so nothing selects it; once per program (machine-config 3, D373).
    /// </summary>
    public const string ToolPointWithoutForm = "CMP016";

    /// <summary>
    /// CMP032, a WARNING: the program's TIP_OFFSET or POINT_RADIUS of a point differs from the [[tool.point]] row of
    /// the tool table by more than one output step of the tool-axis address; no coordinate is corrected
    /// (machine-config 1, D370, D376).
    /// </summary>
    public const string ToolPointDiffersFromTable = "CMP032";

    /// <summary>
    /// CMP033, a WARNING: an OFFSET word on a holder whose tool named its measurement point replaces the register of
    /// the [tool_points] mapping with its own (D393).
    /// </summary>
    public const string OffsetReplacesPointRegister = "CMP033";

    /// <summary>
    /// CMP010: a word of the program needs a template that the machine file does not give, so nothing can be written
    /// for it (machine-config 2, 3).
    /// </summary>
    public const string TemplateMissing = "CMP010";

    /// <summary>
    /// CMP011, a WARNING: PRELOAD on a machine without a preload template is dropped (machine-config 3).
    /// </summary>
    public const string PreloadDropped = "CMP011";

    /// <summary>
    /// CMP020, a WARNING: a value has more decimals than the machine takes for its address, and is written rounded to
    /// them (machine-config 2, phase 3 P3-03).
    /// </summary>
    public const string MoreDecimalsThanTheMachineTakes = "CMP020";

    /// <summary>
    /// CMP021, a WARNING: a line is longer than the max_line_length of the machine (machine-config 2).
    /// </summary>
    public const string LineLongerThanTheMachineTakes = "CMP021";

    /// <summary>
    /// CMP022, a WARNING: a comment holds a character outside the comment_charset of the machine that has no
    /// transliteration (machine-config 2).
    /// </summary>
    public const string CommentCharacterOutsideCharset = "CMP022";
}
