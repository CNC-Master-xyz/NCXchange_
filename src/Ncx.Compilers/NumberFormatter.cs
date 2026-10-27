using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// Writes numbers as the machine takes them, per [format] (machine-config 2): the decimals per address, trailing
/// zeros, the decimal separator, always in the invariant culture (code-guidelines 3.4). NCX never rounds or formats a
/// number and the compiler applies the number format of the target machine (language 2 rule 5): every value is rounded
/// to the decimals of its address, half away from zero on its decimal value, silently (D363); an address [format]
/// decimals leaves out takes the default of the family (D366). What the rounding moves is reported against
/// ROUND_LIMIT by RoundLimitCheck (CMP023).
/// </summary>
public sealed class NumberFormatter
{
    // The decimal point, and the comma of Klartext files (machine-config 2; controllers heidenhain.md 1).
    private const string Point = ".";
    private const string Comma = ",";

    // A decimal keeps at most 28 decimals.
    private const int MostDecimals = 28;

    // D366: angle addresses take four decimals on Heidenhain, its input resolution of 0.0001 degree, and three on
    // Siemens and Fanuc.
    private const int HeidenhainAngleDecimals = 4;
    private const int AngleDecimals = 3;

    // Gap closed 2026-10-27 (delegated, W3-5): a linear address with neither decimals of its own nor of X takes three,
    // the 0.001 mm of every family's IS-B input increment; a component of a tool or normal vector, which is neither a
    // length nor an angle, takes the six decimals the virtual machine keeps (D121).
    private const int LinearDecimals = 3;
    private const int VectorDecimals = 6;

    // The NCX addresses of a sweep or a turn of the frame that a compiler passes for its decimals (D366): ANGLE, the
    // one address of the sweep, and ROTATE.
    private static readonly HashSet<string> s_angleAddresses = new(StringComparer.Ordinal) { "ANGLE", "ROTATE" };

    // The components of a vector as the compilers pass them: TX TY TZ and NX NY NZ (D81), on Siemens A3 B3 C3 and
    // A5 B5 C5 (controllers siemens.md 6).
    private static readonly HashSet<string> s_vectorAddresses = new(StringComparer.Ordinal)
    {
        "TX", "TY", "TZ", "NX", "NY", "NZ", "A3", "B3", "C3", "A5", "B5", "C5",
    };

    private readonly OutputFormat? _format;
    private readonly bool _pointOnWholeNumbers;
    private readonly bool _heidenhain;
    private readonly HashSet<string> _rotaryLetters = new(StringComparer.Ordinal) { "A", "B", "C" };

    /// <summary>
    /// The formatter of one machine.
    /// </summary>
    /// <param name="machine">The machine, its [format], its axes and its controller.</param>
    /// <param name="pointOnWholeNumbers">True for a controller that writes the decimal point on every real number,
    /// X70. on Fanuc (controllers fanuc.md 2, 10 rule 5).</param>
    public NumberFormatter(MachineConfig machine, bool pointOnWholeNumbers)
    {
        _format = machine.Format;
        _pointOnWholeNumbers = pointOnWholeNumbers;
        _heidenhain = machine.Machine.Controller == Controller.Heidenhain;
        foreach (AxisDef axis in machine.Axes)
        {
            if (axis.Kind == AxisKind.Rotary)
            {
                _rotaryLetters.Add(axis.Letter ?? axis.NcxName);
            }
        }

        DecimalSeparator = DecimalSeparatorOf(machine);
    }

    /// <summary>
    /// The decimal separator the machine writes, "." or ",".
    /// </summary>
    public string DecimalSeparator { get; }

    /// <summary>
    /// The decimal separator of a machine as the configuration resolves it: decimal_separator of [format] when the
    /// file writes it, else the separator of the controller family (wave-1 question #64).
    /// </summary>
    /// <param name="machine">The machine.</param>
    public static string DecimalSeparatorOf(MachineConfig machine)
    {
        // A written decimal_separator wins; without it a Heidenhain machine writes the comma of Klartext files and
        // the others the point (machine-config 2; controllers heidenhain.md 1 and 8 rule 2, differences.md, Numbers;
        // wave-1 question #64).
        if (machine.Format?.DecimalSeparator is string written)
        {
            return written == Comma ? Comma : Point;
        }

        return machine.Machine.Controller == Controller.Heidenhain ? Comma : Point;
    }

    /// <summary>
    /// The decimals [format] gives an address; null when it gives none.
    /// </summary>
    /// <param name="address">The address as decimals = { X = 3, F = 1, S = 0 } writes it.</param>
    public int? DecimalsOf(string address)
    {
        if (_format is null || !_format.Decimals.TryGetValue(address, out int decimals))
        {
            return null;
        }

        return Math.Clamp(decimals, 0, MostDecimals);
    }

