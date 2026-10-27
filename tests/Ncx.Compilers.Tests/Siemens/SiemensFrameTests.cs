using Ncx.Core.Machine;
using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// Rule 4 of controllers siemens.md 12 and the frames of the Siemens column of controller-mapping 1: ORIGIN, the chain
/// in program order as TRANS and ATRANS, AROT, AMIRROR, CYCLE800 from [transform], G74 and G75, PRESETON, DIAMON, the
/// transformations and CYCLE832 for TOLERANCE, the sub spindle side whose datum runs Z the other way, and the end of a
/// programmable frame or a swivel that a RAW line made.
/// </summary>
public sealed class SiemensFrameTests
{
    // The swivel of the Hermle C22 U program of the corpus, which the reader keeps as RAW (controllers siemens.md 4).
    private const string RawHermleSwivel = "RAW:SIEMENS=\"CYCLE800(0,\\\"HERMLE\\\",0,39,0,0,0,180,0,-90,0,0,0,-1,)\"";

    // The line the RAW swivel writes.
    private const string HermleSwivel = "CYCLE800(0,\"HERMLE\",0,39,0,0,0,180,0,-90,0,0,0,-1,)";

    // Controller-mapping 1, ORIGIN: G54 to G57 are 1 to 4, G505 to G599 5 and up, G500 0 (siemens 4).
    [Fact]
    public void Origin_OneFiveAndZero_IsG54G505AndG500()
    {
        Assert.Equal(Lines("G54", "G505", "G500"), MillBody("ORIGIN=1", "ORIGIN=5", "ORIGIN=0"));
    }

    // Siemens 4: G599 is the last settable frame; a datum beyond it has no code, an ERROR.
    [Fact]
    public void Origin_Beyond99_IsAnError()
    {
        Assert.Equal(["CMP530"], CompilerCodes(Run(Program(MillHeader, "ORIGIN=100"), Mill())));
    }

    // Siemens 12 rule 4: the chain in program order, TRANS for the first entry and ATRANS, AROT, AMIRROR for the rest
    // (D31; controller-mapping 1, SHIFT, ROTATE, MIRROR).
    [Fact]
    public void Chain_ShiftRotationShiftMirror_IsTransThenTheAdditiveInstructions()
    {
        Assert.Equal(Lines("G54", "TRANS X10 Y5", "AROT RPL=30", "ATRANS Z-2", "AMIRROR X0"),
            MillBody("ORIGIN=1", "SHIFT X=10 Y=5", "ROTATE=30", "SHIFT Z=-2", "MIRROR=X"));
    }

    // Siemens 4: a replacing instruction deletes all earlier programmable frame instructions, so a chain cut from its
    // end is written again from its first entry, TRANS alone where no entry stays (language 4.2: RESET removes its
    // entry and what follows).
    [Fact]
    public void ChainReset_FromItsEnd_IsWrittenAgainFromItsFirstEntry()
    {
        Assert.Equal(Lines("G54", "TRANS X10", "AROT RPL=30", "TRANS X10", "TRANS"),
            MillBody("ORIGIN=1", "SHIFT X=10", "ROTATE=30", "ROTATE=RESET", "SHIFT=RESET"));
    }

    // Language 4.2, ORIGIN starts an empty chain: TRANS alone clears the programmable frame.
    [Fact]
    public void Origin_AfterAShift_ClearsTheProgrammableFrameWithTrans()
    {
        Assert.Equal(Lines("G54", "TRANS X10", "TRANS", "G55"), MillBody("ORIGIN=1", "SHIFT X=10", "ORIGIN=2"));
    }

