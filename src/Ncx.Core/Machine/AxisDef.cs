namespace Ncx.Core.Machine;

/// <summary>
/// One [[axis]]: its names, kind and owner, the limits and reference points in machine coordinates (the G53 / M91
/// frame), and the dynamics for the runtime estimate (machine-config 4, D100).
/// </summary>
public sealed record AxisDef
{
    /// <summary>
    /// id: the machine's own name of the axis, X1, C2.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// ncx: the name NCX programs use for the axis, X, Z2 (D30, D93).
    /// </summary>
    public required string NcxName { get; init; }

    /// <summary>
    /// letter: the address written in the output, X, "C2" (D30); null when left out.
    /// </summary>
    public string? Letter { get; init; }

    /// <summary>
    /// incremental_letter: the incremental form on Fanuc lathes, U for X; null when left out.
    /// </summary>
    public string? IncrementalLetter { get; init; }

    /// <summary>
    /// kind: linear or rotary.
    /// </summary>
    public required AxisKind Kind { get; init; }

    /// <summary>
    /// owner: the resource id the axis belongs to, S1 for the C axis of the main spindle; null when left out.
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// modulo: true on a rotary axis whose limits are its display range and not a travel limit, so that they are
    /// never compared, clamped or reported by the travel-limit analytic (machine-config 4, D193).
    /// </summary>
    public bool Modulo { get; init; }

    /// <summary>
    /// limits: the lower travel limit in machine coordinates; for a modulo axis the start of the display range (D100,
    /// D193); null when left out.
    /// </summary>
    public decimal? Min { get; init; }

    /// <summary>
    /// limits: the upper travel limit in machine coordinates; for a modulo axis the end of the display range (D100,
    /// D193); null when left out.
    /// </summary>
    public decimal? Max { get; init; }

    /// <summary>
    /// rapid: the rapid speed in mm/min or deg/min, for the runtime estimate (D64); null when left out.
    /// </summary>
    public decimal? Rapid { get; init; }

    /// <summary>
    /// max_feed: the feed this axis can follow in mm/min or deg/min; the expander warns or clamps (D64); null when
    /// left out.
    /// </summary>
    public decimal? MaxFeed { get; init; }

    /// <summary>
    /// acceleration: in mm/s² or deg/s², for the trapezoidal profile of the runtime estimate (D64); null when left
    /// out.
    /// </summary>
    public decimal? Acceleration { get; init; }

    /// <summary>
    /// programming: diameter, radius or switchable, for the X axis of a lathe (D60); null when left out.
    /// </summary>
    public Programming? Programming { get; init; }

    /// <summary>
    /// parallel: the linear axis X, Y or Z a rotary axis turns about, the cylinder axis of CYLINDER=n while its owner
    /// holds the workpiece; null when left out, read as Z. The rotary axis of a work spindle always turns about Z
    /// (machine-config 4, D146).
    /// </summary>
    public string? Parallel { get; init; }

    /// <summary>
    /// frames_in_diameter: true when the control keeps frame offsets and actual-value variables of this diameter axis
    /// as diameters (Siemens MD20360 $MC_TOOL_PARAMETER_DEF_MASK bits 3 and 6, Funktionshandbuch Grundfunktionen 2012
    /// p. 902-903); false, the default, when they are radii, as on a Siemens facing axis (p. 901-902). Readers and
    /// compilers read it for TRANS X and $AA_IM (machine-config 4, D125, D216, D240, D388).
    /// </summary>
    public bool FramesInDiameter { get; init; }

    /// <summary>
    /// home: reference point 1 in machine coordinates, for HOME (D100); null for an axis without a reference point.
    /// </summary>
    public decimal? Home { get; init; }

    /// <summary>
    /// home2: reference point 2 in machine coordinates, for HOME under POINT=2 (D100); null when left out.
    /// </summary>
    public decimal? Home2 { get; init; }

    /// <summary>
    /// The value of home, home2 or limits as the virtual machine stores it, a radius (D28): the file writes the X
    /// values of an axis the way it is programmed, diameters for programming = "diameter" or "switchable", so the VM
    /// halves them once where it takes them; radii for "radius" or no programming are taken as written (machine-config
    /// 4, D137). [positions] is not converted: {position:NAME} becomes an X word, which the VM halves under
    /// DIAMETER=ON like every X word.
    /// </summary>
    /// <param name="value">home, home2 or one of the limits as the file writes it; null when left out.</param>
    public decimal? AsStoredRadius(decimal? value)
    {
        bool diameters = Programming is Machine.Programming.Diameter or Machine.Programming.Switchable;
        return value is decimal written && diameters ? written / 2m : value;
    }

    /// <summary>
    /// clamp: the clamping templates of a rotary axis, ON = "M10", OFF = "M11"; empty when left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> Clamp { get; init; } = new Dictionary<string, string>();
}
