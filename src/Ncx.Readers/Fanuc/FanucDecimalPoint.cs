using System.Globalization;
using Ncx.Core.Machine;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The numbers without a decimal point of a Fanuc block under [format] calculator_input = false, parameter 3401#0
/// DPI = 0: an address that can take a decimal point counts least input increments, X70 is 0.070 mm (B-64484EN p.229,
/// 8.3; B-64490EN-05 p.190; controllers fanuc.md 2). The words are rewritten to the value they stand for, with its
/// decimal point, before any concern reads them, so that every concern reads the same number the control does.
/// </summary>
internal static class FanucDecimalPoint
{
    // The addresses that can take a decimal point (B-64484EN p.229, 8.3): on a mill X Y Z U V W A B C I J K Q R F, on
    // a lathe the same without Q and with H in system A.
    private const string MillAddresses = "XYZUVWABCIJKQRF";
    private const string LatheAddresses = "XYZUVWABCIJKRF";

    // The addresses whose value as a non-axis address has the decimals of the reference axis; B U V W as a non-axis
    // address have none (B-64484EN p.527-528, the tables of the argument units, which are the least input increment of
    // each address).
    private const string ReferenceAddresses = "XYZACIJKQR";

    /// <summary>
    /// The block with every number without a decimal point of an address that takes one replaced by its value in mm,
    /// inch, degrees or the unit of the feed; the dwell X and U of G4 stay as written, since G4 counts them in
    /// [machine] dwell_increment (D159). Null in <paramref name="problem"/> unless an F without a decimal point stands
    /// where the feed mode, which gives its unit, is not known.
    /// </summary>
    /// <param name="block">The source block.</param>
    /// <param name="state">The source-side state after the modal codes of the block.</param>
    /// <param name="machine">The machine, its axes and its G-code system.</param>
    /// <param name="feedModeUnknown">True where the feed mode is the caller's, which the reader does not know
    /// (FanucCallerState).</param>
    /// <param name="problem">Why the block cannot be read; null when it can.</param>
    public static SourceBlock Scaled(SourceBlock block, SourceState state, MachineConfig machine, bool feedModeUnknown,
        out string? problem)
    {
        problem = null;
        GcodeSystem? system = machine.Machine.GcodeSystem;
        string addresses = system is null ? MillAddresses : LatheAddresses + (system == GcodeSystem.A ? "H" : "");
        bool dwell = block.Words.Any(word => word.Address == "G" && NativeCode.Of(word) == "G4");
        // The units and the feed mode the header assumes where the program has set none: units_default, per minute
        // (FanucHeader).
        string? unitsCode = state.ActiveCode(FanucModalGroups.Units);
        bool inch = unitsCode is null
            ? machine.Machine.UnitsDefault?.ToUpperInvariant() == "INCH"
            : unitsCode == (system == GcodeSystem.C ? "G70" : "G20");
        var words = new List<SourceWord>();
        bool changed = false;
        foreach (SourceWord word in block.Words)
        {
            decimal? increment = null;
            if (word.Number is decimal && word.Expression is null && word.Address.Length == 1
                && addresses.Contains(word.Address, StringComparison.Ordinal)
                && !word.Text.Contains('.', StringComparison.Ordinal)
                && !(dwell && word.Address is "X" or "U"))
            {
                increment = word.Address == "F"
                    ? (feedModeUnknown ? null : FeedIncrement(state, system, inch))
                    : AddressIncrement(word.Address, machine, system, inch);
                if (word.Address == "F" && increment is null)
                {
                    problem = $"F{word.Text} has no decimal point, so it counts least input increments of the feed "
                        + "(calculator_input = false), whose unit the feed mode gives, which the reader does not know "
                        + "here (B-64484EN p.527-528)";
                }
            }

            if (increment is not decimal unit || word.Number is not decimal units)
            {
                words.Add(word);
                continue;
            }

            int decimals = unit.Scale;
            decimal value = units * unit;
            string text = value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture);
            if (!text.Contains('.', StringComparison.Ordinal))
            {
                text += ".";
            }

            words.Add(word with { Text = text, Number = value });
            changed = true;
        }

        return changed ? block with { Words = words } : block;
    }

    // The least input increment of an address other than F, IS-B (B-64484EN p.527-528, NOTE 2 and 3): a rotary axis
    // 0.001 degree, a linear axis and a non-axis address of the reference axis 0.001 mm or 0.0001 inch; B U V W
    // as a non-axis address take whole numbers; null for no scaling.
    private static decimal? AddressIncrement(string address, MachineConfig machine, GcodeSystem? system, bool inch)
    {
        AxisDef? axis = AxisOf(address, machine, system);
        if (axis is not null)
        {
            return axis.Kind == AxisKind.Rotary ? 0.001m : Linear(inch);
        }

        return ReferenceAddresses.Contains(address, StringComparison.Ordinal) ? Linear(inch) : null;
    }

    private static decimal Linear(bool inch)
    {
        return inch ? 0.0001m : 0.001m;
    }

    // The axis an address names: the address or the incremental letter of an [[axis]] (machine-config 4), H the
    // incremental C of system A (controllers fanuc.md 3).
    private static AxisDef? AxisOf(string address, MachineConfig machine, GcodeSystem? system)
    {
        string name = system == GcodeSystem.A && address == "H" ? "C" : address;
        foreach (AxisDef axis in machine.Axes)
        {
            if ((axis.Letter ?? axis.NcxName) == name || axis.IncrementalLetter == address)
            {
                return axis;
            }
        }

        return null;
    }

    // The least input increment of F by the feed mode (B-64484EN p.527-528; B-64484EN-2 p.104, Unit of F command): a
    // mill 1 mm/min or 0.01 inch/min under G94, 0.01 mm/rev or 0.0001 inch/rev under G95, 0.001 under G93; a lathe
    // 1 mm/min or 0.01 inch/min per minute, 0.0001 mm/rev or 0.000001 inch/rev per revolution. Null where the feed
    // mode is not known.
    private static decimal? FeedIncrement(SourceState state, GcodeSystem? system, bool inch)
    {
        string perMinute = system == GcodeSystem.A ? "G98" : "G94";
        string mode = state.ActiveCode(FanucModalGroups.FeedMode) ?? perMinute;
        string perRevolution = system == GcodeSystem.A ? "G99" : "G95";
        if (mode == perMinute)
        {
            return inch ? 0.01m : 1m;
        }

        if (mode == perRevolution && system is null)
        {
            return inch ? 0.0001m : 0.01m;
        }

        if (mode == perRevolution)
        {
            return inch ? 0.000001m : 0.0001m;
        }

        return mode == "G93" ? 0.001m : null;
    }
}