    // D125: SHIFT X=20 under DIAMETER=ON is a diameter, and a frame on a facing axis is a radius on the control
    // unless MD20360 bit 3 says otherwise (Funktionshandbuch Grundfunktionen 2012 p.901-903), so TRANS X10; also
    // where the chain is written again from the entry the virtual machine stores, ATRANS after AROT.
    [Fact]
    public void Shift_OfXUnderDiameterOn_IsTransWithTheRadius()
    {
        Assert.Equal(Lines("TRANS X10 Z5", "AROT RPL=30", "TRANS X10 Z5"),
            MillTurnBody("SHIFT X=20 Z=5", "ROTATE=30", "ROTATE=RESET"));
    }

    // D125, D388: a machine whose X axis keeps frames in diameters ([[axis]] frames_in_diameter, MD20360 bit 3)
    // writes the diameter as the program gives it.
    [Fact]
    public void Shift_OfXOnAnAxisWithFramesInDiameter_IsTransWithTheDiameter()
    {
        MachineConfig machine = Replaced(MillTurnFile, "programming = \"switchable\"",
            "programming = \"switchable\"\nframes_in_diameter = true");

        Assert.Equal(Lines("TRANS X20 Z5", "AROT RPL=30", "TRANS X20 Z5"),
            Body(Run(Program(MillTurnHeader, "SHIFT X=20 Z=5", "ROTATE=30", "ROTATE=RESET"), machine)));
    }

    // D125, D388: with frames_in_diameter (MD20360 bit 3) frames are diameters "ohne Umrechnung", under DIAMETER=OFF
    // too (Funktionshandbuch Grundfunktionen 2012 p.903), so the radius word X=10 is written TRANS X20.
    [Fact]
    public void Shift_OfXUnderDiameterOffWithFramesInDiameter_IsTransWithTheDiameter()
    {
        MachineConfig machine = Replaced(MillTurnFile, "programming = \"switchable\"",
            "programming = \"switchable\"\nframes_in_diameter = true");
        string header = MillTurnHeader.Replace("DIAMETER=ON", "DIAMETER=OFF", StringComparison.Ordinal);

        Assert.Contains("TRANS X20", LinesOf(Body(Run(Program(header, "SHIFT X=10"), machine))));
    }

    // D125: an expression of X under DIAMETER=ON is halved in the TRANS.
    [Fact]
    public void Shift_OfAnXExpressionUnderDiameterOn_IsHalved()
    {
        Assert.Equal(Lines("R1=40", "TRANS X=(R1)/2"), MillTurnBody("VAR:R1=40", "SHIFT X={$R1}"));
    }

    // Siemens 12 rule 4: CYCLE800(...) from the [transform] template of millturn1.toml, {move} in the place of _ST
    // from [transform] move and {dir} in the place of _DIR from MOVE, TURN and MOVE -1 and STAY 0, the angles of the
    // tilt; TILT=RESET is TILT_OFF, CYCLE800() (machine-config 5; controller-mapping 1, TILT; D262).
    [Fact]
    public void Tilt_TurnThenReset_IsCycle800WithDirMinusOneThenCycle800()
    {
        Assert.Equal(Lines("CYCLE800(1,\"TC1\",0,27,0,0,0,0,45,0,0,0,0,-1,100,1)", "CYCLE800()"),
            MillTurnBody("TILT B=45 MOVE=TURN", "TILT=RESET"));
    }

    // Machine-config 5: {dir} of MOVE=STAY is 0, the frame only computed. The template writes mode 27, the axis-wise
    // order Z Y X, with the angles reversed, which is TILT (D335).
    [Fact]
    public void Tilt_Stay_IsCycle800WithDirZero()
    {
        Assert.Equal(Lines("CYCLE800(1,\"TC1\",0,27,0,0,0,30,20,10,0,0,0,0,100,1)"),
            MillTurnBody("TILT A=10 B=20 C=30 MOVE=STAY"));
    }

    // D262: MOVE=MOVE is the tens digit 1 of _ST, which tracks the tool tip, together with _DIR -1, which positions
    // the rotary axes; the two parameters are independent on the control.
    [Fact]
    public void Tilt_Move_IsTheTensDigitOfStWithDirMinusOne()
    {
        Assert.Equal(Lines("CYCLE800(1,\"TC1\",10,27,0,0,0,0,45,0,0,0,0,-1,100,1)"),
            MillTurnBody("TILT B=45 MOVE=MOVE"));
    }

