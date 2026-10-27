namespace Ncx.Compilers;

/// <summary>
/// One number of a STOCK template (D351): the placeholder it fills, the address whose [format] decimals it is written
/// with, and its value, already rounded to them.
/// </summary>
public sealed record StockNumber
{
    /// <summary>
    /// The placeholder name: "x", "max_z", "diameter".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The address whose decimals apply: X, Y or Z.
    /// </summary>
    public required string Address { get; init; }

    /// <summary>
    /// The value in the active units.
    /// </summary>
    public required decimal Value { get; init; }
}
