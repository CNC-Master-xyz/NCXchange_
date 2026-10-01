using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Fanuc.FanucRead;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The Fanuc column of controller-mapping 5, cycles (controllers fanuc.md 6, 9 rule 5; language 4.7, 4.7.1): the
/// drilling family, CLEARANCE and DEPTH absolute, CYCLE_RETRACT, a call per position block, CYCLE=OFF, K repeats, AXIS
/// on a lathe, the turning cycles of the catalog, and what stays RAW.
/// </summary>
public sealed class FanucCycleTests
{
    // G81 G99 Z-21.732 R5. F565 drills at the current position; every following block with a position calls again;
    // G80 cancels (controller-mapping 5; language 6, the drilling example; BOHREN.fanuc.nc N90).
    [Fact]
    public void G81_WithG99_BecomesDrillCycleWithClearanceRetract()
    {
        Assert.Equal(
            Lines(
                "RAPID X=10 Y=10",
                "RAPID Z=5",
                "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE CYCLE_F=565",
                "CYCLE_CALL",
                "CYCLE_CALL X=30",
                "CYCLE=OFF"),
            Body("G0 X10. Y10.\nZ5.\nG81 G99 Z-21.732 R5. F565\nX30.\nG80"));
    }

    // G98 returns to the initial level, the drilling-axis position before the cycle: CYCLE_RETRACT=SAFE with SAFE
    // (controller-mapping 5).
    [Fact]
    public void G81_WithG98_ReturnsToTheInitialLevel()
    {
        Assert.Equal(
            Lines(
                "RAPID X=10 Y=10 Z=20",
                "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-5 SAFE=20 CYCLE_RETRACT=SAFE CYCLE_F=100",
                "CYCLE_CALL",
                "CYCLE=OFF"),
            Body("G0 X10. Y10. Z20.\nG81 G98 Z-5. R2. F100\nG80"));
    }

    // G73 chip-breaks and G83 pecks with Q, PECK (controller-mapping 5).
    [Fact]
    public void G73AndG83_AreChipBreakAndPeckWithTheirPeck()
    {
        Assert.Equal(
            Lines(
                "RAPID X=0 Y=0 Z=10",
                "CYCLE=CHIP_BREAK SURFACE=0 CLEARANCE=2 DEPTH=-5 CYCLE_RETRACT=CLEARANCE PECK=1.2 CYCLE_F=100",
                "CYCLE_CALL",
                "CYCLE=PECK SURFACE=0 CLEARANCE=2 DEPTH=-5 CYCLE_RETRACT=CLEARANCE PECK=1.2 CYCLE_F=100",
                "CYCLE_CALL X=10",
                "CYCLE=OFF"),
            Body("G0 X0 Y0 Z10.\nG73 G99 Z-5. R2. Q1.2 F100\nG83 X10.\nG80"));
    }

    // G82 dwells P milliseconds at the bottom, CYCLE_DWELL in seconds (controller-mapping 5).
    [Fact]
    public void G82_WithP_HasTheDwellInSeconds()
    {
        Assert.Equal(
            Lines(
                "RAPID X=0 Y=0 Z=10",
                "CYCLE=DRILL_DWELL SURFACE=0 CLEARANCE=2 DEPTH=-5 CYCLE_RETRACT=CLEARANCE CYCLE_F=100 CYCLE_DWELL=0.5",
                "CYCLE_CALL",
                "CYCLE=OFF"),
            Body("G0 X0 Y0 Z10.\nG82 G99 Z-5. R2. P500 F100\nG80"));
    }

