using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Siemens.SiemensRead;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// The frames, the swivelled plane, the reference points, the diameter and the transformations (controllers siemens.md
/// 4, 6, 11 rule 4; controller-mapping 1; language 4.2; D31, D82, D83): the replacing instructions cut the chain at its
/// first entry and the additive ones append, G58 and G59 replace their part of the shift, CYCLE800 is TILT or TILT_AXIS
/// with MOVE and a RETRACT, $P_UIFR writes are RAW.
/// </summary>
public sealed class SiemensFrameTests
{
    // D340: the settable frame and the programmable frame are separate components of the total frame, so a datum of
    // group 8 and G500 leave TRANS standing; ORIGIN empties the NCX chain, and the reader writes the entries of the
    // chain again after it.
    [Fact]
    public void Datum_AfterAProgrammableFrame_WritesTheFrameAgainAfterTheOrigin()
    {
        Assert.Equal(Lines("ORIGIN=1", "SHIFT X=10", "ORIGIN=2", "SHIFT X=10", "ORIGIN=0", "SHIFT X=10"),
            CheckedBody("G54\nTRANS X10\nG55\nG500"));
    }

    // D340 (3) settles the programmable frame of group 3 alone, and the swivel of CYCLE800 belongs to none of its
    // instructions: whether a datum leaves that swivel standing is the open D333, so the ORIGIN writes the shift again
    // and no second TILT, which would move the rotary axes a second time.
    [Fact]
    public void Datum_AfterACycle800Swivel_WritesTheShiftAgainAndNoSecondTilt()
    {
        Assert.Equal(Lines("TILT A=0 B=45 C=0 MOVE=TURN", "SHIFT X=5", "ORIGIN=1", "SHIFT X=5", "LINE X=10 F=100"),
            CheckedBody("CYCLE800(0,\"TABLE\",0,27,0,0,0,0,45,0,0,0,0,-1,100,1)\nATRANS X5\nG54\nG1 X10 F100"));
    }

    // G54 to G57 are the datums 1 to 4, G505 to G599 5 and up, G500 cancels them (controller-mapping 1, ORIGIN).
    [Fact]
    public void G54G505AndG500_AreOrigins()
    {
        Assert.Equal(Lines("ORIGIN=1", "ORIGIN=5", "ORIGIN=0"), CheckedBody("G54\nG505\nG500"));
    }

    // Rule 4: TRANS cuts the chain at its first entry and appends, ATRANS appends, TRANS alone clears (D31).
    [Fact]
    public void TRANS_ThenATRANS_CutsChainThenAppends()
    {
        Assert.Equal(Lines("SHIFT X=10", "SHIFT Y=5", "SHIFT=RESET", "SHIFT=RESET", "SHIFT X=20", "SHIFT=RESET"),
            CheckedBody("TRANS X10\nATRANS Y5\nTRANS X20\nTRANS"));
    }

    // D125: a Siemens facing axis keeps its frame offsets as radii (MD20360 bit 3 off), while NCX reads SHIFT X under
    // DIAMETER=ON as a diameter, so the reader doubles the X of TRANS, ATRANS and G58/G59 under DIAMON; under DIAMOF
    // X is taken as written.
    [Fact]
    public void TransX_UnderDiamon_IsDoubledIntoTheDiameter()
    {
        Assert.Equal(Lines("DIAMETER=ON", "SHIFT X=20 Z=5", "SHIFT X=4", "DIAMETER=OFF", "SHIFT X=3"),
            CheckedBody("DIAMON\nTRANS X10 Z5\nATRANS X2\nDIAMOF\nATRANS X3", MillTurn()));
    }

