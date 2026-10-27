using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Fanuc.FanucRead;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The frames of the Fanuc column of controller-mapping 1 (controllers fanuc.md 4, 9 rules 3 and 6; language 4.2):
/// SHIFT, FRAME=MACHINE, HOME, SETPOS, RPM_MAX, ROTATE, MIRROR, TILT, TILT_AXIS, TCPM, POLAR, CYLINDER, TOLERANCE.
/// </summary>
public sealed class FanucFrameTests
{
    // G52 replaces the previous G52, so the reader writes SHIFT=RESET before the new SHIFT; G52 X0 Y0 cancels
    // (controllers fanuc.md 9 rule 6; controller-mapping 1, SHIFT).
    [Fact]
    public void G52_ReplacesEarlierShift_EmitsReset()
    {
        Assert.Equal(
            Lines("SHIFT X=10 Y=5", "SHIFT=RESET", "SHIFT X=20", "SHIFT=RESET"),
            Body("G52 X10. Y5.\nG52 X20.\nG52 X0 Y0"));
    }

    // In a G52 block U and W are the lathe spelling of the absolute local origin, not an increment, so the second G52
    // states the new origin and does not move the old one (controllers fanuc.md 9 rule 6; D117).
    [Fact]
    public void Shift_G52UWOnSystemA_WritesTheAbsoluteLocalOrigin()
    {
        Assert.Equal(
            Lines("SHIFT X=2.75 Z=1.25", "SHIFT=RESET", "SHIFT X=1 Z=1"),
            Body("G52 U2.75 W1.25\nG52 U1. W1.", Lathe()));
    }

    // D125: on a lathe with X in diameters the G52 X is a diameter, and so is SHIFT X under DIAMETER=ON: the reader
    // writes it as given, neither halved nor doubled, and the program checks.
    [Fact]
    public void Shift_G52XOnADiameterLathe_IsTheDiameterAsWritten()
    {
        string text = Text("%\nO0001\nG0 X40. Z5.\nG52 X10. Z0\nG0 X20.\nG52 U0 W0\nM30\n%\n", Lathe());

        Assert.Equal(Lines("RAPID X=40 Z=5", "SHIFT X=10 Z=0", "RAPID X=20", "SHIFT=RESET"), BodyOf(text));
        Assert.False(Check(text, Lathe()).HasErrors, Check(text, Lathe()).ToText());
    }

    // G53 is FRAME=MACHINE on its block (controllers fanuc.md 9 rule 3).
    [Fact]
    public void G53_IsFrameMachineOnItsBlock()
    {
        Assert.Equal(Lines("RAPID Z=0 FRAME=MACHINE"), Body("G53 G0 Z0"));
    }

    // D242: a G53 block moves at rapid traverse whatever code of group 01 is active, and leaves that code as it was.
    [Fact]
    public void G53_UnderAnActiveG1_IsRapidOnItsBlock()
    {
        Assert.Equal(Lines("LINE X=10 F=200", "RAPID Z=0 FRAME=MACHINE", "LINE X=20"),
            Body("G90 G1 X10. F200\nG53 Z0\nX20."));
    }

    // G91 G28 Z0 is HOME Z; G28 X0 Y0 under G91 HOME X Y (controllers fanuc.md 9 rule 3; examples/2.5D_FRAESEN note 4).
    [Fact]
    public void G91G28Z0_BecomesHomeZ()
    {
        Assert.Equal(Lines("HOME Z", "HOME X Y"), Body("G91 G28 Z0\nG28 X0 Y0"));
    }

    // G30 P2 is HOME with POINT=2 (controllers fanuc.md 9 rule 3).
    [Fact]
    public void G30P2_BecomesHomeWithPoint2()
    {
        Assert.Equal(Lines("HOME Z POINT=2"), Body("G91 G30 P2 Z0"));
    }

    // G28 with a real distance moves to the intermediate point first (controllers fanuc.md 4).
    [Fact]
    public void G28_WithADistance_MovesToTheIntermediatePointFirst()
    {
        Assert.Equal(Lines("RAPID IZ=10", "HOME Z"), Body("G91 G28 Z10."));
    }

