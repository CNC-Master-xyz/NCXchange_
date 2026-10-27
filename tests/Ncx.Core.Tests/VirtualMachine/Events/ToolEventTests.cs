using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Tests.VirtualMachine.Events;

/// <summary>
/// TOOL_BEGIN and TOOL_END when a tool enters or leaves the spindle, with the distance and the block count under the
/// tool, and PRELOAD (virtual machine 3.5, 7; architecture 5.2).
/// </summary>
public sealed class ToolEventTests
{
    // D235: the figures of a TOOL_END or PROGRAM_END without a motion.
    private const string NoDistance = "feedDistance 0, rapidDistance 0, unknownLengthMotions 0, distance 0";

    // VM 7, row TOOL_BEGIN: the tool that enters the spindle, its holder and the rpm of the holder's spindle.
    [Fact]
    public void ToolBegin_ToolN_CarriesTheToolTheHolderAndTheRpm()
    {
        FakeListener listener = EventRuns.Execute("TOOL=1 RPM=1592");

        ToolEvent begin = Assert.Single(listener.Of<ToolEvent>());
        Assert.Equal("TOOL_BEGIN", begin.Kind);
        Assert.Equal(new ToolRef(1), begin.Tool);
        Assert.Equal("H1", begin.Holder);
        Assert.Equal(1592m, begin.Rpm);
        Assert.Equal("TOOL_BEGIN(1): tool 1, holder H1, rpm 1592", begin.ToString());
    }

    // VM 3.5, row TOOL=n: TOOL_END for the old tool, then TOOL_BEGIN for n.
    [Fact]
    public void ToolEnd_ToolChange_EndsTheOldToolBeforeTheNewOneBegins()
    {
        FakeListener listener = EventRuns.Execute("TOOL=1", "TOOL=2");

        Assert.Equal(
            [
                "TOOL_BEGIN(1): tool 1, holder H1, rpm 0",
                "TOOL_END(2): tool 1, holder H1, rpm 0, blocks 1, " + NoDistance,
                "TOOL_BEGIN(2): tool 2, holder H1, rpm 0",
            ],
            ToolLines(listener));
    }

    // P1-05, done when: TOOL_END carries the distance and the block count under the tool, the distance summed from the
    // MOTION lengths, the blocks counted from the block that brought the tool in.
    [Fact]
    public void ToolEnd_AfterMotions_CarriesTheDistanceAndTheBlockCountUnderTheTool()
    {
        FakeListener listener = EventRuns.Execute(
            "UNITS=MM", "RAPID X=0 Y=0 Z=10", "TOOL=1", "F=500", "LINE X=30", "LINE Y=40", "RAPID Z=50", "TOOL=2");

        ToolEvent end = listener.Of<ToolEvent>()[1];
        Assert.Equal(EventPhase.End, end.Phase);
        Assert.Equal(new ToolRef(1), end.Tool);
        Assert.Equal(110m, end.Distance);
        Assert.Equal(70m, end.FeedDistance);
        Assert.Equal(40m, end.RapidDistance);
        Assert.Equal(5, end.Blocks);
        Assert.Equal(
            "TOOL_END(8): tool 1, holder H1, rpm 0, blocks 5, feedDistance 70, rapidDistance 40, "
            + "unknownLengthMotions 0, distance 110",
            end.ToString());
    }

    // VM 3.5, row TOOL=0; architecture 5.2, Loaded to Empty: the tool leaves the spindle and none enters.
    [Fact]
    public void ToolEnd_Tool0_EndsTheToolWithoutABegin()
    {
        FakeListener listener = EventRuns.Execute("TOOL=1", "TOOL=0");

        Assert.Equal(
            [
                "TOOL_BEGIN(1): tool 1, holder H1, rpm 0",
                "TOOL_END(2): tool 1, holder H1, rpm 0, blocks 1, " + NoDistance,
            ],
            ToolLines(listener));
    }

    // VM 7: TOOL_BEGIN and TOOL_END come when a tool enters or leaves the spindle; architecture 5.2: only a transition
    // into Loaded with a new tool raises them.
    [Fact]
    public void ToolN_ToolAlreadyInTheSpindle_RaisesNoToolEvent()
    {
        FakeListener listener = EventRuns.Execute("TOOL=1", "TOOL=1");

        Assert.Equal(["TOOL_BEGIN(1): tool 1, holder H1, rpm 0"], ToolLines(listener));
    }

