namespace Ncx.Compilers;

// The codes of the Fanuc compiler (P3-06), CMP300 to CMP499: CMP300-CMP319 the program frame and the flow,
// CMP320-CMP339 the motion, CMP340-CMP359 the tool, spindle and function words, CMP360-CMP379 the frames,
// CMP380-CMP399 the cycles, CMP400-CMP419 the variables and expressions, CMP420-CMP429 the wait marks.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CMP300, retired 2026-10-27 (D257, D388 3): a program without NUMBER takes a NAME of digits or the lowest free O
    /// number with the WARNING CMP310. The number is not reused.
    /// </summary>
    [Obsolete("Retired by D257: a program without NUMBER takes a generated O number, CMP310.")]
    public const string FanucProgramWithoutNumber = "CMP300";

    /// <summary>
    /// CMP301: a SUB whose NAME is no number has no O number, which names a Fanuc subprogram (controllers fanuc.md 1).
    /// </summary>
    public const string FanucSubNameNotAProgramNumber = "CMP301";

    /// <summary>
    /// CMP302: a conditional JUMP=END in a subprogram has no Fanuc form, since a GOTO stays in its program (controllers
    /// fanuc.md 7).
    /// </summary>
    public const string FanucConditionalEndInSub = "CMP302";

    /// <summary>
    /// CMP303: IF with CALL has no Fanuc form, since IF [ ] THEN only assigns and IF [ ] GOTO only jumps (controllers
    /// fanuc.md 7).
    /// </summary>
    public const string FanucConditionalCall = "CMP303";

    /// <summary>
    /// CMP304, retired 2026-10-27 (D280): REPEAT is written with the counters of [variables] repeat_counters, CMP314
    /// and CMP315. The number is not reused.
    /// </summary>
    [Obsolete("Retired by D280: REPEAT is written with [variables] repeat_counters, CMP314 and CMP315.")]
    public const string FanucRepeatNotWritten = "CMP304";

    /// <summary>
    /// CMP305: CALL of a name that is no Fanuc program number (controllers fanuc.md 1, 7).
    /// </summary>
    public const string FanucCallOfNoProgramNumber = "CMP305";

    /// <summary>
    /// CMP306: an ARG outside the letter table of G65 (controllers fanuc.md 7).
    /// </summary>
    public const string FanucArgumentWithoutLetter = "CMP306";

    /// <summary>
    /// CMP307: SKIP on a block whose tool change or preload the compiler framework writes, which gets no skip mark.
    /// </summary>
    public const string FanucSkipOnToolChange = "CMP307";

    /// <summary>
    /// CMP308: a word of the block has no Fanuc form the compiler writes; nothing is dropped silently (language 2 rule
    /// 8).
    /// </summary>
    public const string FanucWordNotWritten = "CMP308";

    /// <summary>
    /// CMP309: a modal value that a block takes from the state before it, without stating it, differs by the path the
    /// control arrives on (a LABEL that a jump reaches, the block after a skipped one) or comes from an expression, and
    /// the control does not hold it on every path, so no one line is right on all of them (virtual machine 1; D53;
    /// language 2 rule 2).
    /// </summary>
    public const string FanucModalValueNotHeld = "CMP309";

    /// <summary>
    /// CMP310, a WARNING: a program without NUMBER whose NAME is no O number takes the lowest number from 1 that the
    /// output file does not use, 8000 to 9999 left to the builder, and the WARNING names it (D257, D388 3).
    /// </summary>
    // CMP310 was first the ERROR of a CALL that carries both a pass-through ARG:Vn={$Vn} and an argument value, retired
    // when D148 settled that block as the macro call; D388 (3) gives the number to the O-number WARNING of D257.
    public const string FanucProgramNumberGenerated = "CMP310";

    /// <summary>
    /// CMP311, a WARNING: a CALL whose ARG words are the pass-through of a caller's locals, ARG:Vn={$Vn}, is written
    /// as the subprogram call M98, which shares the caller's #1..#33 and carries no arguments (D148), while a G65
    /// macro call of the same locals converts to the same NCX text and is the local-variable level virtual machine 3.6
    /// gives a CALL; which of the two the block is, no document says (row 196 of
    /// ../../../docs/implementation/03-open-questions.md).
    /// </summary>
    public const string FanucCallFormOfTheSharedLocals = "CMP311";

    /// <summary>
    /// CMP312, a WARNING: a comment text with ( or ), whose first ) would end the Fanuc comment and leave the rest of
    /// the line to run as code, is written with [ and ] (D390, R1-14).
    /// </summary>
    public const string FanucBracketInComment = "CMP312";

    /// <summary>
    /// CMP313: UNITS changes the units after the first motion of a program, which a Fanuc control forbids while the
    /// program runs; the units stand in a block of their own at the start of the program (D283).
    /// </summary>
    public const string FanucUnitsChangeInProgram = "CMP313";

    /// <summary>
    /// CMP314: a REPEAT needs a counter of [variables] repeat_counters for its nesting depth, and the machine file has
    /// none or too few; no variable is guessed (D280, machine-config 7).
    /// </summary>
    public const string FanucRepeatCounterMissing = "CMP314";

    /// <summary>
    /// CMP315: a counter of [variables] repeat_counters is a variable the program uses itself, so the count of the
    /// REPEAT would change it (D280, machine-config 7).
    /// </summary>
    public const string FanucRepeatCounterInUse = "CMP315";

    /// <summary>
    /// CMP316: == or != compares two strings, and custom macro B has numbers only (D169; controllers fanuc.md 7).
    /// </summary>
    public const string FanucStringComparison = "CMP316";

    /// <summary>
    /// CMP317: an ARC with CENTER under CYLINDER, where G7.1 takes R only and never I, J or K, so the arc has no Fanuc
    /// output (D143; controller-mapping 1, CYLINDER).
    /// </summary>
    public const string FanucCenterUnderCylinder = "CMP317";

    /// <summary>
    /// CMP318: a block number N stands twice in one O program, so a GOTO to it would be ambiguous (D277; controllers
    /// fanuc.md 10 rule 6).
    /// </summary>
    public const string FanucBlockNumberTwice = "CMP318";

    /// <summary>
    /// CMP320: LINE or ARC with FRAME=MACHINE, which G53 would move at rapid (D242).
    /// </summary>
    public const string FanucFeedMotionInMachineFrame = "CMP320";

    /// <summary>
    /// CMP321: an incremental word on an axis without the incremental letter of G-code system A, or under G53
    /// (controllers fanuc.md 3, 4).
    /// </summary>
    public const string FanucIncrementalWordNotWritable = "CMP321";

    /// <summary>
    /// CMP322: the start of an arc is unknown, so I J K of its absolute CENTER cannot be written (controllers fanuc.md
    /// 4).
    /// </summary>
    public const string FanucArcStartUnknown = "CMP322";

    /// <summary>
    /// CMP323: a block that mixes absolute and incremental words ends at a point that is not known, so it cannot be
    /// written in one of the two modes of G90 and G91 (controllers fanuc.md 3).
    /// </summary>
    public const string FanucMixedWordsUnknownTarget = "CMP323";

    /// <summary>
    /// CMP324, a WARNING: the F of a helix is not known in the STATIC walk, so it is written as it stands, and under
    /// helix_feed = "arc" (parameter 1403#5 = 0) the control runs it along the arc in the plane, a faster feed along
    /// the path (machine-config 2; controllers fanuc.md 4; D354).
    /// </summary>
    public const string FanucHelixFeedNotScaled = "CMP324";

    /// <summary>
    /// CMP340, a WARNING: MFUNC is written as the M code it names, which no function table of the machine names
    /// (language 4.6).
    /// </summary>
    public const string FanucMFuncWritten = "CMP340";

    /// <summary>
    /// CMP341: a measurement point that the registers form cannot map: no [[tool_point]] row, no step rule, or a point
    /// above 1 under tool management (machine-config 3, D373).
    /// </summary>
    public const string FanucToolPointNotMapped = "CMP341";

    /// <summary>
    /// CMP342, a WARNING: the first tool-axis motion after a TOOL runs without a length offset, since the holder has
    /// neither OFFSET:LEN, OFFSET nor the register of a named point and the change applies none; once per tool change
    /// (D389, R1-13).
    /// </summary>
    public const string FanucToolWithoutLengthOffset = "CMP342";

    /// <summary>
    /// CMP343, a WARNING: the limit of RPM_MAX, G50 S or G92 S, is written while the control has another spindle
    /// selected, and the template names no spindle with {spindle} or a P word, so the control limits that spindle
    /// (D408).
    /// </summary>
    public const string FanucSpeedLimitOfAnotherSpindle = "CMP343";

    /// <summary>
    /// CMP360: the transform chain of the block cannot be written with the one G52, G68, G51.1 and G68.2 of a Fanuc
    /// control in program order (D31).
    /// </summary>
    public const string FanucChainNotWritable = "CMP360";

    /// <summary>
    /// CMP361, a WARNING: a word the Fanuc control has no option for is not written: ROT, ROTARY_PATH and ROTARY_FEED
    /// without a template, PATH_MODE with an empty or missing PATH_MODE template (controller-mapping 1, machine-config
    /// 5, D349).
    /// </summary>
    public const string FanucOptionNotWritten = "CMP361";

    /// <summary>
    /// CMP362: TILT_AXIS without a TILT_AXIS_ON template, which the target cannot write without the kinematics module
    /// (D82).
    /// </summary>
    public const string FanucTiltAxisNotWritable = "CMP362";

    /// <summary>
    /// CMP363: RETRACT without a template of [retract], which the target cannot write without the kinematics module
    /// (controller-mapping 1, D83).
    /// </summary>
    public const string FanucRetractNotWritable = "CMP363";

    /// <summary>
    /// CMP380: a cycle that the Fanuc cycle catalog of the machine does not name (machine-config 6, language 4.7.1).
    /// </summary>
    public const string FanucCycleNotInCatalog = "CMP380";

    /// <summary>
    /// CMP381: a drilling cycle of a mill whose AXIS is not the tool axis of the plane (controller-mapping 5, AXIS).
    /// </summary>
    public const string FanucCycleAxisNotTheToolAxis = "CMP381";

    /// <summary>
    /// CMP382, a WARNING: G98 returns to the initial level, where the drilling axis stood before the cycle, and SAFE is
    /// another plane (controller-mapping 5).
    /// </summary>
    public const string FanucSafeIsNotTheInitialLevel = "CMP382";

    /// <summary>
    /// CMP383: CYCLE_RETRACT=SAFE on a lathe of G-code system A, whose G98 and G99 are the feed mode (controllers
    /// fanuc.md 3).
    /// </summary>
    public const string FanucSafeRetractOnSystemA = "CMP383";

    /// <summary>
    /// CMP384, retired 2026-10-27 (D278): a multiple repetitive cycle of a lathe with its CONTOUR is written with its
    /// contour inline now; the errors of that contour are CMP389 to CMP392. The number is not reused.
    /// </summary>
    [Obsolete("Retired by D278: G70 to G73 write their contour inline; CMP389 to CMP392 report what cannot be.")]
    public const string FanucContourCycleNotWritten = "CMP384";

    /// <summary>
    /// CMP385: a cycle word that the catalog entry maps to no address of the Fanuc cycle block and that no rule of the
    /// compiler carries, PITCH of THREAD, DEPTH of TURN_OD (machine-config 6; controller-mapping 5; language 2 rule 8).
    /// </summary>
    public const string FanucCycleWordNotMapped = "CMP385";

    /// <summary>
    /// CMP386: PITCH of a tapping cycle without CYCLE_F in feed per minute where the spindle that turns the tap, the
    /// spindle of the current tool holder, gives no speed: it is off, a positioning axis or under CSS, or its speed is
    /// not known, so the feed PITCH x S of G84 cannot be written (controller-mapping 5, TAP; virtual machine 2.3;
    /// language 2 rule 8).
    /// </summary>
    public const string FanucTapFeedNotKnown = "CMP386";

    /// <summary>
    /// CMP387, an INFO: the Fanuc control counts the peck Q from its own R plane, while NCX counts PECK from SURFACE;
    /// the compiler writes Q = PECK and names the shift once, at the block that defines the cycle, where SURFACE and
    /// CLEARANCE differ (D355).
    /// </summary>
    public const string FanucPeckCountsFromR = "CMP387";

    /// <summary>
    /// CMP388, a WARNING: a CYCLE_DWELL other than 0 on a cycle whose Fanuc code takes no dwell P and that has no
    /// dwell form, PECK (G83), CHIP_BREAK (G73) and BORE (G86) on a machining centre, or DRILL where the catalog has
    /// no DRILL_DWELL; the dwell is dropped (D177).
    /// </summary>
    public const string FanucCycleDwellDropped = "CMP388";

    /// <summary>
    /// CMP389: the first block of the contour of G71 or G72 moves both plane axes, the type II pocketing cycle, on a
    /// machine whose [cycles] declares no contour_type_ii (D278, D388 item 11).
    /// </summary>
    public const string FanucContourTypeIINotDeclared = "CMP389";

    /// <summary>
    /// CMP390: the first block of the contour of G71 does not move X (of G72 Z), which the control rejects with alarm
    /// PS0325 (D278).
    /// </summary>
    public const string FanucContourFirstBlockWithoutInfeed = "CMP390";

    /// <summary>
    /// CMP391: a contour section of G70 to G73 holds a block other than G04, G00 to G03 motion, or a LABEL or JUMP
    /// whose target is inside the range: a CALL, a cycle, a macro call (D278).
    /// </summary>
    public const string FanucContourBlockNotAllowed = "CMP391";

    /// <summary>
    /// CMP392: the first block of a contour of G70 to G73 is no RAPID or LINE, while the first block writes G00 or G01
    /// (D278).
    /// </summary>
    public const string FanucContourFirstBlockNotLinear = "CMP392";

    /// <summary>
    /// CMP393: PECK on a Fanuc cycle code that takes no Q: G76, G87, G88 and G89, and G74 and G84 where the machine
    /// declares no peck rigid tapping ([cycles] peck_rigid_tapping; D279, D388 item 11).
    /// </summary>
    public const string FanucPeckWithoutQ = "CMP393";

    /// <summary>
    /// CMP394: a G73 or G83 cycle without PECK; Q is modal data the native cycles share, so the control would peck
    /// with a Q another cycle left (D279).
    /// </summary>
    public const string FanucPeckNotGiven = "CMP394";

    /// <summary>
    /// CMP395: a native Fanuc cycle parameter whose key is not one letter A..Z, Q215=1 on CYCLE:FANUC (D279).
    /// </summary>
    public const string FanucNativeParameterNotALetter = "CMP395";

    /// <summary>
    /// CMP396: a drilling cycle of a lathe where the drilling-axis position before the cycle, the initial level R
    /// counts from, is not known, so R cannot be written (D248).
    /// </summary>
    public const string FanucLatheRWithoutInitialLevel = "CMP396";

    /// <summary>
    /// CMP397: P or Q of G74, G75 or G76 with a fraction; the lathe takes them in least input increments without a
    /// decimal point, so the value cannot be written (D409; 0i-D B-64304EN-1-01 p.85-89).
    /// </summary>
    public const string FanucCycleIncrementWithFraction = "CMP397";

    /// <summary>
    /// CMP400: a variable that neither the V names of custom macro B nor [variables] map nor [system_variables] name a
    /// Fanuc variable for (language 4.9, machine-config 7, D51).
    /// </summary>
    public const string FanucVariableNotMapped = "CMP400";

    /// <summary>
    /// CMP401: an expression, a function or a value that custom macro B cannot write (controllers fanuc.md 7).
    /// </summary>
    public const string FanucExpressionNotWritable = "CMP401";

    /// <summary>
    /// CMP420: a SYNC mark outside the mark_range of [sync], or a SYNC on a machine without a wait template
    /// (machine-config 5, controllers fanuc.md 8).
    /// </summary>
    public const string FanucMarkNotWritable = "CMP420";
}