    // G28 U0 W0 of system A is HOME X Z; G28 H0 HOME C (controller-mapping 1, HOME).
    [Fact]
    public void G28U0W0_OnSystemA_BecomesHomeXZ()
    {
        Assert.Equal(Lines("HOME X Z", "HOME C"), Body("G28 U0 W0\nG28 H0", Lathe()));
    }

    // D243: an absolute word of G28, a 0 included, is the intermediate point and becomes a RAPID to it in front of
    // the HOME; only an incremental 0 means no intermediate move.
    [Fact]
    public void G28_WithAnAbsoluteZero_MovesToTheIntermediatePointFirst()
    {
        Assert.Equal(Lines("RAPID Z=50", "RAPID Z=0", "HOME Z"), Body("G90 G0 Z50.\nG28 Z0"));
    }

    // D243: B of G-code system A has no incremental letter, so G28 B0 is the absolute intermediate point B0 and the
    // reader writes a RAPID to it in front of the HOME (the Nakamura path 2 blocks).
    [Fact]
    public void G28B0_OnSystemA_MovesBToZeroFirst()
    {
        Assert.Equal(Lines("RAPID B=0", "HOME X B"), Body("G28 U0 B0", Lathe()));
    }

    // G92 X Y declares the current position on a mill, SETPOS (controller-mapping 1, D55).
    [Fact]
    public void G92_OnAMill_IsSetpos()
    {
        Assert.Equal(Lines("SETPOS X=0 Y=0"), Body("G92 X0 Y0"));
    }

    // G50 of system A is SETPOS with axis words and RPM_MAX with S (controller-mapping 1 SETPOS, 4 RPM_MAX).
    [Fact]
    public void G50_OnSystemA_IsTheSpeedLimitOrSetpos()
    {
        Assert.Equal(Lines("RPM_MAX=2500", "SETPOS C=0"), Body("G50 S2500\nG50 C0.", Lathe()));
    }

    // SETPOS carries absolute words only, so the reader folds the G50 U(u) W(w) of system A, under which the current
    // tool position becomes (x+u, z+w), into the coordinate it declares (D117; language 4.2).
    [Fact]
    public void Setpos_G50UWOnSystemA_DeclaresTheCurrentPositionPlusTheValue()
    {
        Assert.Equal(
            Lines("RAPID X=10 Z=10", "SETPOS X=12.75 Z=11.25"),
            Body("G0 X10. Z10.\nG50 U2.75 W1.25", Lathe()));
    }

    // The same fold for a G92 under G91 on a mill (D117).
    [Fact]
    public void Setpos_G91G92OnAMill_DeclaresTheCurrentPositionPlusTheValue()
    {
        Assert.Equal(
            Lines("RAPID X=10 Y=10 Z=10", "SETPOS X=15 Y=15"),
            Body("G90 G0 X10. Y10. Z10.\nG91 G92 X5. Y5."));
    }

    // Where the position the increment is measured from is not known, the block stays RAW (D117, D5).
    [Fact]
    public void Setpos_G50UFromAnUnknownPosition_IsKeptAsRaw()
    {
        Assert.Equal(Lines("RAW:FANUC=\"G50 U2.75\""), Body("G50 U2.75", Lathe()));
    }

    // The NCX the fold writes is a program check runs: an incremental word under SETPOS would be the ERROR PAR012
    // (D117; language 4.3, architecture 3).
    [Fact]
    public void Setpos_FoldedFromAnIncrement_PassesCheck()
    {
        string text = Text("%\nO0001\nG0 X10. Z10.\nG50 U2.75 W1.25\nG0 X0 Z0\nM30\n%", Lathe());
        Assert.False(Check(text, Lathe()).HasErrors, Check(text, Lathe()).ToText());
    }

    // G68 R rotates the plane, G69 cancels (controller-mapping 1, ROTATE). D245 (a): without X Y it turns about the
    // tool position, here the origin of the plane.
    [Fact]
    public void G68R30_IsRotate_AndG69Resets()
    {
        Assert.Equal(Lines("RAPID X=0 Y=0", "ROTATE=30", "ROTATE=RESET"), Body("G0 X0 Y0\nG68 R30.\nG69"));
        Assert.Equal(Lines("ROTATE=30", "ROTATE=RESET"), Body("G68 X0 Y0 R30.\nG69"));
    }