    // D125, D388: with frames_in_diameter the control keeps the frame offsets of X as diameters, under DIAMOF too
    // (MD20360 bit 3, Funktionshandbuch Grundfunktionen 2012 p.903), which NCX writes as they are under DIAMETER=ON
    // and halved into the radius NCX reads under DIAMETER=OFF; the compiler doubles that radius again.
    [Fact]
    public void TransX_WithFramesInDiameter_IsTheDiameterUnderDiamonAndItsHalfUnderDiamof()
    {
        MachineConfig machine = MillTurnWith("programming = \"switchable\"",
            "programming = \"switchable\"\nframes_in_diameter = true");

        Assert.Equal(Lines("DIAMETER=ON", "SHIFT X=10 Z=5", "DIAMETER=OFF", "SHIFT X=4"),
            CheckedBody("DIAMON\nTRANS X10 Z5\nDIAMOF\nATRANS X8", machine));
    }

    // D125: only an X axis whose programming is diameter or switchable has radius frames to double; the mill's X is
    // taken as written under DIAMON.
    [Fact]
    public void TransX_OnARadiusAxis_IsTakenAsWritten()
    {
        Assert.Equal(Lines("DIAMETER=ON", "SHIFT X=10"), Body("DIAMON\nTRANS X10"));
    }

    // D125, D333: under DIAMON the unit of _X0 and _X1 of CYCLE800 depends on MD20360 bit 14, which no machine key
    // carries, so a CYCLE800 with a non-zero X reference point stays RAW.
    [Fact]
    public void CYCLE800_WithAnXReferencePointUnderDiamon_StaysRaw()
    {
        const string Swivel = "CYCLE800(0,\"TABLE\",0,27,1,0,0,0,45,0,0,0,0,-1,100,1)";

        Assert.Equal(Lines("DIAMETER=ON", Raw(Swivel)), Body("DIAMON\n" + Swivel, MillTurn()));
    }

    // D125, D388: with frames_in_diameter the compiler writes _X0 of CYCLE800 as a frame value in diameters, so under
    // DIAMOF the reader halves it into the radius of SHIFT X, as for TRANS X.
    [Fact]
    public void CYCLE800_WithAnXReferencePointUnderDiamofWithFramesInDiameter_IsTheHalfShift()
    {
        MachineConfig machine = MillTurnWith("programming = \"switchable\"",
            "programming = \"switchable\"\nframes_in_diameter = true");

        Assert.Contains("SHIFT X=5",
            Body("DIAMOF\nCYCLE800(0,\"TABLE\",0,27,10,0,0,0,45,0,0,0,0,-1,100,1)", machine));
    }

    // D333, D5: after a CYCLE800 kept as RAW the chain holds a swivel the reader does not know, so a later TRANS and
    // CYCLE800() stay RAW instead of being written without that swivel.
    [Fact]
    public void FrameBlocks_AfterARawCYCLE800_StayRaw()
    {
        const string Swivel = "CYCLE800(0,\"TABLE\",0,27,10,0,0,0,45,0,4,0,0,-1,100,1)";

        Assert.Equal(Lines("DIAMETER=ON", Raw(Swivel), Raw("TRANS X5"), Raw("CYCLE800()")),
            Body("DIAMON\n" + Swivel + "\nTRANS X5\nCYCLE800()", MillTurn()));
    }

    // D240: $AA_IW[X] reads a diameter under DIAMON and maps one to one; $AA_IM[X] reads a radius (MD20360 bits 6/7
    // off), half the diameter $SYS_MPOS_X reads under DIAMETER=ON (language 4.12), so the reader writes the NCX name
    // halved under DIAMON with the WARNING RDR510, the factor not yet measured, and takes it as written under DIAMOF
    // or with frames_in_diameter. The compiler writes $SYS_MPOS_X back as ($AA_IM[X]*2), so R1 keeps its value.
    [Fact]
    public void AaImX_UnderDiamon_IsTheNcxNameHalved()
    {
        const string variables = "call_depth = 8\n\n[system_variables]\nSYS_POS_X = \"$AA_IW[X]\"\n"
            + "SYS_MPOS_X = \"$AA_IM[X]\"\n";
        MachineConfig machine = MillTurnWith("call_depth = 8\n", variables);
        MachineConfig inDiameter = MillTurnWith("call_depth = 8\n", variables, "programming = \"switchable\"",
            "programming = \"switchable\"\nframes_in_diameter = true");

        NcxProgram program = FramedProgram("DIAMON\nR1=$AA_IM[X]\nR2=$AA_IW[X]\nDIAMOF\nR3=$AA_IM[X]", machine);

        Assert.Equal(Lines("DIAMETER=ON", "VAR:R1={($SYS_MPOS_X / 2)}", "VAR:R2={$SYS_POS_X}", "DIAMETER=OFF",
                "VAR:R3={$SYS_MPOS_X}"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensMachinePositionDoubled], Codes(program));
        Assert.Equal(Lines("DIAMETER=ON", "VAR:R1={$SYS_MPOS_X}"), Body("DIAMON\nR1=$AA_IM[X]", inDiameter));
    }

