using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Siemens.SiemensRead;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// The motion (controllers siemens.md 2, 3, 6, 11 rules 2 and 9; controller-mapping 2; language 4.3; D58): G0 to G3
/// with the verb on every block, AC() and IC() per word, DC(), ACP() and ACN() with the direction kept, the arcs by
/// center, radius, opening angle, turns, intermediate point and tangent, the polar coordinates with their poles, the
/// chamfers, roundings and contour angles expanded, the feeds, the machine frame, the units and the tool vector.
/// </summary>
public sealed class SiemensMotionTests
{
    // Rule 2: G0 and G1 are RAPID and LINE, G90 and G91 with AC() and IC() per word; rule 9: numbers as written.
    [Fact]
    public void G0G1_WithAcAndIcPerWord_BecomeRapidAndLine()
    {
        Assert.Equal(Lines("RAPID X=10 Y=20 Z=5", "LINE X=5 IY=2 F=200", "LINE Z=0 IX=10", "LINE X=1.500"),
            CheckedBody("G0 X10 Y20 Z5\nG1 X=AC(5) Y=IC(2) F200\nG91 G1 X10 Z=AC(0)\nG90 X1.500"));
    }

    // D340: G1 is the Siemens standard setting of G group 1, so a block with axis words before the source writes a
    // code of the group moves at LINE, not at RAPID.
    [Fact]
    public void MotionBeforeACodeOfGroupOne_WithAxisWords_IsALine()
    {
        Assert.Equal(Lines("F=200", "LINE X=10 Y=20"), CheckedBody("F200\nX10 Y20"));
    }