    // D245 (a): G68 without X Y turns about the tool position; where the reader does not know it as the origin of the
    // plane, or without R (parameter 5410), the block stays RAW, the control rotates, and its G69 stays RAW too.
    [Fact]
    public void G68_AboutAnUnknownToolPositionOrWithoutR_IsKeptAsRawWithItsG69()
    {
        Assert.Equal(Lines("RAW:FANUC=\"G68 R30.\"", "RAW:FANUC=\"G69\""), Body("G68 R30.\nG69"));
        Assert.Equal(Lines("RAPID X=10 Y=0", "RAW:FANUC=\"G68 R30.\"", "RAW:FANUC=\"G69\""),
            Body("G0 X10. Y0\nG68 R30.\nG69"));
        Assert.Equal(Lines("RAW:FANUC=\"G68 X0 Y0\"", "RAW:FANUC=\"G69\""), Body("G68 X0 Y0\nG69"));
    }

    // D245 (a): the centre of G18 is Z X (the G18 of the first block stands in the header, D34).
    [Fact]
    public void G68_InG18_TakesTheCentreOfTheZXPlane()
    {
        Assert.Equal(Lines("RAW:FANUC=\"G68 X0 Y0 R30.\""), Body("G18\nG68 X0 Y0 R30."));
        Assert.Equal(Lines("ROTATE=30"), Body("G18\nG68 X0 Z0 R30."));
    }

    // ROTATE turns about the origin; G68 about another point stays RAW (language 4.2).
    [Fact]
    public void G68_AboutAnotherPoint_IsKeptAsRaw()
    {
        Assert.Equal(Lines("RAW:FANUC=\"G68 X10. Y0 R30.\""), Body("G68 X10. Y0 R30."));
    }

    // G51.1 X0 mirrors X, G50.1 cancels (controller-mapping 1, MIRROR).
    [Fact]
    public void G511X0_IsMirrorX_AndG501Cancels()
    {
        Assert.Equal(Lines("MIRROR=X", "MIRROR=OFF"), Body("G51.1 X0\nG50.1 X0"));
    }

    // G68.2 with Euler angles is TILT with spatial angles, G53.1 in the next block MOVE=TURN (controller-mapping 1,
    // TILT and MOVE; D82).
    [Fact]
    public void G682_FollowedByG531_IsTiltWithMoveTurn()
    {
        Assert.Equal(Lines("TILT A=45 B=0 C=0 MOVE=TURN"), Body("G68.2 X0 Y0 Z0 I0 J45. K0\nG53.1"));
    }

    // The origin of G68.2 is a SHIFT before the TILT, which G69 resets with it; the Euler angles I90 J45 K-90 turn
    // about Y by 45 degrees (language 4.2, D31).
    [Fact]
    public void G682_WithAnOrigin_IsShiftThenTilt()
    {
        Assert.Equal(
            Lines("SHIFT X=10", "TILT A=0 B=45 C=0", "SHIFT=RESET"),
            Body("G68.2 X10. Y0 Z0 I90. J45. K-90.\nG69"));
    }

    // G69 cancels the tilted plane and keeps the G52 before it (controllers fanuc.md 4). The origin of the G68.2 and
    // the G52 shift stand in one SHIFT, so that the chain holds one shift and SHIFT=RESET means the same under both
    // readings of language 4.2 (wave-1 question #95); after the G69 the chain holds the G52 shift again, which the
    // next G52 resets.
    [Fact]
    public void G69_AfterAG682WithAnOriginOverAG52_KeepsTheG52Shift()
    {
        Assert.Equal(
            Lines(
                "SHIFT X=5",
                "SHIFT=RESET",
                "SHIFT X=15",
                "TILT A=45 B=0 C=0 MOVE=TURN",
                "SHIFT=RESET",
                "SHIFT X=5",
                "RAPID X=0 Y=0",
                "SHIFT=RESET"),
            Body("G52 X5.\nG68.2 X10. Y0 Z0 I0 J45. K0\nG53.1\nG69\nG0 X0 Y0\nG52 X0"));
    }

