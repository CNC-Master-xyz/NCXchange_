using System.Text.RegularExpressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The axis words of a block and how a Fanuc control writes them (language 4.3; machine-config 4; controller-mapping
/// 2): the address letter of [[axis]], the incremental letter of G-code system A, and the diameter of an X axis
/// programmed in diameters (D28, D60).
/// </summary>
internal static partial class FanucAxes
{
    // The standard axes in the order a block writes them (language 5 rule 6, bucket 3).
    private static readonly string[] s_standardAxes = ["X", "Y", "Z", "A", "B", "C"];

    /// <summary>
    /// The axis words of a block, the standard axes in their order and then the machine axes by name (language 5 rule
    /// 6, bucket 3; D93).
    /// </summary>
    /// <param name="block">The block.</param>
    public static List<FanucAxisWord> Of(Block block)
    {
        var axes = new List<FanucAxisWord>();
        foreach (Word word in block.Words)
        {
            if (word.Addr is null && AxisForm().IsMatch(word.Key) && word.Key != "IF")
            {
                bool incremental = word.Key.Length > 1 && word.Key[0] == 'I';
                axes.Add(new FanucAxisWord(incremental ? word.Key.Substring(1) : word.Key, incremental, word));
            }
        }

        axes.Sort((first, second) => RankOf(first.Axis).CompareTo(RankOf(second.Axis)));
        return axes;
    }

    /// <summary>
    /// The address the machine writes an axis with: the letter of [[axis]], or in G-code system A the incremental
    /// letter for an incremental word (machine-config 4; controllers fanuc.md 3); null for an incremental word on an
    /// axis without one in system A.
    /// </summary>
    /// <param name="write">The block being written.</param>
    /// <param name="axis">The NCX name of the axis.</param>
    /// <param name="incremental">True for the incremental form.</param>
    public static string? LetterOf(FanucBlock write, string axis, bool incremental)
    {
        AxisDef? definition = Resolve(write, axis);
        if (incremental && write.System == GcodeSystem.A)
        {
            return definition?.IncrementalLetter;
        }

        return definition?.Letter ?? axis;
    }

    /// <summary>
    /// The factor from a word of the program to the value the machine writes: an X word is a diameter under
    /// DIAMETER=ON, and the machine writes diameters on an X axis programmed in diameters, or switchable and switched
    /// on (language 4.2 DIAMETER; D28, D60); 1 for every other axis.
    /// </summary>
    public static decimal WordFactor(FanucBlock write, string axis)
    {
        if (axis != "X")
        {
            return 1m;
        }

        bool programDiameter = write.After.Frame.Diameter;
        bool machineDiameter = WritesDiameter(write);
        return programDiameter == machineDiameter ? 1m : machineDiameter ? 2m : 0.5m;
    }

    /// <summary>
    /// The factor from a value of the virtual machine, which stores radii (D28), to the value the machine writes.
    /// </summary>
    public static decimal StateFactor(FanucBlock write, string axis)
    {
        return axis == "X" && WritesDiameter(write) ? 2m : 1m;
    }

    // The X axis is written in diameters where it is programmed so, or switchable and switched on (D60).
    private static bool WritesDiameter(FanucBlock write)
    {
        return write.Machine.ResolveAxis("X")?.Programming switch
        {
            Programming.Diameter => true,
            Programming.Switchable => write.After.Frame.Diameter,
            _ => false,
        };
    }

    /// <summary>
    /// The letter of the rotary axis of the workpiece holder, which G7.1 names (D146): the axis its [[resource]] names
    /// or a rotary axis it owns, a table's among them; C where the machine names none.
    /// </summary>
    public static string CylinderLetter(FanucBlock write)
    {
        return CylinderAxis(write)?.Letter ?? "C";
    }

    // An axis name of a program resolves through [[axis]]; A, B and C name the rotary axis of the workpiece holder
    // (virtual machine 3.8 rule 3). D146: under CYLINDER the C word is the length on the circumference of the rotary
    // axis of the workpiece holder, and the compiler writes that axis's letter, never the NCX letter.
    private static AxisDef? Resolve(FanucBlock write, string axis)
    {
        if (axis == "C" && write.After.Frame.Cylinder is not null && CylinderAxis(write) is AxisDef rotary)
        {
            return rotary;
        }

        return write.After.Frame.WorkpieceHolder is string holder
            ? write.Machine.ResolveAxis(axis, holder)
            : write.Machine.ResolveAxis(axis);
    }

    // The rotary axis of the workpiece holder after the block (machine-config 4, D146).
    private static AxisDef? CylinderAxis(FanucBlock write)
    {
        if (write.After.Frame.WorkpieceHolder is not string holder)
        {
            return null;
        }

        if (write.Machine.FindResource(holder)?.Axis is string id && write.Machine.FindAxis(id) is AxisDef named
            && named.Kind == AxisKind.Rotary)
        {
            return named;
        }

        return write.Machine.Axes.FirstOrDefault(axis => axis.Kind == AxisKind.Rotary && axis.Owner == holder);
    }

    private static int RankOf(string axis)
    {
        int index = Array.IndexOf(s_standardAxes, axis);
        return index >= 0 ? index : s_standardAxes.Length + axis[0] * 100 + (axis.Length > 1 ? axis[1] : 0);
    }

    // An axis word: a standard axis, or a machine axis of the form of D93, with I in front for the incremental form.
    [GeneratedRegex("^I?[XYZABCUVW][0-9]{0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex AxisForm();
}
