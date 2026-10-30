using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers;

/// <summary>
/// The numbers a reader computes, a polar point, the centre of a tangential arc, a chamfer or rounding point, a
/// spatial angle, a pitch F or S (D403): rounded to the six decimals of the virtual machine, half away from zero, and
/// written in the shortest form of language 3, trailing zeros dropped. D403 extends D121 to the readers, so that
/// reader and virtual machine keep one precision; the compiler's [format] decimals alone decide the digits written to
/// the control.
/// </summary>
internal static class ComputedNumbers
{
    /// <summary>
    /// The decimals a computed number keeps: the six of the virtual machine, Vec3.VmDecimals of Ncx.Core (D121, D403).
    /// </summary>
    public const int Decimals = 6;

    /// <summary>
    /// A number computed in double, rounded to six decimals, half away from zero, the one rounding NCX defines
    /// (language 4.12, ROUND; D403).
    /// </summary>
    /// <param name="number">The computed number.</param>
    public static decimal Round(double number)
    {
        return Math.Round((decimal)number, Decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// A number computed in decimal, rounded to six decimals, half away from zero (D403).
    /// </summary>
    /// <param name="number">The computed number.</param>
    public static decimal Round(decimal number)
    {
        return Math.Round(number, Decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// A number as an NCX number in the shortest form of language 3: an integer when it has no fraction, a decimal
    /// without trailing zeros otherwise, 5.000 is 5 and -21.7320 is -21.732, -0 is 0. The number is written as it is;
    /// Round gives a computed one its six decimals first.
    /// </summary>
    /// <param name="number">The number.</param>
    public static Value Of(decimal number)
    {
        string text = number.ToString("0.############################", CultureInfo.InvariantCulture);
        if (text == "-0")
        {
            text = "0";
        }

        return text.Contains('.', StringComparison.Ordinal)
            ? new DecimalValue(decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture), text)
            : new IntegerValue(long.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), text);
    }
}
