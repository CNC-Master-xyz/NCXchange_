using Ncx.Compilers.Heidenhain;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// HOME as M91 moves, ORIGIN and the frame chain, the modes of the control, RETRACT and TOLERANCE (controllers
/// heidenhain.md 2, 3; 8 rule 5; controller-mapping 1; language 4.2; machine-config 4 and 5; D31, D82, D83, D85, D86,
/// D100).
/// </summary>
public sealed class HeidenhainFrameTests
{
    // Heidenhain 8 rule 5: HOME is L ... FMAX M91 to the reference coordinates of [[axis]], home = 0 on the iTNC 530 of
    // the repository; an L block, which takes R0 as the first line of the program does, whose compensation the control
    // does not know yet (heidenhain 2).
    [Fact]
    public void Rule5_Home_IsAnM91MoveToTheReferenceCoordinates()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("HOME Z", "HOME X Y"));

        Assert.Equal("L Z+0 R0 FMAX M91\nL X+0 Y+0 FMAX M91", HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 5, language 4.4: COMP applies from the motion of its block on, and HOME is written as an L
    // block, which carries R0, RL and RR (heidenhain 2), so the COMP=OFF of a block without a motion is the R0 of the
    // M91 line that follows it.
    [Fact]
    public void Rule5_HomeAfterCompOff_WritesR0OnTheM91Line()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE Z=-1 F=100", "LINE X=10 COMP=LEFT", "LINE X=20", "COMP=OFF", "HOME X Y", "HOME Z"));

        Assert.Equal("L X+0 Y+0 R0 FMAX\nL Z-1 F100\nL X+10 RL\nL X+20\nL X+0 Y+0 R0 FMAX M91\nL Z+0 FMAX M91",
            HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 5: home2 for POINT=2.
    [Fact]
    public void Rule5_HomeOfPoint2_IsTheM91MoveToHome2()
    {
        var mill = HeidenhainCompile.MillWith(
            "acceleration = 3500\nhome = 0", "acceleration = 3500\nhome = 0\nhome2 = -100");

        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("HOME Z POINT=2"), mill);

        Assert.Equal("L Z-100 R0 FMAX M91", HeidenhainCompile.Body(result));
    }

    // Heidenhain 8 rule 5, D100: an axis without a reference point is an ERROR of this compiler, not of ncx check.
    [Fact]
    public void Rule5_HomeOfAnAxisWithoutReferencePoint_IsTheErrorCmp100()
    {
        var mill = HeidenhainCompile.MillWith("acceleration = 3500\nhome = 0", "acceleration = 3500");

        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("HOME Z"), mill);

        Assert.Empty(result.Files);
        Assert.Equal(4, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainHomeWithoutReferencePoint).Line);
    }

    // Controllers heidenhain.md 3; controller-mapping 1, ORIGIN: ORIGIN=1 is cycle 247 with the preset Q339.
    [Fact]
    public void Origin_IsCycle247WithThePreset()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("ORIGIN=1"));

        Assert.Equal("CYCL DEF 247 INIT. REF.PKT ~\n    Q339=+1", HeidenhainCompile.Body(result));
    }

    // Controllers heidenhain.md 3; controller-mapping 1, SHIFT: cycle 7 axis by axis, and its reset the cycle with
    // every axis at 0.
    [Fact]
    public void Shift_IsCycle7AxisByAxisAndItsResetTheCycleWithZeros()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("SHIFT X=60 Y=40 Z=-5", "SHIFT=RESET"));

        Assert.Equal(
            "CYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+60\nCYCL DEF 7.2 Y+40\nCYCL DEF 7.3 Z-5\n"
            + "CYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+0\nCYCL DEF 7.2 Y+0\nCYCL DEF 7.3 Z+0",
            HeidenhainCompile.Body(result));
    }

    // Controllers heidenhain.md 3; controller-mapping 1, ROTATE and MIRROR: cycle 10 with ROT, cycle 8 with the axes,
    // their cancels cycle 10 with ROT+0 and cycle 8 without axes, in the order of the chain (D31).
    [Fact]
    public void RotateAndMirror_AreCycles10And8InTheOrderOfTheChain()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "ROTATE=30", "MIRROR=X", "MIRROR=OFF", "ROTATE=RESET"));

        Assert.Equal(
            "CYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+30\nCYCL DEF 8.0 SPIEGELN\nCYCL DEF 8.1 X\n"
            + "CYCL DEF 8.0 SPIEGELN\nCYCL DEF 8.1\nCYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+0",
            HeidenhainCompile.Body(result));
    }

    // Language 4.2, D31, and D253(a), D398 (HeidenhainChain): ORIGIN empties the chain, so the shift is
    // cancelled before cycle 247.
    [Fact]
    public void Origin_OverAShift_CancelsTheShiftBeforeCycle247()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("SHIFT X=10", "ORIGIN=1"));

        Assert.Equal(
            "CYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\nCYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+0\n"
            + "CYCL DEF 247 INIT. REF.PKT ~\n    Q339=+1",
            HeidenhainCompile.Body(result));
    }

    // D253(a), D398 (HeidenhainChain.Append): a second cycle 7 would replace the first on the control, so a
    // SHIFT appended while a shift stands in the chain is CMP106.
    [Fact]
    public void Shift_WhileAShiftStandsInTheChain_IsTheErrorCmp106()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("SHIFT X=10", "SHIFT Y=5"));

        Assert.Empty(result.Files);
        Assert.Equal(5, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainTransformReplacesAnother).Line);
    }

    // Language 4.2 and 4.9: the REPEAT runs the blocks from the label again with the rotation of the first pass, and
    // the ROTATE=30 after the label appends a second one, 60 degrees in all, where the cycle 10 written for the way the
    // text runs replaces the rotation on the control (controllers heidenhain.md 3), CMP119 on the REPEAT.
    [Fact]
    public void Repeat_ReachingARotationWithTheOneItAppended_IsTheErrorCmp119()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "LABEL=1", "ROTATE=30", "RAPID X=10 Y=0", "REPEAT=1 TIMES=2"));

        Assert.Empty(result.Files);
        Assert.Equal(7, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelChainsDiffer).Line);
    }

    // Language 4.2 and 4.9: the SHIFT=RESET after the label removes nothing on the way the text runs, so nothing is
    // written for it, and on the second pass it removes the shift of the first, whose cycle 7 stays on the control,
    // CMP119 on the REPEAT.
    [Fact]
    public void Repeat_ReachingAResetOfTheShiftItBrings_IsTheErrorCmp119()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "LABEL=1", "SHIFT=RESET", "RAPID X=0 Y=0", "SHIFT X=10", "RAPID X=5", "REPEAT=1 TIMES=2"));

        Assert.Empty(result.Files);
        Assert.Equal(9, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelChainsDiffer).Line);
    }

    // Language 4.2 and 4.9; D253(a), D398 (HeidenhainChain): ORIGIN after the label cancels the
    // entries of the chain of the way the text runs, none, before cycle 247, and the jump brings the rotation, which
    // ORIGIN removes in the program and cycle 247 does not end on the control as far as the documents say, CMP119 on
    // the JUMP.
    [Fact]
    public void Jump_ReachingAnOriginWithAnotherChain_IsTheErrorCmp119()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "ROTATE=30", "JUMP=1 IF={$Q1 > 0}", "ROTATE=RESET", "LABEL=1", "ORIGIN=1", "RAPID X=0 Y=0"));

        Assert.Empty(result.Files);
        Assert.Equal(5, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelChainsDiffer).Line);
    }

    // Language 4.2 and 4.9; controllers heidenhain.md 3: on the second pass the SHIFT=RESET removes the shift of the
    // first, which the text does not cancel, and the SHIFT X=10 after it writes the cycle 7 that replaces that shift on
    // the control before anything moves, so every pass runs with the shift of the program.
    [Fact]
    public void Repeat_ReachingAResetAndANewShiftBeforeAMotion_WritesTheLoop()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "LABEL=1", "SHIFT=RESET", "SHIFT X=10", "RAPID X=0 Y=0", "REPEAT=1 TIMES=2"));

        Assert.Equal(
            "LBL 1\nCYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\nL X+0 Y+0 R0 FMAX\nCALL LBL 1 REP 2",
            HeidenhainCompile.Body(result));
    }

    // Language 4.2 and 4.9: the jump brings a rotation of 30 degrees and the text one of 15, which the ORIGIN after the
    // label cancels with cycle 10 at ROT+0 before cycle 247, so the control ends the rotation of either way, as the
    // program does.
    [Fact]
    public void Jump_ReachingAnOriginThatCancelsTheTransformOfItsKind_WritesTheJump()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "ROTATE=30", "JUMP=1 IF={$Q1 > 0}", "ROTATE=RESET", "ROTATE=15", "LABEL=1", "ORIGIN=1", "RAPID X=0 Y=0"));

        Assert.Equal(
            "CYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+30\nFN 11: IF +Q1 GT +0 GOTO LBL 1\n"
            + "CYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+0\nCYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+15\nLBL 1\n"
            + "CYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+0\nCYCL DEF 247 INIT. REF.PKT ~\n    Q339=+1\n"
            + "L X+0 Y+0 R0 FMAX",
            HeidenhainCompile.Body(result));
    }

    // Language 4.2 and 4.9; D253: the jump brings a rotation that a shift follows, and the cycle 10 of the ROTATE=45
    // after the label replaces that rotation on the control, where it acts then is not given, while the program applies
    // both rotations, CMP119 on the JUMP.
    [Fact]
    public void Jump_ReachingARotationWhereTheJumpBringsOneThatAShiftFollows_IsTheErrorCmp119()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "ROTATE=30", "SHIFT X=5", "JUMP=1 IF={$Q1 > 0}", "SHIFT=RESET", "ROTATE=RESET", "LABEL=1", "ROTATE=45",
            "RAPID X=0 Y=0"));

        Assert.Empty(result.Files);
        Assert.Equal(6, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelChainsDiffer).Line);
    }

    // Language 4.2 and 4.9: the loop removes the shift it appends, so the REPEAT brings the chain the text brings to
    // the label, and the loop runs as the program does.
    [Fact]
    public void Repeat_ThatLeavesTheChainAsItFoundIt_WritesTheLoop()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "LABEL=1", "SHIFT X=10", "RAPID X=0 Y=0", "SHIFT=RESET", "REPEAT=1 TIMES=2"));

        Assert.Equal(
            "LBL 1\nCYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\nL X+0 Y+0 R0 FMAX\n"
            + "CYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+0\nCALL LBL 1 REP 2",
            HeidenhainCompile.Body(result));
    }

    // Language 4.2, D31: the jump brings the rotation to the label, and the SHIFT after it is appended after the
    // rotation on both ways, cycle 7 after cycle 10 on the control, so the text runs both as the program does.
    [Fact]
    public void Jump_WithAnotherChainToAShiftAfterTheLabel_WritesTheJump()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "ROTATE=15", "JUMP=1 IF={$Q1 > 0}", "ROTATE=RESET", "LABEL=1", "SHIFT X=10", "RAPID X=0 Y=0"));

        Assert.Equal(
            "CYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+15\nFN 11: IF +Q1 GT +0 GOTO LBL 1\n"
            + "CYCL DEF 10.0 DREHUNG\nCYCL DEF 10.1 ROT+0\nLBL 1\nCYCL DEF 7.0 NULLPUNKT\nCYCL DEF 7.1 X+10\n"
            + "L X+0 Y+0 R0 FMAX",
            HeidenhainCompile.Body(result));
    }

    // Language 4.2 and 4.9; virtual machine 3.9: the REPEAT directly after the CALL stands in the walk of the program
    // as its label does, once the subprogram has returned, and brings the rotation of the first pass to the ROTATE=30
    // after the label, 60 degrees in all, where the cycle 10 written for the way the text runs replaces the rotation on
    // the control (controllers heidenhain.md 3), CMP119 on the REPEAT.
    [Fact]
    public void Repeat_DirectlyAfterACallReachingARotationWithTheOneItAppended_IsTheErrorCmp119()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"",
            "UNITS=MM WORKPLANE=XY",
            "LABEL=1",
            "ROTATE=30",
            "RAPID X=10 Y=0",
            "CALL=5",
            "REPEAT=1 TIMES=2",
            "PROGRAM=END",
            "SUB=BEGIN NAME=5",
            "RAPID Z=1",
            "SUB=END",
            "FILE=END"));

        Assert.Empty(result.Files);
        Assert.Equal(8, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelChainsDiffer).Line);
    }

    // Language 4.2 and 4.9; virtual machine 3.9: the LABEL directly after the CALL stands in the walk of the program,
    // and the REPEAT brings the rotation of the first pass to the ROTATE=30 after it, 60 degrees in all, where the
    // cycle 10 written for the way the text runs replaces the rotation on the control, CMP119 on the REPEAT.
    [Fact]
    public void Repeat_ToALabelDirectlyAfterACallReachingARotationWithTheOneItAppended_IsTheErrorCmp119()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"",
            "UNITS=MM WORKPLANE=XY",
            "RAPID X=0 Y=0",
            "CALL=5",
            "LABEL=1",
            "ROTATE=30",
            "RAPID X=10 Y=0",
            "REPEAT=1 TIMES=2",
            "PROGRAM=END",
            "SUB=BEGIN NAME=5",
            "RAPID Z=1",
            "SUB=END",
            "FILE=END"));

        Assert.Empty(result.Files);
        Assert.Equal(9, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainLabelChainsDiffer).Line);
    }

    // Language 4.2 and 4.9; virtual machine 3.9: the label and the REPEAT each stand directly after a CALL, in the
    // walk of the program, and the loop removes the shift it appends, so the REPEAT brings the chain the text brings to
    // the label, and the loop runs as the program does.
    [Fact]
    public void Repeat_DirectlyAfterACallToALabelDirectlyAfterACallThatLeavesTheChainAsItFoundIt_WritesTheLoop()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"",
            "UNITS=MM WORKPLANE=XY",
            "RAPID X=0 Y=0",
            "CALL=5",
            "LABEL=1",
            "SHIFT X=10",
            "RAPID X=0 Y=0",
            "SHIFT=RESET",
            "CALL=5",
            "REPEAT=1 TIMES=2",
            "PROGRAM=END",
            "SUB=BEGIN NAME=5",
            "RAPID Z=1",
            "SUB=END",
            "FILE=END"));

        Assert.Equal(
            ["BEGIN PGM T MM", "L X+0 Y+0 R0 FMAX", "CALL LBL 5", "LBL 1", "CYCL DEF 7.0 NULLPUNKT",
                "CYCL DEF 7.1 X+10", "L X+0 Y+0 FMAX", "CYCL DEF 7.0 NULLPUNKT", "CYCL DEF 7.1 X+0", "CALL LBL 5",
                "CALL LBL 1 REP 2", "M30", "LBL 5", "L Z+1 R0 FMAX", "LBL 0", "END PGM T MM"],
            HeidenhainCompile.LinesOf(HeidenhainCompile.TextOf(result)));
    }

    // Controller-mapping 1, TILT and MOVE; machine-config 5, [transform]: PLANE SPATIAL through TILT_ON with the angles
    // and the {move} of MOVE, PLANE RESET through TILT_OFF.
    [Fact]
    public void Tilt_IsThePlaneTemplateWithTheMoveOfTheMachine()
    {
        var mill = HeidenhainCompile.MillAnd("""
            [transform]
            TILT_ON = "PLANE SPATIAL SPA{a} SPB{b} SPC{c} {move}"
            TILT_OFF = "PLANE RESET STAY"
            move = { TURN = "TURN FMAX", MOVE = "MOVE ABST50 FMAX", STAY = "STAY" }
            """);

        CompileResult result = HeidenhainCompile.Run(
            HeidenhainCompile.Program("TILT A=0 B=45 C=0 MOVE=TURN", "TILT=RESET"), mill);

        Assert.Equal("PLANE SPATIAL SPA0 SPB45 SPC0 TURN FMAX\nPLANE RESET STAY", HeidenhainCompile.Body(result));
    }

    // D82; machine-config 5: an empty TILT_AXIS_ON means the target writes TILT_AXIS only with the kinematics module.
    [Fact]
    public void TiltAxis_WithAnEmptyTemplate_IsTheErrorCmp107()
    {
        var mill = HeidenhainCompile.MillAnd("""
            [transform]
            TILT_AXIS_ON = ""

            [[axis]]
            id = "A1"
            ncx = "A"
            letter = "A"
            kind = "rotary"
            owner = "TABLE1"

            [[axis]]
            id = "C1"
            ncx = "C"
            letter = "C"
            kind = "rotary"
            owner = "TABLE1"
            """);

        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TILT_AXIS A=0 C=90"), mill);

        Assert.Empty(result.Files);
        Assert.Equal(4, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainWordNeedsKinematics).Line);
    }

    // Controllers heidenhain.md 2; controller-mapping 1, TCPM, ROTARY_PATH, ROTARY_FEED; D86: M128 and M129, M126 and
    // M127, M116 and M117, the controller's own M functions where the machine names no template, each on change.
    [Fact]
    public void Modes_AreTheControllersOwnMFunctionsOnChange()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TCPM=ON", "ROTARY_PATH=SHORTEST", "ROTARY_FEED=MM_MIN", "TCPM=ON", "ROTARY_PATH=FULL", "TCPM=OFF"));

        Assert.Equal("M128\nM126\nM116\nM127\nM129", HeidenhainCompile.Body(result));
    }

    // Controller-mapping 1, TCPM; machine-config 5: FUNCTION TCPM where the machine's [transform] names it.
    [Fact]
    public void Tcpm_OfAMachineWithItsTemplates_IsFunctionTcpm()
    {
        var mill = HeidenhainCompile.MillAnd("""
            [transform]
            TCPM_ON = "FUNCTION TCPM F TCP AXIS POS"
            TCPM_OFF = "FUNCTION RESET TCPM"
            """);

        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TCPM=ON", "TCPM=OFF"), mill);

        Assert.Equal("FUNCTION TCPM F TCP AXIS POS\nFUNCTION RESET TCPM", HeidenhainCompile.Body(result));
    }

    // Controller-mapping 1, RETRACT; machine-config 5, [retract]; D83: bare RETRACT is M140 MB MAX, RETRACT=50 is
    // M140 MB50.
    [Fact]
    public void Retract_IsM140MbOfTheMachine()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("RETRACT", "RETRACT=50"));

        Assert.Equal("M140 MB MAX\nM140 MB50", HeidenhainCompile.Body(result));
    }

    // Language 4.4; controllers heidenhain.md 2, differences.md (radius compensation): M140 carries no R0, RL or RR,
    // so a RETRACT after a COMP=OFF that no L block has written would run with the RL of the control, CMP115.
    [Fact]
    public void Retract_AfterACompensationChangeNoLineWrote_IsTheErrorCmp115()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAPID X=0 Y=0", "LINE Z=-1 F=100", "LINE X=10 COMP=LEFT", "COMP=OFF", "RETRACT"));

        Assert.Empty(result.Files);
        Assert.Equal(8, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainCompensationWithoutLine).Line);
    }

    // Controller-mapping 1, TOLERANCE; machine-config 5, [tolerance]; D85: cycle 32 with the tolerance, the mode and
    // the rotary tolerance, and TOLERANCE=OFF the cycle with T0.
    [Fact]
    public void Tolerance_IsCycle32OfTheMachine()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOLERANCE=0.02 TOLERANCE:ROTARY=0.05 TOLERANCE_MODE=FINISH", "TOLERANCE=OFF"));

        Assert.Equal(
            "CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T0,02\nCYCL DEF 32.2 HSC-MODE:0 TA0,05\n"
            + "CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T0",
            HeidenhainCompile.Body(result));
    }

    // D151: without TOLERANCE:ROTARY the ON template is written, cycle 32 without TA, the safe iTNC form.
    [Fact]
    public void ToleranceWithoutRotaryTolerance_IsCycle32WithoutTa()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOLERANCE=0.02"));

        Assert.Equal(
            "CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T0,02\nCYCL DEF 32.2 HSC-MODE:0", HeidenhainCompile.Body(result));
    }

    // The NCX section of a slot on a cylinder of radius 30 along X, the table axis A1 parallel to X under the tool axis
    // Z: positioning above the start point, the plunge to 25, the contour under COMP=LEFT, the retract to 32 (D394).
    private static readonly string[] s_cylinderSection =
    [
        "TOOL=1 RPM=2000", "SPINDLE=CW", "CYLINDER=30", "RAPID X=20 C=40", "RAPID Z=32", "LINE Z=25 F=100",
        "LINE C=50 COMP=LEFT F=250", "ARC=CW X=40 C=70 R=20", "LINE X=60", "ARC=CCW X=60 C=40 CENTER:X=60 CENTER:C=55",
        "RAPID Z=32", "CYLINDER=OFF",
    ];

    // D394: a CYLINDER section of the shape of cycle 27 is CYCL DEF 14 KONTUR, CYCL DEF 27 with Q1 = depth - r,
    // Q6 = plunge start - r, Q10 = |Q1| for one pass, Q11 and Q12 the feeds, Q16 = r and Q17 = 1, and CYCL CALL; on the
    // iTNC 530 the contour subprogram after the M30 takes the rotary letter A and the parallel axis X (D146), the
    // circumference letter first, as on the TNC 640: that is the mirror of the NCX plane (the cylinder axis then C,
    // D102), so COMP=LEFT is RR and an NCX CW arc is DR+ (TNC 640 Zyklen 892905-18 p. 346-347).
    [Fact]
    public void Cylinder_OfTheShapeOfCycle27_IsCycle27WithItsContourAfterTheM30()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(s_cylinderSection), CylinderMill());

        Assert.Equal(
            "TOOL CALL 1 Z S2000\nM3\nCYCL DEF 14.0 KONTUR\nCYCL DEF 14.1 KONTURLABEL 1\n"
            + "CYCL DEF 27 ZYLINDER-MANTEL ~\n    Q1=-5 ~\n    Q3=+0 ~\n    Q6=+2 ~\n    Q10=+5 ~\n    Q11=+100 ~\n"
            + "    Q12=+250 ~\n    Q16=+30 ~\n    Q17=+1\nCYCL CALL",
            HeidenhainCompile.Body(result));
        List<string> lines = HeidenhainCompile.LinesOf(HeidenhainCompile.TextOf(result));
        int end = lines.IndexOf("M30");
        Assert.Equal(
            ["LBL 1", "L A+40 X+20 RR", "L A+50 X+20", "CR A+70 X+40 R+20 DR+", "L A+70 X+60", "CC A+55 X+60",
                "C A+40 X+60 DR-", "LBL 0", "END PGM T MM"],
            lines.GetRange(end + 1, lines.Count - end - 1));
        Assert.Contains(result.Diagnostics.Items,
            diagnostic => diagnostic.Code == DiagnosticCodes.HeidenhainCylinderClearanceUnchecked);
    }

    // D394, D146: on the TNC 640 the contour is always in X along the circumference and Y along the axis; X before Y
    // mirrors the plane of NCX, the cylinder axis then C, so the arc directions and RL/RR flip.
    [Fact]
    public void Cylinder_OnTheTnc640_WritesTheContourInXAndY()
    {
        MachineConfig mill = CylinderMill("dialect = \"TNC640\"");
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(s_cylinderSection), mill);

        List<string> lines = HeidenhainCompile.LinesOf(HeidenhainCompile.TextOf(result));
        int end = lines.IndexOf("M30");
        Assert.Equal(
            ["LBL 1", "L X+40 Y+20 RR", "L X+50 Y+20", "CR X+70 Y+40 R+20 DR+", "L X+70 Y+60", "CC X+55 Y+60",
                "C X+40 Y+60 DR-", "LBL 0", "END PGM T MM"],
            lines.GetRange(end + 1, lines.Count - end - 1));
    }

    // D394: two passes with the same contour at 27 and 25 are one cycle 27 with the infeed Q10 = 3 to Q1 = -5.
    [Fact]
    public void Cylinder_WithTwoPassesAtIncreasingDepth_SetsTheInfeed()
    {
        string[] pass = ["RAPID X=20 C=40", "RAPID Z=32", "LINE Z={0} F=100", "LINE C=50 COMP=LEFT F=250", "LINE X=60",
            "RAPID Z=32"];
        var blocks = new List<string> { "TOOL=1 RPM=2000", "SPINDLE=CW", "CYLINDER=30" };
        blocks.AddRange(pass.Select(block => block.Replace("{0}", "27", StringComparison.Ordinal)));
        blocks.AddRange(pass.Select(block => block.Replace("{0}", "25", StringComparison.Ordinal)));
        blocks.Add("CYLINDER=OFF");

        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program([.. blocks]), CylinderMill());

        Assert.Contains("    Q1=-5 ~\n    Q3=+0 ~\n    Q6=+2 ~\n    Q10=+3 ~\n", HeidenhainCompile.Body(result),
            StringComparison.Ordinal);
    }

    // D394: COMP=OFF on the contour, a RAPID in the contour, and a section without the plunge stay CMP101 and write
    // nothing.
    [Theory]
    [InlineData("LINE C=50 F=250")]
    [InlineData("RAPID C=50 COMP=LEFT")]
    [InlineData("LINE C=50 Z=24 COMP=LEFT F=250")]
    public void Cylinder_NotOfTheShapeOfCycle27_IsTheErrorCmp101(string contour)
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=1 RPM=2000", "SPINDLE=CW", "CYLINDER=30", "RAPID X=20 C=40", "RAPID Z=32", "LINE Z=25 F=100",
            contour, "LINE X=60", "RAPID Z=32", "CYLINDER=OFF"), CylinderMill());

        Assert.Empty(result.Files);
        Diagnostic error = Assert.Single(result.Diagnostics.Items,
            diagnostic => diagnostic.Code == DiagnosticCodes.HeidenhainWordWithoutKlartext && diagnostic.Line == 6);
        Assert.Contains("cycle 27", error.Message, StringComparison.Ordinal);
    }

    // D394 (delegated 2026-10-30): no manual says whether Q11 and Q12 are per revolution under M136, so a section
    // under FEED_MODE=PER_REV is CMP101.
    [Fact]
    public void Cylinder_UnderFeedPerRevolution_IsTheErrorCmp101()
    {
        var blocks = new List<string> { "FEED_MODE=PER_REV" };
        blocks.AddRange(s_cylinderSection);
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program([.. blocks]), CylinderMill());

        Assert.Empty(result.Files);
        Assert.Contains(result.Diagnostics.Items, diagnostic =>
            diagnostic.Code == DiagnosticCodes.HeidenhainWordWithoutKlartext
            && diagnostic.Message.Contains("PER_REV", StringComparison.Ordinal));
    }

    // D394: a set-up clearance Q6 at or below the radius the tool table gives is CMP101.
    [Fact]
    public void Cylinder_WithAClearanceNotAboveTheToolRadius_IsTheErrorCmp101()
    {
        NcxProgram program = Parser.Parse(HeidenhainCompile.Program(s_cylinderSection), "T.ncx", new ParserOptions());
        var tools = new Diagnostics("tools.toml");
        ToolTable? table = ToolTable.LoadText("[[tool]]\nnumber = 1\nkind = \"MILL\"\nradius = 3\n", tools);
        Assert.True(table is not null, tools.ToText());

        CompileResult result = new HeidenhainCompiler().Compile(program, CylinderMill(),
            new CompileOptions { ToolTable = table });

        Assert.Empty(result.Files);
        Assert.Contains(result.Diagnostics.Items, diagnostic =>
            diagnostic.Code == DiagnosticCodes.HeidenhainWordWithoutKlartext
            && diagnostic.Message.Contains("tool radius", StringComparison.Ordinal));
    }

    // The iTNC 530 of machines/ with the rotary table axis A1 of TABLE1 parallel to X (D146).
    private static MachineConfig CylinderMill(string? dialect = null)
    {
        const string Rotary = """

            [[axis]]
            id = "A1"
            ncx = "A"
            letter = "A"
            kind = "rotary"
            owner = "TABLE1"
            parallel = "X"

            [positions]
            """;
        return HeidenhainCompile.MillWithAll(("\n[positions]", Rotary),
            ("dialect = \"iTNC530\"", dialect ?? "dialect = \"iTNC530\""));
    }
}
