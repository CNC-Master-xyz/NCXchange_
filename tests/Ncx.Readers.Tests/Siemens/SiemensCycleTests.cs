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

    // A direct call runs the cycle once at the current position: CYCLE= and one CYCLE_CALL (siemens 7).
    [Fact]
    public void DirectCall_IsCycleAndOneCall()
    {
        Assert.Equal(Lines("RAPID X=10 Y=10 Z=10", Drill, "CYCLE_CALL", "RAPID Z=50"),
            CheckedBody("G0 X10 Y10 Z10\nF100\nCYCLE81(10,0,2,-5,)\nG0 Z50"));
    }

    // Rule 5: _AXN is AXIS, the drilling axis 3 the third geometry axis Z; the parameters the catalog entry has no word
    // for are not written, with a WARNING (siemens 7; machine-config 6).
    [Fact]
    public void CYCLE83WithAxn_IsPeckWithAxis()
    {
        NcxProgram program = FramedProgram("G0 X0 Y0 Z10\nF100\nMCALL CYCLE83(10,0,2,-30,,5,,1,0,0,1,1,3)\nX5\nMCALL");

        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10",
                "CYCLE=PECK AXIS=Z SURFACE=0 CLEARANCE=2 DEPTH=-30 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_F=100 "
                    + "CYCLE_DWELL=0",
                "CYCLE_CALL X=5", "CYCLE=OFF"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensValueNotCarried], Codes(program));
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
                "CYCLE=BORE SURFACE=0 CLEARANCE=2 DEPTH=-20 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_DWELL=1", "CYCLE_CALL"),
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

        Assert.Equal(Lines(Hole, "CYCLE_CALL", Hole, "CYCLE_CALL"),
            CheckedBody("CYCLE81(110,100,2,,65)\nCYCLE81(110,100,2,35)"));
    }

    // D334: with the retract plane below the reference plane the cycle works the other way, so SDIS and DPR count the
    // other way too: CLEARANCE = RFP - SDIS and DEPTH = RFP + DPR.
    [Fact]
    public void CycleDepth_WithTheRetractPlaneBelowTheReferencePlane_CountsTheOtherWay()
    {
        Assert.Equal(Lines("CYCLE=DRILL SURFACE=-100 CLEARANCE=-102 DEPTH=-35 SAFE=-110 CYCLE_RETRACT=SAFE",
                "CYCLE_CALL"),
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

    // An F of an MCALL position block that converts is the feed of the control, as before (controller-mapping 5).
    [Fact]
    public void McallPositionF_ThatConverts_IsTheFeedOfTheControl()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=10", Drill, "CYCLE_CALL X=10 Y=10", "CYCLE=OFF",
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
}