    // D256: a G84 without the rigid-tapping line of the TAP entry in front of it is tapping with a compensating
    // chuck, which is another machining than CYCLE=TAP and has no entry in the Fanuc catalog, so the blocks of the
    // cycle stay RAW and no compiler writes the M29 of rigid tapping in front of them (decisions.md; fanuc 6; D5).
    [Fact]
    public void TapWithoutItsRigidTapLine_IsAnotherMachining_KeepsItsBlocksAsRaw()
    {
        Assert.Equal(
            Lines(
                "SPINDLE=CW RPM=500",
                "RAPID X=0 Y=0 Z=10",
                "RAW:FANUC=\"G84 G99 Z-20. R5. F750\"",
                "RAW:FANUC=\"X30.\"",
                "CYCLE=OFF"),
            Body("S500 M3\nG0 X0 Y0 Z10.\nG84 G99 Z-20. R5. F750\nX30.\nG80"));
    }

    // D256, BOHREN.fanuc.nc N3100 and N3110: the M29 S of rigid tapping directly before the G84 is the before line
    // of the TAP entry of the catalog and belongs to CYCLE=TAP, and its S writes no RPM where the spindle already
    // runs at that speed.
    [Fact]
    public void RigidTapLine_BeforeTheCycleBlock_IsReadIntoTheTapCycle()
    {
        Assert.Equal(
            Lines(
                "SPINDLE=CW RPM=500",
                "RAPID X=0 Y=0 Z=10",
                "CYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=750 PITCH=1.5",
                "CYCLE_CALL",
                "CYCLE=OFF"),
            Body("S500 M3\nG0 X0 Y0 Z10.\nM29 S500\nG84 G99 Z-20. R5. F750\nG80"));
    }

    // D256: the rigid-tapping line enters the mode for the whole run of the cycle - its own evidence has an M29
    // inside a running tapping cycle alarm PS0204, so a legal source cannot repeat the line in front of a second
    // cycle block - so a second G84 until G80 is the same cycle TAP and not another machining.
    [Fact]
    public void RigidTapLine_WithASecondCycleBlockOfTheRun_KeepsReadingTheTapCycle()
    {
        Assert.Equal(
            Lines(
                "SPINDLE=CW RPM=500",
                "RAPID X=0 Y=0 Z=10",
                "CYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=750 PITCH=1.5",
                "CYCLE_CALL",
                "CYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-25 CYCLE_RETRACT=CLEARANCE CYCLE_F=750 PITCH=1.5",
                "CYCLE_CALL",
                "CYCLE=OFF"),
            Body("S500 M3\nG0 X0 Y0 Z10.\nM29 S500\nG84 G99 Z-20. R5. F750\nG84 G99 Z-25. R5. F750\nG80"));
    }

    // D256 names three ways into the rigid-tapping mode, the second of them "M29 S in the same block as the tapping
    // command", so the before line standing in the cycle block itself is the cycle TAP as well.
    [Fact]
    public void RigidTapLine_InTheCycleBlockItself_IsReadIntoTheTapCycle()
    {
        Assert.Equal(
            Lines(
                "SPINDLE=CW RPM=500",
                "RAPID X=0 Y=0 Z=10",
                "CYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=750 PITCH=1.5",
                "CYCLE_CALL",
                "CYCLE=OFF"),
            Body("S500 M3\nG0 X0 Y0 Z10.\nM29 S500 G84 G99 Z-20. R5. F750\nG80"));
    }

    // D256 requires the rigid-tapping line immediately before the cycle block, and a call may stand between the two:
    // where the cycle block begins a section a block of the file calls, the reader does not see which block runs
    // before it, so it can say neither that the line stands there nor that the source programmed the other
    // machining, and the blocks of the cycle stay RAW with that reason (D5; wave-3 question #195).
    [Fact]
    public void TapAtTheHeadOfACalledSubprogram_WhoseLineTheReaderCannotSee_KeepsItsBlocksAsRaw()
    {
        NcxProgram program = Program("%\nO0001\nS500 M3\nG0 X0 Y0 Z10.\nM29 S500\nM98 P100\nG80\nM30\n"
            + "O0100\nG84 G99 Z-20. R5. F750\nM99\n%\n");
        Assert.Contains("RAW:FANUC=\"G84 G99 Z-20. R5. F750\"", NcxWriter.Write(program), StringComparison.Ordinal);
        Assert.Contains("the reader does not see which block runs before this one",
            program.Diagnostics.ToText(), StringComparison.Ordinal);
    }

