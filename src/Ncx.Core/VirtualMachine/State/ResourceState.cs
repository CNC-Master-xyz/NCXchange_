using Ncx.Core.Machine;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The spindle rows (virtual machine 2.4) and the tool rows (2.3, without the offsets) of every resource of the
/// machine. A channel checked alone has its own; in a job they are one per job, read and changed by every channel's
/// virtual machine in round order (virtual machine 2.8, D308). Mutable; only the virtual machine changes it
/// (code-guidelines 7).
/// </summary>
internal sealed class ResourceState
{
    // The channel each spindle belongs to: its own [[resource]] channel, or the channel of a holder whose tool spindle
    // it is (machine-config 4, F23); absent for a spindle no channel owns.
    private readonly Dictionary<string, int> _owners = new(StringComparer.Ordinal);

    // The channels of the job whose program has ended (virtual machine 2.8).
    private readonly HashSet<int> _ended = [];

    /// <summary>
    /// The resources of a machine as a run starts: every work and tool spindle gets the spindle rows of 2.4, every tool
    /// holder the tool rows of 2.3, by resource id (virtual machine 2.8).
    /// </summary>
    /// <param name="machine">The machine configuration.</param>
    /// <param name="jobChannels">The number of channels of the job that shares the state; 1 outside a job.</param>
    public ResourceState(MachineConfig machine, int jobChannels = 1)
    {
        JobChannels = jobChannels;
        foreach (ResourceDef resource in machine.Resources)
        {
            if (resource.IsSpindle)
            {
                Spindles[resource.Id] = new SpindleState();
            }
            else if (resource.Type == ResourceType.ToolHolder)
            {
                Holders[resource.Id] = new HolderState();
            }

            if (resource.Channel is int channel)
            {
                _owners[resource.Spindle ?? resource.Id] = channel;
                _owners.TryAdd(resource.Id, channel);
            }
        }
    }

    /// <summary>
    /// The number of channels that share the state: 1 for a channel checked alone (D308).
    /// </summary>
    public int JobChannels { get; }

    /// <summary>
    /// The state of each spindle by its resource id (virtual machine 2.4).
    /// </summary>
    public Dictionary<string, SpindleState> Spindles { get; } = new();

    /// <summary>
    /// The tool rows of each tool holder by its resource id (virtual machine 2.3).
    /// </summary>
    public Dictionary<string, HolderState> Holders { get; } = new();

    /// <summary>
    /// The program of a channel has ended; true when every channel of the job has ended now.
    /// </summary>
    public bool Ended(int channel)
    {
        _ended.Add(channel);
        return _ended.Count >= JobChannels;
    }

    /// <summary>
    /// Whether the PROGRAM=END of a channel resets a spindle (virtual machine 4): always outside a job; in a job the
    /// spindles the channel owns, and a spindle no channel owns once every channel of the job has ended, since another
    /// channel may still turn it (D308).
    /// </summary>
    public bool EndResets(string spindle, int channel, bool allEnded)
    {
        if (JobChannels < 2)
        {
            return true;
        }

        return _owners.TryGetValue(spindle, out int owner) ? owner == channel : allEnded;
    }
}
