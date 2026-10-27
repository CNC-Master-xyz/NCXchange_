namespace Ncx.Core.Machine;

/// <summary>
/// [transform]: the templates of the transformations, tilted planes and rotary axis options (machine-config 5). An
/// empty template is kept as empty: the target cannot write the word (D82).
/// </summary>
public sealed record TransformTable
{
    /// <summary>
    /// CYLINDER_ON: written for CYLINDER=n, "G7.1 C{r}" with {r} the reference radius (D96); null when left out.
    /// </summary>
    public string? CylinderOn { get; init; }

    /// <summary>
    /// CYLINDER_OFF: written for CYLINDER=OFF, "G7.1 C0"; null when left out.
    /// </summary>
    public string? CylinderOff { get; init; }

    /// <summary>
    /// POLAR_ON: written for POLAR=ON, "G12.1", "TRANSMIT"; null when left out.
    /// </summary>
    public string? PolarOn { get; init; }

    /// <summary>
    /// POLAR_OFF: written for POLAR=OFF, "G13.1"; null when left out.
    /// </summary>
    public string? PolarOff { get; init; }

    /// <summary>
    /// TCPM_ON: written for TCPM=ON, "G43.4 H{offset}", "TRAORI"; null when left out.
    /// </summary>
    public string? TcpmOn { get; init; }

    /// <summary>
    /// TCPM_OFF: written for TCPM=OFF, "G49"; null when left out.
    /// </summary>
    public string? TcpmOff { get; init; }

    /// <summary>
    /// TILT_ON: written for TILT, "G68.2 X{x} Y{y} Z{z} I{a} J{b} K{c}",
    /// "PLANE SPATIAL SPA{a} SPB{b} SPC{c} {move} {rot}" (D269); null when left out.
    /// </summary>
    public string? TiltOn { get; init; }

    /// <summary>
    /// TILT_OFF: written for TILT=RESET, "G69"; null when left out.
    /// </summary>
    public string? TiltOff { get; init; }

    /// <summary>
    /// TILT_AXIS_ON: written for TILT_AXIS, "PLANE AXIAL A{a} B{b} C{c} {move}"; empty when the target needs the
    /// kinematics module (D82); null when left out.
    /// </summary>
    public string? TiltAxisOn { get; init; }

    /// <summary>
    /// TILT_TURN: written after TILT_ON for MOVE=TURN where the control positions the axes in a second block, "G53.1";
    /// null when left out.
    /// </summary>
    public string? TiltTurn { get; init; }

    /// <summary>
    /// ROTARY_PATH_SHORTEST: written for ROTARY_PATH=SHORTEST, "M126" (D86); null when left out.
    /// </summary>
    public string? RotaryPathShortest { get; init; }

    /// <summary>
    /// ROTARY_PATH_FULL: written for ROTARY_PATH=FULL, "M127" (D86); null when left out.
    /// </summary>
    public string? RotaryPathFull { get; init; }

    /// <summary>
    /// ROTARY_FEED_MM_MIN: written for ROTARY_FEED=MM_MIN, "M116" (D86); null when left out.
    /// </summary>
    public string? RotaryFeedMmMin { get; init; }

    /// <summary>
    /// ROTARY_FEED_DEG_MIN: written for ROTARY_FEED=DEG_MIN, "M117" (D86); null when left out.
    /// </summary>
    public string? RotaryFeedDegMin { get; init; }

    /// <summary>
    /// move: the value of {move} per MOVE option, TURN = "TURN FMAX", STAY = "STAY" (Heidenhain); empty when left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> Move { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// rot: the value of {rot} per ROT option, TABLE = "TABLE ROT", COORD = "COORD ROT" (Heidenhain); the ROT of the
    /// block, TABLE when it has none, since an absent ROT means TABLE in NCX (language 4.2, D269); empty when left out,
    /// and then the machine ignores ROT with the WARNING of language 4.2.
    /// </summary>
    public IReadOnlyDictionary<string, string> Rot { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// PATH_MODE_EXACT_STOP: written for PATH_MODE=EXACT_STOP, "G61", "G60"; empty on a control without a modal
    /// switch, Heidenhain Klartext (D349); null when left out. An empty or missing template is a WARNING of the
    /// compiler.
    /// </summary>
    public string? PathModeExactStop { get; init; }

    /// <summary>
    /// PATH_MODE_CONTINUOUS: written for PATH_MODE=CONTINUOUS, "G64" (D349); empty on a control without a modal
    /// switch; null when left out.
    /// </summary>
    public string? PathModeContinuous { get; init; }
}