    // D262: a template whose _DIR is a constant cannot write STAY or MOVE, since that constant turns the rotary axes;
    // an ERROR, while MOVE=TURN still compiles.
    [Fact]
    public void Tilt_WithoutDirInTheTemplate_IsAnErrorForEveryMoveButTurn()
    {
        MachineConfig constant = Replaced(MillTurnFile, "{dir},100,1)\"", "-1,100,1)\"");

        Assert.Equal(["CMP532"], CompilerCodes(Run(Program(MillTurnHeader, "TILT B=45 MOVE=STAY"), constant)));
        Assert.Equal(Lines("CYCLE800(1,\"TC1\",0,27,0,0,0,0,45,0,0,0,0,-1,100,1)"),
            Body(Run(Program(MillTurnHeader, "TILT B=45 MOVE=TURN"), constant)));
    }

    // D333: a swivel to a new plane (_ST ones digit 0) deletes the programmable frames, so a SHIFT before the tilt
    // goes into _X0/_Y0/_Z0 of CYCLE800, the reference point before the rotation (Bedienhandbuch Fräsen 2013
    // p.481, 483; Arbeitsvorbereitung 2013 p.712-713); the TRANS of the SHIFT block stands until then.
    [Fact]
    public void Tilt_AfterAShift_WritesTheShiftIntoX0Y0Z0()
    {
        Assert.Equal(Lines("TRANS X10 Y5 Z-2", "CYCLE800(1,\"TC1\",0,27,10,5,-2,0,30,0,0,0,0,-1,100,1)"),
            Body(Run(Program(MillHeader, "SHIFT X=10 Y=5 Z=-2", "TILT B=30 MOVE=TURN"), FiveAxis())));
    }

    // D333: a SHIFT after the tilt is TRANS after CYCLE800, which the swivel leaves standing, since CYCLE800 writes
    // system frames, not the programmable frame (Bedienhandbuch Fräsen 2013 p.481).
    [Fact]
    public void Shift_AfterATilt_IsTransAfterCycle800()
    {
        Assert.Equal(Lines("CYCLE800(1,\"TC1\",0,27,0,0,0,0,30,0,0,0,0,-1,100,1)", "TRANS X10"),
            Body(Run(Program(MillHeader, "TILT B=30 MOVE=TURN", "SHIFT X=10"), FiveAxis())));
    }

    // D333: ROTATE=RESET after the tilt writes the programmable frame again without CYCLE800; the shift before the
    // tilt stays in its _X0 and is not written a second time as TRANS, so TRANS alone clears the rotation.
    [Fact]
    public void RotateReset_AfterATiltOverAShift_IsTransAloneWithoutCycle800()
    {
        Assert.Equal(Lines("TRANS X10", "CYCLE800(1,\"TC1\",0,27,10,0,0,0,30,0,0,0,0,-1,100,1)", "ROT RPL=30",
                "TRANS"),
            Body(Run(Program(MillHeader, "SHIFT X=10", "TILT B=30 MOVE=TURN", "ROTATE=30", "ROTATE=RESET"),
                FiveAxis())));
    }

    // D333: TILT=RESET clears the swivel with CYCLE800() and its _X0 with it, so the shift that stays in the chain is
    // written again as TRANS.
    [Fact]
    public void TiltReset_OverAShift_WritesTheShiftAgainAsTrans()
    {
        Assert.Equal(Lines("TRANS X10", "CYCLE800(1,\"TC1\",0,27,10,0,0,0,30,0,0,0,0,-1,100,1)", "CYCLE800()",
                "TRANS X10"),
            Body(Run(Program(MillHeader, "SHIFT X=10", "TILT B=30 MOVE=TURN", "TILT=RESET"), FiveAxis())));
    }

