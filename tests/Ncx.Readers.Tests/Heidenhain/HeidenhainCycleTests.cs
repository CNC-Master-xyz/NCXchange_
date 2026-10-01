using Ncx.Core.Model;
using static Ncx.Readers.Tests.Heidenhain.HeidenhainRead;

namespace Ncx.Readers.Tests.Heidenhain;

/// <summary>
/// The machining cycles (controllers heidenhain.md 5, 7 rule 7; controller-mapping 5; language 4.7, 4.7.1): CYCL DEF
/// into CYCLE with absolute coordinates, CYCL CALL, M99 and CYCL CALL PAT into CYCLE_CALL, CYCLE=OFF before the next
/// non-cycle motion, the native form, RAW.
/// </summary>
public sealed class HeidenhainCycleTests
{
    private const string Drill = "2 CYCL DEF 200 BOHREN ~\n    Q200=2 ~\n    Q201=-10 ~\n    Q206=100 ~\n    Q203=0 ~\n"
        + "    Q204=2";

    // Q204 equals Q200 here, so the second clearance does not act and the retract is CLEARANCE (D226).
    private const string DrillWords =
        "CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-10 CYCLE_RETRACT=CLEARANCE CYCLE_F=100";

    // The definition becomes CYCLE with SURFACE = Q203, CLEARANCE = Q203 + Q200, DEPTH = Q203 + Q201, SAFE = Q203 +
    // Q204 (heidenhain 5, 7 rule 7); the Q parameters no word carries are reported.
    [Fact]
    public void CyclDef200_BecomesDrillWithCoordinatesAbsoluteFromQ203()
    {
        NcxProgram program = FramedProgram("1 CYCL DEF 200 BOHREN ~\n    Q200=+2 ~\n    Q201=-20 ~\n    Q206=+150 ~\n"
            + "    Q202=+5 ~\n    Q210=+0 ~\n    Q203=-10 ~\n    Q204=+50 ~\n    Q211=+0\n2 CYCL CALL");

        Assert.Equal(Lines(
            "CYCLE=DRILL SURFACE=-10 CLEARANCE=-8 DEPTH=-30 SAFE=40 CYCLE_RETRACT=SAFE CYCLE_F=150 CYCLE_DWELL=0",
            "CYCLE_CALL"), BodyOf(Ncx.Core.Writing.NcxWriter.Write(program)));
        Assert.Equal([DiagnosticCodes.HeidenhainCycleParameterNotCarried], Codes(program));
    }

    // D163: cycle 200 with Q211 = 0 is DRILL, and with any other Q211 it is DRILL_DWELL with CYCLE_DWELL = Q211
    // (controllers heidenhain.md 7 rule 7; controller-mapping 5).
    [Fact]
    public void CyclDef200_WithADwellAtTheBottom_IsDrillDwellWithTheDwell()
    {
        Assert.Equal(Lines("CYCLE=DRILL_DWELL SURFACE=0 CLEARANCE=2 DEPTH=-10 CYCLE_RETRACT=CLEARANCE CYCLE_F=100 "
            + "CYCLE_DWELL=0.5"),
            Body("1 CYCL DEF 200 BOHREN ~\n    Q200=2 ~\n    Q201=-10 ~\n    Q206=100 ~\n    Q203=0 ~\n"
                + "    Q204=2 ~\n    Q211=0,5"));
    }

    // D163: cycle 203 with Q213 = 0 retracts fully after every infeed, which is PECK, and with a Q213 of at least the
    // number of infeeds, ceil(|Q201| / Q202), it never retracts, which is CHIP_BREAK.
    [Fact]
    public void CyclDef203_WithQ213Zero_IsPeck()
    {
        Assert.Equal(Lines("CYCLE=PECK SURFACE=0 CLEARANCE=2 DEPTH=-20 CYCLE_RETRACT=CLEARANCE PECK=5 CYCLE_F=100 "
            + "CYCLE_DWELL=0"),
            Body(Universal(breaks: 0)));
    }

    [Fact]
    public void CyclDef203_WithQ213AtLeastTheNumberOfInfeeds_IsChipBreak()
    {
        Assert.Equal(Lines("CYCLE=CHIP_BREAK SURFACE=0 CLEARANCE=2 DEPTH=-20 CYCLE_RETRACT=CLEARANCE PECK=5 "
            + "CYCLE_F=100 CYCLE_DWELL=0"),
            Body(Universal(breaks: 4)));
    }

