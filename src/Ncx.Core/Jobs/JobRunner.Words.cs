using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.Jobs;

// The words of language 4.8 as the scheduler applies them after the block that carries them (virtual machine 3.7): SYNC
// with WITH, WAIT_CHANNEL and START_CHANNEL; and the channel the job runs a program on against the CHANNEL of its
// header (language 4.14). The catalog gives SYNC, WAIT_CHANNEL and START_CHANNEL an integer and WITH a list of them.
public sealed partial class JobRunner
{
    // The job words of an executed block, in the order of language 4.8; false after an ERROR, which stops the job.
    private bool ApplyJobWords(ChannelRun channel, Block block)
    {
        if (block.Find("SYNC") is Word sync && !WaitAtMark(channel, block, sync))
        {
            return false;
        }

        if (block.Find("WAIT_CHANNEL") is Word waitChannel && !WaitForChannel(channel, block, waitChannel))
        {
            return false;
        }

        return block.Find("START_CHANNEL") is not Word startChannel || StartChannel(channel, block, startChannel);
    }

    // SYNC=m marks the channel waiting at m with the channels of WITH, all channels of the job without it (virtual
    // machine 3.7, language 4.8), and raises SYNC_WAIT in the round. In a job of one channel nothing waits; the
    // pre-pass of the file warns (3.7, ChannelValidation).
    private bool WaitAtMark(ChannelRun channel, Block block, Word sync)
    {
        if (_channelCount < 2 || sync.Value is not IntegerValue mark)
        {
            return true;
        }

        if (Participants(channel, block) is not List<int> participants)
        {
            return false;
        }

        int markNumber = NumberOf(mark);
        if (!SameParticipants(channel, block, markNumber, participants))
        {
            return false;
        }

        channel.Participants = participants;
        channel.MarkBlock = block;
        _timeline.Add(channel.Vm.ChangeWait(block, markNumber, markNumber, participants, _round));
        return true;
    }

    // D304: the channels waiting at one mark name the same participants. A channel that reaches mark m while another
    // participant of either list waits there with a different set is the ERROR VM573, raised at once, and the job stops
    // there instead of running into the deadlock VM571. NCX does not model the Siemens per-channel release, since the
    // control itself calls the behaviour after it undeterminable; on Fanuc the P lists would raise alarm PS0160 (FANUC
    // Series 30i-A Connection Manual Function B-63943GE-1/02, connector p.949-950: identical waiting M codes with
    // different P commands).
    private bool SameParticipants(ChannelRun channel, Block block, int mark, List<int> participants)
    {
        foreach (ChannelRun other in _channels)
        {
            if (other == channel
                || other.WaitingAt != mark
                || (!participants.Contains(other.Channel) && !other.Participants.Contains(channel.Channel))
                || other.Participants.SequenceEqual(participants))
            {
                continue;
            }

            channel.Vm.Diagnostics.Error(block, DiagnosticCodes.SyncParticipantsDiffer, string.Create(
                CultureInfo.InvariantCulture,
                $"Channels at mark {mark} name different participants: channel {other.Channel} "
                + $"WITH={string.Join(",", other.Participants)}; channel {channel.Channel} "
                + $"WITH={string.Join(",", participants)} (virtual machine 3.7, D304)."));
            return false;
        }

        return true;
    }

    // WAIT_CHANNEL=c waits until channel c has finished (virtual machine 3.7, language 4.8); a channel the job does not
    // run is not waited for (D306), and a channel that has finished releases the wait after the round (D305).
    private bool WaitForChannel(ChannelRun channel, Block block, Word waitChannel)
    {
        if (!TryNamed(channel, block, waitChannel, waitChannel.Value, out ChannelRun? named))
        {
            return false;
        }

        if (named is not ChannelRun awaited)
        {
            return true;
        }

        channel.WaitingFor = awaited.Channel;
        channel.ChannelWaitBlock = block;
        return true;
    }