    // D333: a second swivel to a new plane deletes the first one and the programmed frames (Bedienhandbuch Fräsen
    // 2013 p.483), so a tilt with another tilt before it in the chain, with or without a shift between them, is the
    // ERROR CMP535 until a rule for additive swivels is drafted.
    [Theory]
    [InlineData("TILT A=10 MOVE=TURN")]
    [InlineData("SHIFT X=5")]
    public void Tilt_OnAnotherTilt_IsTheErrorCmp535(string between)
    {
        string[] blocks = between.StartsWith("TILT", StringComparison.Ordinal)
            ? ["TILT B=30 MOVE=TURN", between]
            : ["TILT B=30 MOVE=TURN", between, "TILT A=10 MOVE=TURN"];

        Assert.Contains("CMP535", CompilerCodes(Run(Program(MillHeader, blocks), FiveAxis())));
    }

    // D125, D333: an X shift before a tilt under DIAMETER=ON goes into _X0 as a radius, with the WARNING CMP537,
    // since MD20360 bit 14 may make the cycle take a diameter (unverified).
    [Fact]
    public void Tilt_AfterAnXShiftUnderDiameterOn_IsTheWarningCmp537()
    {
        CompileResult result = Run(Program(MillTurnHeader, "SHIFT X=20", "TILT B=45 MOVE=TURN"), MillTurn());

        Assert.Equal(["CMP537"], CompilerCodes(result));
        Assert.Contains("CYCLE800(1,\"TC1\",0,27,10,0,0,0,45,0,0,0,0,-1,100,1)", LinesOf(Body(result)));
    }

    // D333: a ROTATE or a MIRROR before a tilt is the ERROR CMP535, since the new swivel deletes it and no rule for
    // the additive swivel is drafted.
    [Theory]
    [InlineData("ROTATE=30")]
    [InlineData("MIRROR=X")]
    public void Tilt_AfterARotationOrAMirror_IsTheErrorCmp535(string before)
    {
        Assert.Contains("CMP535",
            CompilerCodes(Run(Program(MillHeader, before, "TILT B=30 MOVE=TURN"), FiveAxis())));
    }

    // D333: a template whose _X0, _Y0 and _Z0 are constants cannot carry a shift before the tilt, CMP532.
    [Fact]
    public void Tilt_AfterAShiftWithoutXyzInTheTemplate_IsTheErrorCmp532()
    {
        MachineConfig constant = Replaced(MillTurnFile, "27,{x},{y},{z},", "27,0,0,0,");

        Assert.Equal(["CMP532"],
            CompilerCodes(Run(Program(MillTurnHeader, "SHIFT Z=5", "TILT B=45 MOVE=TURN"), constant)));
    }

    // D82: TILT_AXIS without TILT_AXIS_ON needs the kinematics module, an ERROR.
    [Fact]
    public void TiltAxis_WithoutTemplate_IsAnError()
    {
        Assert.Equal(["CMP532"], CompilerCodes(Run(Program(MillHeader, "TILT_AXIS A=45 MOVE=TURN"), FiveAxis())));
    }

    // Controller-mapping 1, MOVE and ROT: ROT is not available on Siemens, a WARNING.
    [Fact]
    public void Tilt_WithRot_WarnsThatRotIsNotWritten()
    {
        CompileResult result = Run(Program(MillTurnHeader, "TILT B=45 MOVE=STAY ROT=COORD"), MillTurn());

        Assert.Equal(["CMP533"], CompilerCodes(result));
    }

    // Siemens 4: CYCLE800() cancels the swivel; TILT=RESET removes the tilt (language 4.2). After a RAW swivel the
    // chain of the virtual machine holds no tilt, and the control stays swivelled unless the reset is written:
    // CYCLE800() of TILT_OFF (machine-config 5), the CYCLE800() of the Hermle program the reader reads as TILT=RESET.
    [Fact]
    public void TiltReset_AfterARawSwivel_IsCycle800()
    {
        Assert.Equal(Lines(HermleSwivel, "CYCLE800()"), MillTurnBody(RawHermleSwivel, "TILT=RESET"));
    }

