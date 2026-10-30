using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The numbers of a SINUMERIK source as NCX writes them: taken as written where the source gives them (language 2 rule
/// 5, controllers siemens.md 11 rule 9), and where the reader computes one, the center of an arc, a point of a
/// pattern, the corner of a chamfer, rounded to the six decimals of the virtual machine and written in the shortest
/// form of language 3 (ComputedNumbers, D403).
/// </summary>
internal static class SiemensNumbers
{
    /// <summary>
    /// A computed number as an NCX number: an integer when it has no fraction, a decimal without trailing zeros
    /// otherwise, 50.000 is 50 and -0.5340 is -0.534 (language 3; ComputedNumbers, D403).
    /// </summary>
    /// <param name="number">The number.</param>
    public static Value Of(decimal number)
    {
        return ComputedNumbers.Of(number);
    }

    /// <summary>
    /// A number of the geometry, computed in double, rounded to the six decimals of the virtual machine, half away
    /// from zero, the one rounding NCX defines (language 4.12, ROUND; D403).
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
    /// A number as written in the source as an NCX number, the digits kept: "70." is 70, "-.534" is -0.534, "+10" is
    /// 10 (language 2 rule 5, language 3); null when the text is no number.
    /// </summary>
    /// <param name="text">The number as written.</param>
    public static Value? Parse(string text)
    {
        return new SourceWord { Address = "", Text = text.Trim() }.ToNcxNumber();
    }

    /// <summary>
    /// A whole number of a text, for counts, spindle numbers and modes; null when the text is no whole number.
    /// </summary>
    /// <param name="text">The number as written.</param>
    public static int? WholeNumber(string text)
    {
        return Parse(text) is IntegerValue integer && integer.Number is >= int.MinValue and <= int.MaxValue
            ? (int)integer.Number
            : null;
    }
}
