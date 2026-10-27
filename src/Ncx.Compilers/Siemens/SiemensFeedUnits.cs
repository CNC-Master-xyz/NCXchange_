using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The group 13 pair the compiler writes for UNITS and the feeds it converts (D292; machine-config 1; controllers
/// siemens.md 2): G71/G70 where UNITS equals the basic system of the machine (base_units, MD10240); G710/G700 where it
/// differs and the machine has them (extended_units, MD10260 = 1); otherwise G71/G70, under which the control keeps
/// every feed in the basic system, so the compiler writes each feed converted into it, with the INFO CMP551.
/// "Durch G70/G71 werden Vorschubangaben nicht beeinflusst", and G700/G710 need MD10260 = 1 (840D sl
/// NC-Programmierung SW4.92, connector p.115, p.164-166).
/// </summary>
internal static class SiemensFeedUnits
{
    // Millimetres per inch.
    private const decimal MmPerInch = 25.4m;

    /// <summary>
    /// The code of group 13 for the UNITS of the block after it, null where the units are not known; the INFO CMP551
    /// where the feeds under it are converted into the basic system.
    /// </summary>
    public static string? UnitsCode(SiemensBlock write)
    {
        Units units = write.After.Frame.Units;
        if (units is not (Units.Mm or Units.Inch))
        {
            return null;
        }

        string metric = units == Units.Mm ? "G71" : "G70";
        if (Differs(write) is not true)
        {
            return metric;
        }

        if (write.Machine.Machine.ExtendedUnits)
        {
            return units == Units.Mm ? "G710" : "G700";
        }

        write.Diagnostics.Info(write.Block, DiagnosticCodes.SiemensFeedConverted,
            $"UNITS={Word(units)} is written {metric}, under which the control keeps every feed in the basic system "
            + $"{BaseOf(write)} of the machine (MD10240) and has no G700/G710 (extended_units, MD10260): each feed "
            + $"under it is written converted into {BaseOf(write)} (840D sl NC-Programmierung SW4.92, p.115, "
            + "p.164-166; D292).");
        return metric;
    }

    /// <summary>
    /// The text of a feed word as the control reads it: as written, or converted into the basic system where UNITS
    /// differs from it and the machine has no G700/G710 (D292): a number converted from its exact value and rounded
    /// to the decimals of F, an expression multiplied or divided by 25.4 in parentheses.
    /// </summary>
    /// <param name="write">The block being written.</param>
    /// <param name="value">The value of the feed in the units of the program.</param>
    public static string? Text(SiemensBlock write, Value value)
    {
        if (!Converts(write))
        {
            return SiemensExpressions.ValueText(write, value, "F");
        }

        decimal? number = value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue literal => literal.Number,
            _ => null,
        };
        if (number is decimal exact)
        {
            return Text(write, exact);
        }

        return SiemensExpressions.ValueText(write, value, "F") is string text
            ? "(" + text + ")" + (ToMm(write) ? "*" : "/") + MmPerInch.ToString(CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>
    /// The text of a known feed of the virtual machine, converted as <see cref="Text(SiemensBlock, Value)"/> does.
    /// </summary>
    public static string Text(SiemensBlock write, decimal feed)
    {
        if (!Converts(write))
        {
            return write.Format("F", feed);
        }

        return write.FormatComputed("F", ToMm(write) ? feed * MmPerInch : feed / MmPerInch);
    }

    /// <summary>
    /// True where the feeds are written converted: UNITS differs from the basic system and G700/G710 are not there.
    /// </summary>
    public static bool Converts(SiemensBlock write)
    {
        return Differs(write) is true && !write.Machine.Machine.ExtendedUnits;
    }

    // True where the program's units are inches and the basic system millimetres.
    private static bool ToMm(SiemensBlock write)
    {
        return write.After.Frame.Units == Units.Inch;
    }

    // True where the UNITS of the block after it differ from the basic system of the machine, null where either is not
    // known.
    private static bool? Differs(SiemensBlock write)
    {
        Units units = write.After.Frame.Units;
        if (BaseOf(write) is not string basic || units is not (Units.Mm or Units.Inch))
        {
            return null;
        }

        return Word(units) != basic;
    }

    // The basic system as the UNITS word MM or INCH (D292: base_units, else units_default).
    private static string? BaseOf(SiemensBlock write)
    {
        return write.Machine.Machine.ResolvedBaseUnits;
    }

    // The UNITS word of the units.
    private static string Word(Units units)
    {
        return units == Units.Mm ? "MM" : "INCH";
    }
}
