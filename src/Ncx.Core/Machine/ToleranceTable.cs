namespace Ncx.Core.Machine;

/// <summary>
/// [tolerance]: the templates of the TOLERANCE words (machine-config 5, D85).
/// </summary>
public sealed record ToleranceTable
{
    /// <summary>
    /// ON: written for TOLERANCE=value while TOLERANCE:ROTARY is not set, or on a machine without ON_ROTARY, "G5.1 Q1",
    /// "CYCLE832({tol},{mode},1)"; it has no {rotary} (D151); null when left out.
    /// </summary>
    public string? On { get; init; }

    /// <summary>
    /// ON_ROTARY: written instead of ON while TOLERANCE:ROTARY is set, "CYCLE832({tol},1{mode},{rotary})", the cycle 32
    /// with TA{rotary} (D151); null when left out, and then ON is written and the rotary tolerance reported (D367).
    /// </summary>
    public string? OnRotary { get; init; }

    /// <summary>
    /// OFF: written for TOLERANCE=OFF, "G5.1 Q0"; null when left out.
    /// </summary>
    public string? Off { get; init; }

    /// <summary>
    /// mode: the value of {mode} per TOLERANCE_MODE, FINISH = "0", ROUGH = "1"; empty when left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> Mode { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// held_in: a short phrase naming where the control holds a value the written template has no placeholder for,
    /// "parameter 19581", which the compiler names in CMP015 (D367); null when left out.
    /// </summary>
    public string? HeldIn { get; init; }

    /// <summary>
    /// held_value: the tolerance in millimetres the control holds where held_in says, zero or greater; a value at
    /// least as tight as the program's TOLERANCE makes CMP015 an INFO, an unknown or looser one a WARNING (D367,
    /// designed with the control-held keys of D357); null when left out.
    /// </summary>
    public decimal? HeldValue { get; init; }

    /// <summary>
    /// held_comment: the compiler also writes a comment beside the switch naming the program's value and held_in,
    /// "G5.1 Q1 (TOL 0.005 P19581)", the machine-file option of D367; false when left out.
    /// </summary>
    public bool HeldComment { get; init; }
}
