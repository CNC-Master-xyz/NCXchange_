using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The numbers of a Fanuc source as NCX writes them: taken as written where the source gives them (language 2 rule 5,
/// controllers fanuc.md 9 rule 8), and in the shortest form of language 3 where the reader computes one, an absolute
/// clearance from an incremental R, the centre of an arc from its start and I J K.
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

    // TODO(question): no document says how many decimals a number the reader computes with trigonometry keeps, a point
    // of a polar coordinate, a chamfer, a rounding or a spatial angle (wave-1 question #46); the caller keeps as many
    // as the source words it was computed from, at least three.

    /// <summary>
    /// A number of the geometry, computed in double, rounded to a decimal: half away from zero, the one rounding NCX
    /// defines (language 4.12, ROUND).
    /// </summary>
    /// <param name="number">The computed number.</param>
    /// <param name="decimals">The decimals to keep.</param>
    public static decimal Round(double number, int decimals)
    {
        return Math.Round((decimal)number, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The digits a source number has after its decimal point: 3 for X50.534, 0 for X70. and X70.
    /// </summary>
    /// <param name="word">The source word with a number.</param>
    public static int DecimalsOf(SourceWord word)
    {
        int dot = word.Text.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? 0 : word.Text.Length - dot - 1;
    }

    /// <summary>
    /// The least input increment of a linear address in the units of the block, 0.001 mm or 0.0001 inch: the unit of
    /// an address that takes no decimal point (B-64484EN p.229, 8.3; p.527-528, the increment system IS-B), the
    /// increment system NCX assumes for every family (machine-config 2, decimals; parameter 1004#7 IPR is not
    /// modelled).
    /// </summary>
    /// <param name="block">The block being read, whose source-side state gives G20 or G21 (G70 or G71 in system
    /// C).</param>
    public static decimal LeastInputIncrement(FanucBlock block)
    {
        string inch = block.System == GcodeSystem.C ? "G70" : "G20";
        return block.State.ActiveCode(FanucModalGroups.Units) == inch ? 0.0001m : 0.001m;
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
