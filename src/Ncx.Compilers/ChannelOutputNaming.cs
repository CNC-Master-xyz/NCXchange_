using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// The names of the output files of a job, one per channel (implementation 16, P6-02; controller-mapping 7, CHANNEL;
/// controllers fanuc.md 1), as [format] channel_files says: O1000 and O1000.P-2 by the memory card convention of
/// Nakamura, the job name with _C1 and _C2 as on the STAMA, or the NAME of the program, 1000 and 2000 for the units
/// %_N_1000_MPF and %_N_2000_MPF of the DMG templates. Without channel_files each channel keeps the name the compiler
/// of its family gives the file of its program.
/// </summary>
// D310: [format] channel_files = "path_suffix" | "channel_suffix" | "program_name"; path_suffix writes O<NUMBER> with
// at least four digits for the first channel and O<NUMBER>.P-n for channel n, without an extension; without the key
// each channel keeps its family name. Two channels that would write one file are told apart by _C<n> with the WARNING
// CMP721, the same rule as D266.
internal static class ChannelOutputNaming
{
    // O with at least four digits, O0001 (controllers fanuc.md 1).
    private const int LeastDigits = 4;

    /// <summary>
    /// The files of the channels named after their channels, in their order.
    /// </summary>
    /// <param name="outputs">The file of every channel as the compiler of the family named it.</param>
    /// <param name="job">The job manifest, whose [job] name the channel suffix form takes.</param>
    /// <param name="machine">The machine: [format] channel_files and [machine] channels.</param>
    /// <param name="diagnostics">The diagnostics of the job, which take the WARNING CMP721 of a name two channels
    /// would write.</param>
    public static List<CompiledFile> Name(List<ChannelOutput> outputs, JobManifest job, MachineConfig machine,
        Diagnostics diagnostics)
    {
        var names = new List<string>();
        foreach (ChannelOutput output in outputs)
        {
            names.Add(NameOf(output, job, machine));
        }

        var files = new List<CompiledFile>();
        for (int index = 0; index < outputs.Count; index++)
        {
            string name = names[index];
            if (names.FindAll(other => string.Equals(other, name, StringComparison.OrdinalIgnoreCase)).Count > 1)
            {
                string taken = name;
                name = WithChannel(name, outputs[index].Channel);
                diagnostics.Warning(1, DiagnosticCodes.ChannelFileNameTaken, string.Create(CultureInfo.InvariantCulture,
                    $"Channel {outputs[index].Channel} would write the file {taken}, which another channel of the job "
                    + $"writes too (names are compared case-insensitively), so it is named {name} (machine-config 2, "
                    + $"D310)."));
            }

            files.Add(outputs[index].File with { Name = name });
        }

        return files;
    }

    // The name of one channel's file by the form of channel_files.
    private static string NameOf(ChannelOutput output, JobManifest job, MachineConfig machine)
    {
        string extension = Path.GetExtension(output.File.Name);
        string stem = Path.GetFileNameWithoutExtension(output.File.Name);
        return machine.Format?.ChannelFiles switch
        {
            ChannelFiles.PathSuffix => PathName(output, stem, machine),
            ChannelFiles.ChannelSuffix => WithChannel((job.Name ?? stem) + extension, output.Channel),
            ChannelFiles.ProgramName => (output.Program.Name ?? stem) + extension,
            _ => output.File.Name,
        };
    }

    // The memory card convention of Nakamura (controller-mapping 7, CHANNEL; controllers fanuc.md 1): the program O1000
    // of the first channel of [machine] channels is the file O1000, the program of channel n the file O1000.P-n; a
    // program without NUMBER takes the name of its file instead of the O number.
    private static string PathName(ChannelOutput output, string stem, MachineConfig machine)
    {
        string name = output.Program.Number is int number
            ? "O" + number.ToString(CultureInfo.InvariantCulture).PadLeft(LeastDigits, '0')
            : stem;
        int firstChannel = machine.Machine.Channels.Count > 0 ? machine.Machine.Channels[0] : 1;
        return output.Channel == firstChannel
            ? name
            : name + ".P-" + output.Channel.ToString(CultureInfo.InvariantCulture);
    }

    // The channel suffix of the STAMA files, TEST 5_C1.MPF (controller-mapping 7, CHANNEL).
    private static string WithChannel(string name, int channel)
    {
        return Path.GetFileNameWithoutExtension(name) + "_C" + channel.ToString(CultureInfo.InvariantCulture)
            + Path.GetExtension(name);
    }
}
