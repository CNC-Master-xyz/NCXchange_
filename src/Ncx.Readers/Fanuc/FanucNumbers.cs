using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The numbers of a Fanuc source as NCX writes them: taken as written where the source gives them (language 2 rule 5,
/// controllers fanuc.md 9 rule 8), and in the shortest form of language 3 where the reader computes one, an absolute
/// clearance from an incremental R, the centre of an arc from its start and I J K; a number computed with
/// trigonometry or a quotient keeps 6 decimals (ComputedNumbers, D403).
/// </summary>
internal static class FanucNumbers
{
    /// <summary>
    /// A computed number as an NCX number: an integer when it has no fraction, a decimal without trailing zeros
    /// otherwise, 50.000 is 50 and -0.5340 is -0.534 (language 3).
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

    /// <summary>
    /// A number computed in double, rounded to the given decimals, half away from zero, the one rounding NCX defines
    /// (language 4.12, ROUND): the scaled F of a helix, which keeps 3 (D354). A point, an angle or a pitch the reader
    /// computes keeps 6 instead (ComputedNumbers, D403).
    /// </summary>
    /// <param name="number">The computed number.</param>
    /// <param name="decimals">The decimals to keep.</param>
    public static decimal Round(double number, int decimals)
    {
        return Math.Round((decimal)number, decimals, MidpointRounding.AwayFromZero);
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
}