    // Siemens 4: TRANS alone clears the programmable frame; SHIFT=RESET removes the shift (language 4.2), so after a
    // RAW TRANS the control is shifted until TRANS alone is written, although the chain of the virtual machine is
    // empty.
    [Fact]
    public void ShiftReset_AfterARawTrans_IsTransAlone()
    {
        Assert.Equal(Lines("TRANS X10", "TRANS"), MillBody("RAW:SIEMENS=\"TRANS X10\"", "SHIFT=RESET"));
    }

    // Siemens 4, every replacing instruction deletes all earlier programmable frame instructions: the part of the chain
    // that stays after the reset is written again from its first entry, which ends what the RAW text left.
    [Fact]
    public void ShiftReset_AfterARawTrans_WritesTheRotationThatStaysAgain()
    {
        Assert.Equal(Lines("ROT RPL=30", "TRANS X10", "ROT RPL=30"),
            MillBody("ROTATE=30", "RAW:SIEMENS=\"TRANS X10\"", "SHIFT=RESET"));
    }

    // Language 4.2, ORIGIN starts an empty chain: after a RAW TRANS, TRANS alone clears the programmable frame before
    // the datum (siemens 4), which the RAW line left unknown too.
    [Fact]
    public void Origin_AfterARawTrans_ClearsTheProgrammableFrameWithTrans()
    {
        Assert.Equal(Lines("G54", "TRANS X10", "TRANS", "G54"),
            MillBody("ORIGIN=1", "RAW:SIEMENS=\"TRANS X10\"", "ORIGIN=1"));
    }

    // Language 4.2, ORIGIN starts an empty chain: after a RAW swivel, CYCLE800() of TILT_OFF ends it (siemens 4;
    // machine-config 5); the programmable frame, which the RAW text does not name, stays as it was.
    [Fact]
    public void Origin_AfterARawSwivel_EndsTheSwivelWithCycle800()
    {
        Assert.Equal(Lines("G54", HermleSwivel, "CYCLE800()", "G54"),
            MillTurnBody("ORIGIN=1", RawHermleSwivel, "ORIGIN=1"));
    }

    // Virtual machine 1: the STATIC walk does not follow the jump back, which arrives at the label with the swivel of
    // the RAW line after it, so TILT=RESET after the label writes CYCLE800() on every pass (siemens 4, 8).
    [Fact]
    public void TiltReset_AfterALabelThatAJumpReachesFromARawSwivel_IsCycle800()
    {
        Assert.Equal(Lines("R1=0", "AGAIN:", "CYCLE800()", HermleSwivel, "R1=R1+1", "MCALL", "IF R1<2 GOTOB AGAIN"),
            MillTurnBody("VAR:R1=0", "LABEL=AGAIN", "TILT=RESET", RawHermleSwivel, "VAR:R1={$R1 + 1}",
                "JUMP=AGAIN IF={$R1 < 2}"));
    }

    // D99: a subprogram is written once for every caller from an unknown target state, so a caller's RAW swivel may be
    // active where it starts, and its TILT=RESET writes CYCLE800() (siemens 4; virtual machine 3.9).
    [Fact]
    public void TiltReset_InASubprogramCalledAfterARawSwivel_IsCycle800()
    {
        string program = $"""
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            {MillTurnHeader}
            {RawHermleSwivel}
            CALL=UNTILT
            PROGRAM=END
            SUB=BEGIN NAME=UNTILT
            TILT=RESET
            SUB=END
            FILE=END
            """;

        Assert.EndsWith(Lines("%_N_UNTILT_SPF", "PROC UNTILT", "CYCLE800()", "RET"),
            TextOf(Run(program, MillTurn())), StringComparison.Ordinal);
    }

