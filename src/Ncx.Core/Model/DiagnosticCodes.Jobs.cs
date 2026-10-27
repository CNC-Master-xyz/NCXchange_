namespace Ncx.Core.Model;

// The codes of the job scheduler, VM850-VM899 (the ranges in DiagnosticCodes.cs, D98; implementation 16, P6-01): the
// rules of a job of several channels beyond the deadlock at SYNC (VM571) and the spindle shared between marks (VM572),
// which the channel family of the validation holds since phase 1. Every code has its row in the channel family of the
// table of the validation (VirtualMachine/Validation/ChannelValidation.cs).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// VM850, a WARNING: two channels command one axis of the job's [shared] axes between two marks, checked like the
    /// spindles (virtual machine 3.7; machine-config 8, D20, F24).
    /// </summary>
    public const string AxisSharedBetweenMarks = "VM850";

    /// <summary>
    /// VM851: WITH, WAIT_CHANNEL or START_CHANNEL names a channel that the machine does not have, [machine] channels;
    /// without channels of its own the machine counts the job's (language 4.8; virtual machine 3.7; D306, D388 (8)).
    /// </summary>
    public const string ChannelNotInJob = "VM851";

    /// <summary>
    /// VM852, a WARNING: START_CHANNEL of a channel that runs; nothing starts, as the control requires Reset or
    /// interrupted (language 4.8; virtual machine 3.7; D309).
    /// </summary>
    public const string ChannelAlreadyStarted = "VM852";

    /// <summary>
    /// VM853: the CHANNEL of the program's header names another channel than the job manifest runs the program on; the
    /// reader resolved T words and channel-bound functions for the header's channel (language 4.14; virtual machine
    /// 2.8; machine-config 8; D307).
    /// </summary>
    public const string ChannelOtherThanHeader = "VM853";

    /// <summary>
    /// VM854: the job manifest names one channel twice; a channel runs one program of the job (machine-config 8;
    /// virtual machine 2.8, 3.7).
    /// </summary>
    public const string ChannelTwiceInJob = "VM854";

    /// <summary>
    /// VM855, a WARNING: WITH, WAIT_CHANNEL or START_CHANNEL names a channel the machine has but the job does not run;
    /// the word is taken as if that channel were not named (language 4.8; virtual machine 3.7; D306).
    /// </summary>
    public const string ChannelNotRunByJob = "VM855";

    /// <summary>
    /// VM856, a WARNING: START_CHANNEL of a channel that has finished; the control would restart it, but NCX runs one
    /// program per channel per job, and nothing starts (language 4.8; virtual machine 3.7; D309).
    /// </summary>
    public const string FinishedChannelNotRestarted = "VM856";
}
