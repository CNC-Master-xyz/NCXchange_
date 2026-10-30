using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The numbers of a Klartext source as NCX writes them: taken as written where the source gives them, the comma or the
/// dot as the decimal separator (language 2 rule 5; controllers heidenhain.md 7 rule 8), and in the shortest form of
/// language 3 where the reader computes one, an absolute coordinate from a surface and a depth, a point from a pole.
/// </summary>
internal static class HeidenhainNumbers
{
    /// <summary>
    /// A computed number as an NCX number: an integer when it has no fraction, a decimal without trailing zeros
    /// otherwise, 5.000 is 5 and -21.7320 is -21.732 (language 3; ComputedNumbers.Of).
    /// </summary>
    /// <param name="number">The number.</param>
    public static Value Of(decimal number)
    {
        return ComputedNumbers.Of(number);
    }

    // D403 extends D121 to the readers: a number the reader computes with trigonometry, a point of a polar coordinate,
    // the centre of a tangential arc, a chamfer or rounding point, keeps six decimals, half away from zero, whatever
    // the decimals of the source words it comes from (ComputedNumbers).

    /// <summary>
    /// A number of the geometry, computed in double, rounded to six decimals, half away from zero (D403;
    /// ComputedNumbers.Round).
    /// </summary>
    /// <param name="number">The computed number.</param>
    public static decimal Round(double number)
    {
        return ComputedNumbers.Round(number);
    }

    /// <summary>
    /// The number of an NCX number value; null for any other value, an expression among them.
    /// </summary>
    /// <param name="value">The value of a word.</param>
    public static decimal? NumberOf(Value? value)
    {
        return value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue number => number.Number,
            _ => null,
        };
    }

    /// <summary>
    /// A number as Klartext writes it, a sign in front and the comma or the dot as the decimal separator: "+10",
    /// "-6,964", "1.5"; null for any other text (controllers heidenhain.md 1, 7 rule 8).
    /// </summary>
    /// <param name="text">The text after the address.</param>
    public static decimal? Parse(string text)
    {
        string number = text.Replace(',', '.');
        bool hasDigit = false;
        foreach (char character in number)
        {
            hasDigit |= char.IsAsciiDigit(character);
        }

        return hasDigit && decimal.TryParse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : null;
    }
}
