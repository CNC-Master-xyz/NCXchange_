namespace Ncx.Core.Machine;

/// <summary>
/// [machine]: which machine, which controller and dialect, and the default resources that NCX words without a role
/// address resolve to (machine-config 1).
/// </summary>
public sealed record MachineIdentity
{
    /// <summary>
    /// name: the machine as the shop calls it, "DMU 50".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// controller: selects the reader and the compiler (machine-config 1). Null only on the built-in default machine
    /// of D103, which serves no reader and no compiler (D77).
    /// </summary>
    public Controller? Controller { get; init; }

    /// <summary>
    /// dialect: selects the variant tables of reader and compiler, "iTNC530", "31i-B"; null when the file leaves it
    /// out.
    /// </summary>
    public string? Dialect { get; init; }

    /// <summary>
    /// builder: the machine builder id used by RAW:DMG and by the builder function tables, "dmg"; null when the file
    /// leaves it out.
    /// </summary>
    public string? Builder { get; init; }

    /// <summary>
    /// gcode_system: A, B or C, for Fanuc lathes only; null when the file leaves it out.
    /// </summary>
    public GcodeSystem? GcodeSystem { get; init; }

    /// <summary>
    /// s_binds_to_spindle_word: Fanuc has one S word, owned by the spindle of the M code in the same block; false when
    /// the file leaves it out.
    /// </summary>
    public bool SBindsToSpindleWord { get; init; }

    /// <summary>
    /// channels: the channel numbers of the machine, [1, 2]; empty when the file leaves it out.
    /// </summary>
    public IReadOnlyList<int> Channels { get; init; } = [];

    /// <summary>
    /// units_default: MM or INCH, a value of the UNITS word (language 4.2); null when the file leaves it out.
    /// </summary>
    // TODO: UnitsDefault is kept as the word MM or INCH; it takes the units enumeration of the channel state once the
    // virtual machine defines it (P1-01, code-guidelines 7).
    public string? UnitsDefault { get; init; }

    /// <summary>
    /// default_spindle: the resource id that SPINDLE, RPM and the other spindle words without a role address target
    /// (virtual machine 3.8 rule 2); null when the file leaves it out.
    /// </summary>
    public string? DefaultSpindle { get; init; }

    /// <summary>
    /// default_holder: the resource id that TOOL and PRELOAD without a role address target (virtual machine 3.8 rule
    /// 2); null when the file leaves it out.
    /// </summary>
    public string? DefaultHolder { get; init; }

    /// <summary>
    /// default_workpiece: the resource id of the workpiece holder at program start (virtual machine 2.1); null when the
    /// file leaves it out.
    /// </summary>
    public string? DefaultWorkpiece { get; init; }

    /// <summary>
    /// arc_tolerance: the arc tolerance of D36 in millimetres, like rapid and max_feed, converted for an INCH program;
    /// null when the file leaves it out, and then the D36 defaults per unit apply, 0.01 mm and 0.0005 in (D139,
    /// virtual machine 3.2).
    /// </summary>
    public decimal? ArcTolerance { get; init; }

    /// <summary>
    /// arc_radius_tolerance: how far the radius from the written start and the radius to the written end of an arc
    /// may differ before the compiler reports CMP030, in millimetres, converted for an INCH program; null when the
    /// file leaves it out, and then one output step of the centre's address applies (D364). Fanuc parameter 3410 and
    /// the Heidenhain circleDeviation (7431 on the iTNC 530) are the control's own form of this limit (Fanuc 30i-B
    /// parameter manual B-64490EN-05 p.196; TNC 640 technical manual 892899-18 p.2636, cnc-manuals).
    /// </summary>
    public decimal? ArcRadiusTolerance { get; init; }

    /// <summary>
    /// dwell_increment: the seconds per unit of a Fanuc G4 P, also of G4 X/U without a decimal point and of the P of
    /// the canned cycles; 0.001 when the file leaves it out (D159). The unit follows the increment system, or is 1 ms
    /// under parameter 1015#7 DWT; NCX assumes 3405#1 DWL = 0, a dwell in seconds (Fanuc 30i-B operator's manual
    /// B-64484EN p.188, parameter manual B-64490EN-05 p.59 and p.195, cnc-manuals).
    /// </summary>
    public decimal DwellIncrement { get; init; } = 0.001m;

    /// <summary>
    /// input_increment: the least input increment of a Fanuc control in mm, from the increment system "IS-A", "IS-B"
    /// or "IS-C" of parameter 1013 (B-64490EN-05 p.57-58): 0.01, 0.001 or 0.0001 mm, a tenth of it in inch, the same
    /// number in degrees on a rotary axis; 0.001, IS-B, when the file leaves it out (controllers fanuc.md 2).
    /// </summary>
    public decimal InputIncrement { get; init; } = 0.001m;

    /// <summary>
    /// The least input increment of a linear address in the given units: <see cref="InputIncrement"/> in mm, a tenth
    /// of it in inch (B-64484EN p.527-528, NOTE 2).
    /// </summary>
    /// <param name="inch">True under inch input.</param>
    public decimal LinearIncrement(bool inch)
    {
        return inch ? InputIncrement / 10m : InputIncrement;
    }

    /// <summary>
    /// base_units: the basic system of a Siemens control, MD10240 $MN_SCALING_SYSTEM_IS_METRIC, as a value of UNITS,
    /// MM or INCH; null when the file leaves it out, and then units_default stands for it (D292; SINUMERIK
    /// 840D sl/828D function manual basic functions 2012 p.346, cnc-manuals). See <see cref="ResolvedBaseUnits"/>.
    /// </summary>
    public string? BaseUnits { get; init; }

    /// <summary>
    /// The basic system the Siemens compiler and reader work against: base_units, or units_default without it (D292).
    /// </summary>
    public string? ResolvedBaseUnits => BaseUnits ?? UnitsDefault;

    /// <summary>
    /// extended_units: MD10260 $MN_CONVERT_SCALING_SYSTEM = 1, G700 and G710 are available; false when the file leaves
    /// it out (D292; SINUMERIK 840D sl programming manual NC programming SW4.94 p.158, cnc-manuals).
    /// </summary>
    public bool ExtendedUnits { get; init; }
}