    // D256: the S of the before line is the speed the tapping runs at, so a speed other than the running one is RPM.
    [Fact]
    public void RigidTapLine_WithAnotherSpeed_WritesThatRpm()
    {
        Assert.Equal(
            Lines(
                "SPINDLE=CW RPM=500",
                "RAPID X=0 Y=0 Z=10",
                "RPM=300",
                "CYCLE=TAP SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=750 PITCH=2.5",
                "CYCLE_CALL",
                "CYCLE=OFF"),
            Body("S500 M3\nG0 X0 Y0 Z10.\nM29 S300\nG84 G99 Z-20. R5. F750\nG80"));
    }

    // K3 under G91 repeats the cycle three times at incremental steps: three CYCLE_CALL blocks; R and Z under G91 are
    // made absolute from the initial level (controller-mapping 5, repeats; controllers fanuc.md 6).
    [Fact]
    public void K3_UnderG91_ExpandsIntoThreeCalls()
    {
        Assert.Equal(
            Lines(
                "RAPID X=0 Y=0 Z=10",
                "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-13 CYCLE_RETRACT=CLEARANCE CYCLE_F=100",
                "CYCLE_CALL IX=10",
                "CYCLE_CALL IX=10",
                "CYCLE_CALL IX=10",
                "CYCLE=OFF"),
            Body("G0 X0 Y0 Z10.\nG91 G81 G99 X10. Z-15. R-8. K3 F100\nG80 G90"));
    }

    // G85 reams: CYCLE=REAM with the absolute CLEARANCE and DEPTH (controller-mapping 5; cycles/fanuc.toml).
    [Fact]
    public void G85_WithG99_BecomesReamCycle()
    {
        Assert.Equal(
            Lines(
                "RAPID X=0 Y=0 Z=10",
                "CYCLE=REAM SURFACE=0 CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=CLEARANCE CYCLE_F=420",
                "CYCLE_CALL",
                "CYCLE_CALL X=15",
                "CYCLE=OFF"),
            Body("G0 X0 Y0 Z10.\nG85 G99 Z-20. R5. F420\nX15.\nG80"));
    }

    // G86 bores: CYCLE=BORE, G98 back to the initial level (controller-mapping 5; cycles/fanuc.toml).
    [Fact]
    public void G86_WithG98_BecomesBoreCycleBackToTheInitialLevel()
    {
        Assert.Equal(
            Lines(
                "RAPID X=0 Y=0 Z=10",
                "CYCLE=BORE SURFACE=0 CLEARANCE=2 DEPTH=-12 SAFE=10 CYCLE_RETRACT=SAFE CYCLE_F=80",
                "CYCLE_CALL",
                "CYCLE=OFF"),
            Body("G0 X0 Y0 Z10.\nG86 G98 Z-12. R2. F80\nG80"));
    }

    // A cycle feeds with the modal F of the control, and a feed motion after the cycle moves with it (D29, language
    // 4.7).
    [Fact]
    public void ModalF_OfTheControl_IsTheCycleFeedAndTheFeedAfterIt()
    {
        Assert.Equal(
            Lines(
                "LINE X=0 Y=0 Z=10 F=200",
                "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-5 CYCLE_RETRACT=CLEARANCE CYCLE_F=200",
                "CYCLE_CALL",
                "CYCLE=OFF",
                "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-5 CYCLE_RETRACT=CLEARANCE CYCLE_F=50",
                "CYCLE_CALL X=5",
                "CYCLE=OFF",
                "LINE Z=0 F=50"),
            Body("G1 X0 Y0 Z10. F200\nG81 G99 Z-5. R2.\nG80\nG81 G99 X5. Z-5. R2. F50\nG80\nG1 Z0"));
    }

