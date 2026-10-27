using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Ncx.Core.Machine;

/// <summary>
/// The range of [format] counter_registers: Q parameters of one kind, Q, QL or QR, from a first to a last number,
/// QL90 to QL99, in which the Heidenhain compiler counts a REPEAT that a JUMP leaves (D213, D388 (12)).
/// </summary>
public sealed record CounterRange
{
    private static readonly string[] s_prefixes = ["QL", "QR", "Q"];

    /// <summary>
    /// The kind of the parameters: Q, QL or QR.
    /// </summary>
    public required string Prefix { get; init; }

    /// <summary>
    /// The number of the first parameter.
    /// </summary>
    public required int First { get; init; }

    /// <summary>
    /// The number of the last parameter, not below the first.
    /// </summary>
    public required int Last { get; init; }

    /// <summary>
    /// The range of the machine; null when [format] counter_registers is not given.
    /// </summary>
    public static CounterRange? Of(OutputFormat? format)
    {
        IReadOnlyList<string>? names = format?.CounterRegisters;
        return names is { Count: 2 } && TryParse(names[0], names[1], out CounterRange? range) ? range : null;
    }

    /// <summary>
    /// Reads the range from its first and last name; false when they are no two names of one kind of Q parameter with
    /// the first not after the last.
    /// </summary>
    public static bool TryParse(string first, string last, [NotNullWhen(true)] out CounterRange? range)
    {
        range = null;
        if (!TrySplit(first, out string? prefix, out int from) || !TrySplit(last, out string? lastPrefix, out int to)
            || prefix != lastPrefix || from > to)
        {
            return false;
        }

        range = new CounterRange { Prefix = prefix, First = from, Last = to };
        return true;
    }

    /// <summary>
    /// The name of the parameter of a number of the range, QL91.
    /// </summary>
    public string NameOf(int number)
    {
        return Prefix + number.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TrySplit(string name, [NotNullWhen(true)] out string? prefix, out int number)
    {
        foreach (string candidate in s_prefixes)
        {
            string digits = name.Length > candidate.Length ? name.Substring(candidate.Length) : "";
            if (name.StartsWith(candidate, StringComparison.Ordinal) && digits.All(char.IsAsciiDigit)
                && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number))
            {
                prefix = candidate;
                return true;
            }
        }

        prefix = null;
        number = 0;
        return false;
    }
}
