namespace Ncx.Compilers;

/// <summary>
/// What a family writes for a STOCK block (D351; machine-config 5): the [stock] template of its form and the numbers
/// of its placeholders, which the family formats as it writes coordinates, and the letter of the tool axis for {axis}.
/// </summary>
public sealed record StockForm
{
    /// <summary>
    /// The template, "BLK FORM 0.1 {axis} X{x} Y{y} Z{z}\nBLK FORM 0.2 X{max_x} Y{max_y} Z{max_z}".
    /// </summary>
    public required string Template { get; init; }

    /// <summary>
    /// The name of the template for messages, "[stock] BOX (machine-config 5, D351)".
    /// </summary>
    public required string What { get; init; }

    /// <summary>
    /// The letter of the tool axis of the active WORKPLANE, Z under XY, which {axis} writes.
    /// </summary>
    public required string Axis { get; init; }

    /// <summary>
    /// The numbers of the placeholders: the two corners and, from them, radius, diameter, length and dist.
    /// </summary>
    public required IReadOnlyList<StockNumber> Numbers { get; init; }
}
