using System.Globalization;
using Ncx.Core.Machine;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// D164: whether the Q parameters of a CYCL DEF that no word of its catalog entry carries hold what the Heidenhain
/// compiler would write for them, so that the definition reads into the named entry without losing a value; otherwise
/// the reader keeps it as CYCLE:HEIDENHAIN=n (D94), as D5 requires (decisions.md; machine-config 6; heidenhain 7 rule
/// 7).
/// </summary>
internal static class HeidenhainCycleMatch
{
    private const string ChipBreakLift = "Q256";

    /// <summary>
    /// The first unmapped Q parameter whose value differs from what the compiler writes, as "Q208=100"; null when
    /// every one of them holds that value.
    /// </summary>
    /// <param name="machine">The machine, with the [cycles] keys and the catalog.</param>
    /// <param name="inch">Whether the program is in inches.</param>
    /// <param name="entry">The catalog entry the definition reads into.</param>
    /// <param name="parameters">The Q parameters of the definition, in source order.</param>
    public static string? FirstDiffering(MachineConfig machine, bool inch, CycleEntry entry,
        IReadOnlyList<SourceWord> parameters)
    {
        foreach (SourceWord parameter in parameters)
        {
            if (!entry.Params.Values.Contains(parameter.Address)
                && !Holds(machine, inch, entry, parameters, parameter))
            {
                return parameter.Address + "=" + parameter.Text;
            }
        }

        return null;
    }

    // D164: the block rules of heidenhain 8 rule 7 (a Q202 of cycle 200 of at least the depth is one infeed, Q205 of
    // 203 equals Q202, Q208 of 201 equals Q206), Q256 from [cycles] chip_break_lift before the catalog (D358), the
    // catalog's defaults otherwise. A value the cycle does not use in that form is not compared: Q256 of PECK, whose
    // Q213=0 never breaks a chip, and Q205 while Q212=0 takes nothing off the infeed. Q213 chose PECK or CHIP_BREAK
    // already (D163). Q256 is compared with the value the compiler writes, the key clamped, in inches under an INCH
    // program and rounded (DrillingFamily.HeidenhainChipBreakLift).
    private static bool Holds(MachineConfig machine, bool inch, CycleEntry entry, IReadOnlyList<SourceWord> parameters,
        SourceWord parameter)
    {
        decimal? value = HeidenhainNumbers.Parse(parameter.Text);
        switch (parameter.Address)
        {
            case "Q213" when entry.Name is "PECK" or "CHIP_BREAK":
                return true;
            case "Q202" when entry.Native == "200":
                return value is decimal infeed && NumberOf(parameters, entry.NativeOf("DEPTH")) is decimal depth
                    && infeed >= Math.Abs(depth);
            case "Q205" when entry.Native == "203":
                return NumberOf(parameters, "Q212") is 0m
                    || (value is decimal least && least == NumberOf(parameters, "Q202"));
            case "Q208" when entry.Native == "201":
                return value is decimal retract && retract == NumberOf(parameters, entry.NativeOf("CYCLE_F"));
            case ChipBreakLift:
                return entry.Name == "PECK" || value == DrillingFamily.HeidenhainChipBreakLift(
                    machine.Cycles?.ChipBreakLift, inch, machine.Format);
            default:
                return Default(entry, parameter, value);
        }
    }

    // The catalog's defaults value of the position, a number or the control's literal, Q208=MAX (D164).
    private static bool Default(CycleEntry entry, SourceWord parameter, decimal? value)
    {
        if (!entry.Defaults.TryGetValue(parameter.Address, out string? text))
        {
            return false;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number)
            ? value == number
            : string.Equals(parameter.Text.Trim(), text, StringComparison.OrdinalIgnoreCase);
    }

    private static decimal? NumberOf(IReadOnlyList<SourceWord> parameters, string? name)
    {
        foreach (SourceWord parameter in parameters)
        {
            if (parameter.Address == name)
            {
                return HeidenhainNumbers.Parse(parameter.Text);
            }
        }

        return null;
    }
}
