namespace Ncx.Core.Machine;

/// <summary>
/// [cycles]: the cycle catalog of the controller family that the machine uses, and the distances and options of the
/// control's own cycles that the VM, the runtime estimate and the readers take from the machine (machine-config 6).
/// </summary>
public sealed record CyclesConfig
{
    /// <summary>
    /// catalog: the catalog file, "heidenhain.toml", shipped with NCXchange as &lt;family&gt;.toml and
    /// user-extendable, a shop catalog under any name (D156); null when left out.
    /// </summary>
    public string? CatalogFile { get; init; }

    /// <summary>
    /// surface_clearance: the gap between the Fanuc R plane and the workpiece surface; the Fanuc reader writes
    /// SURFACE = R - surface_clearance, SURFACE = 0 when left out (D158). Millimetres, zero or greater.
    /// </summary>
    public decimal? SurfaceClearance { get; init; }

    /// <summary>
    /// chip_break_lift: the distance CHIP_BREAK lifts before it feeds on (Fanuc parameter 5114, Heidenhain Q256,
    /// Siemens _VRT); null when left out, which means the family's behaviour (D357). Millimetres, zero or greater.
    /// </summary>
    public decimal? ChipBreakLift { get; init; }

    /// <summary>
    /// peck_clearance: the distance above the depth reached at which PECK ends its rapid re-entry (Fanuc parameter
    /// 5115, Siemens _DIS1); null when left out, which means the family's behaviour (D357). Millimetres, zero or
    /// greater.
    /// </summary>
    public decimal? PeckClearance { get; init; }

    /// <summary>
    /// ream_feed_out: the plane up to which REAM feeds out under CYCLE_RETRACT=SAFE; null when left out, which means
    /// the family default, CLEARANCE on Fanuc and Heidenhain and SAFE on Siemens (D378, revising D190).
    /// </summary>
    public ReamFeedOut? ReamFeedOut { get; init; }

    /// <summary>
    /// contour_type_ii: the control has the type II pocketing option of G71/G72 (D278, D388 item 11); false when left
    /// out.
    /// </summary>
    public bool ContourTypeII { get; init; }

    /// <summary>
    /// peck_rigid_tapping: the control has peck rigid tapping, Q on G74/G84 (D279, D388 item 11); false when left
    /// out.
    /// </summary>
    public bool PeckRigidTapping { get; init; }
}