    // VM 3.5, row TOOL: the bare TOOL changes to the preloaded tool, which enters the spindle.
    [Fact]
    public void ToolBegin_BareTool_BeginsThePreloadedTool()
    {
        FakeListener listener = EventRuns.Execute("PRELOAD=3", "TOOL");

        Assert.Equal(["TOOL_BEGIN(2): tool 3, holder H1, rpm 0"], ToolLines(listener));
    }

    // VM 2.3, 3.8 rule 2: the motions and blocks count under the tool of the holder called last, the holder OFFSET
    // addresses as well; the rpm is that of the holder's spindle, the default spindle for a holder without one (VM 5).
    [Fact]
    public void ToolEnd_TwoHolders_CountsUnderTheHolderCalledLast()
    {
        FakeListener listener = EventRuns.Execute(VmMachines.MillTurn(), null,
            "UNITS=MM", "RAPID X=0 Z=0", "TOOL:TURRET1=1", "TOOL:TURRET2=5", "LINE Z=-10 F=100", "TOOL:TURRET1=2",
            "TOOL:TURRET2=0");

        Assert.Equal(
            [
                "TOOL_BEGIN(3): tool 1, holder H1, rpm 0",
                "TOOL_BEGIN(4): tool 5, holder H2, rpm 0",
                "TOOL_END(6): tool 1, holder H1, rpm 0, blocks 1, " + NoDistance,
                "TOOL_BEGIN(6): tool 2, holder H1, rpm 0",
                "TOOL_END(7): tool 5, holder H2, rpm 0, blocks 2, feedDistance 10, rapidDistance 0, "
                + "unknownLengthMotions 0, distance 10",
            ],
            ToolLines(listener));
    }

    // D233: PROGRAM=END keeps the tool in the spindle (VM 4) and raises TOOL_END for it, with its figures, directly
    // before PROGRAM_END.
    [Fact]
    public void ToolEnd_ProgramEnd_ClosesTheToolStillInTheSpindleBeforeProgramEnd()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File("UNITS=MM", "TOOL=1", "RAPID X=0 Y=0 Z=5", "PROGRAM=END"));

        Assert.Equal(
            [
                "TOOL_BEGIN(4): tool 1, holder H1, rpm 0",
                "TOOL_END(6): tool 1, holder H1, rpm 0, blocks 3, feedDistance 0, rapidDistance 0, "
                + "unknownLengthMotions 1, distance 0",
            ],
            ToolLines(listener));
        int toolEnd = listener.Lines.FindIndex(line => line.StartsWith("TOOL_END", StringComparison.Ordinal));
        Assert.StartsWith("PROGRAM_END(6)", listener.Lines[toolEnd + 1], StringComparison.Ordinal);
        Assert.Equal(new ToolRef(1), listener.Of<ToolEvent>()[^1].After.Holders["H1"].SpindleTool);
    }

    // D233: an uncalled SUB walk closes its tools at its SUB=END; a called one closes nothing there.
    [Fact]
    public void ToolEnd_UncalledSubEnd_ClosesItsTools()
    {
        FakeListener listener = EventRuns.Run(VmHarness.File(
            "PROGRAM=END", "SUB=BEGIN NAME=10", "TOOL=3", "SUB=END"));

        Assert.Equal(
            [
                "TOOL_BEGIN(5): tool 3, holder H1, rpm 0",
                "TOOL_END(6): tool 3, holder H1, rpm 0, blocks 2, " + NoDistance,
            ],
            ToolLines(listener));
    }

    // VM 7, row PRELOAD: every PRELOAD with its tool and holder, PRELOAD=0 that clears the preload among them.
    [Fact]
    public void Preload_PreloadWords_CarryTheToolAndTheHolder()
    {
        FakeListener listener = EventRuns.Execute("PRELOAD=5", "PRELOAD=0");

        Assert.Equal(["PRELOAD(1): tool 5, holder H1", "PRELOAD(2): tool 0, holder H1"], listener.LinesOf("PRELOAD"));
        PreloadEvent preload = listener.Of<PreloadEvent>()[0];
        Assert.Equal(new ToolRef(5), preload.Tool);
        Assert.Equal("H1", preload.Holder);
    }

    private static List<string> ToolLines(FakeListener listener)
    {
        var lines = new List<string>();
        foreach (ToolEvent tool in listener.Of<ToolEvent>())
        {
            lines.Add(tool.ToString());
        }

        return lines;
    }
}
