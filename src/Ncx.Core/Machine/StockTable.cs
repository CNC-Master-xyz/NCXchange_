namespace Ncx.Core.Machine;

/// <summary>
/// [stock]: the templates of the STOCK verb, one per value form, written for the blank of the control's graphic
/// (machine-config 5, D351). The placeholders are {axis}, {x} {y} {z}, {max_x} {max_y} {max_z}, {radius},
/// {diameter}, {length} and {dist}.
/// </summary>
public sealed record StockTable
{
    /// <summary>
    /// BOX: written for STOCK=BOX, "BLK FORM 0.1 {axis} X{x} Y{y} Z{z}\nBLK FORM 0.2 X{max_x} Y{max_y} Z{max_z}";
    /// null when left out, and then the compiler writes nothing and reports the INFO CMP012.
    /// </summary>
    public string? Box { get; init; }

    /// <summary>
    /// CYLINDER: written for STOCK=CYLINDER, "WORKPIECE(,,,\"CYLINDER\",192,{max_z},{z},{z},{diameter})"; null when
    /// left out, and then the compiler writes nothing and reports the INFO CMP012.
    /// </summary>
    public string? Cylinder { get; init; }
}
