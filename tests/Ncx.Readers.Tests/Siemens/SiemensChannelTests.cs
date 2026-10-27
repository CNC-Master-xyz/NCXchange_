using static Ncx.Readers.Tests.Siemens.SiemensRead;

namespace Ncx.Readers.Tests.Siemens;

/// <summary>
/// The channels and the program groups (controllers siemens.md 9, 11 rule 7; controller-mapping 5, 7; language 4.14).
/// </summary>
public sealed class SiemensChannelTests
{
    // Rule 7: WAITM(mark, channels) is SYNC with WITH.
    [Fact]
    public void Waitm_IsSyncWithTheChannels()
    {
        Assert.Equal(Lines("SYNC=1 WITH=1,2"), CheckedBody("WAITM(1,1,2)"));
    }

    // Rule 7: INIT and START are START_CHANNEL with the NAME of the program INIT selects, WAITE is WAIT_CHANNEL.
    [Fact]
    public void InitStartAndWaite_AreTheChannelWords()
    {
        Assert.Equal(Lines("NAME=\"CHAN2\" START_CHANNEL=2", "WAIT_CHANNEL=2"),
            CheckedBody("INIT(2,\"CHAN2\")\nSTART(2)\nWAITE(2)"));
    }

    // D291: GROUP_BEGIN with a literal _MODE whose bit 1 is clear is a SECTION with its name; GROUP_END and
    // GROUP_ADDEND, which carry only a level and a spindle, become comment-only lines holding the source text.
    [Fact]
    public void GroupBegin_IsASection_GroupEndAndAddendAreComments()
    {
        Assert.Equal(Lines("SECTION=\"DRILL\"", "RAPID X=0", "; GROUP_ADDEND(0,0)", "; GROUP_END(0,0)"),
            CheckedBody("GROUP_BEGIN(0,\"DRILL\",0,1)\nG0 X0\nGROUP_ADDEND(0,0)\nGROUP_END(0,0)"));
    }

    // D291: with _MODE bit 1 set, ShopTurn's automatic retract to the tool-change point, or with a _MODE that is no
    // literal number, GROUP_BEGIN stays RAW with the RAW WARNING (NC-Programmierung SW4.92 p.1170, cnc-manuals).
    [Theory]
    [InlineData("GROUP_BEGIN(0,\"DRILL\",0,2)")]
    [InlineData("GROUP_BEGIN(0,\"DRILL\",0,3)")]
    [InlineData("GROUP_BEGIN(0,\"DRILL\",0,R1)")]
    [InlineData("GROUP_BEGIN(0,\"DRILL\",0)")]
    public void GroupBegin_WithRetractOrUnknownMode_StaysRaw(string source)
    {
        Assert.Equal(Lines(Raw(source)), Body(source));
    }
}