    // D244 (c): NCX keeps a G52 under the tilted plane of G68.2 native, since whether the control allows it depends on
    // its edition; the tilted plane with its origin stays in the chain until the G69 (D5).
    [Fact]
    public void G52_WhileAG682IsActive_IsKeptAsRaw()
    {
        Assert.Equal(
            Lines(
                "SHIFT X=10",
                "TILT A=45 B=0 C=0",
                "RAW:FANUC=\"G52 X5.\"",
                "RAW:FANUC=\"G52 X20.\"",
                "RAPID X=0 Y=0",
                "SHIFT=RESET"),
            Body("G68.2 X10. Y0 Z0 I0 J45. K0\nG52 X5.\nG52 X20.\nG0 X0 Y0\nG69"));
    }

    // D244 (c): a G52 may not stand while a G68 rotates, PS0412 on the 0i-D: it stays RAW and the rotation stays in
    // the chain.
    [Fact]
    public void G52_ReplacedWhileAG68Rotates_IsKeptAsRawAndTheRotationStays()
    {
        Assert.Equal(
            Lines("SHIFT X=5", "ROTATE=30", "RAW:FANUC=\"G52 X20.\"", "ROTATE=RESET", "SHIFT=RESET"),
            Body("G52 X5.\nG68 X0 Y0 R30.\nG52 X20.\nG69\nG52 X0"));
    }

    // D245 (b): a second G68.2 replaces the first, TILT=RESET (SHIFT=RESET where the old one had an origin) and the
    // new plane; one G69 resets it.
    [Fact]
    public void G682_WhileOneIsActive_ReplacesIt()
    {
        Assert.Equal(
            Lines("TILT A=45 B=0 C=0", "TILT=RESET", "TILT A=30 B=0 C=0", "TILT=RESET"),
            Body("G68.2 X0 Y0 Z0 I0 J45. K0\nG68.2 X0 Y0 Z0 I0 J30. K0\nG69"));
        Assert.Equal(
            Lines("SHIFT X=10", "TILT A=45 B=0 C=0", "SHIFT=RESET", "SHIFT X=20", "TILT A=30 B=0 C=0", "SHIFT=RESET"),
            Body("G68.2 X10. Y0 Z0 I0 J45. K0\nG68.2 X20. Y0 Z0 I0 J30. K0\nG69"));
    }

    // D245 (b): a second 2D G68 under absolute R replaces the first, ROTATE=RESET plus ROTATE; under G91 parameter
    // 5400#0 decides whether R adds, so it stays RAW after the ROTATE=RESET of the rotation it replaces, and so does
    // the G69 of the rotation the control then holds.
    [Fact]
    public void G68_WhileOneIsActive_ReplacesItUnderG90AndStaysRawUnderG91()
    {
        Assert.Equal(
            Lines("ROTATE=30", "ROTATE=RESET", "ROTATE=45", "ROTATE=RESET"),
            Body("G90 G68 X0 Y0 R30.\nG68 X0 Y0 R45.\nG69"));
        Assert.Equal(
            Lines("ROTATE=30", "ROTATE=RESET", "RAW:FANUC=\"G91 G68 X0 Y0 R45.\"", "RAW:FANUC=\"G69\""),
            Body("G90 G68 X0 Y0 R30.\nG91 G68 X0 Y0 R45.\nG69"));
    }

    // D245: the cross forms, a G68 under G68.2 and a G68.2 under G68, raise PS5462 on the control and stay RAW; the
    // G69 resets what the chain holds.
    [Fact]
    public void G682AndG68_UnderEachOther_AreKeptAsRaw()
    {
        Assert.Equal(
            Lines("TILT A=45 B=0 C=0", "RAW:FANUC=\"G68 X0 Y0 R30.\"", "TILT=RESET"),
            Body("G68.2 X0 Y0 Z0 I0 J45. K0\nG68 X0 Y0 R30.\nG69"));
        Assert.Equal(
            Lines("ROTATE=30", "RAW:FANUC=\"G68.2 X0 Y0 Z0 I0 J30. K0\"", "ROTATE=RESET"),
            Body("G68 X0 Y0 R30.\nG68.2 X0 Y0 Z0 I0 J30. K0\nG69"));
        NcxProgram program = FramedProgram("G68.2 X0 Y0 Z0 I0 J45. K0\nG68 X0 Y0 R30.");
        Assert.Contains(program.Diagnostics.Items, diagnostic => diagnostic.Message.Contains("PS5462",
            StringComparison.Ordinal));
    }