    // START_CHANNEL=c starts the program of channel c, the program its NAME selects or else the program of the
    // channel's [[channel]] (language 4.8). The started channel executes its first block in the next round, and what
    // the starting channel did before the block is before everything the started channel does. D309: a channel named
    // by a START_CHANNEL anywhere in the job's files waits to be started (StartChannels); one that runs is not started
    // again (VM852, the control requires Reset or interrupted), and one that has finished is not restarted (VM856): the
    // control would restart it, but NCX runs one program per channel per job, an NCX restriction.
    private bool StartChannel(ChannelRun channel, Block block, Word startChannel)
    {
        if (!TryNamed(channel, block, startChannel, startChannel.Value, out ChannelRun? named))
        {
            return false;
        }

        if (named is not ChannelRun started)
        {
            return true;
        }

        if (started.Finished)
        {
            channel.Vm.Diagnostics.Warning(block, DiagnosticCodes.FinishedChannelNotRestarted, string.Create(
                CultureInfo.InvariantCulture,
                $"{startChannel.ToCanonical()}: channel {started.Channel} has finished; the control would restart "
                + $"it, but NCX runs one program per channel per job, and nothing starts (language 4.8, virtual "
                + $"machine 3.7, D309)."));
            return true;
        }

        if (!started.AwaitsStart)
        {
            channel.Vm.Diagnostics.Warning(block, DiagnosticCodes.ChannelAlreadyStarted, string.Create(
                CultureInfo.InvariantCulture,
                $"{startChannel.ToCanonical()}: channel {started.Channel} runs already, and nothing starts; the "
                + $"control requires Reset or interrupted (language 4.8, virtual machine 3.7, D309)."));
            return true;
        }

        started.AwaitsStart = false;
        string? programName = block.Find("NAME")?.Value is StringValue name ? name.Content : started.ManifestProgram;
        Begin(started, programName);
        _shared.Synchronized([channel.Channel, started.Channel]);
        return true;
    }

    // WITH=1,2 names the channels that take part in the SYNC of its block, all channels of the job without it (language
    // 4.8). The channel of the SYNC waits at the mark itself, so it always takes part: "the channel waits until every
    // participating channel has reached" the mark (4.8). A channel the machine has but the job does not run is left
    // out (D306). Null after the ERROR for a channel the machine does not have.
    private List<int>? Participants(ChannelRun channel, Block block)
    {
        var participants = new List<int>();
        if (block.Find("WITH") is Word with)
        {
            foreach (Value item in ItemsOf(with.Value))
            {
                if (!TryNamed(channel, block, with, item, out ChannelRun? named))
                {
                    return null;
                }

                if (named is not null)
                {
                    AddOnce(participants, named.Channel);
                }
            }
        }
        else
        {
            foreach (ChannelRun run in _channels)
            {
                AddOnce(participants, run.Channel);
            }
        }

        AddOnce(participants, channel.Channel);
        participants.Sort();
        return participants;
    }

