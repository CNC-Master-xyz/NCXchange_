using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// The canned cycles of the Fanuc compiler (controllers fanuc.md 6; controller-mapping 5; language 4.7): the
/// definition written with its first call, R and Z from the absolute planes, G98 and G99 from CYCLE_RETRACT, F where
/// the control's feed differs, the positions of the later calls, G80.
/// </summary>
public sealed class FanucCyclesTests
{
    // The cycle catalog of the Fanuc mills, which carries TAP_COMPENSATING (D359) beside the built-in family.
    private const string MillCatalog = "[cycles]\ncatalog = \"fanuc.toml\"";

    // The drilling of BOHREN.fanuc.nc: tool, spindle, the position and the approach before the cycle.
    private const string Approach = "TOOL=1\nSPINDLE=CW RPM=1000\nRAPID X=10 Y=10\nRAPID Z=5";

    // Controller-mapping 5, fanuc 6: G81 G99 Z-21.732 R5. F565 defines and calls at its position; every later call is
    // its position alone; CYCLE=OFF is G80, after which the motion writes G0 again (fanuc 3, group 01).
    [Fact]
    public void Drill_DefinedAndCalled_IsG81WithRZAndFThenThePositions()
    {
        string body = FanucCompile.Body(Approach
            + "\nCYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE CYCLE_F=565\nCYCLE_CALL"
            + "\nCYCLE_CALL X=30\nCYCLE=OFF\nRAPID Z=50");

        Assert.Equal(
            "T1 M6\nS1000 M3\nG0 G17 X10. Y10.\nZ5.\nG81 G99 Z-21.732 R5. F565.\nX30.\nG80\nG0 Z50.\n", body);
    }

    // D218, BOHREN.fanuc.nc N170: the F of a cycle block is the control's feed, so a later cycle with the same CYCLE_F
    // writes none; PECK is Q.
    [Fact]
    public void Peck_WithTheFeedOfTheControl_WritesNoF()
    {
        string body = FanucCompile.Body(Approach
            + "\nCYCLE=DRILL CLEARANCE=5 DEPTH=-20 CYCLE_F=565\nCYCLE_CALL\nCYCLE=OFF"
            + "\nCYCLE=PECK CLEARANCE=5 DEPTH=-20 PECK=1.2 CYCLE_F=565\nCYCLE_CALL X=30\nCYCLE=OFF");

        Assert.EndsWith("G81 G99 Z-20. R5. F565.\nG80\nG83 G99 X30. Z-20. R5. Q1.2\nG80\n", body,
            StringComparison.Ordinal);
    }

    // Controller-mapping 5, CYCLE_RETRACT: SAFE is G98, the initial level where the drilling axis stood.
    [Fact]
    public void Retract_ToSafe_IsG98()
    {
        string body = FanucCompile.Body(Approach
            + "\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 SAFE=5 CYCLE_RETRACT=SAFE CYCLE_F=100\nCYCLE_CALL");

        Assert.EndsWith("G81 G98 Z-10. R2. F100.\n", body, StringComparison.Ordinal);
    }

