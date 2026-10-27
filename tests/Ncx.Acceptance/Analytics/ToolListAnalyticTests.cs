using Ncx.Acceptance.Cli;
using Ncx.Analytics;
using Ncx.Analytics.Runtime;
using Ncx.Analytics.ToolList;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The tool list of virtual machine 8 and architecture 9: one row per tool use from TOOL_BEGIN to TOOL_END with rpm,
/// feeds, offsets, the cutting and the rapid distance, the block count, the estimated time and whether the tool was
/// preloaded, on the examples without a machine file (D103) and over a block range (D67; implementation 14, P4-02).
/// </summary>
public sealed class ToolListAnalyticTests
{
    private const int Precision = 6;

    // P4-02 done when: the tool list of 2.5D_FRAESEN names its tools with the right distances. The tools the example
    // names are 1 and the preloaded 0, which is no tool (language 4.4), so the list has one row, tool 1. By hand from
    // the example: cutting, the LINE and ARC moves of lines 19 to 30, 2 + 9.025 + 43.4 + 7.854 + 86 + 7.854 + 86 +
    // 7.854 + 86 + 7.854 + 43.4 + 9.025 = 396.266, and of lines 37 to 45, 2 + 5.025 + 2.119 + 5.785 + 94.782 + 31.95 +
    // 5.785 + 2.119 + 5.025 = 154.59, 550.856 in all, 550.856466 at the six decimals of the VM (D121); rapid, lines
    // 18, 31, 35, 36 and 46: 10 + 12 + 69.906769 + 10 + 12 = 113.906769; unknown: the RAPIDs of lines 15 and 16
    // from the unknown start and the machine-frame moves of lines 48 and 49; blocks: lines 10 to 51 without the blank
    // lines 13 and 32, 40.
    [Fact]
    public void ToolList_Fraesen25D_NamesToolOneWithTheDistancesOfTheExample()
    {
        ToolListAnalytic tools = RunExample("2.5D_FRAESEN.ncx");

        ToolUse use = Assert.Single(Rows(tools));
        Assert.Equal(new ToolRef(1), use.Tool);
        Assert.Equal("H1", use.Holder);
        Assert.False(use.Preloaded);
        Assert.Equal(550.856466, use.Cutting, Precision);
        Assert.Equal(113.906769, use.Rapid, Precision);
        Assert.Equal(4, use.Unknown);
        Assert.Equal(40, use.Blocks);
        Assert.Equal("1592", use.RpmText());
        Assert.Equal("2387 PER_MIN, 1.5 PER_REV", use.FeedsText());
        Assert.Equal("LEN 1, RAD 1", use.OffsetsText());
    }