    // D245: the 3D conversion G68 with I J K stays RAW; a second one accumulates on the control, a third raises PS5043,
    // and the one G69 that ends them stays RAW.
    [Fact]
    public void G68WithIJK_IsKeptAsRawAndAThirdNamesPs5043()
    {
        NcxProgram program = FramedProgram(
            "G68 X0 Y0 Z0 I0 J0 K1 R30.\nG68 X0 Y0 Z0 I1 J0 K0 R10.\nG68 X0 Y0 Z0 I0 J1 K0 R5.\nG69\nG69");
        Assert.Equal(
            Lines(
                "RAW:FANUC=\"G68 X0 Y0 Z0 I0 J0 K1 R30.\"",
                "RAW:FANUC=\"G68 X0 Y0 Z0 I1 J0 K0 R10.\"",
                "RAW:FANUC=\"G68 X0 Y0 Z0 I0 J1 K0 R5.\"",
                "RAW:FANUC=\"G69\""),
            BodyOf(NcxWriter.Write(program)));
        Assert.Single(program.Diagnostics.Items, diagnostic => diagnostic.Message.Contains("PS5043",
            StringComparison.Ordinal));
    }

    // D245: a G68, G68.2 or G51.1 kept RAW for another reason still switches its mode on the control, so its G69 or
    // G50.1 stays RAW; G68.3 and G68.4 tilt the plane too, so a G54 under them stays RAW (D244 (c)).
    [Fact]
    public void ModesKeptAsRaw_KeepTheirCancelRaw()
    {
        Assert.Equal(
            Lines("RAW:FANUC=\"G62 G68 X0 Y0 R30.\"", "RAW:FANUC=\"G69\"", "RAW:FANUC=\"G62 G51.1 X0\"",
                "RAW:FANUC=\"G50.1 X0\""),
            Body("G62 G68 X0 Y0 R30.\nG69\nG62 G51.1 X0\nG50.1 X0"));
        Assert.Equal(
            Lines("RAW:FANUC=\"G68.4 X0 Y0 Z0 I0 J30. K0\"", "RAW:FANUC=\"G54\"", "RAW:FANUC=\"G69\"", "ORIGIN=1"),
            Body("G68.4 X0 Y0 Z0 I0 J30. K0\nG54\nG69\nG54"));
    }

    // D245: a cancel kept as RAW ends the entries the chain holds beside it, MIRROR=OFF and ROTATE=RESET, so the chain
    // stays known.
    [Fact]
    public void CancelKeptAsRaw_EndsTheEntriesOfTheChainBesideIt()
    {
        Assert.Equal(
            Lines("MIRROR=X", "RAW:FANUC=\"G51.1 Y5.\"", "MIRROR=OFF", "RAW:FANUC=\"G50.1 X0 Y0\"", "SHIFT X=5"),
            Body("G51.1 X0\nG51.1 Y5.\nG50.1 X0 Y0\nG52 X5."));
        Assert.Equal(
            Lines("ROTATE=30", "ROTATE=RESET", "RAW:FANUC=\"G68 X10. Y10. R45.\"", "RAW:FANUC=\"G69\"", "SHIFT X=5"),
            Body("G68 X0 Y0 R30.\nG68 X10. Y10. R45.\nG69\nG52 X5."));
    }

    // D124: G50.1 cancels the mirror of the axes it names and keeps the others (B-64484EN p.367); the chain holds one
    // MIRROR with the set of the mirrored axes, which a change replaces with MIRROR=OFF and the new set.
    [Fact]
    public void G501_CancelsOnlyTheAxesItNames()
    {
        Assert.Equal(
            Lines("MIRROR=X,Y", "MIRROR=OFF", "MIRROR=Y", "MIRROR=OFF", "MIRROR=X", "MIRROR=OFF", "MIRROR=X,Y",
                "MIRROR=OFF", "MIRROR=Y"),
            Body("G51.1 X0 Y0\nG50.1 X0\nG50.1 Y0\nG51.1 X0\nG51.1 Y0\nG50.1 X0"));
    }