    // A G0 after the calls ends the cycle of a mill, so that the block moves and does not drill.
    [Fact]
    public void G0_AfterTheCalls_EndsTheCycleOfAMill()
    {
        Assert.Equal(
            Lines(
                "RAPID X=0 Y=0 Z=10",
                "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-5 CYCLE_RETRACT=CLEARANCE CYCLE_F=100",
                "CYCLE_CALL",
                "RAPID X=20 CYCLE=OFF"),
            Body("G0 X0 Y0 Z10.\nG81 G99 Z-5. R2. F100\nG0 X20."));
    }

    // On a lathe G87 drills along X on the circumference, AXIS=X, called at every C position (controller-mapping 5,
    // AXIS; D59; examples/POLAR_FACE.ncx).
    [Fact]
    public void LatheG87_DrillsAlongXAtEveryCPosition()
    {
        Assert.Equal(
            Lines(
                "RAPID X=60 Z=-20 C=0",
                "CYCLE=DRILL AXIS=X CLEARANCE=54 DEPTH=30 CYCLE_RETRACT=CLEARANCE CYCLE_F=0.05",
                "CYCLE_CALL",
                "CYCLE_CALL C=120",
                "CYCLE=OFF"),
            Body("G0 X60. Z-20. C0\nG87 X30. R54. F0.05\nC120.\nG80", Lathe()));
    }

    // D160: of two entries on one native the reader takes the first in catalog order whose params cover the words of
    // the block, so a lathe G83 with a peck depth Q is PECK and one without it DRILL; the same holds for G87 along X.
    [Theory]
    [InlineData("G83 Z-40. R-5. F0.2",
        "CYCLE=DRILL AXIS=Z SURFACE=0 CLEARANCE=-5 DEPTH=-40 CYCLE_RETRACT=CLEARANCE CYCLE_F=0.2")]
    [InlineData("G83 Z-40. R-5. Q2000 F0.2",
        "CYCLE=PECK AXIS=Z SURFACE=0 CLEARANCE=-5 DEPTH=-40 CYCLE_RETRACT=CLEARANCE PECK=2000 CYCLE_F=0.2")]
    public void LatheDrillingEntry_G83WithAndWithoutQ_IsPeckAndDrill(string source, string cycle)
    {
        Assert.Equal(Lines("RAPID Z=5", cycle, "CYCLE_CALL", "CYCLE=OFF"),
            Body("G0 Z5.\n" + source + "\nG80", Lathe()));
    }

    // D161: THREAD is G76, the one-shot cycle that cuts the whole thread, and the modal G92, which cuts one pass per
    // block like G90 and G94, is THREAD_PASS.
    [Fact]
    public void FanucThreadCycleNames_G92OfSystemA_IsThreadPass()
    {
        Assert.Equal(
            Lines("RAPID X=70 Z=2", "CYCLE=THREAD_PASS", "CYCLE_CALL X=60 Z=-30", "RAPID X=100 CYCLE=OFF"),
            Body("G0 X70. Z2.\nG92 X60. Z-30.\nG0 X100.", Lathe()));
    }

    // G90 of system A is the modal turning cycle TURN_OD of the catalog: CYCLE= and a CYCLE_CALL per block with X and
    // Z (language 4.7.1; controller-mapping 5). D187: X(U) and Z(W) are modal data the cycle keeps, and a call leaves
    // the tool at its start point, so the reader writes both end-point words on every call.
    [Fact]
    public void G90_OfSystemA_IsTurnOdWithACallPerBlock()
    {
        Assert.Equal(
            Lines(
                "RAPID X=70 Z=2",
                "CYCLE=TURN_OD CYCLE_F=0.3",
                "CYCLE_CALL X=60 Z=-70",
                "CYCLE_CALL X=50 Z=-70",
                "RAPID X=100 CYCLE=OFF"),
            Body("G0 X70. Z2.\nG90 X60. Z-70. F0.3\nX50.\nG0 X100.", Lathe()));
    }