    // Machine-config 5: a machine without TILT_OFF in [transform] cannot write the end of a swivel that a RAW line may
    // have made, which TILT=RESET asks for: the ERROR of the missing template, not a program that stays swivelled.
    [Fact]
    public void TiltReset_AfterARawSwivel_OnAMachineWithoutTiltOff_IsAnError()
    {
        Assert.Equal(["CMP010"], CompilerCodes(Run(Program(MillHeader, RawHermleSwivel, "TILT=RESET"), Mill())));
    }

    // Siemens 4: MSG changes no frame, and the text of its message names no instruction, so ORIGIN after it writes the
    // datum and no reset (controller-mapping 1, COMMENT: MSG is an operator message).
    [Fact]
    public void Origin_AfterAMessageThatNamesTrans_WritesNoReset()
    {
        Assert.Equal(Lines("G54", "MSG(\"TRANS X10\")", "G54"),
            MillTurnBody("ORIGIN=1", "RAW:SIEMENS=\"MSG(\\\"TRANS X10\\\")\"", "ORIGIN=1"));
    }

    // Controller-mapping 1, HOME: G74 with the machine axis names and 0 (siemens 3), G75 with the axes, FP= the point.
    [Fact]
    public void Home_ReferencePointAndPoint2_IsG74WithMachineNamesAndG75WithFp()
    {
        Assert.Equal(Lines("G74 X1=0 Z1=0", "G75 X0 Z0 FP=2"), MillTurnBody("HOME X Z", "HOME X Z POINT=2"));
    }

    // Controller-mapping 1, SETPOS: PRESETON(C, 0), one call per axis of [setpos] (D55).
    [Fact]
    public void Setpos_IsPresetonPerAxis()
    {
        Assert.Equal(Lines("G74 X1=0 Z1=0", "PRESETON(X,300)", "PRESETON(Z,10)"),
            MillTurnBody("HOME X Z", "SETPOS X=300 Z=10"));
    }

    // Controller-mapping 1, DIAMETER: DIAMOF and DIAMON of [diameter] on the switchable X axis of millturn1.toml, and
    // the X words as the control has them: radii after DIAMOF (D60).
    [Fact]
    public void Diameter_OffThenOn_IsDiamofAndDiamonWithTheX()
    {
        Assert.Equal(Lines("DIAMOF", "G0 X20", "DIAMON", "G0 X40"),
            MillTurnBody("DIAMETER=OFF", "RAPID X=20", "DIAMETER=ON", "RAPID X=40"));
    }

    // D146: under TRACYL the C word of the workpiece holder's rotary axis, a length on the circumference, is written
    // as the Y of the transformation in G19, Z stays along the cylinder, and TRAFOOF writes the plane of the program
    // again (840D sl/828D Arbeitsvorbereitung 2013 p.352-353: N40 TRACYL(40), N50 G19); the NCX letter C is never
    // written.
    [Fact]
    public void Cylinder_TheCWord_IsTheYOfTracylInG19()
    {
        string body = MillTurnBody("SPINDLE_MODE:MAIN=AXIS", "CYLINDER=20", "LINE X=30 F=100", "LINE Z=-10 C=15",
            "LINE IC=5", "CYLINDER=OFF", "RAPID C=0");

        Assert.Equal(Lines("M70", "TRACYL(40)", "G19", "G1 X30 F100", "G1 Z-10 Y15", "G1 Y=IC(5)", "TRAFOOF", "G18",
            "G0 C0"), body);
    }

