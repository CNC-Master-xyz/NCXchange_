namespace Ncx.Core.Machine;

/// <summary>
/// One measurement point of a tool as the shop measured it, a [[tool.point]] row of the tool table (D376): the point
/// k, its tip offset a, its register radius r and the wear the control adds to r, all in the machine's units_default
/// (machine-config 1). The compiler compares a and r with the program (CMP032), the virtual machine adds the wear to
/// the radius it reports (D374); nothing moves a coordinate.
/// </summary>
public sealed record ToolPointData
{
    /// <summary>
    /// k: the point, from 1.
    /// </summary>
    public required int K { get; init; }

    /// <summary>
    /// tip_offset: the axial offset a of the point from the tool tip, not negative.
    /// </summary>
    public required decimal TipOffset { get; init; }

    /// <summary>
    /// radius: the register radius r of the point, 0 by default.
    /// </summary>
    public decimal Radius { get; init; }

    /// <summary>
    /// wear: what the control adds to the register radius, 0 by default (D374).
    /// </summary>
    public decimal Wear { get; init; }
}
