using System.Text.RegularExpressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The axis words of a block and how a SINUMERIK control writes them (controllers siemens.md 1, 3, 12 rule 2;
/// controller-mapping 2; machine-config 4, 5; language 4.3): the address of [[axis]], with the equals sign after an
/// address with an extension, IC() for an incremental word, DC() for the shortest way of a rotary axis, the diameter of
/// an X axis programmed in diameters, and Z negated on the side of a holder whose datum runs Z the other way.
/// </summary>
internal static partial class SiemensAxes
{
    // The standard axes in the order a block writes them (language 5 rule 6, bucket 3).
    private static readonly string[] s_standardAxes = ["X", "Y", "Z", "A", "B", "C"];

    /// <summary>
    /// The axis words of a block, the standard axes in their order and then the machine axes by name (language 5 rule
    /// 6, bucket 3; D93).
    /// </summary>
    public static List<SiemensAxisWord> Of(Block block)
    {
        var axes = new List<SiemensAxisWord>();
        foreach (Word word in block.Words)
        {
            if (word.Addr is null && AxisForm().IsMatch(word.Key))
            {
                bool incremental = word.Key.Length > 1 && word.Key[0] == 'I';
                axes.Add(new SiemensAxisWord(incremental ? word.Key.Substring(1) : word.Key, incremental, word));
            }
        }

        axes.Sort((first, second) => RankOf(first.Axis).CompareTo(RankOf(second.Axis)));
        return axes;
    }

    /// <summary>
    /// The machine axis an axis name of the program moves: through [[axis]], A, B and C to the rotary axis of the
    /// workpiece holder (virtual machine 3.8 rule 3); null for an axis the machine file does not name.
    /// </summary>
    public static AxisDef? Resolve(SiemensBlock write, string axis)
    {
        return write.After.Frame.WorkpieceHolder is string holder
            ? write.Machine.ResolveAxis(axis, holder)
            : write.Machine.ResolveAxis(axis);
    }

    /// <summary>
    /// The address the machine writes an axis with, the letter of [[axis]]: X, Z2, C2 (machine-config 4; D30).
    /// </summary>
    public static string LetterOf(SiemensBlock write, string axis)
    {
        return CylinderLetter(write, axis) ?? Resolve(write, axis)?.Letter ?? axis;
    }

    /// <summary>
    /// True for the rotary word of the cylinder plane under CYLINDER=n, which TRACYL writes as the linear Y, a length
    /// on the circumference (D146): a word that names the rotary axis of the workpiece holder, the axis C resolves to
    /// (virtual machine 3.8 rule 3). Another rotary word under CYLINDER is no length of this cylinder (ReportCylinder).
    /// </summary>
    public static bool IsCylinderLength(SiemensBlock write, string axis)
    {
        return write.After.Frame.Cylinder is not null
            && Resolve(write, axis) is { Kind: AxisKind.Rotary } named
            && Resolve(write, "C") is AxisDef holderAxis && named.Id == holderAxis.Id;
    }

    /// <summary>
    /// True for a rotary axis word, whatever axis it names (machine-config 4).
    /// </summary>
    public static bool IsRotary(SiemensBlock write, string axis)
    {
        return Resolve(write, axis)?.Kind == AxisKind.Rotary;
    }

    // D146: under TRACYL the control takes the developed surface in the axes of the transformation: Y around the
    // circumference, Z along the cylinder axis and X the infeed perpendicular to it (840D sl/828D Arbeitsvorbereitung
    // 2013, connector p.352-353; the example writes N100 G1 Y10 for a groove parallel to the circumference). The NCX
    // C word, a length on the circumference (D102), is Y; on a cylinder about X or Y (a table axis with [[axis]]
    // parallel) the cylinder axis is written as Z and the radial Z as X. A compiler never passes the NCX letter
    // through.
    private static string? CylinderLetter(SiemensBlock write, string axis)
    {
        if (write.After.Frame.Cylinder is null)
        {
            return null;
        }

        if (IsCylinderLength(write, axis))
        {
            return "Y";
        }

        string cylinderAxis = write.After.Frame.CylinderAxis;
        if (cylinderAxis == "Z")
        {
            return null;
        }

        return axis == cylinderAxis ? "Z" : axis == "Z" ? "X" : null;
    }

    /// <summary>
    /// The address of [format] decimals for an axis address: the address itself where decimals lists it, else its
    /// letter without the extension, Z for Z2 (machine-config 2).
    /// </summary>
    public static string DecimalsAddress(SiemensBlock write, string letter)
    {
        return write.Numbers.DecimalsOf(letter) is not null || letter.Length == 0 ? letter : letter.Substring(0, 1);
    }

    /// <summary>
    /// The factor from a word of the program to the value the machine writes: an X word is a diameter under
    /// DIAMETER=ON, and the machine writes diameters on an X axis programmed in diameters, or switchable and switched
    /// on (language 4.2 DIAMETER; machine-config 4; D28, D60); a Z word is negated on a side whose datum runs Z the
    /// other way (machine-config 5, SUB_frame; D57); 1 for every other axis.
    /// </summary>
    public static decimal WordFactor(SiemensBlock write, string axis)
    {
        if (axis == "Z")
        {
            return IsZNegated(write) ? -1m : 1m;
        }

        if (axis != "X")
        {
            return 1m;
        }

        bool programDiameter = write.After.Frame.Diameter;
        bool machineDiameter = WritesDiameter(write);
        return programDiameter == machineDiameter ? 1m : machineDiameter ? 2m : 0.5m;
    }

    /// <summary>
    /// The factor from a value of the virtual machine, which stores radii (D28), to the value the machine writes, with
    /// Z negated as for a word.
    /// </summary>
    public static decimal StateFactor(SiemensBlock write, string axis)
    {
        if (axis == "Z")
        {
            return IsZNegated(write) ? -1m : 1m;
        }

        return axis == "X" && WritesDiameter(write) ? 2m : 1m;
    }

    /// <summary>
    /// The factor from a word of a SHIFT to the value of TRANS, ATRANS or the _X0 of CYCLE800: as for a motion word,
    /// except on a diameter X axis whose frames the control keeps in radii, which takes the radius of the diameter
    /// the program wrote (D125: "a Siemens reader keeps TRANS X as a radius unless the machine file sets the MD20360
    /// bit 3 equivalent"; [[axis]] frames_in_diameter, D388; Funktionshandbuch Grundfunktionen 2012 p.901-903).
    /// </summary>
    public static decimal FrameWordFactor(SiemensBlock write, string axis)
    {
        if (axis != "X")
        {
            return WordFactor(write, axis);
        }

        // MD20360 bit 3: frames of a facing axis are diameters "ohne Umrechnung", under DIAMOF too (Funktionshandbuch
        // Grundfunktionen 2012 p.903), so a radius word is doubled; without it they are radii, so a diameter word is
        // halved.
        bool diameterWord = write.After.Frame.Diameter;
        return FramesInDiameter(write) ? (diameterWord ? 1m : 2m) : (diameterWord ? 0.5m : 1m);
    }

    /// <summary>
    /// The factor from a frame value of the virtual machine, a radius (D28), to the value of TRANS or ATRANS: 2 only
    /// where the X axis keeps its frames in diameters (D125, D388).
    /// </summary>
    public static decimal FrameStateFactor(SiemensBlock write, string axis)
    {
        if (axis != "X")
        {
            return StateFactor(write, axis);
        }

        return FramesInDiameter(write) ? 2m : 1m;
    }

    /// <summary>
    /// The factor from a machine-frame actual value of X, $AA_IM[X], to the NCX value: 2 under DIAMETER=ON on an X axis
    /// written in diameters whose control keeps actual values in radii (MD20360 bits 6 and 7 off, [[axis]]
    /// frames_in_diameter = false), 1/2 under DIAMETER=OFF where they are diameters (frames_in_diameter = true), else 1
    /// (D240, D388; Funktionshandbuch Grundfunktionen 2012 p.901-903).
    /// </summary>
    public static decimal ActualValueFactor(SiemensBlock write)
    {
        // MD20360 bits 6/7 set: actual values are diameters under DIAMOF too, so a radius of the program is half of
        // them; off: they are radii, so a diameter of the program is twice them.
        bool diameterWord = write.After.Frame.Diameter;
        if (FramesInDiameter(write))
        {
            return diameterWord ? 1m : 0.5m;
        }

        return diameterWord && WritesDiameter(write) ? 2m : 1m;
    }

    /// <summary>
    /// The value of a SHIFT word on an axis as TRANS writes it, with the frame factor of the axis (D125).
    /// </summary>
    public static string? FrameValueOf(SiemensBlock write, string axis, Value value, string letter)
    {
        decimal factor = FrameWordFactor(write, axis);
        string decimals = DecimalsAddress(write, letter);
        return value switch
        {
            IntegerValue integer => Number(write, decimals, integer.Number, factor),
            DecimalValue number => Number(write, decimals, number.Number, factor),
            ExprValue expression => Scaled(SiemensExpressions.Text(write, expression), factor),
            _ => null,
        };
    }

    /// <summary>
    /// True on the side of a workpiece holder whose datum runs Z the other way, SUB_frame = "datum": the virtual
    /// machine keeps the holder's own frame with +Z out of its chuck, and the compiler negates Z (machine-config 5;
    /// virtual machine 3.4; D57). A block in the machine frame is not on a side.
    /// </summary>
    public static bool IsZNegated(SiemensBlock write)
    {
        return !write.Block.Has("FRAME") && SideFrame(write, write.After) == WorkpieceFrame.Datum;
    }

    /// <summary>
    /// The convention of the side a state works on: the frame of the role of its workpiece holder in [workpiece]; null
    /// for a holder without one.
    /// </summary>
    public static WorkpieceFrame? SideFrame(SiemensBlock write, ChannelSnapshot state)
    {
        if (state.Frame.WorkpieceHolder is not string holder
            || write.Machine.Workpiece is not WorkpieceConfig workpiece)
        {
            return null;
        }

        foreach (KeyValuePair<string, string> role in write.Machine.Roles)
        {
            if (role.Value == holder && workpiece.Frames.TryGetValue(role.Key, out WorkpieceFrame frame))
            {
                return frame;
            }
        }

        return null;
    }

    /// <summary>
    /// An axis word as the control writes it: X42, Z2=-58, X=IC(5), C=DC(90), X=R1*2 (controllers siemens.md 1, 3;
    /// controller-mapping 2); null, with the ERROR of the expression, where its value cannot be written.
    /// </summary>
    public static string? Word(SiemensBlock write, SiemensAxisWord axis)
    {
        string letter = LetterOf(write, axis.Axis);
        string? value = ValueOf(write, axis.Axis, axis.Word.Value, letter);
        if (value is null)
        {
            return null;
        }

        // DC() takes the shortest way of a rotary axis where ROTARY_PATH=SHORTEST is active: "rotary axes with DC() by
        // the compiler" (machine-config 5, ROTARY_PATH_SHORTEST; controller-mapping 1, D86).
        bool shortest = !axis.Incremental
            && !IsCylinderLength(write, axis.Axis)
            && write.After.Frame.RotaryPath == RotaryPath.Shortest
            && Resolve(write, axis.Axis)?.Kind == AxisKind.Rotary;
        string function = axis.Incremental ? "IC" : shortest ? "DC" : "";
        return Address(letter, value, function, axis.Word.Value is ExprValue);
    }

    /// <summary>
    /// The value of a word on an axis as the machine writes it, with the factor of the axis: a number with the decimals
    /// of the axis address, an expression in its SINUMERIK text.
    /// </summary>
    public static string? ValueOf(SiemensBlock write, string axis, Value value, string letter)
    {
        decimal factor = WordFactor(write, axis);
        string decimals = DecimalsAddress(write, letter);
        return value switch
        {
            IntegerValue integer => Number(write, decimals, integer.Number, factor),
            DecimalValue number => Number(write, decimals, number.Number, factor),
            ExprValue expression => Scaled(SiemensExpressions.Text(write, expression), factor),
            _ => null,
        };
    }

    /// <summary>
    /// An address with its value (controllers siemens.md 1): the single letter with a constant directly, X42; the
    /// equals sign after an address with an extension, after a value that is an expression, and before a function,
    /// Z2=-58, X=R1*2, X=IC(5).
    /// </summary>
    /// <param name="address">The address, X or Z2.</param>
    /// <param name="value">The value as written.</param>
    /// <param name="function">IC, DC, AC, or empty.</param>
    /// <param name="expression">True when the value is an expression.</param>
    public static string Address(string address, string value, string function, bool expression)
    {
        if (function.Length > 0)
        {
            return address + "=" + function + "(" + value + ")";
        }

        return address.Length == 1 && !expression ? address + value : address + "=" + value;
    }

    // A number with the factor of its axis; a computed value is rounded to the decimals of its address.
    private static string Number(SiemensBlock write, string address, decimal number, decimal factor)
    {
        return factor == 1m ? write.Format(address, number) : write.Format(address, number * factor);
    }

    // An expression with the factor of its axis.
    private static string? Scaled(string? text, decimal factor)
    {
        if (text is null || factor == 1m)
        {
            return text;
        }

        return factor switch
        {
            -1m => "-(" + text + ")",
            2m => "(" + text + ")*2",
            _ => "(" + text + ")/2",
        };
    }

    // The frames of X are diameters where the machine sets frames_in_diameter, whatever DIAMON says (MD20360 bit 3;
    // D125, D388); a radius otherwise, the default of the control.
    private static bool FramesInDiameter(SiemensBlock write)
    {
        return write.Machine.ResolveAxis("X")?.FramesInDiameter == true;
    }

    // The X axis is written in diameters where it is programmed so, or switchable and switched on (D60).
    private static bool WritesDiameter(SiemensBlock write)
    {
        return write.Machine.ResolveAxis("X")?.Programming switch
        {
            Programming.Diameter => true,
            Programming.Switchable => write.After.Frame.Diameter,
            _ => false,
        };
    }

    private static int RankOf(string axis)
    {
        int index = Array.IndexOf(s_standardAxes, axis);
        return index >= 0 ? index : s_standardAxes.Length + (axis[0] * 100) + (axis.Length > 1 ? axis[1] : 0);
    }

    // An axis word: a standard axis, or a machine axis of the form of D93, with I in front for the incremental form.
    [GeneratedRegex("^I?[XYZABCUVW][0-9]{0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex AxisForm();
}
