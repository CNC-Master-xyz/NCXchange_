using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.Jobs;

/// <summary>
/// The state the channels of a job share (D308; virtual machine 2.3, 2.4, 2.8): the spindle and holder state is one per
/// job, read and changed by every channel in round order; lastHolder, the offsets, the point words (D388 (7)), the
/// coolant and the rest of virtual machine 2 stay per channel.
/// </summary>
public sealed class SharedStateTests
{
    // D308: the spindle channel 1 starts is the spindle channel 2 sees, from the round after the block on.
    [Fact]
    public void Spindle_StartedByChannelOne_ChannelTwoSeesItTurn()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("SPINDLE:MAIN=CW RPM:MAIN=1000", "UNITS=MM", "UNITS=MM", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        SpindleSnapshot seen = LastBefore(job, 2, "PROGRAM_END").Spindles["S1"];
        Assert.Equal(SpindleDirection.Clockwise, seen.Direction);
        Assert.Equal(1000m, seen.Rpm);
    }

    // D308, virtual machine 4: PROGRAM=END of one channel leaves a spindle no channel owns turning while another
    // channel of the job runs; once every channel has ended it is OFF.
    [Fact]
    public void ProgramEnd_OfOneChannel_LeavesTheSpindleOfNoChannelTurningForTheOthers()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("SPINDLE:MAIN=CW RPM:MAIN=1000", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal(SpindleDirection.Clockwise, LastBefore(job, 2, "PROGRAM_END").Spindles["S1"].Direction);
        Assert.Equal(SpindleDirection.Off, job.Result.Channels[1].Vm.Snapshot().Spindles["S1"].Direction);
    }

    // D308: the tool in the spindle of a holder is one per job; the offsets stay per channel, as the control's
    // tool-compensation memory is per channel.
    [Fact]
    public void Holder_ToolChangedByChannelOne_ChannelTwoSeesTheToolButNotTheOffset()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("TOOL=5 OFFSET=5", "UNITS=MM", "UNITS=MM", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        HolderSnapshot seen = LastBefore(job, 2, "PROGRAM_END").Holders["H1"];
        Assert.Equal(new ToolRef(5), seen.SpindleTool);
        Assert.Equal(0, seen.OffsetCombined);
        Assert.Equal(5, LastBefore(job, 1, "PROGRAM_END").Holders["H1"].OffsetCombined);
    }

    // D308, virtual machine 7: a listener of channel 2 sees no tool change and no state change for the tool that
    // channel 1 changed; the Before of channel 2's next block takes the shared rows as they stand.
    [Fact]
    public void Events_ToolChangedByChannelOne_RaiseNothingInChannelTwo()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("UNITS=MM", "TOOL=5", "UNITS=MM", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=INCH", "UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        Assert.Equal(-1, job.IndexOf(2, "TOOL_BEGIN"));
        Assert.DoesNotContain(job.EventsOf(2), vmEvent => vmEvent is StateChangeEvent change
            && change.Variable.StartsWith("TOOL", StringComparison.Ordinal));
        Assert.NotEqual(-1, job.IndexOf(1, "TOOL_BEGIN"));
    }

    // D308 with D372, D388 (7): TOOL_POINT, TIP_OFFSET and POINT_RADIUS select and size registers, so they are offset
    // state and stay per channel.
    [Fact]
    public void PointWords_OfChannelOne_StayInChannelOne()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("TOOL=5 TOOL_POINT=2 TIP_OFFSET=1", "UNITS=MM", "UNITS=MM", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        HolderSnapshot seen = LastBefore(job, 2, "PROGRAM_END").Holders["H1"];
        Assert.Equal(1, seen.ToolPoint);
        Assert.Equal(0m, seen.TipOffset);
        Assert.Equal(2, LastBefore(job, 1, "PROGRAM_END").Holders["H1"].ToolPoint);
    }

    // D308: coolant and lastHolder stay per channel (virtual machine 2.5, 2.8).
    [Fact]
    public void Coolant_SwitchedOnByChannelOne_StaysOffInChannelTwo()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("COOLANT=ON", "UNITS=MM", "UNITS=MM", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "PROGRAM=END"));

        job.AssertFinished();
        Assert.False(LastBefore(job, 2, "PROGRAM_END").Coolant["STANDARD"]);
        Assert.True(LastBefore(job, 1, "PROGRAM_END").Coolant["STANDARD"]);
    }

    // D308: the UNKNOWN mark of a shared spindle row is shared with it. Channel 2 sets the speed of the main spindle
    // from an expression, which STATIC mode does not evaluate; channel 1 must not take the stale 4000 rpm as known and
    // warns no VM441 for its feed per revolution (virtual machine 1, D288).
    [Fact]
    public void Rpm_SetFromAnExpressionByChannelTwo_IsUnknownInChannelOne()
    {
        MachineConfig machine = Ncx.Core.Tests.VirtualMachine.VmMachines.Default();
        var axes = new List<AxisDef>();
        foreach (AxisDef axis in machine.Axes)
        {
            axes.Add(axis.Id == "Z" ? axis with { MaxFeed = 10000m } : axis);
        }

        JobHarness job = JobHarness.Run(new JobSetup
        {
            Files =
            [
                JobHarness.File("UNITS=MM", "WORKPIECE=MAIN", "SPINDLE:MAIN=CW RPM:MAIN=4000", "SYNC=110",
                    "SYNC=111", "FEED_MODE=PER_REV", "LINE Z=-10 F=5", "PROGRAM=END"),
                JobHarness.File("UNITS=MM", "SYNC=110", "RPM:MAIN={$V528}", "SYNC=111", "DWELL=1", "PROGRAM=END"),
            ],
            Machine = machine with { Axes = axes },
            Mode = Ncx.Core.VirtualMachine.ExecutionMode.Static,
        });

        job.AssertFinished();
        Assert.DoesNotContain(DiagnosticCodes.FeedAboveMaxFeed, job.Codes());
        Assert.Contains("RPM:S1", LastBefore(job, 1, "PROGRAM_END").Unknown);
    }

    // D308 with VM551: a WORKPIECE change warns only for a spindle that is the channel's own; the main spindle that
    // channel 2 turns says nothing about the part of channel 1, while the one channel 1 turns itself still warns.
    [Theory]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public void WorkpieceChange_WhileAnotherChannelTurnsTheSpindle_WarnsOnlyForItsOwn(int turnedBy, bool warns)
    {
        string turn = "SPINDLE:MAIN=CW RPM:MAIN=1000";
        JobHarness job = JobHarness.Run(new JobSetup
        {
            Files =
            [
                JobHarness.File(turnedBy == 1 ? turn : "UNITS=MM", "UNITS=MM", "UNITS=MM", "WORKPIECE=SUB",
                    "PROGRAM=END"),
                JobHarness.File(turnedBy == 2 ? turn : "UNITS=MM", "UNITS=MM", "UNITS=MM", "UNITS=MM", "UNITS=MM",
                    "PROGRAM=END"),
            ],
            Machine = Ncx.Core.Tests.VirtualMachine.VmMachines.MillTurn(),
        });

        Assert.Equal(warns, job.Codes().Contains(DiagnosticCodes.WorkpieceChangeWhileSpindleRuns));
    }

    // The state a channel had in front of its last event of this kind.
    private static ChannelSnapshot LastBefore(JobHarness job, int channel, string kind)
    {
        List<VmEvent> events = job.EventsOf(channel);
        return events.FindLast(vmEvent => vmEvent.Kind == kind)?.Before
            ?? throw new InvalidOperationException($"Channel {channel} raised no {kind}.");
    }
}