    // D187: the control brings the tool back to the start point A after a simple turning cycle, which is why the
    // manual's own repeat example cuts four progressively deeper passes with incremental U words counted from that
    // same start point, not from the previous end point (decisions.md; fanuc 6).
    [Fact]
    public void RepeatOfASimpleTurningCycle_WithIncrementalWords_CountsFromTheStartPoint()
    {
        Assert.Equal(
            Lines(
                "RAPID X=70 Z=2",
                "CYCLE=TURN_OD CYCLE_F=0.4",
                "CYCLE_CALL X=62 Z=-64",
                "CYCLE_CALL X=54 Z=-64",
                "CYCLE_CALL X=46 Z=-64",
                "RAPID X=100 CYCLE=OFF"),
            Body("G0 X70. Z2.\nG90 U-8. W-66. F0.4\nU-16.\nU-24.\nG0 X100.", Lathe()));
    }

    // D175: the F of the thread cycle G92 is the thread lead, the word PITCH, while the F of G90 and G94 is the
    // cutting feed CYCLE_F (decisions.md; language 4.7, the PITCH row; fanuc 6). G92 is THREAD_PASS (D161).
    [Fact]
    public void Feed_OnTheThreadCycleG92_IsThePitchAndNotTheCycleFeed()
    {
        string text = Text("%\nO0001\nG0 X70. Z2.\nG92 X60. Z-70. F2.0\nX50.\nG0 X100.\nM30\n%\n", Lathe());

        Assert.Equal(
            Lines(
                "RAPID X=70 Z=2",
                "CYCLE=THREAD_PASS PITCH=2.0",
                "CYCLE_CALL X=60 Z=-70",
                "CYCLE_CALL X=50 Z=-70",
                "RAPID X=100 CYCLE=OFF"),
            BodyOf(text));
        AssertFormatsToItself(text);
        Assert.False(Check(text, Lathe()).HasErrors, Check(text, Lathe()).ToText());
    }

    // D175 gives the taper R of G90, G92 and G94 the catalog word TAPER, which cycles/fanuc-lathe.toml maps, but the
    // parser takes no parameter name of a catalog entry yet (D114): the reader keeps such a block RAW, so nothing is
    // lost
    // (D5) and the text it writes is one that check can run (code-guidelines 4).
    [Fact]
    public void Taper_OnASimpleTurningCycle_KeepsItsBlockRawUntilTheParserTakesTheCatalogWord()
    {
        string text = Text("%\nO0001\nG0 X70. Z2.\nG90 X60. Z-70. R-2. F0.3\nM30\n%\n", Lathe());

        Assert.Contains("RAW:FANUC=\"G90 X60. Z-70. R-2. F0.3\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TAPER", text, StringComparison.Ordinal);
    }