    // Controller-mapping 5: G98 returns to the initial level, so a SAFE plane elsewhere is a WARNING.
    [Fact]
    public void Retract_ToSafeAwayFromTheInitialLevel_IsAWarningCmp382()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program((Approach
            + "\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 SAFE=20 CYCLE_RETRACT=SAFE CYCLE_F=100\nCYCLE_CALL").Split('\n')),
            FanucCompile.Mill());

        Assert.Contains(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucSafeIsNotTheInitialLevel);
    }

    // Fanuc 6: a position block under an active G81 drills, so a motion after a call without CYCLE=OFF writes G80
    // first.
    [Fact]
    public void Motion_AfterACallWithoutCycleOff_EndsTheCycleWithG80()
    {
        string body = FanucCompile.Body(Approach
            + "\nCYCLE=DRILL CLEARANCE=5 DEPTH=-20 CYCLE_F=100\nCYCLE_CALL\nRAPID Z=50");

        Assert.EndsWith("G81 G99 Z-20. R5. F100.\nG80\nG0 Z50.\n", body, StringComparison.Ordinal);
    }

    // BOHREN.fanuc.nc N3100 and N3110, controller-mapping 5 TAP: M29 S500 before G84 G99 Z-20. R5. P0 F750. D256:
    // the M29 line is the "before" of the TAP entry of the catalog, which the compiler writes from the cycle, with
    // the speed of the spindle that turns the tap.
    [Fact]
    public void Tap_WithDwellAndFeed_IsG84WithPAndFBehindTheRigidTappingLine()
    {
        string body = FanucCompile.Body("TOOL=1\nSPINDLE=CW RPM=500\nRAPID X=10 Y=10\nRAPID Z=5"
            + "\nCYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE "
            + "CYCLE_F=750 CYCLE_DWELL=0 PITCH=1.5\nCYCLE_CALL");

        Assert.EndsWith("M29 S500\nG84 G99 Z-20. R5. P0 F750.\n", body, StringComparison.Ordinal);
    }

    // D256: the rigid-tapping line enters a mode that holds for the whole run of the cycle, and its evidence has an
    // M29 inside a running tapping cycle alarm PS0204, so a second definition of the same CYCLE=TAP - another DEPTH
    // until CYCLE=OFF - is written without the line.
    [Fact]
    public void Tap_WithASecondDefinitionOfTheRun_WritesTheRigidTappingLineOnlyOnce()
    {
        string body = FanucCompile.Body("TOOL=1\nSPINDLE=CW RPM=500\nRAPID X=10 Y=10\nRAPID Z=5"
            + "\nCYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=750 PITCH=1.5\nCYCLE_CALL"
            + "\nCYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-25 CYCLE_RETRACT=CLEARANCE CYCLE_F=750 PITCH=1.5\nCYCLE_CALL"
            + "\nCYCLE=OFF");

        Assert.EndsWith("M29 S500\nG84 G99 Z-20. R5. F750.\nG84 G99 Z-25. R5.\nG80\n", body,
            StringComparison.Ordinal);
    }

    // D361, D377: the rigid-tapping line is the machine's, and a [[cycle]] table that names the compiled channel
    // overrides the channel-less entry for that channel.
    [Fact]
    public void Tap_WithATableOfTheCompiledChannel_WritesThatChannelsLine()
    {
        string body = FanucCompile.Body("TOOL=1\nSPINDLE=CW RPM=500\nRAPID X=10 Y=10\nRAPID Z=5"
            + "\nCYCLE=TAP CLEARANCE=5 DEPTH=-20 CYCLE_F=750\nCYCLE_CALL",
            "[[cycle]]\nname = \"TAP\"\nchannel = 1\nnative = \"G84\"\nmodal = true\nbefore = \"M329 S{rpm}\"");

        Assert.EndsWith("M329 S500\nG84 G99 Z-20. R5. F750.\n", body, StringComparison.Ordinal);
    }

    // D359: TAP_COMPENSATING, the floating tap, is G84 without the rigid-tapping line.
    [Fact]
    public void TapCompensating_IsG84WithoutTheRigidTappingLine()
    {
        string body = FanucCompile.Body("TOOL=1\nSPINDLE=CW RPM=500\nRAPID X=10 Y=10\nRAPID Z=5"
            + "\nCYCLE=TAP_COMPENSATING CLEARANCE=5 DEPTH=-20 CYCLE_F=750\nCYCLE_CALL", MillCatalog);

        Assert.EndsWith("Z5.\nG84 G99 Z-20. R5. F750.\n", body, StringComparison.Ordinal);
        Assert.DoesNotContain("M29", body, StringComparison.Ordinal);
    }

    // D359: where the machine's TAP sets before = "", its G84 is always rigid and TAP_COMPENSATING is not in its
    // catalog: CMP380.
    [Fact]
    public void TapCompensating_OnAMachineWhoseG84IsAlwaysRigid_IsTheErrorCmp380()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program(("TOOL=1\nSPINDLE=CW RPM=500\nRAPID Z=5"
            + "\nCYCLE=TAP_COMPENSATING CLEARANCE=5 DEPTH=-20 CYCLE_F=750\nCYCLE_CALL").Split('\n')),
            FanucCompile.Mill(extra: MillCatalog
                + "\n\n[[cycle]]\nname = \"TAP\"\nnative = \"G84\"\nmodal = true\nbefore = \"\""));

        Assert.Contains(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucCycleNotInCatalog);
    }

    // D359, D256: a TAP_COMPENSATING after a TAP run on the same G84 ends the rigid mode with G80 first, and a TAP
    // after a TAP_COMPENSATING run writes its line only after G80, since an M29 inside a running tapping cycle
    // alarms PS0204.
    [Fact]
    public void TapAndTapCompensating_OnOneG84_EndTheRunWithG80BetweenThem()
    {
        string body = FanucCompile.Body("TOOL=1\nSPINDLE=CW RPM=500\nRAPID X=10 Y=10\nRAPID Z=5"
            + "\nCYCLE=TAP CLEARANCE=5 DEPTH=-20 CYCLE_F=750\nCYCLE_CALL"
            + "\nCYCLE=TAP_COMPENSATING CLEARANCE=5 DEPTH=-20 CYCLE_F=750\nCYCLE_CALL X=20"
            + "\nCYCLE=TAP CLEARANCE=5 DEPTH=-20 CYCLE_F=750\nCYCLE_CALL X=30", MillCatalog);

        Assert.EndsWith("M29 S500\nG84 G99 Z-20. R5. F750.\nG80\nG84 G99 X20. Z-20. R5.\nG80\nM29 S500\n"
            + "G84 G99 X30. Z-20. R5.\n", body, StringComparison.Ordinal);
    }

    // Language 4.7 CYCLE_DWELL (seconds), controllers fanuc.md 6 (P in milliseconds) and 7: an expression dwell is P
    // with the expression times 1000.
    [Fact]
    public void CycleDwell_AsAnExpression_IsPWithTheExpressionTimes1000()
    {
        string body = FanucCompile.Body(Approach
            + "\nVAR:V1=2\nCYCLE=DRILL_DWELL CLEARANCE=5 DEPTH=-20 CYCLE_DWELL={$V1} CYCLE_F=100\nCYCLE_CALL");

        Assert.EndsWith("#1 = 2\nG82 G99 Z-20. R5. P[#1 * 1000] F100.\n", body, StringComparison.Ordinal);
    }

    // Controller-mapping 5 TAP: without CYCLE_F the feed of G84 is the pitch times the speed per minute.
    [Fact]
    public void Tap_WithPitchOnly_FeedsThePitchTimesTheSpeed()
    {
        string body = FanucCompile.Body(Approach + "\nCYCLE=TAP CLEARANCE=5 DEPTH=-20 PITCH=1.5\nCYCLE_CALL");

        Assert.EndsWith("G84 G99 Z-20. R5. F1500.\n", body, StringComparison.Ordinal);
    }

    // Controller-mapping 5 TAP, controllers fanuc.md 7: a pitch from an expression feeds with the expression times the
    // known speed, the speed a constant of the expression.
    [Fact]
    public void Tap_WithAPitchExpression_FeedsTheExpressionTimesTheSpeed()
    {
        string body = FanucCompile.Body(
            Approach + "\nVAR:V2=1.5\nCYCLE=TAP CLEARANCE=5 DEPTH=-20 PITCH={$V2}\nCYCLE_CALL");

        Assert.EndsWith("#2 = 1.5\nM29 S1000\nG84 G99 Z-20. R5. F[#2 * 1000]\n", body, StringComparison.Ordinal);
    }

    // Controller-mapping 5 TAP, language 2 rule 8: the feed of G84 is the pitch times the speed, and a speed from an
    // expression is not known in STATIC mode (virtual machine 1), so the pitch cannot be written and is an ERROR.
    [Fact]
    public void Tap_WithPitchAndASpeedFromAnExpression_IsTheErrorCmp386()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(FanucCompile.Program(
            "VAR:V1=500", "TOOL=1", "SPINDLE=CW RPM={$V1}", "RAPID X=10 Y=10", "RAPID Z=4",
            "CYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE PITCH=1.5", "CYCLE_CALL"),
            FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucTapFeedNotKnown, error.Code);
    }

    // Controller-mapping 5 TAP, virtual machine 2.3 and 5: the tap turns with the spindle of the current tool holder,
    // the driven tool TOOL of the turret, so the feed per minute is PITCH times its 800, not the speed of the main
    // spindle, which is off.
    [Fact]
    public void Tap_OnTheDrivenToolOfALathe_FeedsWithTheSpeedOfTheDrivenTool()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=500",
            "SPINDLE:MAIN=OFF",
            "TOOL=1 OFFSET=1",
            "SPINDLE:TOOL=CW RPM:TOOL=800",
            "RAPID X=0 Z=5",
            "CYCLE=TAP CLEARANCE=2 DEPTH=-10 PITCH=1",
            "CYCLE_CALL",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END"), FanucCompile.Lathe()));

        Assert.Contains("\nG84 Z-10. R-3. F800.\n", text, StringComparison.Ordinal);
    }

    // Controller-mapping 5 TAP, language 2 rule 8: the spindle of the current tool holder is off, so PITCH x S has no
    // speed, and G84 would tap with the feed the control has: CMP386.
    [Fact]
    public void Tap_WhileTheSpindleOfTheHolderIsOff_IsTheErrorCmp386()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=500",
            "TOOL=1 OFFSET=1",
            "RAPID X=0 Z=5",
            "CYCLE=TAP CLEARANCE=2 DEPTH=-10 PITCH=1",
            "CYCLE_CALL",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END"), FanucCompile.Lathe()));

        Assert.Equal(DiagnosticCodes.FanucTapFeedNotKnown, error.Code);
    }

    // D175: the catalog entry of G92 (THREAD_PASS, D161) maps PITCH to F, the thread lead, so the compiler writes it
    // through that address instead of turning it into a feed per revolution as it does for TAP (decisions.md;
    // fanuc 6; machine-config 6).
    [Fact]
    public void ThreadPassCycle_WithPitch_WritesTheLeadAsTheFOfTheCycleBlock()
    {
        const string Thread =
            "[[cycle]]\nname = \"THREAD_PASS\"\nnative = \"G92\"\nmodal = true\nparams = { PITCH = \"F\" }";
        string text = FanucCompile.TextOf(FanucCompile.Run(FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=500",
            "RAPID X=70 Z=2",
            "CYCLE=THREAD_PASS PITCH=1.5",
            "CYCLE_CALL X=60 Z=-30",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END"), FanucCompile.Lathe(FanucCompile.DefaultSync, Thread)));

        Assert.Contains("\nG92 X60. Z-30. F1.5\n", text, StringComparison.Ordinal);
    }

    // Machine-config 6, language 2 rule 8, D175: an entry of G92 that maps no address to PITCH leaves the thread
    // cycle cutting with the feed the control has: CMP385, never a thread without its lead. D161: G92 is
    // THREAD_PASS, the modal cycle that cuts one pass per block.
    [Fact]
    public void Thread_WithPitch_IsTheErrorCmp385()
    {
        const string ThreadPass = "[[cycle]]\nname = \"THREAD_PASS\"\nnative = \"G92\"\nmodal = true\n"
            + "params = { CYCLE_F = \"F\" }";
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=500",
            "RAPID X=70 Z=2",
            "CYCLE=THREAD_PASS PITCH=1.5",
            "CYCLE_CALL X=60 Z=-30",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END"), FanucCompile.Lathe(FanucCompile.DefaultSync, ThreadPass)));

        Assert.Equal(DiagnosticCodes.FanucCycleWordNotMapped, error.Code);
    }

    // Machine-config 6, language 2 rule 8, D175: the catalog entry of G90 maps neither DEPTH nor CLEARANCE, whose Z and
    // R would be no planes of the turning cycle (R is its taper): CMP385 for each.
    [Fact]
    public void TurnOd_WithDepthAndClearance_IsTheErrorCmp385()
    {
        const string TurnOd =
            "[[cycle]]\nname = \"TURN_OD\"\nnative = \"G90\"\nmodal = true\nparams = { CYCLE_F = \"F\" }";
        CompileResult result = FanucCompile.Run(FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=500",
            "RAPID X=70 Z=2",
            "CYCLE=TURN_OD CYCLE_F=0.3 CLEARANCE=2 DEPTH=-30",
            "CYCLE_CALL X=60 Z=-30",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END"), FanucCompile.Lathe(FanucCompile.DefaultSync, TurnOd));

        Assert.Empty(result.Files);
        Assert.Equal(2, FanucCompile.CompilerDiagnostics(result).Count(
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucCycleWordNotMapped));
    }

    // Language 4.7 (PITCH is the pitch of TAP), D157: no address and no rule of G81 carries PITCH: CMP385.
    [Fact]
    public void Drill_WithPitch_IsTheErrorCmp385()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(FanucCompile.Program((Approach
            + "\nCYCLE=DRILL CLEARANCE=5 DEPTH=-20 PITCH=1.5\nCYCLE_CALL").Split('\n')), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucCycleWordNotMapped, error.Code);
    }

    // Language 4.3 (F is modal) and 5 rule 2, D29, D218: F of a CYCLE_CALL is the feed of the program, while the F of a
    // Fanuc cycle block is the feed of the cycle, so the next LINE writes it.
    [Fact]
    public void Call_WithAFeed_LeavesTheFeedToTheNextLine()
    {
        string body = FanucCompile.Body(Approach
            + "\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=100\nCYCLE_CALL X=5 F=300\nCYCLE=OFF\nLINE X=10");

        Assert.EndsWith("G81 G99 X5. Z-10. R2. F100.\nG80\nG1 X10. F300.\n", body, StringComparison.Ordinal);
    }

    // Language 4.3 and 5 rule 2, controllers fanuc.md 2 and 4: F of a CYCLE block, which writes nothing of its own
    // before its call, is F alone, and the cycle block writes its CYCLE_F.
    [Fact]
    public void Cycle_WithAFeed_WritesTheFeedAlone()
    {
        string body = FanucCompile.Body(Approach
            + "\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=100 F=300\nCYCLE_CALL\nCYCLE=OFF\nLINE X=10");

        Assert.EndsWith("F300.\nG81 G99 Z-10. R2. F100.\nG80\nG1 X10. F300.\n", body, StringComparison.Ordinal);
    }

    // Language 4.7 (CYCLE is modal), virtual machine 1: the call after the label executes DRILL where the control
    // comes from the block before and PECK where it comes from the jump, and Fanuc defines a cycle with its first call,
    // so the control holds neither: CMP309, never the G81 of the walk on both paths.
    [Fact]
    public void Call_AfterALabelThatAJumpReachesWithAnotherCycle_IsTheErrorCmp309()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(FanucCompile.Program((Approach
            + "\nVAR:V1=0\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=100\nLABEL=10\nCYCLE_CALL X=5"
            + "\nCYCLE=PECK CLEARANCE=2 DEPTH=-10 PECK=1 CYCLE_F=100\nCYCLE_CALL X=6\nVAR:V1={$V1 + 1}"
            + "\nJUMP=10 IF={$V1 < 3}").Split('\n')), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucModalValueNotHeld, error.Code);
    }

    // Controller-mapping 1, SKIP; language 4.7: the next call executes PECK where the skipped CYCLE block ran and DRILL
    // where it did not, and neither definition can stand for both paths: CMP309.
    [Fact]
    public void Call_AfterASkippedCycleBlock_IsTheErrorCmp309()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(FanucCompile.Program((Approach
            + "\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=100\nCYCLE_CALL"
            + "\nSKIP CYCLE=PECK CLEARANCE=2 DEPTH=-10 PECK=1\nCYCLE_CALL X=30").Split('\n')), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucModalValueNotHeld, error.Code);
    }

    // Language 4.7, virtual machine 1: DRILL is the cycle on both paths to the label, so the call after it writes the
    // definition again, as the control may have ended it.
    [Fact]
    public void Call_AfterALabelWithTheSameCycleOnEveryPath_DefinesItAgain()
    {
        string body = FanucCompile.Body(Approach
            + "\nVAR:V1=0\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=100\nLABEL=10\nCYCLE_CALL X=5\nRAPID Z=5"
            + "\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.EndsWith("N10\nG17 G90 G81 G99 X5. Z-10. R2. F100.\nG80\nG0 Z5.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body, StringComparison.Ordinal);
    }

    // D99, fanuc 6 (a position block under an active G81 drills; D247): a subprogram is written
    // from an unknown target state, so G80 stands before its first motion, and after its return the caller writes the
    // definition of the cycle again with its next call.
    [Fact]
    public void Call_OfASubprogramBetweenTwoCycleCalls_EndsTheCycleAndDefinesItAgain()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
            "TOOL=1",
            "SPINDLE=CW RPM=1000",
            "RAPID X=0 Y=0 Z=5",
            "CYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=100",
            "CYCLE_CALL X=5 Y=5",
            "CALL=100",
            "CYCLE_CALL X=10 Y=5",
            "CYCLE=OFF",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "RAPID X=20 Y=20",
            "SUB=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Mill()));

        Assert.Contains("\nG81 G99 X5. Y5. Z-10. R2. F100.\nM98 P0100\nG81 G99 X10. Y5. Z-10. R2.\nG80\nM30\n"
            + "O0100\nG80\nG0 G17 G90 X20. Y20.\nM99\n", text, StringComparison.Ordinal);
    }

    // Controllers fanuc.md 6, D59, D160: a lathe drills along X with G87, without G98 and G99 in system A, whose X
    // stays a diameter.
    [Fact]
    public void Drill_AlongXOnALatheOfSystemA_IsG87()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "RAPID X=60 Z=-10",
            "CYCLE=DRILL AXIS=X CLEARANCE=54 DEPTH=30 CYCLE_F=50",
            "CYCLE_CALL",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Lathe()));

        // D248: R is the signed distance from the initial level X60 as a radius, (54 - 60) / 2.
        Assert.Contains("\nG87 X30. R-3. F50.\nG80\n", text, StringComparison.Ordinal);
    }

    // D248: on a lathe R counts from the initial level, so a drilling cycle where the drilling axis is not known, after
    // a position from an expression in STATIC mode, is the ERROR CMP396.
    [Fact]
    public void Drill_OnALatheFromAnUnknownInitialLevel_IsTheErrorCmp396()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "VAR:V1=5",
            "RAPID Z={$V1}",
            "CYCLE=DRILL AXIS=Z CLEARANCE=2 DEPTH=-30 CYCLE_F=50",
            "CYCLE_CALL",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END");

        CompileResult result = FanucCompile.Run(program, FanucCompile.Lathe());

        Assert.Contains(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucLatheRWithoutInitialLevel);
    }

    // Language 4.7.1, controller-mapping 5: the simple turning cycle G90 of system A is modal, one call per block.
    [Fact]
    public void TurnOd_OnALatheOfSystemA_IsG90WithOneCallPerBlock()
    {
        const string TurnOd =
            "[[cycle]]\nname = \"TURN_OD\"\nnative = \"G90\"\nmodal = true\nparams = { CYCLE_F = \"F\" }";
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "RAPID X=70 Z=2",
            "CYCLE=TURN_OD CYCLE_F=0.3",
            "CYCLE_CALL X=60 Z=-70",
            "CYCLE_CALL X=50 Z=-70",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program,
            FanucCompile.Lathe(FanucCompile.DefaultSync, TurnOd)));

        // G80 cancels the drilling cycles only; the simple turning cycle is a code of group 01, which the next code of
        // group 01 ends (B-64484EN-1 p.46), so CYCLE=OFF writes no G80 for it.
        Assert.Contains("\nG90 X60. Z-70. F0.3\nX50. Z-70.\nM30\n", text, StringComparison.Ordinal);
    }

    // B-64484EN-1 p.46: a simple turning cycle ends with a code of group 01 other than G90, G92 and G94, and G80
    // cancels the drilling cycles only (p.309), so a RAPID that ends TURN_OD is G0 alone, the block the reader read it
    // from, never G0 G80.
    [Theory]
    [InlineData("RAPID X=100 CYCLE=OFF", "\nG90 X60. Z-70. F0.3\nG0 X100.\nM30\n")]
    [InlineData("CYCLE=OFF\nRAPID X=100", "\nG90 X60. Z-70. F0.3\nG0 X100.\nM30\n")]
    [InlineData("LINE X=80 F=0.2 CYCLE=OFF", "\nG90 X60. Z-70. F0.3\nG1 X80. F0.2\nM30\n")]
    public void TurnOd_EndedByAMotion_IsTheMotionCodeWithoutG80(string end, string written)
    {
        const string TurnOd =
            "[[cycle]]\nname = \"TURN_OD\"\nnative = \"G90\"\nmodal = true\nparams = { CYCLE_F = \"F\" }";
        var lines = new List<string>
        {
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "RAPID X=70 Z=2",
            "CYCLE=TURN_OD CYCLE_F=0.3",
            "CYCLE_CALL X=60 Z=-70",
        };
        lines.AddRange(end.Split('\n'));
        lines.Add("PROGRAM=END");
        lines.Add("FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(FanucCompile.Lines(lines.ToArray()),
            FanucCompile.Lathe(FanucCompile.DefaultSync, TurnOd)));

        Assert.Contains(written, text, StringComparison.Ordinal);
    }

    // A lathe takes no decimal point in Q (B-64484EN p.229), so the peck of G83 is written in least input increments,
    // 0.001 mm or 0.0001 inch, as the lathe manual writes it (B-64484EN-1 p.96, Q5000).
    [Theory]
    [InlineData("MM", "PECK=2", "X=0 Z=5", "CLEARANCE=2 DEPTH=-40", "\nG83 Z-40. R-3. Q2000 F0.2\n")]
    [InlineData("MM", "PECK=0.5", "X=0 Z=5", "CLEARANCE=2 DEPTH=-40", "\nG83 Z-40. R-3. Q500 F0.2\n")]
    [InlineData("INCH", "PECK=0.1", "X=0 Z=0.2", "CLEARANCE=0.1 DEPTH=-1", "\nG83 Z-1. R-0.1 Q1000 F0.2\n")]
    public void Peck_OnALathe_IsQInLeastInputIncrements(string units, string peck, string start, string planes,
        string written)
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            $"FEED_MODE=PER_REV COMP=OFF UNITS={units} WORKPLANE=XY DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=500",
            "RAPID " + start,
            $"CYCLE=PECK {planes} {peck} CYCLE_F=0.2",
            "CYCLE_CALL",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END"), FanucCompile.Lathe()));

        Assert.Contains(written, text, StringComparison.Ordinal);
    }

    // B-64484EN-2 p.104 and p.108: an S command between M29 and G84 is alarm PS0203 and an M29 inside a running tapping
    // cycle PS0204, so a new speed inside a run of the rigid TAP ends the run with G80 before the S and enters it again
    // with the line and the full definition at the next call; the program stays runnable.
    [Fact]
    public void Tap_WithANewSpeedInsideTheRun_EndsTheRunAndEntersItAgain()
    {
        string body = FanucCompile.Body("TOOL=1\nSPINDLE=CW RPM=500\nRAPID X=10 Y=10\nRAPID Z=5"
            + "\nCYCLE=TAP CLEARANCE=5 DEPTH=-20 CYCLE_F=750\nCYCLE_CALL\nSPINDLE=CW RPM=600\nCYCLE_CALL X=30"
            + "\nCYCLE=OFF");

        Assert.EndsWith("M29 S500\nG84 G99 Z-20. R5. F750.\nG80\nS600 M3\nM29 S600\nG84 G99 X30. Z-20. R5.\nG80\n",
            body, StringComparison.Ordinal);
    }

    // A spindle stop inside a run of the rigid TAP ends the run the same way (B-64484EN-2 p.104).
    [Fact]
    public void Tap_WithASpindleStopInsideTheRun_EndsTheRunFirst()
    {
        string body = FanucCompile.Body("TOOL=1\nSPINDLE=CW RPM=500\nRAPID X=10 Y=10\nRAPID Z=5"
            + "\nCYCLE=TAP CLEARANCE=5 DEPTH=-20 CYCLE_F=750\nCYCLE_CALL\nSPINDLE=OFF");

        Assert.EndsWith("G84 G99 Z-20. R5. F750.\nG80\nM5\n", body, StringComparison.Ordinal);
    }

    // D174: the lathe manual gives the simple turning cycles G90, G92, G94 of system A as G77, G78, G79 in system B
    // and G20, G21, G24 in system C; the catalog keeps the system A numbers and the compiler translates by
    // gcode_system (controllers fanuc.md 3; decisions.md).
    [Theory]
    [InlineData("A", "G90")]
    [InlineData("B", "G77")]
    [InlineData("C", "G20")]
    public void TurningCycleCodes_EveryGcodeSystem_TakeTheCodesOfThatSystem(string system, string code)
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "RAPID X=70 Z=2",
            "CYCLE=TURN_OD CYCLE_F=0.3",
            "CYCLE_CALL X=60 Z=-70",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END");
        string machine = FanucCompile.Lathe()
            .Replace("gcode_system = \"A\"", $"gcode_system = \"{system}\"", StringComparison.Ordinal);

        string text = FanucCompile.TextOf(FanucCompile.Run(program, machine));

        Assert.Contains("\n" + code + " X60. Z-70. F0.3\n", text, StringComparison.Ordinal);
    }

    // D161: THREAD is Fanuc G76, the one-shot cycle that cuts the whole thread, and G92, the modal cycle that cuts
    // one pass per block, is THREAD_PASS; the lathe catalog carries both names (decisions.md).
    [Fact]
    public void FanucThreadCycleNames_ThreadPassOnALatheOfSystemA_IsG92()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "RAPID X=70 Z=2",
            "CYCLE=THREAD_PASS",
            "CYCLE_CALL X=60 Z=-30",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Lathe()));

        // G92 is a code of group 01 that G80 does not cancel (B-64484EN-1 p.46), so CYCLE=OFF writes no G80 for it.
        Assert.Contains("\nG92 X60. Z-30.\nM30\n", text, StringComparison.Ordinal);
    }

    // D177, D362: G81 and G83 have no P, and a CYCLE_DWELL of 0, which the readers write for Q211=0 and DTB=0, is the
    // behaviour the control has anyway: dropped silently, not CMP385.
    [Theory]
    [InlineData("DRILL", "", "G81 G99 Z-20. R5. F100.\n")]
    [InlineData("PECK", " PECK=2", "G83 G99 Z-20. R5. Q2. F100.\n")]
    public void DrillAndPeck_WithCycleDwellZero_DropTheDwell(string cycle, string peck, string written)
    {
        string body = FanucCompile.Body(Approach
            + $"\nCYCLE={cycle} CLEARANCE=5 DEPTH=-20{peck} CYCLE_F=100 CYCLE_DWELL=0\nCYCLE_CALL");

        Assert.EndsWith(written, body, StringComparison.Ordinal);
    }

    // D114, D175: TAPER, a parameter name of the catalog entry, is the taper R of G90 (fanuc-lathe.toml).
    [Fact]
    public void TurnOd_WithTaper_WritesTheTaperAsR()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "RAPID X=70 Z=2",
            "CYCLE=TURN_OD CYCLE_F=0.3 TAPER=-2",
            "CYCLE_CALL X=60 Z=-70",
            "CYCLE=OFF",
            "PROGRAM=END",
            "FILE=END");

        CompileResult result = FanucCompile.Run(program, FanucCompile.Lathe());

        Assert.Empty(FanucCompile.CompilerDiagnostics(result));
        Assert.Contains("\nG90 X60. Z-70. R-2. F0.3\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
    }

    // D177: a DRILL with a dwell other than 0 is written as G82 (DRILL_DWELL), a REAM with one as its dwell_native
    // G89; P is round(seconds / dwell_increment), 0.001 by default (D159).
    [Theory]
    [InlineData("DRILL", "G82 G99 Z-20. R5. P500 F100.\n")]
    [InlineData("REAM", "G89 G99 Z-20. R5. P500 F100.\n")]
    public void DrillAndReam_WithADwell_TakeTheirDwellCode(string cycle, string written)
    {
        string body = FanucCompile.Body(Approach
            + $"\nCYCLE={cycle} CLEARANCE=5 DEPTH=-20 CYCLE_F=100 CYCLE_DWELL=0.5\nCYCLE_CALL");

        Assert.EndsWith(written, body, StringComparison.Ordinal);
    }

    // D177: PECK, CHIP_BREAK and BORE have no P on a machining centre, so a dwell other than 0 is dropped with the
    // WARNING CMP388.
    [Theory]
    [InlineData("PECK", " PECK=2", "G83 G99 Z-20. R5. Q2. F100.\n")]
    [InlineData("CHIP_BREAK", " PECK=2", "G73 G99 Z-20. R5. Q2. F100.\n")]
    [InlineData("BORE", "", "G86 G99 Z-20. R5. F100.\n")]
    public void PeckChipBreakAndBore_WithADwell_DropItWithTheWarningCmp388(string cycle, string peck, string written)
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program((Approach
            + $"\nCYCLE={cycle} CLEARANCE=5 DEPTH=-20{peck} CYCLE_F=100 CYCLE_DWELL=0.5\nCYCLE_CALL").Split('\n')),
            FanucCompile.Mill());

        Assert.EndsWith(written + "M30\n%\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Diagnostic warning = Assert.Single(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucCycleDwellDropped);
        Assert.Equal(Severity.Warning, warning.Severity);
    }

    // D355: the control counts Q from its own R plane while NCX counts PECK from SURFACE, so the compiler writes Q =
    // PECK and reports the shift once, at the block that defines the cycle, as the INFO CMP387.
    [Fact]
    public void Peck_WithAGapBetweenSurfaceAndClearance_IsTheInfoCmp387Once()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program((Approach
            + "\nCYCLE=PECK SURFACE=0 CLEARANCE=2 DEPTH=-20 PECK=3 CYCLE_F=100\nCYCLE_CALL\nCYCLE_CALL X=30")
                .Split('\n')),
            FanucCompile.Mill());

        Diagnostic info = Assert.Single(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucPeckCountsFromR);
        Assert.Equal(Severity.Info, info.Severity);
        Assert.Contains("G83 G99 Z-20. R2. Q3. F100.\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
    }

    // D355: where SURFACE and CLEARANCE coincide, or the block gives no SURFACE, there is no shift and no INFO.
    [Theory]
    [InlineData(" SURFACE=2")]
    [InlineData("")]
    public void Peck_WithoutAGap_IsNoInfo(string surface)
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program((Approach
            + $"\nCYCLE=PECK{surface} CLEARANCE=2 DEPTH=-20 PECK=3 CYCLE_F=100\nCYCLE_CALL").Split('\n')),
            FanucCompile.Mill());

        Assert.DoesNotContain(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucPeckCountsFromR);
    }

    // D362: PECK=0 on a cycle without that motion is the behaviour the control has anyway and is dropped silently.
    [Fact]
    public void Drill_WithPeckZero_DropsItSilently()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program((Approach
            + "\nCYCLE=DRILL CLEARANCE=2 DEPTH=-20 PECK=0 CYCLE_F=100\nCYCLE_CALL").Split('\n')), FanucCompile.Mill());

        Assert.DoesNotContain(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucCycleWordNotMapped);
        Assert.Contains("G81 G99 Z-20. R2. F100.\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
    }

    // D279: PECK on G84 is Q only where the machine declares peck rigid tapping ([cycles] peck_rigid_tapping, D388);
    // elsewhere it is the ERROR CMP393.
    [Fact]
    public void Tap_WithPeck_IsQOnlyWithPeckRigidTapping()
    {
        string blocks = "TOOL=1\nSPINDLE=CW RPM=500\nRAPID X=10 Y=10\nRAPID Z=5"
            + "\nCYCLE=TAP CLEARANCE=5 DEPTH=-20 PECK=2 CYCLE_F=750\nCYCLE_CALL";
        CompileResult without = FanucCompile.Run(FanucCompile.Program(blocks.Split('\n')), FanucCompile.Mill());
        string with = FanucCompile.Body(blocks, "[cycles]\npeck_rigid_tapping = true");

        Assert.Contains(FanucCompile.CompilerDiagnostics(without),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucPeckWithoutQ);
        Assert.EndsWith("M29 S500\nG84 G99 Z-20. R5. Q2. F750.\n", with, StringComparison.Ordinal);
    }

    // D279: Q is modal data the native cycles share, so a G73 or G83 without PECK would take the Q another cycle
    // left: the ERROR CMP394.
    [Fact]
    public void Peck_WithoutPeck_IsTheErrorCmp394()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program((Approach
            + "\nCYCLE=PECK CLEARANCE=2 DEPTH=-20 CYCLE_F=100\nCYCLE_CALL").Split('\n')), FanucCompile.Mill());

        Assert.Contains(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucPeckNotGiven);
    }

    // D279: a native Fanuc parameter key is one letter A..Z, so Q215=1 on CYCLE:FANUC is the ERROR CMP395.
    [Fact]
    public void NativeCycle_WithAParameterOfMoreThanOneLetter_IsTheErrorCmp395()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program((Approach
            + "\nCYCLE:FANUC=G83 Q215=1\nCYCLE_CALL").Split('\n')), FanucCompile.Mill());

        Assert.Contains(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucNativeParameterNotALetter);
    }

    // The contour program of the D278 tests: G71 and G70 name one contour section.
    private static string ContourProgram(string first, string cycles = "")
    {
        return FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "RAPID X=40 Z=2",
            "CYCLE=ROUGH_TURN CONTOUR=C1",
            "CYCLE_CALL",
            "CYCLE=OFF",
            "CYCLE=FINISH CONTOUR=C1",
            "CYCLE_CALL",
            "CYCLE=OFF",
            "RAPID X=100" + cycles,
            "PROGRAM=END",
            "SUB=BEGIN NAME=C1",
            first,
            "LINE Z=-10 F=0.2",
            "LINE X=40",
            "SUB=END",
            "FILE=END");
    }

    // D278, D388 item 4: the contour is written inline after the first G71 of the program, numbered after its
    // greatest label (D277), never as an O program; the later G70 repeats only P and Q; the CYCLE=OFF of a one-shot
    // cycle writes nothing (D224).
    [Fact]
    public void ContourCycles_G71AndG70_WriteTheContourInlineOnce()
    {
        CompileResult result = FanucCompile.Run(ContourProgram("RAPID X=20"), FanucCompile.Lathe());
        string text = FanucCompile.TextOf(result);

        Assert.DoesNotContain(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Severity == Severity.Error);
        Assert.Contains("\nG71 P1 Q2\nN1 G0 X20.\nG1 Z-10. F0.2\nN2 X40.\nG99\nG70 P1 Q2\n", text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("M99", text, StringComparison.Ordinal);
        Assert.DoesNotContain("G80", text.Substring(text.IndexOf("G71", StringComparison.Ordinal)),
            StringComparison.Ordinal);
    }

    // D278: a first block that names Z where Z does not move is type I, X alone; one that moves both is type II, which
    // only a machine with [cycles] contour_type_ii takes (CMP389).
    [Fact]
    public void ContourCycles_FirstBlockOfG71_IsTypeIOrTypeII()
    {
        string typeI = FanucCompile.TextOf(FanucCompile.Run(ContourProgram("RAPID X=20 Z=2"), FanucCompile.Lathe()));
        CompileResult typeII = FanucCompile.Run(ContourProgram("RAPID X=20 Z=1"), FanucCompile.Lathe());
        string declared = FanucCompile.TextOf(FanucCompile.Run(ContourProgram("RAPID X=20 Z=1"),
            FanucCompile.Lathe().Replace("catalog = \"fanuc-lathe.toml\"",
                "catalog = \"fanuc-lathe.toml\"\ncontour_type_ii = true", StringComparison.Ordinal)));

        Assert.Contains("\nN1 G0 X20.\n", typeI, StringComparison.Ordinal);
        Assert.Contains(FanucCompile.CompilerDiagnostics(typeII),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucContourTypeIINotDeclared);
        Assert.Contains("\nN1 G0 X20. Z1.\n", declared, StringComparison.Ordinal);
    }

    // D278: a first block of G71 that does not move X is alarm PS0325, CMP390; a first block that is no RAPID or LINE
    // is CMP392; a block other than motion, G04 or a label inside the range is CMP391.
    [Theory]
    [InlineData("RAPID Z=1", "CMP390")]
    [InlineData("ARC=CW X=20 Z=0 R=2", "CMP392")]
    [InlineData("RAPID X=20 COOLANT=ON", "CMP391")]
    public void ContourCycles_ASectionTheContourCannotHold_IsAnError(string first, string code)
    {
        CompileResult result = FanucCompile.Run(ContourProgram(first), FanucCompile.Lathe());

        Assert.Contains(FanucCompile.CompilerDiagnostics(result), diagnostic => diagnostic.Code == code);
    }

    // A lathe program around one contour cycle, with the blocks given before and after it and in the contour.
    private static string ContourProgramWith(string before, string after, params string[] contour)
    {
        var lines = new List<string>
        {
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "RAPID X=40 Z=2",
        };
        lines.AddRange(before.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        lines.AddRange(["CYCLE=ROUGH_TURN CONTOUR=500", "CYCLE_CALL", "CYCLE=OFF"]);
        lines.AddRange(after.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        lines.AddRange(["RAPID X=100", "PROGRAM=END", "SUB=BEGIN NAME=500"]);
        lines.AddRange(contour);
        lines.AddRange(["SUB=END", "FILE=END"]);
        return FanucCompile.Lines([.. lines]);
    }

    // D278, D277: the contour's own labels take free numbers of the O program, P names the line of the first motion,
    // never a bare label line, and no N stands twice.
    [Fact]
    public void ContourCycles_TheContoursOwnLabels_TakeFreeNumbersOfTheProgram()
    {
        CompileResult result = FanucCompile.Run(ContourProgramWith("LABEL=1", "",
            "LABEL=7", "RAPID X=20", "LINE Z=-10 F=0.2"), FanucCompile.Lathe());
        string text = FanucCompile.TextOf(result);

        Assert.DoesNotContain(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Severity == Severity.Error);
        Assert.Contains("\nG71 P2 Q3\nN2 G0 X20.\nN3 G1 Z-10. F0.2\n", text, StringComparison.Ordinal);
    }

    // D278: a later G70 under another feed mode runs the range under that mode, so the first contour blocks write
    // the feed mode, the plane and G90/G91 the contour runs under; and a contour at a jump target writes no G80.
    [Fact]
    public void ContourCycles_TheRange_WritesItsFeedModeAndNoCycleCancel()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(ContourProgramWith("LABEL=3",
            "FEED_MODE=PER_MIN\nRAPID X=40 Z=2\nCYCLE=FINISH CONTOUR=500\nCYCLE_CALL\nCYCLE=OFF\nJUMP=3",
            "RAPID X=20", "LINE Z=-10 F=0.2", "LINE X=40"), FanucCompile.Lathe()));
        int start = text.IndexOf("G71", StringComparison.Ordinal);
        string range = text.Substring(start, text.IndexOf("X40.\n", start, StringComparison.Ordinal) - start);

        // D278 (B-64484EN-1 p.90): the range holds G00 to G03, G04, axis words and F only; the feed mode it runs
        // under stands in front of the cycle line.
        Assert.Contains("G1 Z-10. F0.2", range, StringComparison.Ordinal);
        Assert.DoesNotContain("G99", range, StringComparison.Ordinal);
        Assert.DoesNotContain("G80", range, StringComparison.Ordinal);
        Assert.Contains("\nG99\nG70 P", text, StringComparison.Ordinal);
    }

    // D278, D388 item 4: a contour section that a CALL also names is written as an O program too.
    [Fact]
    public void ContourCycles_AContourACallNamesToo_IsWrittenAsAnOProgramAsWell()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(ContourProgramWith("", "RAPID X=40 Z=2\nCALL=500",
            "RAPID X=20", "LINE Z=-10 F=0.2", "LINE X=40"), FanucCompile.Lathe()));

        Assert.Contains("M98 P0500", text, StringComparison.Ordinal);
        Assert.Contains("\nO0500\n", text, StringComparison.Ordinal);
    }

    // D278: the control runs the blocks P to Q of a G70, whose codes and F take effect (B-64484EN-1 p.76, p.281), so a
    // feed after the G70 writes its feed mode again.
    [Fact]
    public void ContourCycles_AFeedAfterTheG70_WritesItsFeedModeAgain()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(ContourProgramWith("",
            "FEED_MODE=PER_MIN\nRAPID X=40 Z=2\nCYCLE=FINISH CONTOUR=500\nCYCLE_CALL\nCYCLE=OFF\nLINE X=100 F=200",
            "RAPID X=20", "LINE Z=-10 F=0.2", "LINE X=40"), FanucCompile.Lathe()));

        Assert.Contains("\nG99\nG70 P1 Q2\nG1 G18 G98 X100. F200.\n", text, StringComparison.Ordinal);
    }
}