    // D163: a Q213 between 0 and the number of infeeds is the mixed form, which has no NCX cycle and is written
    // natively (language 4.7.1, D94).
    [Fact]
    public void CyclDef203_WithQ213BetweenZeroAndTheInfeeds_IsWrittenNatively()
    {
        Assert.StartsWith("CYCLE:HEIDENHAIN=203", Body(Universal(breaks: 3)), StringComparison.Ordinal);
    }

    // D163 sends the mixed form to CYCLE:HEIDENHAIN=203, which the test above pins. A native parameter takes a number
    // or an expression (language 4.7.1, D94), so a Q parameter that carries a literal of the control instead has no
    // NCX value and the definition is kept as RAW with its calls (heidenhain 7 rule 9): that is the Q208=MAX of the
    // fourth operation of BOHREN.h, the one such block the repository owns. Whether a parameter may carry such a
    // literal at all is the open D115, and D164 writes Q208=MAX back (03-open-questions, wave 2 question 94).
    [Fact]
    public void MixedCycle203_WithAQParameterThatIsAControlLiteral_IsKeptAsRaw()
    {
        string body = Body(Universal(breaks: 3, retract: "MAX"));

        Assert.StartsWith("RAW:HEIDENHAIN=\"1 CYCL DEF 203", body, StringComparison.Ordinal);
        Assert.DoesNotContain("CYCLE:HEIDENHAIN=203", body, StringComparison.Ordinal);
    }

    // A cycle 203 of four infeeds, Q201 = -20 over Q202 = 5, with the given number of chip breaks and retract feed.
    private static string Universal(int breaks, string retract = "100")
    {
        return "1 CYCL DEF 203 UNIVERSAL-BOHREN ~\n    Q200=2 ~\n    Q201=-20 ~\n    Q206=100 ~\n    Q202=5 ~\n"
            + "    Q210=0 ~\n    Q203=0 ~\n    Q204=2 ~\n    Q212=0 ~\n    Q213=" + breaks + " ~\n    Q205=5 ~\n"
            + "    Q211=0 ~\n    Q208=" + retract + " ~\n    Q256=0,6";
    }

    // Without Q204 the cycle returns to CLEARANCE (controller-mapping 5, CYCLE_RETRACT); cycle 207 is TAP with Q239 as
    // PITCH.
    [Fact]
    public void CyclDef207_WithoutQ204_IsTapBackToClearance()
    {
        Assert.Equal(Lines("CYCLE=TAP SURFACE=0 CLEARANCE=2 DEPTH=-20 CYCLE_RETRACT=CLEARANCE PITCH=1.5"),
            Body("1 CYCL DEF 207 GEW.-BOHREN GS NEU ~\n    Q200=2 ~\n    Q201=-20 ~\n    Q239=1,5 ~\n    Q203=0"));
    }

    // D226: the second set-up clearance acts only when it is programmed greater than the first, so a Q204 from 0 up
    // to Q200 reads as CYCLE_RETRACT=CLEARANCE without SAFE (controllers heidenhain.md 7 rule 7).
    [Fact]
    public void Q204_NotGreaterThanQ200_IsCycleRetractClearanceWithoutSafe()
    {
        Assert.Equal(Lines(DrillWords), Body(Drill));
    }

    [Fact]
    public void Q204_GreaterThanQ200_IsSafeWithCycleRetractSafe()
    {
        Assert.Equal(Lines("CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-10 SAFE=50 CYCLE_RETRACT=SAFE CYCLE_F=100"),
            Body("2 CYCL DEF 200 BOHREN ~\n    Q200=2 ~\n    Q201=-10 ~\n    Q206=100 ~\n    Q203=0 ~\n"
                + "    Q204=50"));
    }

    // M99 in a rapid positioning in the plane is the CYCLE_CALL at its position; the definition stays until the next
    // CYCL DEF, and CYCLE=OFF stands before the next non-cycle motion (heidenhain 5, 7 rule 7).
    [Fact]
    public void M99_InARapidPositioning_IsTheCallAndCycleOffStandsBeforeTheNextMotion()
    {
        Assert.Equal(Lines(
            "TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1 RPM=1000",
            DrillWords,
            "CYCLE_CALL X=10 Y=10 COMP=OFF",
            "CYCLE_CALL X=20",
            "CYCLE=OFF",
            "RAPID Z=50"),
            Body("1 TOOL CALL 1 Z S1000\n" + Drill + "\n3 L X+10 Y+10 R0 FMAX M99\n4 L X+20 FMAX M99\n5 L Z+50 FMAX"));
    }

