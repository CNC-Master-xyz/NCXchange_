using Ncx.Core.Model;
using Ncx.Core.Tests.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The measurement point of a tool on its holder (language 4.4, virtual machine 2.3; D372): TOOL_POINT, TIP_OFFSET and
/// POINT_RADIUS are modal per holder, and a TOOL without them resets the holder to point 1, a = 0, r = 0.
/// </summary>
public sealed class ToolPointTests
{
    // VM 2.3, D372: a holder starts at point 1 with a = 0 and r = 0, what every program without the words says.
    [Fact]
    public void Holder_AtStart_IsPointOneAtTheTip()
    {
        HolderOffsets holder = new VmHarness(VmMachines.Default()).State.Offsets["H1"];

        Assert.Equal(1, holder.ToolPoint);
        Assert.Equal(0m, holder.TipOffset);
        Assert.Equal(0m, holder.PointRadius);
    }

    // D372: the three words in a TOOL block set the point of the tool that arrives.
    [Fact]
    public void PointWords_InTheToolBlock_SetThePointOfTheNewTool()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL=5 TOOL_POINT=2 TIP_OFFSET=2 POINT_RADIUS=8");

        vm.AssertNoDiagnostics();
        HolderOffsets holder = vm.State.Offsets["H1"];
        Assert.Equal(2, holder.ToolPoint);
        Assert.Equal(2m, holder.TipOffset);
        Assert.Equal(8m, holder.PointRadius);
    }

    // D372: a TOOL block that carries none of them resets the holder to point 1, a = 0, r = 0, so a new tool never
    // inherits the point of the old one.
    [Fact]
    public void Tool_WithoutPointWords_ResetsToPointOne()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("TOOL=5 TOOL_POINT=2 TIP_OFFSET=2 POINT_RADIUS=8", "TOOL=6");

        HolderOffsets holder = vm.State.Offsets["H1"];
        Assert.Equal(1, holder.ToolPoint);
        Assert.Equal(0m, holder.TipOffset);
        Assert.Equal(0m, holder.PointRadius);
    }

    // D372: the point changes without a tool change in a block of its own, and a word the block leaves out keeps its
    // value, the words being modal.
    [Fact]
    public void PointWords_InABlockOfTheirOwn_ChangeThePointWithoutAToolChange()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("TOOL=5 TOOL_POINT=2 TIP_OFFSET=2 POINT_RADIUS=8", "TOOL_POINT=3 TIP_OFFSET=1.5");

        HolderOffsets holder = vm.State.Offsets["H1"];
        Assert.Equal(new ToolRef(5), vm.State.Holders["H1"].SpindleTool);
        Assert.Equal(3, holder.ToolPoint);
        Assert.Equal(1.5m, holder.TipOffset);
        Assert.Equal(8m, holder.PointRadius);
    }

    // D391: without an address the words address the holder of the last TOOL, as OFFSET does (VM 3.8 rule 2).
    [Fact]
    public void PointWords_WithoutAnAddress_GoToTheHolderOfTheLastTool()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("TOOL:TURRET2=3", "TOOL_POINT=2 TIP_OFFSET=1");

        Assert.Equal(2, vm.State.Offsets["H2"].ToolPoint);
        Assert.Equal(1m, vm.State.Offsets["H2"].TipOffset);
        Assert.Equal(1, vm.State.Offsets["H1"].ToolPoint);
    }

    // D372, D391: TOOL_POINT with a role addresses that holder, and the tip offset of its block follows it.
    [Fact]
    public void ToolPointWithARole_AddressesThatHolderAndTakesTheTipOffsetWithIt()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("TOOL:TURRET1=4", "TOOL:TURRET2=3", "TOOL_POINT:TURRET1=2 TIP_OFFSET=1");

        Assert.Equal(2, vm.State.Offsets["H1"].ToolPoint);
        Assert.Equal(1m, vm.State.Offsets["H1"].TipOffset);
        Assert.Equal(1, vm.State.Offsets["H2"].ToolPoint);
        Assert.Equal(0m, vm.State.Offsets["H2"].TipOffset);
    }

    // VM 1: a value from an expression is UNKNOWN in STATIC mode, named by its state key.
    [Fact]
    public void TipOffset_FromAnExpression_IsUnknownInStaticMode()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL=5", "TIP_OFFSET={$Q1 * 2}");

        Assert.Contains("TIP_OFFSET:H1", vm.State.Unknown);
    }

    // VM 2.3: a snapshot keeps the point the holder had when it was taken.
    [Fact]
    public void Point_ChangedAfterASnapshot_SnapshotKeepsIt()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL=5 TOOL_POINT=2 TIP_OFFSET=2 POINT_RADIUS=8");
        HolderSnapshot snapshot = vm.State.Snapshot().Holders["H1"];

        vm.Execute("TOOL=6");

        Assert.Equal(2, snapshot.ToolPoint);
        Assert.Equal(2m, snapshot.TipOffset);
        Assert.Equal(8m, snapshot.PointRadius);
    }

    // VM 6, 7: the trace names the three variables per holder like the offsets, TOOL_POINT:H1.
    [Fact]
    public void StateChange_PointWords_OneEventPerVariableAndHolder()
    {
        FakeListener listener = EventRuns.Execute("TOOL=5 TOOL_POINT=2 TIP_OFFSET=2 POINT_RADIUS=8");

        Assert.Equal(
        [
            "STATE_CHANGE(1): TOOL:H1 0 -> 5",
            "STATE_CHANGE(1): TOOL_POINT:H1 1 -> 2",
            "STATE_CHANGE(1): TIP_OFFSET:H1 0 -> 2",
            "STATE_CHANGE(1): POINT_RADIUS:H1 0 -> 8",
        ], listener.LinesOf("STATE_CHANGE"));
    }
}
