using Ncx.Core.Machine;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// Whether a spindle is a channel's own (D308 with D288): in a job the spindle rows are one per job, and a channel
/// takes a spindle for its feed per revolution, its default spindle and its WORKPIECE change only when the spindle
/// belongs to the channel ([[resource]] channel, or the channel of a holder whose tool spindle it is) or turns
/// because of the channel's own SPINDLE word. Outside a job every spindle is the channel's own.
/// </summary>
public static class SpindleChannels
{
    /// <summary>
    /// True when the spindle is the channel's own.
    /// </summary>
    /// <param name="machine">The machine, whose resources name the channel a spindle belongs to.</param>
    /// <param name="channel">The channel that asks.</param>
    /// <param name="spindle">The resource id of the spindle.</param>
    /// <param name="startedBy">The channel whose SPINDLE word last set its direction; null for none.</param>
    public static bool IsOwn(MachineConfig machine, int channel, string spindle, int? startedBy)
    {
        if (startedBy is null || startedBy == channel)
        {
            return true;
        }

        foreach (ResourceDef resource in machine.Resources)
        {
            if (resource.Channel == channel && (resource.Id == spindle || resource.Spindle == spindle))
            {
                return true;
            }
        }

        return false;
    }
}