    // Rule 2: DC(), ACP() and ACN() give the path of a rotary axis that the NCX word does not say; the position is C=
    // and the direction a RAW word of the block, written back in place by the Siemens compiler (controller-mapping 2).
    [Fact]
    public void DcAcpAcn_KeepTheDirectionAsARawWord()
    {
        NcxProgram program = FramedProgram("G0 C=DC(90)\nG0 C=ACP(45)", MillTurn());

        Assert.Equal(Lines("RAPID C=90 RAW:SIEMENS=\"C=DC(90)\"", "RAPID C=45 RAW:SIEMENS=\"C=ACP(45)\""),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensWordsKeptAsRaw, DiagnosticCodes.SiemensWordsKeptAsRaw], Codes(program));
    }

    // CR= is the radius form, I=AC() J=AC() the absolute center, I J K the incremental one (siemens 3;
    // controller-mapping 2, ARC).
    [Fact]
    public void Arcs_ByRadiusAndByCenter_BecomeArcs()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "ARC=CW X=10 Y=0 R=5 F=100", "ARC=CCW X=0 Y=0 CENTER:X=5 CENTER:Y=0",
                "ARC=CW X=10 Y=0 CENTER:X=5 CENTER:Y=0"),
            CheckedBody("G0 X0 Y0\nG2 X10 Y0 CR=5 F100\nG3 X0 Y0 I=AC(5) J=AC(0)\nG2 X10 Y0 I5 J0"));
    }

    // AR= with the center is the opening angle, ANGLE; AR= with the end point gives the center (siemens 3;
    // controller-mapping 2, ARC).
    [Fact]
    public void Ar_WithTheCenterIsAngle_WithTheEndPointGivesTheCenter()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE X=10 Y=0 F=100", "ARC=CW CENTER:X=10 CENTER:Y=10 ANGLE=90",
                "ARC=CCW X=30 Y=20 CENTER:X=10 CENTER:Y=30"),
            CheckedBody("G0 X0 Y0\nG1 X10 Y0 F100\nG2 AR=90 I=AC(10) J=AC(10)\nG3 X30 Y20 AR=90"));
    }

    // TURN= adds full turns: the sweep is ANGLE, the end of the helix in Z stays (siemens 3; controller-mapping 2,
    // ARC).
    [Fact]
    public void Turn_BecomesTheAngleOfTheFullTurns()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=0", "ARC=CW Z=-3 CENTER:X=5 CENTER:Y=0 ANGLE=1080 F=100"),
            CheckedBody("G0 X0 Y0 Z0\nG2 X0 Y0 I5 J0 TURN=2 Z-3 F100"));
    }

    // CIP through an intermediate point and CT tangent to the path before are converted with the source-side positions
    // into arcs with their centers (siemens 3; controller-mapping 2, ARC).
    [Fact]
    public void CipAndCt_BecomeArcsWithTheirCenters()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE X=10 Y=0 F=100", "ARC=CCW X=10 Y=10 CENTER:X=10 CENTER:Y=5",
                "ARC=CW X=0 Y=20 CENTER:X=10 CENTER:Y=20"),
            CheckedBody("G0 X0 Y0\nG1 X10 Y0 F100\nCIP X10 Y10 I1=15 J1=5\nCT X0 Y20"));
    }

    // AP= and RP= about the pole of G110, G111 or G112 become Cartesian end points (siemens 3; controller-mapping 2).
    [Fact]
    public void PolarCoordinates_AboutTheirPoles_BecomeCartesian()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0 Z=0", "LINE X=15 Y=10 F=100", "LINE X=11 Y=15"),
            CheckedBody("G0 X0 Y0 Z0\nG110 X10 Y10\nG1 AP=0 RP=5 F100\nG112 AP=90 RP=5\nG1 AP=0 RP=1"));
    }

    // CHF= is a chamfer of that length, CHR= one of that width along each line: both expand into lines (siemens 3;
    // D58).
    [Fact]
    public void ChfAndChr_AreExpandedIntoLines()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE X=8.586 Y=0 F=100", "LINE X=10 Y=1.414", "LINE Y=10",
                "LINE X=19 Y=10", "LINE X=20 Y=11", "LINE Y=20"),
            CheckedBody("G0 X0 Y0\nG1 X10 CHF=2 F100\nG1 Y10\nG1 X20 CHR=1\nG1 Y20"));
    }

    // RND= rounds one corner, RNDM= every corner until RNDM=0: both expand into arcs (siemens 3; D58).
    [Fact]
    public void RndAndRndm_AreExpandedIntoArcs()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE X=8 Y=0 F=100", "ARC=CCW X=10 Y=2 R=2", "LINE X=10 Y=10"),
            CheckedBody("G0 X0 Y0\nG1 X10 Y0 RND=2 F100\nG1 X10 Y10"));
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE X=9 Y=0 F=100", "ARC=CCW X=10 Y=1 R=1", "LINE X=10 Y=9",
                "ARC=CW X=11 Y=10 R=1", "LINE X=20 Y=10"),
            CheckedBody("G0 X0 Y0\nG1 X10 Y0 F100 RNDM=1\nG1 X10 Y10\nG1 X20 Y10 RNDM=0"));
    }

    // ANG= gives a line by its angle, with one end coordinate, or without one meeting the line of the next block
    // (siemens 3; D58).
    [Fact]
    public void Ang_InOneAndInTwoBlocks_IsExpandedIntoLines()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE X=10 Y=10 F=100"), CheckedBody("G0 X0 Y0\nG1 ANG=45 X10 F100"));
        Assert.Equal(Lines("RAPID X=0 Y=0", "LINE X=10 Y=10 F=100", "LINE X=20 Y=0"),
            CheckedBody("G0 X0 Y0\nG1 ANG=45 F100\nG1 X20 Y0 ANG=-45"));
    }

    // FB= feeds its own block and the modal F returns after it (controller-mapping 2, F); G4 F dwells in seconds
    // (controller-mapping 1, DWELL).
    [Fact]
    public void FbAndG4_FeedOneBlockAndDwell()
    {
        Assert.Equal(Lines("DWELL=2", "LINE X=10 F=50", "LINE X=20 F=100"),
            CheckedBody("G4 F2\nG1 X10 F100 FB=50\nG1 X20"));
    }

    // G4 S dwells by revolutions of the master spindle and G4 S2= by those of spindle 2: with the speed known it is
    // DWELL in seconds, and the S of the dwell does not touch the speed, so a second dwell counts with the same one
    // (siemens 3; controller-mapping 1, DWELL).
    [Fact]
    public void G4S_WithTheSpeedKnown_BecomesDwellInSeconds()
    {
        Assert.Equal(Lines("SPINDLE=CW RPM=1000", "DWELL=0.6", "DWELL=1.8"), CheckedBody("S1000 M3\nG4 S10\nG4 S30"));
        Assert.Equal(Lines("SPINDLE:SUB=CW RPM:SUB=500", "DWELL=1.2"),
            CheckedBody("S2=500 M2=3\nG4 S2=10", MillTurn()));
    }

    // G4 S of a spindle whose speed the reader does not know stays RAW (controller-mapping 1, DWELL).
    [Fact]
    public void G4S_WithoutTheSpeed_StaysRaw()
    {
        NcxProgram program = FramedProgram("G4 S10");

        Assert.Equal(Lines(Raw("G4 S10")), BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // G53, G153 and SUPA move in the machine frame, FRAME=MACHINE, and D0 in the block is OFFSET=0 (controller-mapping
    // 1, FRAME and OFFSET).
    [Fact]
    public void G53G153AndSupa_AreFrameMachine_WithD0AsOffset0()
    {
        Assert.Equal(Lines("RAPID Z=0 OFFSET=0 FRAME=MACHINE", "RAPID Z=0 FRAME=MACHINE", "RAPID X=0 FRAME=MACHINE"),
            CheckedBody("G53 G0 Z0 D0\nSUPA G0 Z0\nG153 G0 X0"));
    }

    // Rule 9: an expression as the value of an address becomes an NCX expression, R1 the variable $R1.
    [Fact]
    public void Expression_AsTheValue_BecomesAnNcxExpression()
    {
        Assert.Equal(Lines("RAPID X=1 Y=1", "LINE X={$R1 * 2} Y={$R2 + 1} F=100"),
            CheckedBody("G0 X1 Y1\nG1 X=R1*2 Y=R2+1 F100"));
    }

    // D292: G70 and G71 switch only the geometry, the feeds stay in the basic system of MD10240 (base_units, here the
    // units_default MM), so under G70 the reader converts every feed from mm into inch with the WARNING RDR507;
    // G700 switches the feeds too and converts nothing (NC-Programmierung SW4.92 p.164-166, cnc-manuals).
    [Fact]
    public void FeedUnderG70OnAMetricBase_IsConvertedWithTheWarning()
    {
        NcxProgram program = FramedProgram("G70 G1 X1 F254\nG1 X2 F=R1\nG700 G1 X3 F10\nG71 G1 X4 F500");

        Assert.Equal(Lines("LINE X=1 F=10 UNITS=INCH", "LINE X=2 F={($R1) / 25.4}", "LINE X=3 F=10",
                "LINE X=4 F=500 UNITS=MM"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensFeedInTheBasicSystem, DiagnosticCodes.SiemensFeedInTheBasicSystem],
            Codes(program));
    }

    // D292: the control keeps the physical feed across a switch of group 13, while an NCX F is a number in the active
    // UNITS, so the reader converts the modal feed into the new unit and writes it on the next feed motion, with
    // RDR507 (Funktionshandbuch Grundfunktionen 2012 p.342; NC-Programmierung SW4.92 p.165).
    [Fact]
    public void ModalFeedAcrossAUnitsSwitch_IsConvertedAndWrittenAgain()
    {
        NcxProgram program = FramedProgram("G1 X1 F254\nG700\nG1 X2\nG710 G0 X3\nG1 X4");

        Assert.Equal(Lines("LINE X=1 F=254", "UNITS=INCH", "LINE X=2 F=10", "RAPID X=3 UNITS=MM", "LINE X=4 F=254"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.SiemensFeedInTheBasicSystem, DiagnosticCodes.SiemensFeedInTheBasicSystem],
            Codes(program));
    }

    // D292: where the reader does not know the feed of the control, a RAW block with an F before the switch, it warns.
    [Fact]
    public void UnitsSwitch_WithAnUnknownFeed_IsAWarning()
    {
        NcxProgram program = FramedProgram("G1 X1 F=FOO\nG700");

        Assert.Contains(DiagnosticCodes.SiemensFeedInTheBasicSystem, Codes(program));
    }

    // G70, G71, G700 and G710 are UNITS (controller-mapping 1, UNITS).
    [Fact]
    public void G70G71G700G710_AreUnits()
    {
        Assert.Equal(Lines("RAPID X=0", "RAPID X=1 UNITS=INCH", "UNITS=MM", "UNITS=INCH", "UNITS=MM"),
            CheckedBody("G0 X0\nG70 X1\nG710\nG700\nG71"));
    }

    // TRAORI is TCPM=ON, the tool vector A3 B3 C3 under it TX TY TZ, TRAFOOF ends it (siemens 6; controller-mapping 2).
    [Fact]
    public void ToolVector_UnderTraori_IsTxTyTz()
    {
        Assert.Equal(Lines("TCPM=ON", "LINE X=1 Y=1 Z=1 TX=0 TY=0 TZ=1 F=100", "TCPM=OFF"),
            CheckedBody("TRAORI\nG1 X1 Y1 Z1 A3=0 B3=0 C3=1 F100\nTRAFOOF"));
    }

    // D5 with D346: a block kept RAW with an S the reader could not read sets a speed the reader does not know, so a
    // G4 S after it, which dwells by revolutions of that spindle, stays RAW instead of using the speed before it
    // (controller-mapping 1, DWELL). The S2000 of a block that another word keeps RAW first is never read at all.
    [Theory]
    [InlineData("S=R1 DIV 2")]
    [InlineData("S=R[R1]")]
    [InlineData("IF $P_SEARCH\nS2000\nENDIF")]
    [InlineData("TRANS X=R[R1] S2000")]
    [InlineData("STOPRE S2000")]
    public void G4S_AfterARawBlockWithAnS_StaysRaw(string middle)
    {
        NcxProgram program = FramedProgram("S1000 M3\n" + middle + "\nG4 S5");

        Assert.EndsWith(Lines([.. middle.Split('\n').Select(Raw), Raw("G4 S5")]), BodyOf(NcxWriter.Write(program)),
            StringComparison.Ordinal);
    }

    // A block kept RAW for another word whose S the reader did read runs at that speed, and G4 S after it dwells by
    // its revolutions (controller-mapping 1, DWELL).
    [Fact]
    public void G4S_AfterARawBlockWhoseSWasRead_DwellsByThatSpeed()
    {
        NcxProgram program = FramedProgram("S1000 M3\nG0 X=R[R1] S2000\nG4 S5");

        Assert.EndsWith(Lines(Raw("G0 X=R[R1] S2000"), "DWELL=0.15"), BodyOf(NcxWriter.Write(program)),
            StringComparison.Ordinal);
    }
}