    // D240, D388: with frames_in_diameter (MD20360 bits 6/7) $AA_IM[X] reads a diameter under DIAMOF too, twice the
    // radius $SYS_MPOS_X reads under DIAMETER=OFF, so the reader writes the NCX name doubled, without RDR510 since the
    // machine file states the factor; the compiler writes $SYS_MPOS_X back as ($AA_IM[X]/2).
    [Fact]
    public void AaImX_UnderDiamofWithFramesInDiameter_IsTheNcxNameDoubled()
    {
        MachineConfig inDiameter = MillTurnWith("call_depth = 8\n", "call_depth = 8\n\n[system_variables]\n"
            + "SYS_MPOS_X = \"$AA_IM[X]\"\n", "programming = \"switchable\"",
            "programming = \"switchable\"\nframes_in_diameter = true");

        NcxProgram program = FramedProgram("DIAMOF\nR1=$AA_IM[X]", inDiameter);

        Assert.Equal(Lines("DIAMETER=OFF", "VAR:R1={($SYS_MPOS_X * 2)}"), BodyOf(NcxWriter.Write(program)));
        Assert.Empty(Codes(program));
    }

    // Rule 4: ROT and MIRROR cut the chain like TRANS, AROT and AMIRROR append; MIRROR=OFF resets a mirror (D124).
    [Fact]
    public void ROTAndMIRROR_CutTheChain_AROTAndAMIRROR_Append()
    {
        Assert.Equal(Lines("ROTATE=30", "ROTATE=10", "ROTATE=RESET", "ROTATE=RESET", "MIRROR=X", "MIRROR=Y",
                "MIRROR=OFF", "MIRROR=OFF"),
            CheckedBody("ROT Z30\nAROT Z10\nMIRROR X0\nAMIRROR Y0\nROT"));
    }

    // A replacing instruction removes every kind: TRANS after ROT removes the rotation too (D31).
    [Fact]
    public void TRANS_AfterARotation_RemovesTheRotation()
    {
        Assert.Equal(Lines("SHIFT X=10", "SHIFT=RESET", "ROTATE=30", "ROTATE=RESET", "SHIFT X=5 Y=5", "SHIFT X=1"),
            CheckedBody("TRANS X10\nROT Z30\nTRANS X5 Y5\nATRANS X1"));
    }