    // The channel that a WITH, WAIT_CHANNEL or START_CHANNEL names (language 4.8), split by the machine (D306): a
    // channel of the job; a channel the machine has but the job does not run is the WARNING VM855, and the word is
    // taken as if it were not named (named null); a channel not in [machine] channels is the ERROR VM851, as the
    // control alarms too (PS0161, a P value inappropriate for the system configuration: FANUC 0i-F Wartung
    // B-64605EN, connector p.630-631). Without channels of its own the machine counts the job's channels as its own
    // (D388 (8)). False after the ERROR.
    private bool TryNamed(ChannelRun channel, Block block, Word word, Value value, out ChannelRun? named)
    {
        named = null;
        int? number = value is IntegerValue integer ? NumberOf(integer) : null;
        if (number is int id && FindChannel(id) is ChannelRun run)
        {
            named = run;
            return true;
        }

        if (number is int other && _machine.Machine.Channels.Contains(other))
        {
            channel.Vm.Diagnostics.Warning(block, DiagnosticCodes.ChannelNotRunByJob, string.Create(
                CultureInfo.InvariantCulture,
                $"{word.ToCanonical()} names channel {other}, which the machine has but the job does not run (the "
                + $"channels of the job are {string.Join(", ", JobChannelIds())}); the word is taken as if channel "
                + $"{other} were not named (language 4.8, virtual machine 3.7, D306)."));
            return true;
        }

        var machineChannels = new SortedSet<int>(_machine.Machine.Channels);
        machineChannels.UnionWith(JobChannelIds());
        string what = number is int missing ? missing.ToString(CultureInfo.InvariantCulture) : value.ToString() ?? "";
        channel.Vm.Diagnostics.Error(block, DiagnosticCodes.ChannelNotInJob,
            $"{word.ToCanonical()} names channel {what}, which the machine does not have; the channels of the machine "
            + $"are {string.Join(", ", machineChannels)} (language 4.8, virtual machine 3.7, D306).");
        return false;
    }

    // The channels of the job in the order of the manifest.
    private List<int> JobChannelIds()
    {
        var ids = new List<int>();
        foreach (ChannelRun run in _channels)
        {
            ids.Add(run.Channel);
        }

        return ids;
    }

    // The job runs a program on the channel of its [[channel]] (machine-config 8, D15). D307: the manifest decides the
    // channel; a header CHANNEL that differs from it is the ERROR VM853, because the reader resolved T words and
    // channel-bound functions for the header's channel (D219, D56). A header without CHANNEL runs silently on the
    // manifest's channel (D223). False after the ERROR.
    private static bool CheckHeaderChannel(ChannelRun channel)
    {
        if (channel.Section is not Section running)
        {
            return true;
        }

        Block header = channel.Program.Blocks[running.FirstBlock];
        if (header.Find("CHANNEL") is not Word word
            || word.Value is not IntegerValue number
            || NumberOf(number) == channel.Channel)
        {
            return true;
        }

        channel.Diagnostics.Error(header, DiagnosticCodes.ChannelOtherThanHeader, string.Create(
            CultureInfo.InvariantCulture,
            $"{word.ToCanonical()} in the header of a program that the job manifest runs on channel "
            + $"{channel.Channel}; the manifest decides the channel, and the reader resolved the T words and the "
            + $"channel-bound functions for the header's (machine-config 8, virtual machine 2.8, D307)."));
        return false;
    }

    // The channels that a START_CHANNEL in a file of the job names (language 4.8).
    private HashSet<int> ChannelsStartedByWord()
    {
        var started = new HashSet<int>();
        var files = new HashSet<NcxProgram>(ReferenceEqualityComparer.Instance);
        foreach (ChannelRun channel in _channels)
        {
            if (!files.Add(channel.Program))
            {
                continue;
            }

            foreach (Block block in channel.Program.Blocks)
            {
                if (block.Find("START_CHANNEL")?.Value is IntegerValue number)
                {
                    started.Add(NumberOf(number));
                }
            }
        }

        return started;
    }

    // The channel numbers of WITH: one integer, or a list of them (language 3, list; 4.8).
    private static List<Value> ItemsOf(Value value)
    {
        var items = new List<Value>();
        if (value is not ListValue list)
        {
            items.Add(value);
            return items;
        }

        foreach (string item in list.Items)
        {
            items.Add(long.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out long number)
                ? new IntegerValue(number, item)
                : new IdentValue(item));
        }

        return items;
    }

    // A channel number or a mark, within the range of an int.
    private static int NumberOf(IntegerValue value)
    {
        return (int)Math.Clamp(value.Number, int.MinValue, int.MaxValue);
    }

    private static void AddOnce(List<int> numbers, int number)
    {
        if (!numbers.Contains(number))
        {
            numbers.Add(number);
        }
    }
}