    // P4-02 test first: the tool list of INCREMENTAL_SUB names tools 2 and 3. Tool 2 cuts 4 * (5 + 30) = 140 mm in the
    // four passes of the subprogram and rapids 4 * (5 + 33.54102) mm there and 48 mm up to Z=50, 202.16408; its first
    // two RAPIDs start from the unknown position; its blocks are lines 7 to 13, the four passes of lines 29 to 34 and
    // lines 14 and 15, 33. Tool 3 comes in by the bare TOOL on line 16, preloaded by PRELOAD=3 on line 8; its HOME
    // moves find no reference point on the default machine (D100) and have no length; its blocks are lines 16, 17, 18,
    // 22, 23, 24, 25, 26, 19, 20 and 27, 11, the LABEL and COMMENT blocks among them.
    [Fact]
    public void ToolList_IncrementalSub_NamesToolsTwoAndThree()
    {
        ToolListAnalytic tools = RunExample("INCREMENTAL_SUB.ncx");

        List<ToolUse> rows = Rows(tools);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new ToolRef(2), rows[0].Tool);
        Assert.False(rows[0].Preloaded);
        Assert.Equal(140, rows[0].Cutting, Precision);
        Assert.Equal(202.16408, rows[0].Rapid, Precision);
        Assert.Equal(2, rows[0].Unknown);
        Assert.Equal(33, rows[0].Blocks);
        Assert.Equal("200-800 PER_MIN", rows[0].FeedsText());
        Assert.Equal("4000", rows[0].RpmText());
        Assert.Equal(new ToolRef(3), rows[1].Tool);
        Assert.True(rows[1].Preloaded);
        Assert.Equal(0, rows[1].Cutting, Precision);
        Assert.Equal(2, rows[1].Unknown);
        Assert.Equal(11, rows[1].Blocks);
        Assert.Equal("-", rows[1].RpmText());
    }

    // VM 2.7: STATIC mode does not count blocksExecuted, so a block that raises no event (the LABEL and COMMENT blocks
    // 19, 22 and 23) is not seen: tool 3 of INCREMENTAL_SUB has lines 16, 17, 18, 20, 24, 25, 26 and 27, 8 blocks.
    [Fact]
    public void ToolList_IncrementalSubStatic_CountsTheBlocksThatRaiseEvents()
    {
        var tools = new ToolListAnalytic(AnalyticRuns.Options(DefaultMachine.Create(), mode: ExecutionMode.Static));
        AnalyticRuns.RunExample("INCREMENTAL_SUB.ncx", tools, ExecutionMode.Static);

        List<ToolUse> rows = Rows(tools);
        Assert.Equal(33, rows[0].Blocks);
        Assert.Equal(8, rows[1].Blocks);
    }

    // VM 8, D67: over lines 33 to 51 of 2.5D_FRAESEN the row of tool 1 has the circular pocket only: cutting 154.59,
    // rapid 69.907 + 10 + 12 = 91.907, the two machine-frame moves of unknown length, the 19 blocks of those lines.
    [Fact]
    public void ToolList_BlockRange_CountsOnlyTheBlocksOnItsLines()
    {
        var tools = new ToolListAnalytic(
            AnalyticRuns.Options(DefaultMachine.Create(), new BlockRange { From = 33, To = 51 }));
        AnalyticRuns.RunExample("2.5D_FRAESEN.ncx", tools);

        ToolUse use = Assert.Single(Rows(tools));
        Assert.Equal(154.590538, use.Cutting, Precision);
        Assert.Equal(91.906769, use.Rapid, Precision);
        Assert.Equal(2, use.Unknown);
        Assert.Equal(19, use.Blocks);
    }

    // Architecture 9: one row per tool use; a tool called twice has two rows.
    [Fact]
    public void ToolList_ToolCalledTwice_HasARowPerUse()
    {
        ToolListAnalytic tools = Run("UNITS=MM", "TOOL=1", "TOOL=2", "TOOL=1");

        List<ToolUse> rows = Rows(tools);
        Assert.Equal([new ToolRef(1), new ToolRef(2), new ToolRef(1)], rows.ConvertAll(row => row.Tool));
    }

    // VM 3.5, 8: preload behaviour, whether the tool was preloaded before the change that brought it in: tool 5 was,
    // tool 6 was not.
    [Fact]
    public void ToolList_Preload_MarksTheToolThatArrivesPreloaded()
    {
        ToolListAnalytic tools = Run("UNITS=MM", "PRELOAD=5", "TOOL=5", "TOOL=6");

        List<ToolUse> rows = Rows(tools);
        Assert.True(rows[0].Preloaded);
        Assert.False(rows[1].Preloaded);
    }

    // Implementation 14, P4-02: the estimated time of a tool is the time the runtime estimate charges under it; without
    // a machine file distance over feed: 396.265928 mm at 2387 mm/min and 154.590538 mm at 1.5 mm/rev times 1592 rpm
    // (six decimals, D121).
    [Fact]
    public void ToolList_EstimatedTime_IsTheTimeOfTheRuntimeEstimateUnderTheTool()
    {
        ToolListAnalytic tools = RunExample("2.5D_FRAESEN.ncx");
        var runtime = new RuntimeAnalytic(AnalyticRuns.Options(DefaultMachine.Create()));
        AnalyticRuns.RunExample("2.5D_FRAESEN.ncx", runtime);

        ToolUse use = Assert.Single(Rows(tools));
        Assert.Equal((396.265928 / 2387 * 60) + (154.590538 / (1.5 * 1592) * 60), use.Seconds, Precision);
        Assert.Equal(runtime.SecondsOfTool("1"), use.Seconds, Precision);
    }

    // D289: under CSS the rpm range of a tool takes the extremes of a cutting motion, the rpm at the start and at the
    // end X radius each capped by RPM_MAX, not the mean the runtime estimate times the motion with. A facing cut from
    // radius 50 to radius 5 at VC=200 turns at 200000 / (2 * pi * 50) = 637 rpm where it starts and would reach 6366
    // rpm at radius 5, which RPM_MAX=3000 caps; the mean radius 27.5 would report the single value 1157.
    [Fact]
    public void CssRpmRange_FacingCutThatChangesX_SpansTheStartAndTheEndRadius()
    {
        MachineConfig machine = AnalyticMachines.Mill(AnalyticMachines.ExactStop);
        var tools = new ToolListAnalytic(AnalyticRuns.Options(machine));
        AnalyticRuns.Run(
            CliHarness.OneProgram("UNITS=MM", "TOOL=1", "RPM_MAX=3000", "VC=200", "CSS=ON", "SPINDLE=CW",
                "FEED_MODE=PER_REV", "RAPID X=50 Y=0 Z=0", "LINE X=5 F=0.2", "SPINDLE=OFF"),
            machine, tools);

        ToolUse use = Assert.Single(Rows(tools));
        Assert.Equal("637-3000", use.RpmText());
    }

    // D374: the tool list prints the points a tool cut in beside its offsets, with the values in effect; the TOOL=3
    // that follows starts at point 1 with a = r = 0, which prints nothing, and keeps the registers.
    [Fact]
    public void ToolList_PointWords_ArePrintedBesideTheOffsets()
    {
        ToolListAnalytic tools = Run("UNITS=MM", "TOOL=2 OFFSET:LEN=2", "RAPID X=0 Y=0 Z=10",
            "TOOL_POINT=2 TIP_OFFSET=1 POINT_RADIUS=5", "RAPID Z=5", "TOOL=3", "RAPID Z=20");

        List<ToolUse> rows = Rows(tools);
        Assert.Equal("LEN 2, POINT 2 TIP 1 RAD 5", rows[0].OffsetsText());
        Assert.Equal("LEN 2", rows[1].OffsetsText());
    }

    // D374, virtual machine 1: a TIP_OFFSET from an expression is UNKNOWN in STATIC mode, and the tool list prints it
    // as ?, not the value held before.
    [Fact]
    public void ToolList_UnknownTipOffset_IsPrintedUnknown()
    {
        var tools = new ToolListAnalytic(AnalyticRuns.Options(DefaultMachine.Create(), mode: ExecutionMode.Static));
        AnalyticRuns.Run(CliHarness.OneProgram("UNITS=MM", "TOOL=2 TIP_OFFSET=1", "TIP_OFFSET={$Q1 * 2}",
            "RAPID X=0 Y=0 Z=10"), DefaultMachine.Create(), tools, ExecutionMode.Static);

        Assert.Equal("POINT 1 TIP ? RAD 0", Assert.Single(Rows(tools)).OffsetsText());
    }

    // The rows of the report: the uses with a block in the range.
    private static List<ToolUse> Rows(ToolListAnalytic tools)
    {
        var rows = new List<ToolUse>();
        foreach (ToolUse use in tools.Uses)
        {
            if (use.Blocks > 0)
            {
                rows.Add(use);
            }
        }

        return rows;
    }

    private static ToolListAnalytic RunExample(string example)
    {
        var tools = new ToolListAnalytic(AnalyticRuns.Options(DefaultMachine.Create()));
        AnalyticRuns.RunExample(example, tools);
        return tools;
    }

    private static ToolListAnalytic Run(params string[] blocks)
    {
        var tools = new ToolListAnalytic(AnalyticRuns.Options(DefaultMachine.Create()));
        AnalyticRuns.Run(CliHarness.OneProgram(blocks), DefaultMachine.Create(), tools);
        return tools;
    }
}