    // Rule 4: SCALE cuts the chain and its factors stay a RAW word; the scale stays in the frame of the control, so the
    // next instruction that deletes the frame stays RAW (D5).
    [Fact]
    public void SCALE_CutsTheChain_AndLeavesItUnknown()
    {
        NcxProgram program = FramedProgram("TRANS X10\nSCALE X2\nTRANS X5");

        Assert.Equal(Lines("SHIFT X=10", "SHIFT=RESET", Raw("SCALE X2"), Raw("TRANS X5")),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensWordsKeptAsRaw, DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // A frame instruction kept as RAW leaves the chain unknown: the RESETs of its cut are not in the output, so the
    // next replacing instruction stays RAW too (D5; language 4.2).
    [Fact]
    public void FrameInstruction_KeptAsRaw_LeavesTheChainUnknown()
    {
        Assert.Equal(Lines("ROTATE=30", Raw("MIRROR X"), Raw("TRANS X5")), CheckedBody("ROT Z30\nMIRROR X\nTRANS X5"));
    }

    // G58 replaces the absolute part of the shift and G59 the additive part, per axis (controller-mapping 1, ORIGIN):
    // appended where the chain holds no such part, replaced where it is the last entry, RAW otherwise.
    [Fact]
    public void G58AndG59_ReplaceTheirPartOfTheShift()
    {
        Assert.Equal(Lines("SHIFT X=10", "SHIFT X=5", "SHIFT=RESET", "SHIFT X=5 Y=2", Raw("G58 X20")),
            CheckedBody("G58 X10\nG59 X5\nG59 Y2\nG58 X20"));
    }

    // Rule 4: CYCLE800 with axis-wise angles in the order X Y Z (_MODE 57) is TILT, _DIR -1 MOVE=TURN, _FR=1 a RETRACT
    // in front of it; an additive swivel (_ST 1) appends, a new one replaces the tilts of CYCLE800, CYCLE800() resets
    // them (controller-mapping 1, TILT, MOVE, RETRACT; D82, D83).
    [Fact]
    public void CYCLE800Mode57_BecomesTiltWithMoveAndRetract()
    {
        Assert.Equal(Lines("RETRACT", "TILT A=0 B=30 C=0 MOVE=TURN", "RETRACT", "TILT A=0 B=0 C=10 MOVE=TURN",
                "RETRACT", "TILT=RESET", "TILT=RESET", "TILT A=0 B=20 C=0 MOVE=TURN", "TILT=RESET"),
            CheckedBody("CYCLE800(1,\"TABLE\",0,57,0,0,0,0,30,0,0,0,0,-1,100,1)\n"
                + "CYCLE800(1,\"TABLE\",1,57,0,0,0,0,0,10,0,0,0,-1,100,1)\n"
                + "CYCLE800(1,\"TABLE\",0,57,0,0,0,0,20,0,0,0,0,-1,100,1)\nCYCLE800()"));
    }

    // D333: CYCLE800 writes system frames, not the programmable frame, so TRANS, ROT, MIRROR and SCALE cut only the
    // programmable entries after the last tilt of CYCLE800 and the tilt survives; a programmable entry before such a
    // tilt keeps the block RAW, since NCX cannot remove it without the tilt.
    [Fact]
    public void Trans_AfterCYCLE800_CutsOnlyTheProgrammableEntries()
    {
        const string Tilt = "CYCLE800(0,\"TABLE\",0,27,0,0,0,0,45,0,0,0,0,-1,100,1)";
        const string Additive = "CYCLE800(0,\"TABLE\",1,27,0,0,0,0,45,0,0,0,0,-1,100,1)";

        Assert.Equal(Lines("TILT A=0 B=45 C=0 MOVE=TURN", "SHIFT X=5", "SHIFT=RESET", "ROTATE=30"),
            CheckedBody(Tilt + "\nTRANS X5\nROT RPL=30"));
        Assert.Equal(Lines("SHIFT X=5", "TILT A=0 B=45 C=0 MOVE=TURN", Raw("TRANS Y2")),
            Body("ATRANS X5\n" + Additive + "\nTRANS Y2"));
    }

    // D333: a new CYCLE800 (_ST ones digit 0) resets the earlier tilts and every programmable entry before its TILT;
    // _X0, _Y0, _Z0 are a SHIFT before the TILT and _X1, _Y1, _Z1 one after it, which CYCLE800() removes with the tilt.
    [Fact]
    public void CYCLE800_New_ResetsTheChain_ReferencePointsAreShifts()
    {
        Assert.Equal(Lines("SHIFT X=5", "SHIFT=RESET", "SHIFT X=1 Y=2 Z=3", "TILT A=0 B=45 C=0 MOVE=TURN", "SHIFT X=4",
                "SHIFT=RESET", "TILT=RESET", "SHIFT=RESET"),
            CheckedBody("ATRANS X5\nCYCLE800(0,\"TABLE\",0,27,1,2,3,0,45,0,4,0,0,-1,100,1)\nCYCLE800()"));
    }

    // D335: _A is the first rotation of the order the mode gives, not the angle about X; _MODE 39 is the order Z X Y,
    // so its _A turns about Z and is the C of TILT. _DIR 0 only computes the frame, MOVE=STAY; _FR=5 retracts by _FR_I
    // (controller-mapping 1, TILT; D83).
    [Fact]
    public void CYCLE800AxisWiseMode_WithOneTurningAngle_BecomesTheTiltAngleOfThatAxis()
    {
        Assert.Equal(Lines("RETRACT=50", "TILT A=0 B=0 C=30 MOVE=STAY"),
            CheckedBody("CYCLE800(5,\"TABLE\",0,39,0,0,0,30,0,0,0,0,0,0,50,1)"));
    }

    // D335: _MODE 39 is the axis-wise order Z X Y, the form of the Hermle program of the corpus; the reader converts
    // it into the X, Y, Z order of TILT through the rotation matrix, Rz(180) Rx(0) Ry(-90) = Rz(180) Ry(-90).
    [Fact]
    public void CYCLE800AxisWiseMode_WithTwoTurningAngles_BecomesTiltThroughTheRotationMatrix()
    {
        Assert.Equal(Lines("TILT A=0 B=-90 C=180 MOVE=TURN"),
            CheckedBody("CYCLE800(0,\"HERMLE\",0,39,0,0,0,180,0,-90,0,0,0,-1,)"));
    }

    // D335: _MODE 27 is the order Z Y X, which is exactly TILT with A = _C, B = _B and C = _A; the projection-angle
    // mode (bits 7 and 6 = 10) has no construction in the manuals and stays RAW.
    [Fact]
    public void CYCLE800Mode27_IsTiltWithTheAnglesReversed()
    {
        Assert.Equal(Lines("TILT A=-10.196 B=0 C=288.079 MOVE=TURN"),
            CheckedBody("CYCLE800(0,\"TABLE\",0,27,0,0,0,288.079,0,-10.196,0,0,0,-1,0,1)"));
    }

    [Fact]
    public void CYCLE800ProjectionAngleMode_StaysRaw()
    {
        const string Projection = "CYCLE800(0,\"TABLE\",0,155,0,0,0,30,20,10,0,0,0,-1,)";
        NcxProgram program = FramedProgram(Projection);

        Assert.Equal(Lines(Raw(Projection)), BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // CYCLE800 in the rotary-axes mode (_MODE bits 7 and 6 = 11) is TILT_AXIS on the rotary axes the [transform]
    // TILT_AXIS_ON template names, CYCLE800() resets it (controller-mapping 1, TILT_AXIS; D82, D296).
    [Fact]
    public void CYCLE800RotaryAxesMode_BecomesTiltAxis()
    {
        Assert.Equal(Lines("RETRACT", "TILT_AXIS A=-30 C=90 MOVE=TURN", "TILT_AXIS=RESET"),
            CheckedBody("CYCLE800(1,\"TABLE\",0,192,0,0,0,-30,90,0,0,0,0,-1,100,1)\nCYCLE800()", FiveAxis()));
    }

    // D296: the axes of the rotary-axes mode come from the places of _A and _B in the TILT_AXIS_ON template, not from
    // the order of the [[axis]] list, so a template that names C first reads the first angle into C (the canonical
    // order then writes A before C, language 5 rule 6).
    [Fact]
    public void RotaryAxesMode_TakesItsAxesFromTheTiltAxisOnTemplate_NotFromTheAxisOrder()
    {
        string template = "CYCLE800(1,\"TABLE\",0,192,0,0,0,{c},{a},0,0,0,0,{dir},100,1)";

        Assert.Equal(Lines("TILT_AXIS A=90 C=-30 MOVE=TURN"),
            CheckedBody("CYCLE800(0,\"TABLE\",0,192,0,0,0,-30,90,0,0,0,0,-1,100,1)", FiveAxisWith(template)));
    }

    // D296: without a TILT_AXIS_ON template the machine does not say which axes the mode names, and the block stays
    // RAW with its WARNING.
    [Fact]
    public void RotaryAxesMode_OnAMachineWithoutATiltAxisOnTemplate_StaysRaw()
    {
        string source = "CYCLE800(0,\"TABLE\",0,192,0,0,0,-30,90,0,0,0,0,-1,100,1)";

        Assert.Equal(Lines(Raw(source)), Body(source, FiveAxisWith("")));
    }

    // ROTS turns by two spatial angles, TILT with the third 0, and AROTS appends (siemens 4; controller-mapping 1,
    // TILT).
    [Fact]
    public void ROTSAndAROTS_AreTilts()
    {
        Assert.Equal(Lines("TILT A=10 B=20 C=0", "TILT A=5 B=0 C=0"), CheckedBody("ROTS X10 Y20\nAROTS X5"));
    }

    // Rule 4: a $P_UIFR write sets the datum table, a data write kept as RAW (controller-mapping 1, ORIGIN).
    [Fact]
    public void PUifrWrite_IsRaw()
    {
        Assert.Equal(Lines(Raw("$P_UIFR[1,X,TR]=10")), CheckedBody("$P_UIFR[1,X,TR]=10"));
    }

    // G74 is HOME, G75 FP=2 HOME with POINT, PRESETON is SETPOS (controller-mapping 1, HOME and SETPOS).
    [Fact]
    public void G74G75AndPreseton_AreHomeAndSetpos()
    {
        Assert.Equal(Lines("HOME Z", "HOME Z POINT=2", "SETPOS X=0"),
            CheckedBody("G74 Z1=0\nG75 FP=2 Z1=0\nPRESETON(X,0)"));
    }

    // D260: G75 FP=2 to FP=4 are the fixed points of MD30600, HOME POINT=2..4; G75 FP=1, and G75 without FP, which
    // the manual defines as FP=1, have no NCX POINT, since POINT=1 is the reference point G74 approaches: RAW with
    // the WARNING RDR509.
    [Theory]
    [InlineData("G75 FP=1 Z1=0")]
    [InlineData("G75 Z1=0")]
    public void G75FixedPoint1_StaysRawWithTheWarning(string source)
    {
        NcxProgram program = FramedProgram(source);

        Assert.Equal(Lines(Raw(source)), BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensFixedPoint1HasNoPoint, DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // D260: G75 FP=4 is HOME POINT=4; a fixed point above 4 names none the control has (MD30600).
    [Fact]
    public void G75FixedPoints_AreHomePoint2To4()
    {
        Assert.Equal(Lines("HOME X POINT=4", Raw("G75 FP=5 X1=0")), Body("G75 FP=4 X1=0\nG75 FP=5 X1=0"));
    }

    // DIAMON and DIAMOF switch the diameter programming, DIAM90 doubles the incremental X (controller-mapping 1,
    // DIAMETER).
    [Fact]
    public void DiamonDiamofAndDiam90_AreDiameter()
    {
        Assert.Equal(Lines("RAPID X=50 Z=2", "DIAMETER=ON", "LINE X=40 F=0.2", "DIAMETER=OFF", "LINE X=10",
                "DIAMETER=ON", "LINE IX=4"),
            CheckedBody("G0 X50 Z2\nDIAMON\nG1 X40 F0.2\nDIAMOF\nG1 X10\nDIAM90\nG91 G1 X2", MillTurn()));
    }

    // TRANSMIT is POLAR=ON, TRACYL(d) CYLINDER with the radius, TRAFOOF ends the transformation that is on (siemens 6;
    // controller-mapping 1, POLAR and CYLINDER).
    [Fact]
    public void TransmitAndTracyl_ArePolarAndCylinder()
    {
        Assert.Equal(Lines("POLAR=ON", "LINE X=10 Y=5 F=100", "POLAR=OFF", "CYLINDER=20", "LINE Y=10", "CYLINDER=OFF"),
            CheckedBody("TRANSMIT\nG1 X10 Y5 F100\nTRAFOOF\nTRACYL(40)\nG1 Y10\nTRAFOOF", MillTurn()));
    }
}