    // D124, D245: an axis a G51.1 kept as RAW mirrors stays native, so its G50.1 stays RAW, while the G50.1 of an axis
    // the chain holds writes MIRROR=OFF; G51.1 and G50.1 under a G68 raise an alarm (B-64484EN p.368) and stay RAW.
    [Fact]
    public void Mirror_OfANativeAxisAndUnderG68_StaysRaw()
    {
        Assert.Equal(
            Lines("RAW:FANUC=\"G51.1 Y5.\"", "MIRROR=X", "MIRROR=OFF", "RAW:FANUC=\"G50.1 Y0\""),
            Body("G51.1 Y5.\nG51.1 X0\nG50.1 X0\nG50.1 Y0"));
        Assert.Equal(
            Lines("ROTATE=30", "RAW:FANUC=\"G51.1 X0\"", "ROTATE=RESET"),
            Body("G68 X0 Y0 R30.\nG51.1 X0\nG69"));
    }

    // D225: at a label a jump enters with another chain, and at the head of a loop or the target of a jump back over
    // a range that changes the frames, the chain is unknown, so the G69 after it stays RAW.
    [Fact]
    public void Chain_AtJumpTargetsAndLoopHeads_IsForgottenWhereTheEdgesDisagree()
    {
        Assert.Equal(
            Lines("VAR:V1=1", "ROTATE=30", "JUMP=10 IF={$V1 == 1}", "ROTATE=RESET", "LABEL=10",
                "RAW:FANUC=\"N10 G69\""),
            Body("#1=1\nG68 X0 Y0 R30.\nIF [#1 EQ 1] GOTO 10\nG69\nN10 G69"));
        Assert.Contains("RAW:FANUC=\"G68 X0 Y0 R10.\"",
            Body("#1=0\nWHILE [#1 LT 3] DO1\nG68 X0 Y0 R10.\nG69\n#1=#1+1\nEND1"), StringComparison.Ordinal);
        Assert.Contains("RAW:FANUC=\"N30 G68 X0 Y0 R10.\"",
            Body("N30 G68 X0 Y0 R10.\nG69\n#5=#5+1\nIF [#5 LT 3] GOTO 30"), StringComparison.Ordinal);
        Assert.Equal(
            Lines("VAR:V1=1", "JUMP=10 IF={$V1 == 1}", "VAR:V2=2", "LABEL=10", "ROTATE=30"),
            Body("#1=1\nIF [#1 EQ 1] GOTO 10\n#2=2\nN10 G68 X0 Y0 R30."));
    }

    // D225: a program M99 loops back to its start begins the second pass with the chain the first leaves, so where it
    // ends with a rotation on, the chain at the start is unknown and its G69 stays RAW; one that cancels it keeps it.
    [Fact]
    public void Chain_AtTheStartOfAProgramM99Loops_IsForgottenWhereThePassEndsRotated()
    {
        string rotated = Text("%\nO0001\nG0 X0 Y0\nG69\nG68 X0 Y0 R30.\nG1 X6. F100\nM99\n%\n");
        string cancelled = Text("%\nO0001\nG0 X0 Y0\nG69\nG68 X0 Y0 R30.\nG1 X6. F100\nG69\nM99\n%\n");

        Assert.Contains("\nRAW:FANUC=\"G69\"\nRAW:FANUC=\"G68 X0 Y0 R30.\"\n", rotated, StringComparison.Ordinal);
        Assert.Contains("\nROTATE=30\n", cancelled, StringComparison.Ordinal);
        Assert.DoesNotContain("RAW", cancelled, StringComparison.Ordinal);
    }

    // D244 (a): G52 holds in all of G54..G59, so after the ORIGIN the reader writes the G52 shift again; G52 X0 then
    // cancels it.
    [Fact]
    public void G52_ThenG55_WritesTheShiftAgainAfterTheOrigin()
    {
        Assert.Equal(
            Lines("SHIFT X=5 Y=2", "ORIGIN=2", "SHIFT X=5 Y=2", "SHIFT=RESET"),
            Body("G52 X5. Y2.\nG55\nG52 X0 Y0"));
    }