    // D146: the cylinder plane of NCX turns ARC=CCW from the cylinder axis Z toward C (virtual machine 3.2); TRACYL
    // works in G19, which turns G3 from Y, the circumference, toward Z (Programmierhandbuch Grundlagen 2013 p.154:
    // G19 "Ebenenanwahl 2. - 3. Geometrieachse"; Arbeitsvorbereitung 2013 p.352-353). The same arc in the same
    // coordinates is therefore G2 for CCW and G3 for CW, with C as Y and its center as J.
    [Theory]
    [InlineData("ARC=CW Z=-20 C=25 R=10", "G3 Z-20 Y25 CR=10")]
    [InlineData("ARC=CCW Z=-20 C=25 R=10", "G2 Z-20 Y25 CR=10")]
    [InlineData("ARC=CCW Z=-20 C=25 CENTER:Z=-20 CENTER:C=15", "G2 Z-20 Y25 K=AC(-20) J=AC(15)")]
    [InlineData("ARC=CW Z=-20 C=25 CENTER:IZ=0 CENTER:IC=10", "G3 Z-20 Y25 K0 J10")]
    public void Cylinder_Arc_TurnsTheOtherWayInTheG19OfTracyl(string arc, string written)
    {
        string body = MillTurnBody("SPINDLE_MODE:MAIN=AXIS", "CYLINDER=20", "LINE X=30 F=100", "LINE Z=-10 C=15",
            arc);

        Assert.Equal(Lines("M70", "TRACYL(40)", "G19", "G1 X30 F100", "G1 Z-10 Y15", written), body);
    }

    // D146: the orientation change that turns NCX CCW into G2 under the G19 of TRACYL mirrors left and right as well,
    // so COMP=LEFT is G42 and COMP=RIGHT G41 while CYLINDER is on (Arbeitsvorbereitung 2013 p.352; Grundlagen 2013
    // p.154; gap closed 2026-10-28), and G41 again after TRAFOOF.
    [Theory]
    [InlineData("LEFT", "G42")]
    [InlineData("RIGHT", "G41")]
    public void Cylinder_Compensation_IsMirroredInTheG19OfTracyl(string side, string code)
    {
        string body = MillTurnBody("SPINDLE_MODE:MAIN=AXIS", "CYLINDER=20", "LINE X=30 F=100",
            "LINE Z=-10 C=15 COMP=" + side);

        Assert.Contains(code + " ", body, StringComparison.Ordinal);
        Assert.DoesNotContain(code == "G42" ? "G41" : "G42", body, StringComparison.Ordinal);
    }

    // D146: a Y word, a rotary word other than the workpiece holder's (C2 of the sub spindle) and a sweep of a turn or
    // more, whose TURN= end point needs the plane of the developed surface, are the ERROR CMP536.
    [Theory]
    [InlineData("ARC=CW ANGLE=400 CENTER:Z=-20 CENTER:C=15")]
    [InlineData("LINE Y=5")]
    [InlineData("LINE C2=10")]
    public void Cylinder_ArcYOrAnotherRotaryWord_IsTheErrorCmp536(string block)
    {
        CompileResult result = Run(Program(MillTurnHeader, "SPINDLE_MODE:MAIN=AXIS", "SPINDLE_MODE:SUB=AXIS",
            "CYLINDER=20", "LINE X=30 F=100", "LINE Z=-10 C=15", block), MillTurn());

        Assert.Contains("CMP536", Codes(result));
    }

    // D125: under DIAMETER=ON the VM stores the X of SHIFT X=20 as the radius 10; the TRANS that MIRROR=OFF rewrites
    // from the chain entry writes the same radius 10 as the SHIFT block did, since a frame of the control on a facing
    // axis is a radius (Funktionshandbuch Grundfunktionen 2012 p.901-903).
    [Fact]
    public void D125_ShiftRewrittenFromTheChainUnderDiamon_KeepsTheRadius()
    {
        string body = MillTurnBody("DIAMETER=ON", "SHIFT X=20 Z=-2", "MIRROR=Z", "MIRROR=OFF");

        Assert.DoesNotContain("X20", body, StringComparison.Ordinal);
        Assert.Equal(2, body.Split("TRANS X10 Z-2").Length - 1);
    }