    // M99 in a positioning at the feed or along the tool axis moves first and calls at the point it reached.
    [Fact]
    public void M99_InAFeedOrToolAxisPositioning_MovesThenCalls()
    {
        Assert.Equal(Lines(DrillWords, "LINE X=10 F=200", "CYCLE_CALL", "RAPID X=20 Z=5", "CYCLE_CALL"),
            Body(Drill + "\n3 L X+10 F200 M99\n4 L X+20 Z+5 FMAX M99"));
    }

    // The positioning between the definition and its first call does not switch the cycle off; a call after CYCLE=OFF
    // writes the definition again, which the control keeps until the next CYCL DEF (heidenhain 5).
    [Fact]
    public void Call_AfterTheCycleWasSwitchedOff_WritesTheDefinitionAgain()
    {
        Assert.Equal(Lines(DrillWords, "RAPID X=10 Y=10", "CYCLE_CALL", "CYCLE=OFF", "RAPID Z=50", DrillWords,
            "CYCLE_CALL X=30"),
            Body(Drill + "\n3 L X+10 Y+10 FMAX\n4 CYCL CALL\n5 L Z+50 FMAX\n6 L X+30 FMAX M99"));
    }

    // A TOOL CALL ends the cycle of NCX while the control keeps the definition, so CYCLE=OFF stands before the TOOL
    // block and no WARNING falls on the source (D227).
    [Fact]
    public void CycleOff_BeforeAToolCallWhileTheCycleIsOn_StandsInFrontOfTheToolBlock()
    {
        Assert.Equal(Lines(DrillWords, "CYCLE_CALL", "CYCLE=OFF", "TOOL=2 OFFSET:LEN=2 OFFSET:RAD=2 RPM=500"),
            Body(Drill + "\n3 CYCL CALL\n4 TOOL CALL 2 Z S500"));
    }

    // CYCL CALL PAT calls at every point of the PATTERN DEF, one CYCLE_CALL per point (heidenhain 7 rule 7).
    [Fact]
    public void CyclCallPat_ExpandsToOneCallPerPoint()
    {
        string text = Text("0 BEGIN PGM P MM\n1 PATTERN DEF ~\n    POS1 (X+25 Y+33,5 Z+0) ~\n    POS2 (X+50 Y+75 Z+0)\n"
            + Drill + "\n3 CYCL CALL PAT FMAX\n4 M30\n5 END PGM P MM\n");

        Assert.Equal(Lines(DrillWords, "CYCLE_CALL X=25 Y=33.5", "CYCLE_CALL X=50 Y=75"), BodyOf(text));
        AssertFormatsToItself(text);
    }

    // D254: a workpiece surface in Z other than 0 acts in addition to the Q203 of the cycle, so the CYCLE block is
    // written again before that point's call with SURFACE, CLEARANCE, DEPTH and SAFE shifted by it, and the shifted
    // cycle is switched off after the pattern. The Q204 of this definition equals its Q200, so the retract is
    // CLEARANCE and the block carries no SAFE to shift (D226).
    [Fact]
    public void PatternSurface_OtherThanZero_ShiftsTheCycleOfThatPoint()
    {
        Assert.Equal(Lines(
            DrillWords,
            "CYCLE_CALL X=25 Y=33.5",
            "CYCLE=DRILL SURFACE=-3 CLEARANCE=-1 DEPTH=-13 CYCLE_RETRACT=CLEARANCE CYCLE_F=100",
            "CYCLE_CALL X=50 Y=75",
            "CYCLE=OFF"),
            Body("1 PATTERN DEF ~\n    POS1 (X+25 Y+33,5 Z+0) ~\n    POS2 (X+50 Y+75 Z-3)\n" + Drill
                + "\n3 CYCL CALL PAT FMAX"));
    }

    // A PATTERN DEF no CYCL CALL PAT calls stays RAW (heidenhain 7 rule 9).
    [Fact]
    public void PatternDef_WithoutACall_IsKeptAsRaw()
    {
        Assert.Equal([DiagnosticCodes.KeptAsRaw], Codes(FramedProgram("1 PATTERN DEF ~\n    POS1 (X+25 Y+33 Z+0)")));
    }

    // A call without a definition is an ERROR of the source and stays RAW (heidenhain 5).
    [Fact]
    public void CyclCall_WithoutADefinition_IsAnErrorAndKeptAsRaw()
    {
        Assert.Equal([DiagnosticCodes.HeidenhainCycleCallWithoutDefinition, DiagnosticCodes.KeptAsRaw],
            Codes(FramedProgram("1 CYCL CALL")));
    }

