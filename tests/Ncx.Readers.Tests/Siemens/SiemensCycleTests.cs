using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Siemens.SiemensRead;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// The cycles (controllers siemens.md 7, 11 rule 5; controller-mapping 5; machine-config 6; D94, D157): the sl
/// signatures through the catalog into CYCLE= blocks, _AXN as AXIS, the modal F before MCALL as CYCLE_F, the MCALL
/// positions as CYCLE_CALL, MCALL alone as CYCLE=OFF, the patterns expanded into calls.
/// </summary>
public sealed class SiemensCycleTests
{
    private const string Drill = "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-5 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_F=100";

    // Rule 5: MCALL CYCLE81 makes the cycle modal with the F before it as CYCLE_F, every block with a position calls
    // it, MCALL alone is CYCLE=OFF; the retract plane RTP is SAFE with CYCLE_RETRACT=SAFE (D157).
    [Fact]
    public void MCALLCYCLE81_PositionsBecomeCycleCalls()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10",
                "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-5 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_F=200",
                "CYCLE_CALL X=10 Y=10", "CYCLE_CALL X=20", "CYCLE=OFF", "RAPID Z=50"),
            CheckedBody("G0 X0 Y0 Z10\nF200\nMCALL CYCLE81(10,0,2,-5,)\nX10 Y10\nX20\nMCALL\nG0 Z50"));
    }

    // A direct call runs the cycle once at the current position: CYCLE=, one CYCLE_CALL and CYCLE=OFF, since only
    // MCALL is modal (siemens 7; D224).
    [Fact]
    public void DirectCall_IsCycleAndOneCallThenCycleOff()
    {
        Assert.Equal(Lines("RAPID X=10 Y=10 Z=10", Drill, "CYCLE_CALL", "CYCLE=OFF", "RAPID Z=50"),
            CheckedBody("G0 X10 Y10 Z10\nF100\nCYCLE81(10,0,2,-5,)\nG0 Z50"));
    }

    // Rule 5: _AXN is AXIS, the drilling axis 3 the third geometry axis Z; the constant PECK is FDPR (D181); the
    // parameters the catalog entry has no word for, DTB, DTS and FRF, are not written, with a WARNING (siemens 7;
    // machine-config 6).
    [Fact]
    public void CYCLE83WithAxn_IsPeckWithAxis()
    {
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\nF100\nMCALL CYCLE83(10,0,2,-30,,,5,0,1,1,1,1,3)\nX5\nMCALL");

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10",
                "CYCLE=PECK AXIS=Z SURFACE=0 CLEARANCE=2 DEPTH=-30 SAFE=10 CYCLE_RETRACT=SAFE PECK=5 CYCLE_F=100",
                "CYCLE_CALL X=5", "CYCLE=OFF"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensValueNotCarried], Codes(program));
    }

    // D181: NCX has only the constant PECK, FDPR with _DAM = 0 and FDEP empty, so a CYCLE83 that sets the first depth
    // FDEP or a degression _DAM stays native.
    [Theory]
    [InlineData("CYCLE83(10,0,2,-30,,-5,,0,,,,1)")]
    [InlineData("CYCLE83(10,0,2,-30,,,5,1,,,,1)")]
    public void CYCLE83_WithFdepOrADegression_StaysRaw(string call)
    {
        Assert.EndsWith(Raw(call) + "\n", Body("G0 X0 Y0 Z10\nF100\n" + call), StringComparison.Ordinal);
    }

    // D181 (gap closed 2026-10-27): the dwell at the final depth of CYCLE83 is _DTD, position 16, as on CYCLE830; an
    // omitted VARI is 0, chip breaking (D164).
    [Fact]
    public void CYCLE83_DwellAtTheFinalDepth_IsDtdAndWithoutVariChipBreak()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10",
                "CYCLE=CHIP_BREAK SURFACE=0 CLEARANCE=2 DEPTH=-30 SAFE=10 CYCLE_RETRACT=SAFE PECK=5 CYCLE_F=100 "
                    + "CYCLE_DWELL=0.5",
                "CYCLE_CALL", "CYCLE=OFF"),
            CheckedBody("G0 X0 Y0 Z10\nF100\nCYCLE83(10,0,2,-30,,,5,,,,,,,,,0.5)"));
    }

    // D358: _VRT is the chip-break lift the machine holds in [cycles] chip_break_lift (1 mm on the mill), so the same
    // value is carried; another value is not written, with the WARNING.
    [Fact]
    public void CYCLE83_LiftOfTheMachine_IsCarriedAndAnotherIsWarned()
    {
        NcxProgram same = FramedProgram("G0 X0 Y0 Z10\nF100\nCYCLE83(10,0,2,-30,,,5,,,,,0,,,1)");
        NcxProgram other = FramedProgram("G0 X0 Y0 Z10\nF100\nCYCLE83(10,0,2,-30,,,5,,,,,0,,,2)");

        Assert.Empty(Codes(same));
        Assert.Equal([DiagnosticCodes.SiemensValueNotCarried], Codes(other));
    }

    // D181: MPIT is the ISO metric size, whose ISO 261 coarse pitch is PITCH, 1.5 for M10; a size that is no ISO
    // coarse size stays native.
    [Fact]
    public void CYCLE84_MetricSize_IsTheCoarsePitch()
    {
        Assert.Contains("CYCLE=TAP SURFACE=0 CLEARANCE=2 DEPTH=-15 SAFE=10 CYCLE_RETRACT=SAFE PITCH=1.5",
            CheckedBody("G0 X0 Y0 Z10\nCYCLE84(10,0,2,-15,,,,10)"), StringComparison.Ordinal);
        Assert.Contains(Raw("CYCLE84(10,0,2,-15,,,,11)"), Body("G0 X0 Y0 Z10\nCYCLE84(10,0,2,-15,,,,11)"),
            StringComparison.Ordinal);
    }

    // D380: the ones digit of _AMODE of CYCLE830 is 1 for an absolute _DP and 0, or an omitted _AMODE, for an
    // incremental one, which counts from RFP away from the retract plane; S_FB, position 18, is CYCLE_F.
    [Fact]
    public void CYCLE830_DepthAbsoluteOrIncremental_ByTheOnesDigitOfAmode()
    {
        const string Hole = "CYCLE=DEEP_HOLE SURFACE=0 CLEARANCE=2 DEPTH=-25 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_F=150";

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Hole, "CYCLE_CALL", "CYCLE=OFF", Hole, "CYCLE_CALL", "CYCLE=OFF"),
            CheckedBody("G0 X0 Y0 Z10\n" + Cycle830("25", "") + "\n" + Cycle830("-25", "1")));
    }

    // D380 with D181: an _AMODE that sets another digit than the depth mode stays native, and the reason names no
    // VARI of CYCLE83.
    [Fact]
    public void CYCLE830_WithAnotherModeDigit_StaysRawWithItsOwnReason()
    {
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\n" + Cycle830("-25", "11"));

        Assert.EndsWith(Raw(Cycle830("-25", "11")) + "\n", BodyOf(NcxWriter.Write(program)), StringComparison.Ordinal);
        Assert.DoesNotContain("VARI of CYCLE83", program.Diagnostics.ToText(), StringComparison.Ordinal);
    }

    // D379: the sl SLOT1 (the _DMODE thousands digit 1, NUM 1, RAD and INDA empty) with its reference point at the
    // position reached is SLOT at the call position; LENG, WID and FFP1 have no NCX word (WARNING).
    [Fact]
    public void Slot1_SlFormAtThePositionReached_IsSlot()
    {
        NcxProgram program = FramedProgram("G0 X20 Y10 Z10\n" + Slot1("20", "10", "1"));

        Assert.Equal(Lines("RAPID X=20 Y=10 Z=10",
                "CYCLE=SLOT SURFACE=0 CLEARANCE=2 DEPTH=-5 SAFE=10 CYCLE_RETRACT=SAFE PECK=2 CYCLE_F=100",
                "CYCLE_CALL", "CYCLE=OFF"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensValueNotCarried], Codes(program));
    }

    // D379: a SLOT1 whose reference point lies away from the position reached, or the older form with several slots,
    // stays native.
    [Theory]
    [InlineData("30", "10", "1")]
    [InlineData("20", "10", "2")]
    public void Slot1_AwayFromThePositionOrWithSeveralSlots_StaysRaw(string cpa, string cpo, string num)
    {
        Assert.EndsWith(Raw(Slot1(cpa, cpo, num)) + "\n", Body("G0 X20 Y10 Z10\n" + Slot1(cpa, cpo, num)),
            StringComparison.Ordinal);
    }

    // D387: a literal F on a position block under MCALL is the feed of this and the later holes and the path feed:
    // the CYCLE block stands again with that CYCLE_F before the CYCLE_CALL, and the next feed motion keeps it.
    [Fact]
    public void McallPositionF_Literal_DefinesTheCycleAgainWithThatFeed()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Drill, "CYCLE_CALL X=10 Y=10",
                Drill.Replace("CYCLE_F=100", "CYCLE_F=200", StringComparison.Ordinal), "CYCLE_CALL X=20",
                "CYCLE_CALL X=30", "CYCLE=OFF", "LINE X=40 F=200"),
            CheckedBody("G0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nX10 Y10\nX20 F200\nX30\nMCALL\nG1 X40"));
    }

    // Rule 5: HOLES1 and HOLES2 call the modal cycle at every position of the row and of the circle (siemens 7).
    [Fact]
    public void Holes1AndHoles2_ExpandIntoCycleCalls()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Drill, "CYCLE_CALL X=10 Y=0", "CYCLE_CALL X=20 Y=0",
                "CYCLE_CALL X=30 Y=0", "CYCLE=OFF"),
            CheckedBody("G0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nHOLES1(0,0,0,10,10,3)\nMCALL"));
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Drill, "CYCLE_CALL X=20 Y=0", "CYCLE_CALL X=0 Y=20",
                "CYCLE_CALL X=-20 Y=0", "CYCLE_CALL X=0 Y=-20", "CYCLE=OFF"),
            CheckedBody("G0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nHOLES2(0,0,20,0,0,4)\nMCALL"));
    }

    // D259: a pattern the reader cannot expand keeps the whole modal range RAW with it, the F before the MCALL, the
    // MCALL that armed the cycle and the MCALL alone that ends it, so that the pattern keeps its drilling cycle.
    [Fact]
    public void ModalRange_OfAPatternTheReaderCannotExpand_StaysRawAsAWhole()
    {
        string[] range =
        [
            "F200", "MCALL CYCLE81(10,0,2,-5,)", "CYCLE801(0,0,0,10,10,3,3,0,0,0,0)", "MCALL",
        ];
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\n" + string.Join('\n', range));

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Raw(range[0]), Raw(range[1]), Raw(range[2]), Raw(range[3])),
            BodyOf(NcxWriter.Write(program)));
    }

    // D422: from a RAW MCALL of a subprogram to the MCALL alone that disarms it every block stays RAW, since each
    // position block calls the modal subprogram and a compiler would otherwise write F, S or plane blocks between them
    // that fire it (D259); the block after the range is read again.
    [Fact]
    public void ModalRange_OfASubprogramMcall_StaysRawToTheMcallThatEndsIt()
    {
        string[] range = ["MCALL BOHR", "G0 X10 Y10", "X20", "MCALL"];
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\n" + string.Join('\n', range) + "\nG0 X30");

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Raw(range[0]), Raw(range[1]), Raw(range[2]), Raw(range[3]),
                "RAPID X=30"),
            BodyOf(NcxWriter.Write(program)));
    }

    // D422: a position between the MCALL of a pattern the reader keeps RAW and the pattern stays RAW with the range.
    [Fact]
    public void ModalRange_OfAPatternTheReaderCannotExpand_KeepsAPositionInsideItRaw()
    {
        string[] range =
        [
            "F200", "MCALL CYCLE81(10,0,2,-5,)", "G0 X5 Y5", "CYCLE801(0,0,0,10,10,3,3,0,0,0,0)", "MCALL",
        ];
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\n" + string.Join('\n', range));

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Raw(range[0]), Raw(range[1]), Raw(range[2]), Raw(range[3]),
                Raw(range[4])),
            BodyOf(NcxWriter.Write(program)));
    }

    // D422: without an MCALL alone that ends it, the range runs to the end of the unit.
    [Fact]
    public void ModalRange_WithoutAnMcallThatEndsIt_RunsToTheEndOfTheUnit()
    {
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\nMCALL BOHR\nG0 X10\nG1 X20 F100");

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Raw("MCALL BOHR"), Raw("G0 X10"), Raw("G1 X20 F100")),
            BodyOf(NcxWriter.Write(program)));
    }

    // D259: only the MCALL that ends a modal range the reader kept RAW stays RAW with it. A bare MCALL in a unit
    // whose callers the reader cannot all see ends no such range and is the lossless CYCLE=OFF.
    [Fact]
    public void ModalRange_ABareMcallUnderACallerTheReaderDoesNotKnow_IsCycleOff()
    {
        NcxProgram program = Program("%_N_MAIN_MPF\nG0 X0\nM30\n%_N_HELP_SPF\nPROC HELP\nMCALL\nG0 X5\nRET\n");

        Assert.Contains(Lines("SUB=BEGIN NAME=HELP", "CYCLE=OFF"), NcxWriter.Write(program), StringComparison.Ordinal);
    }

    // D259: a HOLES1 whose arguments the reader cannot read takes its range with it the same way.
    [Fact]
    public void ModalRange_OfAHoles1WithAnUnreadableCount_StaysRawAsAWhole()
    {
        string[] range = ["F100", "MCALL CYCLE81(10,0,2,-5,)", "HOLES1(0,0,0,10,10,R5)", "MCALL"];
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\n" + string.Join('\n', range));

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Raw(range[0]), Raw(range[1]), Raw(range[2]), Raw(range[3])),
            BodyOf(NcxWriter.Write(program)));
    }

    // The parameters of CYCLE86 that the catalog entry BORE names no word for are not written, with a WARNING
    // (machine-config 6).
    [Fact]
    public void CYCLE86_ParametersWithoutAWord_AreNotWrittenWithAWarning()
    {
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\nCYCLE86(10,0,2,-20,,1,3,1,1,0,0)");

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10",
                "CYCLE=BORE SURFACE=0 CLEARANCE=2 DEPTH=-20 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_DWELL=1", "CYCLE_CALL",
                "CYCLE=OFF"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensValueNotCarried], Codes(program));
    }

    // D334: SDIS and DPR are entered without a sign and the cycle works the direction out from RTP and RFP, which it
    // assumes lie the one before the other. With RTP above RFP, CLEARANCE = RFP + SDIS and DEPTH = RFP - DPR, so the
    // manual's own pair CYCLE81(110,100,2,,65) and CYCLE81(110,100,2,35) is one and the same hole.
    [Fact]
    public void CycleDepth_FromTheUnsignedDpr_CountsFromTheReferencePlaneAwayFromTheRetractPlane()
    {
        const string Hole = "CYCLE=DRILL SURFACE=100 CLEARANCE=102 DEPTH=35 SAFE=110 CYCLE_RETRACT=SAFE";

        Assert.Equal(Lines(Hole, "CYCLE_CALL", "CYCLE=OFF", Hole, "CYCLE_CALL", "CYCLE=OFF"),
            CheckedBody("CYCLE81(110,100,2,,65)\nCYCLE81(110,100,2,35)"));
    }

    // D334: with the retract plane below the reference plane the cycle works the other way, so SDIS and DPR count the
    // other way too: CLEARANCE = RFP - SDIS and DEPTH = RFP + DPR.
    [Fact]
    public void CycleDepth_WithTheRetractPlaneBelowTheReferencePlane_CountsTheOtherWay()
    {
        Assert.Equal(Lines("CYCLE=DRILL SURFACE=-100 CLEARANCE=-102 DEPTH=-35 SAFE=-110 CYCLE_RETRACT=SAFE",
                "CYCLE_CALL", "CYCLE=OFF"),
            CheckedBody("CYCLE81(-110,-100,2,,65)"));
    }

    // D341: CYCLE801 works a grid of _NUM1 by _NUM2 positions, _DIS1 and _DIS2 apart, from the reference point at the
    // base angle _STA; the reader expands it into one call per position, as it does HOLES1 and HOLES2.
    [Fact]
    public void Cycle801_AsAGrid_ExpandsIntoOneCallPerPosition()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Drill, "CYCLE_CALL X=10 Y=5", "CYCLE_CALL X=30 Y=5",
                "CYCLE_CALL X=10 Y=20", "CYCLE_CALL X=30 Y=20", "CYCLE=OFF"),
            CheckedBody("G0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nCYCLE801(10,5,0,20,15,2,2)\nMCALL"));
    }

    // D341: CYCLE802 works up to nine free positions; the nine-digit codes _XA and _YA say every position is absolute.
    [Fact]
    public void Cycle802_WithAbsolutePositions_ExpandsIntoOneCallPerPosition()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Drill, "CYCLE_CALL X=10 Y=20", "CYCLE_CALL X=30 Y=40",
                "CYCLE=OFF"),
            CheckedBody("G0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\n"
                + "CYCLE802(111111111,111111111,10,20,30,40)\nMCALL"));
    }

    // D341: the manual leaves out the unneeded positions of CYCLE802 at the end, so a position that follows a left-out
    // one is a hole the reader cannot place; the call stays RAW rather than losing the hole, and the whole modal range
    // stays RAW with it, so that the pattern keeps its drilling cycle (D259).
    [Fact]
    public void Cycle802_WithAPositionAfterALeftOutOne_StaysRaw()
    {
        string[] range =
        [
            "F100", "MCALL CYCLE81(10,0,2,-5,)", "CYCLE802(111111111,111111111,10,20,,,30,40)", "MCALL",
        ];
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\n" + string.Join('\n', range));

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Raw(range[0]), Raw(range[1]), Raw(range[2]), Raw(range[3])),
            BodyOf(NcxWriter.Write(program)));
    }

    // D341: the ones digit of _DMODE carries the plane of CYCLE801, and the expansion works in the modal working
    // plane; a mode that is given and not 0 keeps the call RAW, as the drilling cycles keep their mode integers, and
    // the whole modal range stays RAW with it (D259).
    [Fact]
    public void Cycle801_WithAPlaneOfItsOwnInDmode_StaysRaw()
    {
        string[] range = ["F100", "MCALL CYCLE81(10,0,2,-5,)", "CYCLE801(10,5,0,20,15,2,2,0,,,,,,3)", "MCALL"];
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\n" + string.Join('\n', range));

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Raw(range[0]), Raw(range[1]), Raw(range[2]), Raw(range[3])),
            BodyOf(NcxWriter.Write(program)));
    }

    // CYCLE832 is TOLERANCE with the mode of the machine's table, CYCLE832(0, 0, 1) switches it off (controller-mapping
    // 1, TOLERANCE; D85).
    [Fact]
    public void CYCLE832_IsTolerance()
    {
        Assert.Equal(Lines("TOLERANCE=0.02 TOLERANCE_MODE=FINISH", "TOLERANCE=OFF"),
            CheckedBody("CYCLE832(0.02,1,0.1)\nCYCLE832(0,0,1)"));
    }

    // D346 with the manual audit of 2026-10-26: the F of an MCALL position block that has no NCX value, a DIV whose
    // operand may be negative, keeps the block RAW with RDR506; the feed of the control after it is unknown, so the
    // next feed motion writes no F and runs at the feed the RAW block set (D5; controller-mapping 5, CYCLE_F).
    [Fact]
    public void McallPositionF_WithADivThatMayBeNegative_KeepsTheBlockRawWithTheWarning()
    {
        NcxProgram program = FramedProgram(
            "R1=4\nG0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nX10 Y10 F=R1 DIV 2\nMCALL\nG1 X30");
        string body = BodyOf(NcxWriter.Write(program));

        Assert.Contains(Raw("X10 Y10 F=R1 DIV 2") + "\n", body, StringComparison.Ordinal);
        Assert.DoesNotContain("CYCLE_CALL X=10 Y=10", body, StringComparison.Ordinal);
        Assert.EndsWith("CYCLE=OFF\nLINE X=30\n", body, StringComparison.Ordinal);
        Assert.Equal([DiagnosticCodes.SiemensDivisionRoundingUnknown, DiagnosticCodes.KeptAsRaw],
            Codes(program).Take(2));
    }

    // The F of an MCALL position block that NCX cannot express, an indirect R[R1], keeps the block RAW with the
    // reason, instead of being dropped (D5; controller-mapping 6, VAR).
    [Fact]
    public void McallPositionF_WithAnIndirectRParameter_KeepsTheBlockRaw()
    {
        NcxProgram program = FramedProgram(
            "G0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nX10 Y10 F=R[R1]\nMCALL\nG1 X30");
        string body = BodyOf(NcxWriter.Write(program));

        Assert.Contains(Raw("X10 Y10 F=R[R1]") + "\n", body, StringComparison.Ordinal);
        Assert.EndsWith("CYCLE=OFF\nLINE X=30\n", body, StringComparison.Ordinal);
        Assert.Equal(DiagnosticCodes.KeptAsRaw, Codes(program)[0]);
        Assert.Contains("R[R1]", program.Diagnostics.Items[0].Message, StringComparison.Ordinal);
    }

    // D292 with D387: under G70 on a metric machine the F of an MCALL position block stays in the basic system on the
    // control like any feed, so the reader converts it into inch for CYCLE_F and the path feed, with RDR507.
    [Fact]
    public void McallPositionF_UnderG70_IsConvertedIntoTheNcxUnit()
    {
        string body = CheckedBody(
            "G0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nX10 Y10\nG70 X20 F508\nMCALL\nG1 X30");

        Assert.Contains("CYCLE_F=20", body, StringComparison.Ordinal);
        Assert.Contains("LINE X=30 F=20", body, StringComparison.Ordinal);
    }

    // D387: an F of an MCALL position block that converts to an expression is the feed of this hole and of the path:
    // the CYCLE block stands again with CYCLE_F the expression (controller-mapping 5).
    [Fact]
    public void McallPositionF_ThatConverts_IsTheFeedOfTheCycleAndOfTheControl()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Drill,
                Drill.Replace("CYCLE_F=100", "CYCLE_F={INT(14 / 2)}", StringComparison.Ordinal),
                "CYCLE_CALL X=10 Y=10", "CYCLE=OFF",
                "LINE X=30 F={INT(14 / 2)}"),
            CheckedBody("G0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nX10 Y10 F=14 DIV 2\nMCALL\nG1 X30"));
    }

    // D259: the F before an MCALL that a pattern the reader cannot expand keeps RAW leaves the feed it sets known,
    // so the feed motion after the range writes it.
    [Fact]
    public void ModalRange_KeptRaw_LeavesTheFeedOfItsF()
    {
        string[] range =
        [
            "F200", "MCALL CYCLE81(10,0,2,-5,)", "CYCLE801(0,0,0,10,10,3,3,0,0,0,0)", "MCALL",
        ];
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\n" + string.Join('\n', range) + "\nG1 X30");

        Assert.EndsWith(Lines(Raw(range[3]), "LINE X=30 F=200"), BodyOf(NcxWriter.Write(program)),
            StringComparison.Ordinal);
    }

    // D5 with D346: a negative DTB dwells by revolutions of the master spindle; after a block kept RAW with an S the
    // reader does not know that speed, and the cycle stays RAW (controller-mapping 5, CYCLE_DWELL).
    [Theory]
    [InlineData("S=R1 DIV 2")]
    [InlineData("S=R[R1]")]
    public void NegativeDtb_AfterARawBlockWithAnS_KeepsTheCycleRaw(string middle)
    {
        NcxProgram program = FramedProgram("S1000 M3\n" + middle + "\nG0 X0 Y0 Z10\nF100\nCYCLE82(10,0,2,-5,,-2)");

        Assert.EndsWith(Raw("CYCLE82(10,0,2,-5,,-2)") + "\n", BodyOf(NcxWriter.Write(program)),
            StringComparison.Ordinal);
    }

    // With the speed known, a negative DTB is converted to seconds (controller-mapping 5, CYCLE_DWELL).
    [Fact]
    public void NegativeDtb_WithTheSpeedKnown_IsTheDwellInSeconds()
    {
        string body = BodyOf(NcxWriter.Write(FramedProgram("S1000 M3\nG0 X0 Y0 Z10\nF100\nCYCLE82(10,0,2,-5,,-2)")));

        Assert.Contains("CYCLE_DWELL=0.12", body, StringComparison.Ordinal);
    }

    // D387 (review B1): F=R1 on a position block is the CYCLE_F of the call as an expression, never the old feed.
    [Fact]
    public void McallPositionF_FromAVariable_DefinesTheCycleAgainWithTheExpression()
    {
        Assert.Contains(Lines(Drill.Replace("CYCLE_F=100", "CYCLE_F={$R1}", StringComparison.Ordinal),
                "CYCLE_CALL X=20"),
            CheckedBody("R1=250\nG0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nX10 Y10\nX20 F=R1\nMCALL"),
            StringComparison.Ordinal);
    }

    // SW4.92 p.540 (review B2): F or S alone with G0 active under MCALL calls the cycle at the position reached, a
    // CYCLE_CALL there, after the CYCLE block again with the new CYCLE_F (D387).
    [Fact]
    public void FeedOrSpeedAloneUnderMcall_CallsTheCycleAtThePositionReached()
    {
        string body = CheckedBody("S1000 M3\nG0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nX10 Y10\nF200\nS1200\n"
            + "X20 Y10\nMCALL");

        Assert.Contains(Lines("CYCLE_CALL X=10 Y=10", "F=200",
                Drill.Replace("CYCLE_F=100", "CYCLE_F=200", StringComparison.Ordinal), "CYCLE_CALL"),
            body, StringComparison.Ordinal);
        Assert.Contains(Lines("RPM=1200", "CYCLE_CALL", "CYCLE_CALL X=20 Y=10"), body, StringComparison.Ordinal);
    }

    // SW4.92 p.539 (review B4): a direct call inside an armed MCALL range leaves the MCALL armed, so the CYCLE block of
    // the modal cycle stands again after the direct call instead of CYCLE=OFF, and the next position calls it.
    [Fact]
    public void DirectCall_InsideAModalRange_DefinesTheModalCycleAgain()
    {
        Assert.Contains(Lines("CYCLE_CALL X=10 Y=10",
                "CYCLE=DRILL_DWELL SURFACE=0 CLEARANCE=2 DEPTH=-5 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_F=100 CYCLE_DWELL=1",
                "CYCLE_CALL", Drill, "CYCLE_CALL X=20 Y=10", "CYCLE=OFF"),
            CheckedBody("G0 X0 Y0 Z10\nF100\nMCALL CYCLE81(10,0,2,-5,)\nX10 Y10\nCYCLE82(10,0,2,-5,,1)\nX20 Y10\n"
                + "MCALL"),
            StringComparison.Ordinal);
    }

    // D380: _DTD of CYCLE830 is seconds by the _AMODE thousands digit, not by its sign (SW4.92 p.1123), so a negative
    // one stays native; D181: MPIT counts only with _PITA 0 (SW4.92 p.1129-1130).
    [Fact]
    public void Cycle830NegativeDwellAndMpitWithAnotherPitchUnit_StayRaw()
    {
        string dwell = Cycle830("-25", "1", "-1");

        Assert.EndsWith(Raw(dwell) + "\n", Body("G0 X0 Y0 Z10\n" + dwell), StringComparison.Ordinal);
        Assert.EndsWith(Raw("CYCLE84(10,0,2,-15,,,,10,,,,,,2)") + "\n",
            Body("G0 X0 Y0 Z10\nCYCLE84(10,0,2,-15,,,,10,,,,,,2)"), StringComparison.Ordinal);
    }

    // D181: the ISO coarse pitch is millimetres, so in an inch program PITCH is that pitch in inches.
    [Fact]
    public void CYCLE84_MetricSizeInAnInchProgram_IsThePitchInInches()
    {
        Assert.Contains("PITCH=0.059055", Body("G70\nG0 X0 Y0 Z1\nCYCLE84(1,0,0.1,-1,,,,10)"),
            StringComparison.Ordinal);
    }

    // D362: a position without an NCX word that is 0 is the control's behaviour and gets no WARNING.
    [Fact]
    public void UnmappedPositionsOfZero_GetNoWarning()
    {
        Assert.Empty(Codes(FramedProgram("G0 X0 Y0 Z10\nCYCLE86(10,0,2,-20,,0,0,0,0,0,0)")));
    }

    // D379 with p.1057: SLOT2 reads its _AMODE ones digit as SLOT1 does, 2 absolute.
    [Fact]
    public void Slot2_WithAbsoluteAmode_IsCircSlot()
    {
        Assert.Contains("CYCLE=CIRC_SLOT",
            Body("G0 X40 Y40 Z10\nSLOT2(10,0,2,-5,,1,90,10,40,40,20,0,0,100,300,2,0,0,0,,,,,,,,,1000,2)"),
            StringComparison.Ordinal);
    }

    // A CYCLE830 with _DP, _AMODE and _DTD, S_FB = 150 at position 18 (SW4.92 p.1119-1123).
    private static string Cycle830(string depth, string amode, string dwell = "")
    {
        string[] positions = new string[38];
        Array.Fill(positions, "");
        positions[0] = "10";
        positions[1] = "0";
        positions[2] = "2";
        positions[3] = depth;
        positions[12] = dwell;
        positions[17] = "150";
        positions[37] = amode;
        return "CYCLE830(" + string.Join(",", positions).TrimEnd(',') + ")";
    }

    // A sl SLOT1 at the reference point _CPA/_CPO with NUM, length 30, width 8, FFD 100, FFP1 200, _MID 2 and the
    // _DMODE thousands digit 1 (SW4.92 p.1052-1054).
    private static string Slot1(string cpa, string cpo, string num)
    {
        string[] positions = new string[30];
        Array.Fill(positions, "");
        positions[0] = "10";
        positions[1] = "0";
        positions[2] = "2";
        positions[3] = "-5";
        positions[5] = num;
        positions[6] = "30";
        positions[7] = "8";
        positions[8] = cpa;
        positions[9] = cpo;
        positions[13] = "100";
        positions[14] = "200";
        positions[15] = "2";
        positions[29] = "1000";
        return "SLOT1(" + string.Join(",", positions) + ")";
    }

    // D379, D5: CIRC_SLOT is the sl SLOT2 only, NUM = 1 with the _DMODE thousands digit 1 ("Funktionen SLOT2 ab SW
    // 2.5", SW4.92 p.1057); a SLOT2 without it is no CIRC_SLOT, so its geometry is never dropped.
    [Fact]
    public void Slot2_WithoutTheSlFormDigit_IsNoCircSlot()
    {
        string body = Body("G0 X0 Y0 Z10\nSLOT2(10,0,2,-5,,1,30,8,0,0,20,0,0,100,200,2)\nG0 Z50");

        Assert.DoesNotContain("CIRC_SLOT", body, StringComparison.Ordinal);
        Assert.Contains(Raw("SLOT2(10,0,2,-5,,1,30,8,0,0,20,0,0,100,200,2)"), body, StringComparison.Ordinal);
    }
}
