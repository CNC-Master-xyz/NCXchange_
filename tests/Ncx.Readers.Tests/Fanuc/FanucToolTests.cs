using Ncx.Core.Machine;
using Ncx.Core.Model;
using static Ncx.Readers.Tests.Fanuc.FanucRead;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The Fanuc column of controller-mapping 3, tools, and of controller-mapping 4, spindles (controllers fanuc.md 5, 9
/// rule 2; language 4.4, 4.5): PRELOAD, TOOL, the turret form, the offsets, the builder's ATC, the S binding rule, CSS
/// and ORIENT.
/// </summary>
public sealed class FanucToolTests
{
    // T5 alone brings tool 5 to the change position, PRELOAD=5 (controller-mapping 3).
    [Fact]
    public void T5Alone_BecomesPreload()
    {
        Assert.Equal(Lines("PRELOAD=5"), Body("T5"));
    }

    // T4 M6 in one block changes to 4, TOOL=4 (controller-mapping 3).
    [Fact]
    public void T4M6_BecomesTool4()
    {
        Assert.Equal(Lines("TOOL=4"), Body("T4 M6"));
    }

    // M6 alone changes to the preloaded tool, TOOL=n from the source-side state (controller-mapping 3, D91).
    [Fact]
    public void M6Alone_TakesThePreloadedTool()
    {
        Assert.Equal(Lines("PRELOAD=5", "TOOL=5"), Body("T5\nM6"));
    }

    // M6 without any preload is an ERROR in the source (controller-mapping 3).
    [Fact]
    public void M6_WithoutPreload_IsAnError()
    {
        NcxProgram program = FramedProgram("M6");

        Assert.Equal([DiagnosticCodes.FanucChangeWithoutPreload], Codes(program));
        Assert.Equal(Lines("TOOL"), BodyOf(Core.Writing.NcxWriter.Write(program)));
    }

    // T5 after the M6 in the same block is illegal on most controls; the reader reports it (controller-mapping 3).
    [Fact]
    public void T4M6T5_IsAnErrorAndStaysRaw()
    {
        NcxProgram program = FramedProgram("T4 M6 T5");

        Assert.Equal([DiagnosticCodes.FanucToolAfterChange, DiagnosticCodes.KeptAsRaw], Codes(program));
        Assert.Equal(Lines("RAW:FANUC=\"T4 M6 T5\""), BodyOf(Core.Writing.NcxWriter.Write(program)));
    }

    // T0 M6 empties the spindle, TOOL=0 (controller-mapping 3, TOOL=0; language 4.4).
    [Fact]
    public void T0M6_BecomesTool0()
    {
        Assert.Equal(Lines("TOOL=0"), Body("T0 M6"));
    }

    // T0 clears the preload, PRELOAD=0 (language 4.4; 2.5D_FRAESEN N50).
    [Fact]
    public void T0Alone_ClearsThePreload()
    {
        Assert.Equal(Lines("PRELOAD=0"), Body("T0"));
    }

    // On a turret lathe T0656 is station 6 with offset 56, T0100 cancels the offset of station 1 (controller-mapping
    // 3).
    [Fact]
    public void LatheT0656_BecomesTool6Offset56()
    {
        Assert.Equal(Lines("TOOL=6 OFFSET=56", "TOOL=1 OFFSET=0"), Body("T0656\nG0 T0100", Lathe()));
    }

    // D219: the tool holder decides what a T word of a lathe means. [tool_change] describes the change of
    // default_holder, so a T word of that holder is read through its templates first: on the Mori Seiki NTX1000 the
    // tool spindle's T9001 matches the preload "T{tool:04}" and is PRELOAD=9001, a bare magazine tool number, not
    // the turret form TOOL=90 OFFSET=1 (decisions.md; controller-mapping 3; machine-config 3).
    [Fact]
    public void ToolWord_MatchingThePreloadTemplateOfTheDefaultHolder_IsThePreload()
    {
        Assert.Equal(Lines("PRELOAD=9001"),
            Body("T9001", LoadMachineFile("docs/spec/examples/machines/mori-ntx1000-mapps.toml")));
    }

    // D219: a T word of a holder that is not default_holder is the turret form of fanuc 5 and carries that holder's
    // role, because a TOOL without one reaches default_holder; the holder is the one whose channel is the channel of
    // the file, turret 2 of the Nakamura NTJX in the path-2 file O1000.P-2 (decisions.md; controller-mapping 3;
    // virtual machine 3.8 rules 1 and 2; language 4.4).
    [Fact]
    public void ToolWord_InAFileOfTheChannelOfAnotherHolder_IsTheTurretFormWithThatHoldersRole()
    {
        string text = TextOfFile("O1000.P-2", "%\nO1000\nG0 T0656\nM30\n%\n",
            LoadMachineFile("docs/spec/examples/machines/nakamura-ntjx.toml"));

        Assert.Contains("TOOL:TURRET2=6 OFFSET=56", text, StringComparison.Ordinal);
    }

    // D219: [tool_change] describes the change of default_holder, so a template that begins with a G code, the
    // "G0 T{tool:02}{offset:02}" of the WY-250L (4810004EEE p.67), reads the T words of that holder's path alone; in
    // the path-2 file the same line is the turret form with the role of turret 2, and in the path-1 file the change
    // of default_holder.
    [Fact]
    public void ToolWord_MatchingAGTemplateInTheFileOfAnotherHolder_IsTheTurretFormWithThatHoldersRole()
    {
        MachineConfig machine = LoadMachineFile("docs/spec/examples/machines/nakamura-wy250l.toml");

        Assert.Contains("TOOL:TURRET2=12 OFFSET=12", TextOfFile("O1000.P-2", "%\nO1000\nG0T1212\nM30\n%\n", machine),
            StringComparison.Ordinal);
        Assert.Contains("TOOL=1 OFFSET=1", TextOfFile("O1000", "%\nO1000\nG0T0101\nM30\n%\n", machine),
            StringComparison.Ordinal);
    }

    // The builder's change macro of [tool_change], G340 T0101. A02., is the change with its offset and the preload of
    // the next tool; G341 T02. the preload alone (controller-mapping 3, Nakamura).
    [Fact]
    public void G340_OfTheBuilder_IsToolOffsetAndPreload()
    {
        Assert.Equal(Lines("PRELOAD=2 TOOL=1 OFFSET=1", "PRELOAD=3"), Body("G340 T0101. A02.\nG341 T03.", Lathe()));
    }

    // G43 H is OFFSET:LEN where it stands, G49 cancels (controller-mapping 3; D7).
    [Fact]
    public void G43H1_IsTheLengthOffset_AndG49Cancels()
    {
        Assert.Equal(Lines("RAPID Z=5 OFFSET:LEN=1", "OFFSET:LEN=0"), Body("G43 Z5. H1\nG49"));
    }

    // G44, the length offset subtracted, has no NCX word and stays RAW.
    [Fact]
    public void G44_IsKeptAsRaw()
    {
        Assert.Equal(Lines("RAW:FANUC=\"G44 Z5. H1\""), Body("G44 Z5. H1"));
    }

    // M3 S in one block starts the spindle with its speed (controller-mapping 4, the S binding rule).
    [Fact]
    public void S1592M3_IsSpindleCwWithRpm()
    {
        Assert.Equal(Lines("SPINDLE=CW RPM=1592"), Body("S1592 M3"));
    }

    // S belongs to the spindle whose M code stands in the same block, otherwise to the spindle selected last
    // (controller-mapping 4).
    [Fact]
    public void S_BelongsToTheSpindleOfItsBlockElseToTheLastOne()
    {
        Assert.Equal(
            Lines("SPINDLE:SUB=CW RPM:SUB=500", "RPM:SUB=800", "SPINDLE=CW RPM=900"),
            Body("M53 S500\nS800\nM3 S900", Lathe()));
    }

    // G96 S is CSS with the cutting speed VC, G97 S the speed again (controller-mapping 4, CSS and VC).
    [Fact]
    public void G96S120_IsCssWithTheCuttingSpeed_AndG97Ends()
    {
        Assert.Equal(Lines("SPINDLE=CW CSS=ON VC=120", "RPM=1500 CSS=OFF"), Body("G96 S120 M3\nG97 S1500", Lathe()));
    }

    // M19 is ORIENT, with the angle of S where it stands (controller-mapping 4, ORIENT).
    [Fact]
    public void M19_IsOrientWithTheAngleOfS()
    {
        Assert.Equal(Lines("ORIENT=90", "ORIENT=0"), Body("M19 S90.\nM19"));
    }
}
