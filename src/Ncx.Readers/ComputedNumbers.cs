namespace Ncx.Readers;

/// <summary>
/// The precision of a number a reader computes, a point of a polar coordinate, the centre of a tangential arc, the
/// points of a chamfer or a rounding, a spatial angle, a pitch F / S: the one fixed precision of the virtual machine,
/// 6 decimals rounded half away from zero, in MM and INCH alike, whatever the decimals of the source words (D403,
/// extending D121 to the readers). The compiler's [format] decimals alone decide the digits written to the control.
/// </summary>
internal static class ComputedNumbers
{
    /// <summary>The decimals a computed number keeps (D403, D121).</summary>
    public const int Decimals = 6;

    /// <summary>
    /// A number of the geometry, computed in double, rounded to 6 decimals, half away from zero, the one rounding NCX
    /// defines (language 4.12, ROUND; D403).
    /// </summary>
    /// <param name="number">The computed number.</param>
    public static decimal Round(double number)
    {
        return Round((decimal)number);
    }

    /// <summary>
    /// A computed decimal rounded to 6 decimals, half away from zero (language 4.12, ROUND; D403).
    /// </summary>
    /// <param name="number">The computed number.</param>
    public static decimal Round(decimal number)
    {
        return Math.Round(number, Decimals, MidpointRounding.AwayFromZero);
    }
}
