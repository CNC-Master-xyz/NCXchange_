using Ncx.Core.Machine;

namespace Ncx.Compilers;

/// <summary>
/// The program_layout a compile writes (machine-config 2, D48): the key of [format] where the machine file writes it,
/// else the default of the controller family, file_per_program for Heidenhain, where one program is one file
/// (controllers heidenhain.md 1, 8 rule 6; language 4.13), and one_file for Fanuc O programs and Siemens %_N_ units
/// (row W3-3 of 03-open-questions; as TemplateSet takes the comma as the Heidenhain decimal separator).
/// </summary>
internal static class ProgramLayouts
{
    /// <summary>
    /// The program_layout of the machine, written or the family default.
    /// </summary>
    public static ProgramLayout Of(MachineConfig machine)
    {
        if (machine.Format?.ProgramLayout is ProgramLayout written)
        {
            return written;
        }

        return machine.Machine.Controller == Controller.Heidenhain
            ? ProgramLayout.FilePerProgram
            : ProgramLayout.OneFile;
    }
}
