namespace Ncx.Compilers;

// The codes of the Siemens compiler (P5-02, the folder Siemens/), CMP500 to CMP699 (D98): CMP500-CMP509 the block and
// the program layout, CMP510-CMP529 the tools, the spindles and the functions, CMP530-CMP549 the frames and the
// transformations, CMP550-CMP569 the motion, CMP570-CMP589 the cycles, CMP590-CMP609 the flow, the variables and the
// expressions, CMP610-CMP619 the channels.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CMP500: a word of the block has no SINUMERIK form the compiler writes, and the compiler never drops a word
    /// silently (language 2 rule 8; controllers siemens.md 12).
    /// </summary>
    public const string SiemensWordNotWritten = "CMP500";

    /// <summary>
    /// CMP501, a WARNING: the name of a program or subprogram holds a character a SINUMERIK name does not, and is
    /// written with an underscore for it (controllers siemens.md 1).
    /// </summary>
    public const string SiemensNameChanged = "CMP501";

    /// <summary>
    /// CMP502: SKIP on a block with TOOL or PRELOAD; the tool change is written from [tool_change] without the skip
    /// mark (controller-mapping 1, SKIP; machine-config 3).
    /// </summary>
    public const string SiemensSkipOnToolChange = "CMP502";

    /// <summary>
    /// CMP503, a WARNING: two programs or two subprograms of the file have one unit name, and the later one is written
    /// with _2 behind it, so that neither is lost (controllers siemens.md 1, 12 rule 1; language 2 rule 8).
    /// </summary>
    public const string SiemensUnitNameTaken = "CMP503";

    /// <summary>
    /// CMP510, a WARNING: OFFSET:LEN and OFFSET:RAD name two registers, and the D of the control is one register for
    /// length and radius (controller-mapping 3, OFFSET:LEN; controllers siemens.md 5).
    /// </summary>
    public const string SiemensOffsetRegistersDiffer = "CMP510";

    /// <summary>
    /// CMP511: SETMS needs the number of a spindle, which neither the templates of its role nor its resource give
    /// (controllers siemens.md 5, 12 rule 3).
    /// </summary>
    public const string SiemensSpindleNumberUnknown = "CMP511";

    /// <summary>
    /// CMP512: SPINDLE_SYNC names another pair than the one [spindle_sync] couples, the default spindle leading the
    /// other work spindle (machine-config 5; language 4.5; D154).
    /// </summary>
    public const string SiemensSpindlePairNotWritable = "CMP512";

    /// <summary>
    /// CMP513, a WARNING: MFUNC writes a raw M function that the machine configuration does not name (language 4.6).
    /// </summary>
    public const string SiemensRawMFunction = "CMP513";

    /// <summary>
    /// CMP514: a measurement point that the edge form cannot map: under edge_numbering = "flat" a point without a
    /// [[tool_point]] row (machine-config 3, D373).
    /// </summary>
    public const string SiemensToolPointNotMapped = "CMP514";

    /// <summary>
    /// CMP530: ORIGIN names a datum beyond G599, the last settable frame of the control (controllers siemens.md 4;
    /// controller-mapping 1, ORIGIN).
    /// </summary>
    public const string SiemensOriginNotWritable = "CMP530";

    /// <summary>
    /// CMP531: an entry of the frame chain has a value that is not known where the compiler must write it (language
    /// 4.2; D31).
    /// </summary>
    public const string SiemensChainNotWritable = "CMP531";

    /// <summary>
    /// CMP532: a tilt the [transform] template of the machine cannot write: TILT_AXIS without TILT_AXIS_ON, or a MOVE
    /// that {dir} has no value for (machine-config 5; controller-mapping 1, TILT, MOVE; D82).
    /// </summary>
    public const string SiemensTiltNotWritable = "CMP532";

    /// <summary>
    /// CMP533, a WARNING: an option the control or the machine has no form for is not written, ROT of a tilt, a
    /// rotary option without its template, or PATH_MODE with an empty or missing PATH_MODE template (controller-mapping
    /// 1, MOVE and ROT; machine-config 5; D82, D86, D349).
    /// </summary>
    public const string SiemensOptionNotWritten = "CMP533";

    /// <summary>
    /// CMP534: RETRACT without a template in [retract], which only the kinematics module could compute
    /// (controller-mapping 1, RETRACT; D83).
    /// </summary>
    public const string SiemensRetractNotWritable = "CMP534";

    /// <summary>
    /// CMP550: an ARC with ANGLE whose start, center or end the virtual machine does not know, so that neither AR nor
    /// TURN can be written (language 4.3, ANGLE; controllers siemens.md 12 rule 2; D84).
    /// </summary>
    public const string SiemensArcNotWritable = "CMP550";

    /// <summary>
    /// CMP570: a cycle the cycle catalog of the machine has no entry for (language 4.7.1; machine-config 6).
    /// </summary>
    public const string SiemensCycleNotInCatalog = "CMP570";

    /// <summary>
    /// CMP571: a word of the cycle that its entry maps to no position of the signature, and no rule of the family
    /// carries (machine-config 6; controllers siemens.md 12 rule 5).
    /// </summary>
    public const string SiemensCycleWordNotWritable = "CMP571";

    /// <summary>
    /// CMP572: a value the call of the cycle needs is not known: the plane RTP returns to, the reference plane, or the
    /// drilling axis (controller-mapping 5; D188).
    /// </summary>
    public const string SiemensCycleValueMissing = "CMP572";

    /// <summary>
    /// CMP573: CLEARANCE lies on the other side of SURFACE than SAFE, and SDIS is a distance without a sign that moves
    /// the reference plane towards the retract plane (controller-mapping 5; D334).
    /// </summary>
    public const string SiemensCycleClearanceOnTheWrongSide = "CMP573";

    /// <summary>
    /// CMP574, a WARNING: a word of the cycle that its entry maps to no position while the native cycle reaches the
    /// same value from another word of the block, the CYCLE_F of TAP and TAP_COMPENSATING, which tap with the feed of
    /// PITCH; it is not written (D362, D181; controller-mapping 5).
    /// </summary>
    public const string SiemensCycleWordDerived = "CMP574";

    /// <summary>
    /// CMP575, an ERROR: a native cycle of CYCLE:SIEMENS=n whose signature the cycle catalog does not know, whose
    /// parameters are not written positionally (D279; language 4.7.1, D94).
    /// </summary>
    public const string SiemensNativeCycleSignatureUnknown = "CMP575";

    /// <summary>
    /// CMP576, a WARNING: a position the control needs for the cycle stays empty because no NCX word carries it, the
    /// length, width, feed and VARI of SLOT1, the spindle of CYCLE830 (D379, D380; SW4.92 p.1053, 1121).
    /// </summary>
    public const string SiemensCycleRequiredPositionEmpty = "CMP576";

    /// <summary>
    /// CMP590: an expression holds an operator or a function the SINUMERIK language has no form for (controllers
    /// siemens.md 8; language 4.12).
    /// </summary>
    public const string SiemensExpressionNotWritable = "CMP590";

    /// <summary>
    /// CMP591: a variable of the program has no SINUMERIK name and no entry of [variables] map covers it
    /// (machine-config 7; language 4.9; D173).
    /// </summary>
    public const string SiemensVariableNotMapped = "CMP591";

    /// <summary>
    /// CMP592: a SYS_ name that [system_variables] of the machine does not map (language 4.12; machine-config 7; D51).
    /// </summary>
    public const string SiemensSystemVariableNotMapped = "CMP592";

    /// <summary>
    /// CMP593: a call of a program outside the file with arguments or a repeat count, which EXTCALL does not take
    /// (controllers siemens.md 8; controller-mapping 6, CALL).
    /// </summary>
    public const string SiemensExternalCallNotWritable = "CMP593";

    /// <summary>
    /// CMP594: JUMP=END in a subprogram, which ends the program from the call (language 4.9; virtual machine 3.6),
    /// and a SINUMERIK subprogram has no documented form that ends the program: a GOTOF goes to a label of its own
    /// unit (controllers siemens.md 8).
    /// </summary>
    public const string SiemensJumpToEndInSubprogram = "CMP594";

    /// <summary>
    /// CMP595: a variable that more than one unit of the file uses and that [variables] declared of the machine does
    /// not name: a SINUMERIK DEF is local to its unit, so the compiler can neither declare the name once nor declare
    /// it twice (controllers siemens.md 8, 12 rule 7; machine-config 7; D261).
    /// </summary>
    public const string SiemensVariableNotDeclarable = "CMP595";

    /// <summary>
    /// CMP596: a bare F or S block under a modal call that a RAW line armed, which the compiler does not write,
    /// because S or F with G0 or G1 active calls the modal subprogram and the pattern of the RAW range would drill one
    /// hole more (controllers siemens.md 7; controller-mapping 5, CYCLE_F; D259).
    /// </summary>
    public const string SiemensWordUnderARawModalCall = "CMP596";

    /// <summary>
    /// CMP597: JUMP=END in a subprogram whose PROC line, a RAW line of the file, declares SAVE: the return
    /// RET("label",,$P_STACK) that ends the program from the call (D344) cannot stand there, since RET "can only be
    /// used in subprograms that were not defined with the attribute SAVE" (840D sl NC-Programmierung SW4.92, p.523).
    /// The manual contradicts itself: its DISPLOF example (p.518) ends a PROC ... SAVE DISPLOF with RET, and p.526 says
    /// a return over several levels evaluates the SAVE of each level; CMP597 is the conservative reading of p.523.
    /// </summary>
    // TODO(question): which reading of the manual holds, RET forbidden in a SAVE subprogram (p.523) or allowed (the
    // examples of p.518 and the rule of p.526), is open (open questions, manual audit 2026-10-26, row 1).
    public const string SiemensReturnInSaveSubprogram = "CMP597";

    /// <summary>
    /// CMP598: a RAW PROC line in a SUB section that names another subprogram or declares other parameters than the
    /// REAL ones its calls give: the unit carries one PROC line, and the compiler does not guess which (controllers
    /// siemens.md 8, 12 rule 1; 840D sl NC-Programmierung SW4.92, p.510-512).
    /// </summary>
    public const string SiemensProcLineNotOwn = "CMP598";

    /// <summary>
    /// CMP610: SYNC without a wait template in [sync], or with a mark outside its mark_range (machine-config 5;
    /// controllers siemens.md 9).
    /// </summary>
    public const string SiemensMarkNotWritable = "CMP610";
}