    // D244 (c): G52 to G59 and G92 under the 2D G68 or G51.1 raise PS0412 on the 0i-D and stay RAW, and so do G28 and
    // G30 (d); under G68.2 G28 is HOME.
    [Fact]
    public void CoordinateCodes_UnderG68OrG511_AreKeptAsRawNamingThePs0412()
    {
        NcxProgram program = FramedProgram("G68 X0 Y0 R30.\nG55\nG92 X0\nG91 G28 Z0\nG69");
        Assert.Equal(
            Lines("ROTATE=30", "RAW:FANUC=\"G55\"", "RAW:FANUC=\"G92 X0\"", "RAW:FANUC=\"G91 G28 Z0\"",
                "ROTATE=RESET"),
            BodyOf(NcxWriter.Write(program)));
        Assert.Equal(3, program.Diagnostics.Items.Count(diagnostic => diagnostic.Message.Contains("PS0412",
            StringComparison.Ordinal)));
        Assert.Equal(Lines("MIRROR=X", "RAW:FANUC=\"G54\"", "MIRROR=OFF"), Body("G51.1 X0\nG54\nG50.1 X0"));
        Assert.Equal(Lines("TILT A=45 B=0 C=0", "RAW:FANUC=\"G54\"", "HOME Z", "TILT=RESET"),
            Body("G68.2 X0 Y0 Z0 I0 J45. K0\nG54\nG91 G28 Z0\nG69"));
    }

    // D244 (d): a G92 cancels the G52 shift of the axes it names: SHIFT=RESET, the SETPOS, then the G52 shift of the
    // other axes.
    [Fact]
    public void G92_UnderAG52_ResetsTheShiftOfItsAxes()
    {
        Assert.Equal(
            Lines("SHIFT X=5 Y=2", "SHIFT=RESET", "SETPOS X=0", "SHIFT Y=2", "SHIFT=RESET"),
            Body("G52 X5. Y2.\nG92 X0\nG52 Y0"));
    }

    // G68.1 stays RAW until its words are mapped to the axis angles of TILT_AXIS.
    [Fact]
    public void G681_IsKeptAsRaw()
    {
        Assert.Equal(Lines("RAW:FANUC=\"G68.1 X0 Y0 Z0 I0 J0 K1 R30.\""), Body("G68.1 X0 Y0 Z0 I0 J0 K1 R30."));
    }

    // G43.4 H1 is TCPM=ON with its length offset, G49 ends both (controller-mapping 1, TCPM; 3, OFFSET).
    [Fact]
    public void G434H1_IsTcpmOnWithTheLengthOffset_AndG49EndsIt()
    {
        Assert.Equal(Lines("OFFSET:LEN=1 TCPM=ON", "OFFSET:LEN=0 TCPM=OFF"), Body("G43.4 H1\nG49"));
    }

    // Under G43.5 the I J K of a line are the tool vector TX TY TZ (controller-mapping 2, D81).
    [Fact]
    public void G435_LineWithIJK_IsLineWithTheToolVector()
    {
        Assert.Equal(
            Lines("OFFSET:LEN=1 TCPM=ON", "LINE X=10 TX=0 TY=0.5 TZ=0.866 F=100"),
            Body("G43.5 H1\nG1 X10. I0 J0.5 K0.866 F100"));
    }

    // G12.1 and G13.1 switch POLAR, and the builder's G112 and G113 of [transform] too (controller-mapping 1).
    [Fact]
    public void G121AndG112_ArePolarOnAndOff()
    {
        Assert.Equal(Lines("POLAR=ON", "POLAR=OFF"), Body("G12.1\nG13.1"));
        Assert.Equal(Lines("POLAR=ON", "POLAR=OFF"), Body("G112\nG113", Lathe()));
    }

    // G7.1 C30. is CYLINDER=30, C0 off; the builder's G107 of [transform] too (controller-mapping 1, D96).
    [Fact]
    public void G71AndG107_AreCylinderWithTheRadius()
    {
        Assert.Equal(Lines("CYLINDER=30", "CYLINDER=OFF"), Body("G7.1 C30.\nG7.1 C0"));
        Assert.Equal(Lines("CYLINDER=25", "CYLINDER=OFF"), Body("G107 C25.\nG107 C0", Lathe()));
    }

    // G5.1 Q0 is TOLERANCE=OFF; G5.1 Q1 with the tolerance in a parameter stays RAW (controller-mapping 1).
    [Fact]
    public void G51Q0_IsToleranceOff_AndQ1StaysRaw()
    {
        Assert.Equal(Lines("TOLERANCE=OFF", "RAW:FANUC=\"G5.1 Q1\""), Body("G5.1 Q0\nG5.1 Q1"));
    }
}