    /// <summary>
    /// The decimals an address is written with: those of [format] decimals, else the default of the family (D366).
    /// </summary>
    /// <param name="address">The address of [format] decimals whose decimals apply.</param>
    public int WrittenDecimalsOf(string address)
    {
        return DecimalsOf(address) ?? DefaultDecimals(address);
    }

    /// <summary>
    /// True for an angle address: ANGLE, ROTATE and the letters of the rotary axes (D366).
    /// </summary>
    /// <param name="address">The address of [format] decimals.</param>
    public bool IsAngle(string address)
    {
        return !s_vectorAddresses.Contains(address)
            && (s_angleAddresses.Contains(address) || _rotaryLetters.Contains(address)
                || (address.Length > 1 && _rotaryLetters.Contains(address.Substring(0, 1))
                    && address.Substring(1).All(char.IsAsciiDigit)));
    }

    /// <summary>
    /// Writes a number of an address: X, F, S, or the address whose decimals a native address takes, X for I.
    /// </summary>
    /// <param name="address">The address of [format] decimals whose decimals apply.</param>
    /// <param name="value">The value, as the virtual machine or the block has it.</param>
    /// <param name="block">The block it is written for.</param>
    /// <returns>The digits with the decimal separator of the machine, without the address.</returns>
    public string Format(string address, decimal value, Block block)
    {
        // D363: rounding a number to the decimals of its address is silent, half away from zero on the decimal value
        // as written (15.0175 becomes 15.018); CMP020 is retired. D366: no address is written with all its digits.
        _ = block;
        int decimals = WrittenDecimalsOf(address);
        decimal written = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        return Digits(written, DecimalsOf(address)).Replace(Point, DecimalSeparator, StringComparison.Ordinal);
    }

    /// <summary>
    /// Writes a feed, the address F of [format] decimals.
    /// </summary>
    public string FormatFeed(decimal value, Block block)
    {
        return Format("F", value, block);
    }

    /// <summary>
    /// Writes a spindle speed, the address S of [format] decimals.
    /// </summary>
    public string FormatSpeed(decimal value, Block block)
    {
        return Format("S", value, block);
    }

    // The digits of a number that fits: under trailing_zeros = true padded with zeros to the decimals of its address,
    // otherwise without trailing zeros (machine-config 2); a whole number of an address that takes decimals keeps its
    // point on a controller that writes one on every real number (controllers fanuc.md 10 rule 5); zero never gets a
    // minus sign.
    // The padding and the point follow the decimals [format] writes, not the defaults of D366, so that a count or a
    // parameter of an address the file leaves out stays as it was, L2 and Q215=0.
    private string Digits(decimal value, int? decimals)
    {
        decimal number = value == 0m ? 0m : value;
        string text = _format is { TrailingZeros: true } && decimals is int places
            ? number.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : WithoutTrailingZeros(number.ToString(CultureInfo.InvariantCulture));
        if (_pointOnWholeNumbers && decimals > 0 && !text.Contains(Point, StringComparison.Ordinal))
        {
            text += Point;
        }

        return text;
    }

    // 70.500 is 70.5 and 70.000 is 70.
    private static string WithoutTrailingZeros(string text)
    {
        if (!text.Contains(Point, StringComparison.Ordinal))
        {
            return text;
        }

        return text.TrimEnd('0').TrimEnd('.');
    }

    // D366: an angle address takes four decimals on Heidenhain and three on Siemens and Fanuc; a linear address
    // takes the decimals of X.
    private int DefaultDecimals(string address)
    {
        if (s_vectorAddresses.Contains(address))
        {
            return VectorDecimals;
        }

        if (IsAngle(address))
        {
            return _heidenhain ? HeidenhainAngleDecimals : AngleDecimals;
        }

        // Gap closed 2026-10-27 (delegated, W3-5): a Q, QL or QR parameter of a Klartext cycle takes the four decimals
        // of the cycle parameters' input range, 0.0001 to 99999.9999 (D358: "the four decimals of Q256").
        if (_heidenhain && IsKlartextParameter(address))
        {
            return HeidenhainAngleDecimals;
        }

        return DecimalsOf("X") ?? LinearDecimals;
    }

    private static bool IsKlartextParameter(string address)
    {
        string digits = address.StartsWith("QL", StringComparison.Ordinal)
            || address.StartsWith("QR", StringComparison.Ordinal) ? address.Substring(2)
            : address.StartsWith('Q') ? address.Substring(1) : "";
        return digits.Length > 0 && digits.All(char.IsAsciiDigit);
    }
}
