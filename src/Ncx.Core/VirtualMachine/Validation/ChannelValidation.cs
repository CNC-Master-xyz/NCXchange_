using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// Channels and synchronization (language 4.8, 4.14; virtual machine 3.7, 5): SYNC in a single-channel job, and the
/// rules of the job scheduler (Jobs/JobRunner): the deadlock at SYNC, two channels on one spindle or one axis of the
/// job's [shared] between marks, and the channels a job and its channel words name.
/// </summary>
internal static class ChannelValidation
{
    // The stage that raises the rules of several channels (virtual machine 3.7).
    private const string JobScheduler = "the job scheduler";

    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Channel",
        Summary = "Channels and their synchronization in a job (language 4.8, 4.14; VM 3.7, 5; machine-config 8).",
        Rules =
        [
            ValidationRule.Warning(DiagnosticCodes.SyncInSingleChannelJob,
                "SYNC in a single-channel job; the channel does not wait.", "VM 3.7, 5"),
            ValidationRule.Error(DiagnosticCodes.SyncDeadlock,
                "Deadlock: no channel can execute a block and some channel has not finished; the ERROR names what each "
                + "channel waits for.", "VM 3.7, 5; D305") with { RaisedBy = JobScheduler },
            ValidationRule.Warning(DiagnosticCodes.SpindleSharedBetweenMarks,
                "Two channels on one spindle of [shared] between marks.", "VM 3.7, 5; machine-config 8; D20")
                with { RaisedBy = JobScheduler },
            ValidationRule.Error(DiagnosticCodes.SyncParticipantsDiffer,
                "A channel reaches a mark while another participant of either list waits there with other "
                + "participants; the job stops.", "VM 3.7, 5; D304") with { RaisedBy = JobScheduler },
            ValidationRule.Warning(DiagnosticCodes.AxisSharedBetweenMarks,
                "Two channels on one axis of [shared] between marks, checked like the spindles.",
                "VM 3.7; machine-config 8; D20") with { RaisedBy = JobScheduler },
            ValidationRule.Error(DiagnosticCodes.ChannelNotInJob,
                "WITH, WAIT_CHANNEL or START_CHANNEL names a channel the machine does not have.",
                "language 4.8; VM 3.7; D306") with { RaisedBy = JobScheduler },
            ValidationRule.Warning(DiagnosticCodes.ChannelAlreadyStarted,
                "START_CHANNEL of a channel that runs; nothing starts.", "language 4.8; VM 3.7; D309")
                with { RaisedBy = JobScheduler },
            ValidationRule.Error(DiagnosticCodes.ChannelOtherThanHeader,
                "The CHANNEL of the program's header names another channel than the job manifest runs it on.",
                "language 4.14; VM 2.8; machine-config 8; D307") with { RaisedBy = JobScheduler },
            ValidationRule.Error(DiagnosticCodes.ChannelTwiceInJob,
                "The job manifest names one channel twice.", "machine-config 8; VM 2.8, 3.7")
                with { RaisedBy = JobScheduler },
            ValidationRule.Warning(DiagnosticCodes.ChannelNotRunByJob,
                "WITH, WAIT_CHANNEL or START_CHANNEL names a channel the machine has but the job does not run; the "
                + "word is taken as if that channel were not named.", "language 4.8; VM 3.7; D306")
                with { RaisedBy = JobScheduler },
            ValidationRule.Warning(DiagnosticCodes.FinishedChannelNotRestarted,
                "START_CHANNEL of a channel that has finished; NCX runs one program per channel per job, and "
                + "nothing starts.", "language 4.8; VM 3.7; D309") with { RaisedBy = JobScheduler },
        ],
    };

    /// <summary>
    /// Tells whether the run of a file without a job manifest is a single-channel job: the machine has one channel
    /// ([machine] channels names one, or the key is absent) and every program of the file runs on one channel
    /// (language 4.1, CHANNEL; 4.14; virtual machine 3.7, D196). A file for one path of a multi-channel machine is
    /// not, so its SYNC does not warn; check --job checks its marks.
    /// </summary>
    public static bool IsSingleChannelJob(NcxProgram program, MachineConfig machine)
    {
        if (machine.Machine.Channels.Count > 1)
        {
            return false;
        }

        var channels = new HashSet<int>();
        foreach (Section section in program.Programs)
        {
            channels.Add(section.Channel);
        }

        return channels.Count <= 1;
    }

    /// <summary>
    /// SYNC in a single-channel job is a WARNING, and the channel does not wait: no other channel reaches the mark
    /// (virtual machine 3.7, 5).
    /// </summary>
    public static void CheckSync(Block block, bool singleChannelJob, Diagnostics diagnostics)
    {
        if (!singleChannelJob || block.Find("SYNC") is not Word sync)
        {
            return;
        }

        diagnostics.Warning(block, DiagnosticCodes.SyncInSingleChannelJob,
            $"{sync.ToCanonical()} in a single-channel job: no other channel reaches the mark, and the channel does "
            + "not wait (virtual machine 3.7).");
    }
}