    // G70 to G76 of system A name the contour that follows them with P and Q: the reader turns the range into a SUB
    // section of the file behind the program's end and writes the catalog cycle with CONTOUR=name on the cycle block,
    // called once where it stands (language 4.7, CONTOUR, and 4.7.1; machine-config 6, contour; D65; controller-mapping
    // 5, the turning cycles and CYCLE_CALL).
    [Theory]
    [InlineData("G70", "FINISH")]
    [InlineData("G71", "ROUGH_TURN")]
    [InlineData("G72", "ROUGH_FACE")]
    public void G70ToG76_WithTheContourThatFollows_AreTheCatalogCycleWithTheContourAsASubSection(string code,
        string cycle)
    {
        string text = Text(
            "%\nO0001\nG0 X40. Z2.\n" + code + " P10 Q20\nN10 G0 X20.\nN20 G1 Z-10. F0.2\nG0 X100.\nM30\n%\n", Lathe());

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1",
                "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY DIAMETER=ON CYCLE=OFF",
                "RAPID X=40 Z=2",
                "CYCLE=" + cycle + " CONTOUR=CONTOUR_10_20",
                "CYCLE_CALL",
                "RAPID X=100",
                "PROGRAM=END",
                "SUB=BEGIN NAME=CONTOUR_10_20",
                "RAPID X=20",
                "LINE Z=-10 F=0.2",
                "SUB=END",
                "FILE=END"),
            text);
        AssertFormatsToItself(text);
        Assert.False(Check(text, Lathe()).HasErrors, Check(text, Lathe()).ToText());
    }

    // D176: P and Q of G74, G75 and G76 are infeed steps and thread data, not a block range, so those entries carry
    // no contour and the block keeps the words its entry does not map: the reader keeps it RAW with its range, and no
    // SUB section is made (decisions.md; fanuc 6; D5).
    [Theory]
    [InlineData("G75")]
    [InlineData("G76")]
    public void ContourRange_OnTheCyclesThatTakeNone_IsNoContourAndTheBlockStaysRaw(string code)
    {
        string text = Text(
            "%\nO0001\nG0 X40. Z2.\n" + code + " P10 Q20\nN10 G0 X20.\nN20 G1 Z-10. F0.2\nM30\n%\n", Lathe());

        Assert.Contains("RAW:FANUC=\"" + code + " P10 Q20\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CONTOUR", text, StringComparison.Ordinal);
    }

    // G71 roughs along the contour that follows it and G70 finishes along the same one: both name its one SUB section
    // (language 4.7.1; D65).
    [Fact]
    public void G71AndG70_OfOneContour_ShareItsSubSection()
    {
        string text = Text(
            "%\nO0001\nG0 X40. Z2.\nG71 P10 Q20\nN10 G0 X20.\nN20 G1 Z-10. F0.2\nG70 P10 Q20\nM30\n%\n", Lathe());

        Assert.Equal(
            Lines(
                "RAPID X=40 Z=2",
                "CYCLE=ROUGH_TURN CONTOUR=CONTOUR_10_20",
                "CYCLE_CALL",
                "CYCLE=FINISH CONTOUR=CONTOUR_10_20",
                "CYCLE_CALL"),
            BodyOf(text));
        Assert.Single(text.Split('\n'), line => line == "SUB=BEGIN NAME=CONTOUR_10_20");
    }

    // A contour that does not follow its cycle is no range the documents turn into a SUB section (language 4.7.1, "the
    // contour that follows"); the cycle and its contour stay RAW, as the control reads them.
    [Fact]
    public void G70_WithAContourThatDoesNotFollowIt_IsKeptAsRawWithItsContour()
    {
        Assert.Equal(
            Lines(
                "RAW:FANUC=\"G70 P10 Q20\"",
                "RAPID X=50",
                "RAW:FANUC=\"N10 G0 X20.\"",
                "RAW:FANUC=\"N20 G1 Z-10.\""),
            Body("G70 P10 Q20\nG0 X50.\nN10 G0 X20.\nN20 G1 Z-10.", Lathe()));
    }

    // The other words of G70 to G76, depth of cut, allowances and feed, have no NCX word (wave-1 question #18): a G71
    // with U, W and F stays RAW with the blocks of its contour, which the RAW cycle reads as the control does (D5).
    [Fact]
    public void G71_WithWordsTheCatalogDoesNotMap_IsKeptAsRawWithItsContour()
    {
        Assert.Equal(
            Lines(
                "RAW:FANUC=\"G71 P10 Q20 U0.5 W0.1 F0.2\"",
                "RAW:FANUC=\"N10 G0 X20.\"",
                "RAW:FANUC=\"N20 G1 Z-10.\"",
                "RAPID X=100"),
            Body("G71 P10 Q20 U0.5 W0.1 F0.2\nN10 G0 X20.\nN20 G1 Z-10.\nG0 X100.", Lathe()));
    }

    // G76 of a mill has no cycle in the catalog: its blocks stay RAW until G80.
    [Fact]
    public void MillG76_WithoutACatalogEntry_KeepsItsBlocksRaw()
    {
        Assert.Equal(
            Lines(
                "RAPID X=0 Y=0 Z=10",
                "RAW:FANUC=\"G76 G99 Z-5. R2. Q0.5 F100\"",
                "RAW:FANUC=\"X10.\"",
                "CYCLE=OFF"),
            Body("G0 X0 Y0 Z10.\nG76 G99 Z-5. R2. Q0.5 F100\nX10.\nG80"));
    }
}
