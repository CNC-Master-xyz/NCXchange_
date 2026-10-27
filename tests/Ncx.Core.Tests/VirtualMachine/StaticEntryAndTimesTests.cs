using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The STATIC walk of the 2026-10-27 decisions: every program starts from the initial state of its channel (D134),
/// the entry state of an uncalled subprogram when the first program has no verb (D135), a CALL whose TIMES is an
/// expression (D136), and the ARG names that are locals of their call (D149).
/// </summary>
public sealed class StaticEntryAndTimesTests
{
    // D134: a program after the first starts from the initial state of its channel, the tool included; nothing is
    // carried over from the previous program.
    [Fact]
    public void SecondProgram_StartsFromTheInitialState()
    {
        string text = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="FIRST"
            UNITS=INCH TOOL=4
            PRELOAD=6
            PROGRAM=END
            PROGRAM=BEGIN NAME="SECOND"
            PROGRAM=END
            FILE=END
            """;

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Assert.Equal(new ToolRef(0), vm.State.Holders["H1"].SpindleTool);
        Assert.Equal(Units.Unknown, vm.State.Frame.Units);
    }

    // D135: a first program without a verb gives the entry state as it stands at its first CALL, before the callee;
    // a verb inside the callee does not count.
    [Fact]
    public void UncalledSub_FirstProgramWithoutVerb_TakesTheStateAtTheFirstCall()
    {
        string text = VmHarness.File(
            "UNITS=INCH WORKPLANE=ZX FEED_MODE=PER_REV",
            "CALL=10",
            "UNITS=MM WORKPLANE=XY",
            "PROGRAM=END",
            "SUB=BEGIN NAME=10",
            "UNITS=MM",
            "RAPID X=1",
            "SUB=END",
            "SUB=BEGIN NAME=20",
            "SUB=END");
        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        ChannelState entry = vm.Vm.EntryStateOfUncalledSub(1);

        Assert.Equal(Units.Inch, entry.Frame.Units);
        Assert.Equal(Workplane.ZX, entry.Frame.Workplane);
        Assert.Equal(FeedMode.PerRev, entry.Motion.FeedMode);
    }

    // D135: without a verb and without a CALL, the state as the walk reaches PROGRAM=END, before that block resets the
    // feed mode.
    [Fact]
    public void UncalledSub_FirstProgramWithoutVerbOrCall_TakesTheStateBeforeProgramEnd()
    {
        string text = VmHarness.File(
            "UNITS=INCH WORKPLANE=ZX FEED_MODE=PER_REV", "PROGRAM=END", "SUB=BEGIN NAME=20", "SUB=END");
        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        ChannelState entry = vm.Vm.EntryStateOfUncalledSub(1);

        Assert.Equal(Units.Inch, entry.Frame.Units);
        Assert.Equal(Workplane.ZX, entry.Frame.Workplane);
        Assert.Equal(FeedMode.PerRev, entry.Motion.FeedMode);
    }

    // D135: a verb in the program's own blocks still wins over its first CALL.
    [Fact]
    public void UncalledSub_VerbAfterTheFirstCall_StillWins()
    {
        string text = VmHarness.File(
            "UNITS=INCH", "CALL=10", "UNITS=MM", "RAPID X=1", "PROGRAM=END", "SUB=BEGIN NAME=10", "SUB=END",
            "SUB=BEGIN NAME=20", "SUB=END");
        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        Assert.Equal(Units.Mm, vm.Vm.EntryStateOfUncalledSub(1).Frame.Units);
    }

    // D136: a CALL whose TIMES is an expression walks the subprogram once; every axis that pass moved becomes unknown,
    // an axis it left where it found it stays known, and the TIMES expression counts as unresolved.
    [Fact]
    public void CallWithTimesFromAnExpression_WalksOnceAndForgetsTheMovedAxes()
    {
        string text = VmHarness.File(
            "UNITS=MM",
            "RAPID X=0 Y=0 Z=0",
            "CALL=10 TIMES={$Q1}",
            "VAR:Q9=1",
            "PROGRAM=END",
            "SUB=BEGIN NAME=10",
            "RAPID X=5",
            "RAPID Y=3",
            "RAPID Y=0",
            "SUB=END");
        var listener = new PositionAtBlock(6);
        VmHarness vm = new VmHarness(VmMachines.Default());
        vm.Vm.Subscribe(listener);
        NcxProgram program = Ncx.Core.Parsing.Parser.Parse(text, "test.ncx", new Ncx.Core.Parsing.ParserOptions());

        RunResult result = vm.Vm.Run(program);

        Assert.False(result.Stopped);
        Assert.Equal([DiagnosticCodes.UnresolvedExpressions], vm.Codes());
        Assert.False(listener.Position!["X"].Known);
        Assert.True(listener.Position["Y"].Known);
        Assert.True(listener.Position["Z"].Known);
    }

    // D149: every name an ARG assigns is a local of the call, hiding a caller variable of that name until SUB=END.
    [Fact]
    public void Call_ArgOfAnyName_HidesTheCallersVariableUntilSubEnd()
    {
        InterpretedHarness vm = InterpretedHarness.RunClean(VmHarness.File(
            "VAR:A=7", "CALL=10 ARG:A=1", "VAR:Q2={$A}", "PROGRAM=END",
            "SUB=BEGIN NAME=10", "VAR:Q1={$A}", "VAR:A=3", "SUB=END"));

        Assert.Equal("1", vm.Var("Q1"));
        Assert.Equal("7", vm.Var("Q2"));
    }

    // D149: an ARG name the caller never assigned is gone again after the return.
    [Fact]
    public void Call_ArgOfANewName_IsUnassignedAfterTheReturn()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(VmHarness.File(
            "CALL=10 ARG:B=1", "VAR:Q2={$B}", "PROGRAM=END", "SUB=BEGIN NAME=10", "SUB=END"));

        Assert.Equal([DiagnosticCodes.UnassignedVariableRead], vm.Codes());
    }

    // The position at the block of a line, as the events of that block show it before the block.
    private sealed class PositionAtBlock(int line) : Ncx.Core.VirtualMachine.Events.IVmListener
    {
        public IReadOnlyDictionary<string, AxisPosition>? Position { get; private set; }

        public void On(Ncx.Core.VirtualMachine.Events.VmEvent e)
        {
            if (e.Block.Line == line && Position is null)
            {
                Position = e.Before.Motion.Position;
            }
        }
    }
}
