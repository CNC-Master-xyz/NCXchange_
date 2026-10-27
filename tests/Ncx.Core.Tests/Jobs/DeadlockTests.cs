using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;

namespace Ncx.Core.Tests.Jobs;

/// <summary>
/// The deadlock at SYNC (virtual machine 3.7, 5; architecture 5.4): all channels waiting with no releasable mark is an
/// ERROR naming the marks per channel, and the job stops.
/// </summary>
public sealed class DeadlockTests
{
    // P6-01 done when: a job whose channels wait at different marks reports the deadlock with both marks, in ncx check
    // (STATIC) and in ncx analyze (INTERPRETED). Two channels waiting at different marks with the same participants is
    // the deadlock case of VM 3.7.
    [Theory]
    [InlineData(ExecutionMode.Interpreted)]
    [InlineData(ExecutionMode.Static)]
    public void Deadlock_ChannelsWaitingAtDifferentMarks_IsErrorNamingBothMarks(ExecutionMode mode)
    {
        JobHarness job = JobHarness.Run(new JobSetup
        {
            Files =
            [
                JobHarness.File("SYNC=100", "SYNC=200", "PROGRAM=END"),
                JobHarness.File("SYNC=200", "SYNC=100", "PROGRAM=END"),
            ],
            Mode = mode,
        });

        Diagnostic deadlock = job.Single(DiagnosticCodes.SyncDeadlock);
        Assert.Equal(Severity.Error, deadlock.Severity);
        Assert.Equal("ch1.ncx", deadlock.File);
        Assert.Equal(3, deadlock.Line);
        Assert.Equal(
            "Deadlock: channel 1 waits at SYNC=100 with 1,2 (ch1.ncx line 3); channel 2 waits at SYNC=200 with 1,2 "
            + "(ch2.ncx line 3); no channel can go on and no mark can be released (virtual machine 3.7).",
            deadlock.Message);
        Assert.True(job.Result.Stopped);
        Assert.Equal(2, job.Result.Rounds);
    }

    // VM 3.7: a channel that waits at a mark of a channel that has finished can never go on; the deadlock names the
    // finished channel (the TODO(question) of JobRunner.ReportDeadlock).
    [Fact]
    public void Deadlock_MarkOfAChannelThatHasFinished_IsErrorNamingIt()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("UNITS=MM", "SYNC=100", "PROGRAM=END"),
            JobHarness.File("PROGRAM=END"));

        Diagnostic deadlock = job.Single(DiagnosticCodes.SyncDeadlock);
        Assert.Contains("channel 1 waits at SYNC=100 with 1,2 (ch1.ncx line 4); channel 2 has finished",
            deadlock.Message, StringComparison.Ordinal);
    }

    // VM 3.7: two channels that each wait for the end of the other.
    [Fact]
    public void Deadlock_EachChannelWaitsForTheOther_IsError()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("WAIT_CHANNEL=2", "PROGRAM=END"),
            JobHarness.File("WAIT_CHANNEL=1", "PROGRAM=END"));

        Diagnostic deadlock = job.Single(DiagnosticCodes.SyncDeadlock);
        Assert.Contains("channel 1 waits for channel 2 to finish (ch1.ncx line 3); channel 2 waits for channel 1 to "
            + "finish (ch2.ncx line 3)", deadlock.Message, StringComparison.Ordinal);
    }

    // D304: a channel that reaches a mark while another participant of either list waits there with a different set is
    // the ERROR VM573, raised at once; the job stops there instead of running into the deadlock VM571.
    [Fact]
    public void Mark_ReachedWithDifferentParticipants_IsVm573AndStopsTheJob()
    {
        JobHarness job = JobHarness.Run(
            JobHarness.File("SYNC=100 WITH=1,2", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "SYNC=100 WITH=1,2,3", "PROGRAM=END"),
            JobHarness.File("UNITS=MM", "UNITS=MM", "SYNC=100 WITH=2,3", "PROGRAM=END"));

        Diagnostic error = job.Single(DiagnosticCodes.SyncParticipantsDiffer);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal("ch2.ncx", error.File);
        Assert.Equal(4, error.Line);
        Assert.StartsWith("Channels at mark 100 name different participants: channel 1 WITH=1,2; channel 2 "
            + "WITH=1,2,3", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(DiagnosticCodes.SyncDeadlock, job.Codes());
        Assert.True(job.Result.Stopped);
        Assert.Equal(3, job.Result.Rounds);
    }

    // D305: VM571 no longer says that every channel waits; it names what each channel waits for.
    [Fact]
    public void Deadlock_Text_NamesWhatEachChannelWaitsFor()
    {
        string rule = Ncx.Core.VirtualMachine.Validation.DiagnosticTable.Find(DiagnosticCodes.SyncDeadlock)?.Rule ?? "";

        Assert.DoesNotContain("every channel waits", rule, StringComparison.Ordinal);
        Assert.Contains("no channel can execute a block", rule, StringComparison.Ordinal);
    }
}