    // Controller-mapping 1, TCPM, CYLINDER and POLAR: TRAORI, TRACYL with the working diameter and its plane G19,
    // TRANSMIT and TRAFOOF of [transform], the plane of the program again after TRACYL (millturn1.toml; D96, D146).
    [Fact]
    public void Transformations_AreTheTemplatesOfTheMachine()
    {
        Assert.Equal(Lines("TRACYL(60)", "G19", "TRAFOOF", "G18", "TRANSMIT", "TRAFOOF", "TRAORI", "TRAFOOF"),
            MillTurnBody("CYLINDER=30", "CYLINDER=OFF", "POLAR=ON", "POLAR=OFF", "TCPM=ON", "TCPM=OFF"));
    }

    // Controller-mapping 1, TOLERANCE: CYCLE832(tol, mode, otol) of [tolerance], OFF as CYCLE832(0,0,1) (D85).
    [Fact]
    public void Tolerance_OnAndOff_IsCycle832()
    {
        Assert.Equal(Lines("CYCLE832(0.02,13,0.05)", "CYCLE832(0,0,1)"),
            MillTurnBody("TOLERANCE=0.02 TOLERANCE:ROTARY=0.05 TOLERANCE_MODE=ROUGH", "TOLERANCE=OFF"));
    }

    // D151: without TOLERANCE:ROTARY the ON template is written, the SINUMERIK Operate form without an orientation
    // tolerance, whose third parameter is 1.
    [Fact]
    public void Tolerance_WithoutRotary_IsTheOnTemplate()
    {
        Assert.Equal(Lines("CYCLE832(0.02,1,1)"), MillTurnBody("TOLERANCE=0.02"));
    }

    // Controller-mapping 1, RETRACT: without a template of [retract] only the kinematics module could compute it, an
    // ERROR (D83).
    [Fact]
    public void Retract_WithoutTemplate_IsAnError()
    {
        Assert.Equal(["CMP534"], CompilerCodes(Run(Program(MillHeader, "RETRACT"), Mill())));
    }

    // Machine-config 5, SUB_frame = "datum": on the sub spindle side the datum runs Z the other way and the compiler
    // negates Z (D57; virtual machine 3.4). millturn1.toml names no selection code, so WORKPIECE writes nothing
    // there (D222).
    [Fact]
    public void ZOnTheSubSpindleSide_WithADatum_IsNegated()
    {
        Assert.Equal(Lines("G0 X40 Z5", "G0 X40 Z-5", "G1 Z=IC(2) F0.1"),
            MillTurnBody("RAPID X=40 Z=5", "WORKPIECE=SUB", "RAPID X=40 Z=5", "LINE IZ=-2 F=0.1"));
    }

    // The TODO(question) of SiemensArcs: with Z negated, an arc in the ZX plane turns the other way round.
    [Fact]
    public void ArcOnTheSubSpindleSide_WithADatum_TurnsTheOtherWay()
    {
        Assert.Equal(Lines("G0 X40 Z-5", "G3 X50 Z-10 CR=5 F0.1"),
            MillTurnBody("WORKPIECE=SUB", "RAPID X=40 Z=5", "ARC=CW X=50 Z=10 R=5 F=0.1"));
    }

    // D222: a [workpiece] template holds the selection code alone, so a machine that names one writes it where
    // WORKPIECE stands and the datum of the program stays ORIGIN.
    [Fact]
    public void Workpiece_TemplateOfTheSelectionCode_WritesItWithTheOriginOfTheProgram()
    {
        MachineConfig machine = Replaced(MillTurnFile, "# no MAIN and no SUB:", "SUB = \"M813\"   #");

        Assert.Equal(Lines("G54", "M813", "G0 X40 Z-5"),
            Body(Run(Program(MillTurnHeader, "ORIGIN=1", "WORKPIECE=SUB", "RAPID X=40 Z=5"), machine)));
    }
}