    // A catalog cycle whose parameters are no words of the language is written natively, CYCLE:HEIDENHAIN=n with its Q
    // parameters in source order (language 4.7.1, D94).
    [Fact]
    public void CyclDef251_IsWrittenNatively()
    {
        string text = Text("0 BEGIN PGM N MM\n1 CYCL DEF 251 RECHTECKTASCHE ~\n    Q215=0 ~\n    Q218=60 ~\n"
            + "    Q219=40 ~\n    Q201=-10\n2 M30\n3 END PGM N MM\n");

        Assert.Equal(Lines("CYCLE:HEIDENHAIN=251 Q215=0 Q218=60 Q219=40 Q201=-10"), BodyOf(text));
        AssertFormatsToItself(text);
    }

    // A cycle the catalog does not know, an OEM cycle, stays RAW, and so do its calls (heidenhain 7 rule 9).
    [Fact]
    public void CyclDef_OfACycleTheCatalogDoesNotKnow_IsKeptAsRawWithItsCalls()
    {
        NcxProgram program = FramedProgram("1 CYCL DEF 361 STANDARD TFB ~\n    Q1=1\n2 CYCL CALL");

        Assert.Equal(3, program.Blocks.Count(block => block.Find("RAW") is not null));
        Assert.Equal([DiagnosticCodes.KeptAsRaw, DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // A CYCL DEF the reader keeps as RAW, CYCL DEF 12.0 PGM CALL over two blocks, replaces the active definition all
    // the same, since the definition stays active until the next CYCL DEF: the M99 after it stays RAW and does not call
    // the cycle before it (heidenhain 5, 7 rule 9).
    [Fact]
    public void CyclDef_KeptAsRawOverSeveralBlocks_ReplacesTheDefinitionAndItsCallsStayRaw()
    {
        NcxProgram program = FramedProgram(Drill + "\n3 L X+10 Y+10 Z+5 R0 FMAX M99\n4 CYCL DEF 12.0 PGM CALL\n"
            + "5 CYCL DEF 12.1 PGM TNC:\\SUB.H\n6 L X+20 FMAX M99\n7 L X+30 FMAX M99");

        Assert.Equal(1, program.Blocks.Count(block => block.Verb?.Key == "CYCLE_CALL"));
        Assert.Equal(4, program.Blocks.Count(block => block.Find("RAW") is not null));
        Assert.Equal(Enumerable.Repeat(DiagnosticCodes.KeptAsRaw, 4), Codes(program));
    }

    // An older cycle written over several blocks, CYCL DEF 1.0 TIEFBOHREN to 1.5, is the definition the CYCL CALL after
    // it calls: the call stays RAW with its definition, and the valid program has no ERROR (heidenhain 5, 7 rule 9).
    [Fact]
    public void CyclCall_OfAnOlderCycleOverSeveralBlocks_IsKeptAsRawWithoutAnError()
    {
        NcxProgram program = FramedProgram(
            "1 CYCL DEF 1.0 TIEFBOHREN\n2 CYCL DEF 1.1 ABST 2\n3 CYCL DEF 1.2 TIEFE -20\n"
            + "4 CYCL DEF 1.3 ZUSTLG 5\n5 CYCL DEF 1.4 V.ZEIT 0\n6 CYCL DEF 1.5 F100\n7 CYCL CALL");

        Assert.Equal(Enumerable.Repeat(DiagnosticCodes.KeptAsRaw, 7), Codes(program));
    }

    // A CYCL DEF whose cycle number the reader cannot read stays RAW and replaces the active definition all the same:
    // the M99 after it does not call the cycle before it (heidenhain 5).
    [Fact]
    public void CyclDef_WithoutACycleNumber_ReplacesTheDefinitionAndItsCallsStayRaw()
    {
        NcxProgram program = FramedProgram(Drill + "\n3 L X+10 Y+10 R0 FMAX M99\n4 CYCL DEF\n5 L X+20 FMAX M99");

        Assert.Equal(1, program.Blocks.Count(block => block.Verb?.Key == "CYCLE_CALL"));
        Assert.Equal([DiagnosticCodes.KeptAsRaw, DiagnosticCodes.KeptAsRaw], Codes(program));
    }

    // The drilling of BOHREN.h checks without an ERROR (virtual machine 3.3).
    [Fact]
    public void Drilling_ChecksWithoutError()
    {
        string text = Text("0 BEGIN PGM D MM\n1 TOOL CALL 1 Z S1000\n" + Drill + "\n3 M3\n4 L X+10 Y+10 R0 FMAX\n"
            + "5 L Z+5 FMAX M99\n6 L X+20 FMAX M99\n7 L Z+50 FMAX\n8 M30\n9 END PGM D MM\n");
        Diagnostics check = Check(text);

        Assert.False(check.HasErrors, check.ToText());
    }
}
