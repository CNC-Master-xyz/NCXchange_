namespace Ncx.Core.Machine;

/// <summary>
/// [dynamics]: the control behaviour for the runtime estimate (machine-config 4, D64).
/// </summary>
public sealed record DynamicsConfig
{
    /// <summary>
    /// block_time: the seconds the control needs per block; a short segment cannot be faster; null when left out.
    /// </summary>
    public decimal? BlockTime { get; init; }

    /// <summary>
    /// path_mode: continuous or exact stop; null when left out.
    /// </summary>
    public PathMode? PathMode { get; init; }

    /// <summary>
    /// corner_speed: the mm/min carried through a corner in continuous mode; null when left out.
    /// </summary>
    public decimal? CornerSpeed { get; init; }

    /// <summary>
    /// rapid_exact_stop: whether the end of a RAPID is a rest point of the path, overriding the family default, true
    /// on a Fanuc-family machine (G00 in-position check) and false otherwise (D286); null when left out.
    /// </summary>
    public bool? RapidExactStop { get; init; }
}
