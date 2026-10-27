using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The Q parameters of a CYCL DEF from the words of its CYCLE block (controllers heidenhain.md 5; 8 rule 7;
/// controller-mapping 5; machine-config 6): the words of the entry's params, the planes relative to the surface Q203,
/// Q204 from CYCLE_RETRACT, and the Q parameters of the signature that no word carries: the block rules, Q256 from the
/// machine and the catalog's defaults (D163, D164, D226, D358).
/// </summary>
internal static class HeidenhainCycleValues
{
    private const string Surface = "SURFACE";
    private const string Clearance = "CLEARANCE";
    private const string Depth = "DEPTH";
    private const string Safe = "SAFE";
    private const string Peck = "PECK";
    private const string CycleFeed = "CYCLE_F";
    private const string CycleDwell = "CYCLE_DWELL";

    private const string ChipBreakLift = "Q256";
    private const decimal MillimetresPerInch = 25.4m;

    // D358: Q256 takes 0.1000 to 99999.9999 on the iTNC 530 (iTNC 530 Zyklen 670388-12, NC-SW 340490-07, cycle 203
    // parameters, connector p.89-90), in millimetres.
    private const decimal LeastLift = 0.1m;
    private const decimal GreatestLift = 99999.9999m;

    // The words whose numbers the Q parameters are computed from (language 4.7).
    private static readonly string[] s_numberWords =
        [Surface, Clearance, Depth, Safe, Peck, CycleFeed, CycleDwell, "PITCH"];

    /// <summary>
    /// The Q parameters of the cycle in the order of the signature, each as Qnnn=value with its sign; null when one has
    /// no value, which is reported on the block.
    /// </summary>
    public static List<string>? Of(HeidenhainBlock writing, CycleEntry entry)
    {
        Dictionary<string, decimal>? words = NumbersOf(writing);
        if (words is null)
        {
            return null;
        }

        // Q203 is the surface every plane is relative to (heidenhain 5; machine-config 6, absolute_from_surface).
        if (!words.TryGetValue(Surface, out decimal surface))
        {
            Report(writing, entry, "Q203", "the cycle block names no SURFACE, to which Q203 and the planes relative to "
                + "it belong");
            return null;
        }

        var parameters = new List<string>();
        foreach (string parameter in entry.Signature)
        {
            string? value = WordOf(entry, parameter) is string word
                ? Mapped(writing, entry, word, words, surface)
                : Unmapped(writing, entry, parameter, words, surface);
            if (value is null)
            {
                Report(writing, entry, parameter, ReasonOf(writing, entry, parameter));
                return null;
            }

            parameters.Add(parameter + "=" + value);
        }

        return parameters;
    }

    // A Q parameter the entry maps to a word: its number, relative to the surface for the planes (machine-config 6).
    private static string? Mapped(HeidenhainBlock writing, CycleEntry entry, string word,
        Dictionary<string, decimal> words, decimal surface)
    {
        string parameter = entry.NativeOf(word)!;
        switch (word)
        {
            // CYCLE_RETRACT=SAFE becomes Q204 (heidenhain 8 rule 7): SAFE = Q203 + Q204.
            // D226: for CYCLE_RETRACT=CLEARANCE the compiler writes Q204 equal to Q200, the clearance above the
            // surface, which ends the tool there because the second clearance acts only above the first
            // (decisions.md).
            // D188: a plane without a value, SAFE missing under CYCLE_RETRACT=SAFE, is the compiler's ERROR here,
            // CMP104 (decisions.md).
            case Safe:
                bool toSafe = writing.Block.Find("CYCLE_RETRACT")?.Value is IdentValue { Name: Safe };
                string plane = toSafe ? Safe : Clearance;
                if (!words.TryGetValue(plane, out decimal level))
                {
                    return null;
                }

                if (toSafe)
                {
                    WarnSafeNotAboveClearance(writing, entry, words, surface);
                }

                return Signed(writing, parameter, entry.ToNative(Safe, level, surface));

            // D163: both DRILL and DRILL_DWELL are cycle 200, told apart by Q211, the dwell at the hole bottom:
            // Q211 is CYCLE_DWELL, or 0 when the block has none (decisions.md).
            case CycleDwell:
                return Signed(writing, parameter, words.TryGetValue(CycleDwell, out decimal dwell) ? dwell : 0m);

            // Without PECK the cycle drills to the depth in one infeed (virtual machine 3.3: PECK splits the feed), the
            // Q202 of cycle 200 in D164.
            case Peck:
                return Infeed(words, surface) is decimal infeed ? Signed(writing, parameter, infeed) : null;

            default:
                return words.TryGetValue(word, out decimal value)
                    ? Signed(writing, parameter, entry.ToNative(word, value, surface))
                    : null;
        }
    }

    // D163: Q213, the number of chip breaks before the control retracts, tells PECK from CHIP_BREAK on cycle 203:
    // 0 for PECK, which retracts fully after every infeed, and the number of infeeds, the depth over PECK rounded up,
    // for CHIP_BREAK, which the drilling never reaches (decisions.md).
    // D164: three values depend on the block and are rules of the compiler, as BOHREN.h writes them: Q202 of cycle 200
    // is the full depth (one infeed), Q205 of 203 equals Q202, and Q208 of 201 equals Q206. Q256 comes from the machine
    // key [cycles] chip_break_lift, its one source (D358). Every other position takes the catalog's `defaults`, a
    // number or the control's literal such as Q208=MAX (decisions.md; machine-config 6).
    private static string? Unmapped(HeidenhainBlock writing, CycleEntry entry, string parameter,
        Dictionary<string, decimal> words, decimal surface)
    {
        decimal? infeed = Infeed(words, surface);
        switch (parameter)
        {
            case "Q202" or "Q205":
                return infeed is decimal step ? Signed(writing, parameter, step) : null;
            case ChipBreakLift:
                return Lift(writing, entry);
            case "Q208" when entry.Native == "201":
                return words.TryGetValue(CycleFeed, out decimal feed) ? Signed(writing, parameter, feed) : null;
            case "Q213" when entry.Name == "PECK":
                return Signed(writing, parameter, 0m);
            case "Q213" when entry.Name == "CHIP_BREAK":
                return infeed is decimal peck && peck > 0 && DepthBelow(words, surface) is decimal depth
                    ? Signed(writing, parameter, Math.Ceiling(depth / peck))
                    : null;
            default:
                return Default(writing, entry, parameter);
        }
    }

    // The `defaults` value of a position: a number with its sign, or the control's literal as written, MAX (D164).
    private static string? Default(HeidenhainBlock writing, CycleEntry entry, string parameter)
    {
        if (!entry.Defaults.TryGetValue(parameter, out string? text))
        {
            return null;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number)
            ? Signed(writing, parameter, number)
            : text;
    }

    // D358: Q256 from [cycles] chip_break_lift, a distance in millimetres (D357) written in the units of the program.
    // A lift outside the input range of Q256 is written as the nearest value of the range with the WARNING CMP121,
    // where the cycle breaks chips at all (CHIP_BREAK; PECK with Q213=0 never lifts). Gap closed 2026-10-27
    // (delegated): without the key, which the loader requires of a machine file (CFG158), the compiler takes the
    // absent-key lift 0 of the virtual machine (D357), so it writes 0.1 with CMP121.
    private static string Lift(HeidenhainBlock writing, CycleEntry entry)
    {
        decimal millimetres = writing.Machine.Cycles?.ChipBreakLift ?? 0m;
        decimal written = Math.Clamp(millimetres, LeastLift, GreatestLift);
        if (written != millimetres && entry.Name != "PECK")
        {
            writing.Warning(DiagnosticCodes.HeidenhainChipBreakLiftOutOfRange,
                $"Q256 of cycle {entry.Native} ({entry.Name}): [cycles] chip_break_lift = "
                + $"{millimetres.ToString(CultureInfo.InvariantCulture)} mm lies outside the input range of Q256, "
                + $"0.1 to 99999.9999 mm, so {written.ToString(CultureInfo.InvariantCulture)} mm is written (D358; "
                + "iTNC 530 cycle manual, cycle 203).");
        }

        bool inch = writing.After.Frame.Units == Units.Inch;
        return Signed(writing, ChipBreakLift, inch ? written / MillimetresPerInch : written);
    }

    // D157: Q204 at or below Q200 does not act, so CYCLE_RETRACT=SAFE with such a SAFE ends at CLEARANCE: a WARNING
    // (decisions.md; D226).
    private static void WarnSafeNotAboveClearance(HeidenhainBlock writing, CycleEntry entry,
        Dictionary<string, decimal> words, decimal surface)
    {
        if (words.TryGetValue(Safe, out decimal safe) && words.TryGetValue(Clearance, out decimal clearance)
            && entry.ToNative(Safe, safe, surface) <= entry.ToNative(Clearance, clearance, surface))
        {
            writing.Warning(DiagnosticCodes.HeidenhainSafeNotAboveClearance,
                $"CYCLE_RETRACT=SAFE: SAFE lies at or below CLEARANCE, so Q204 does not act and cycle {entry.Native} "
                + "retracts only to the set-up clearance Q200 (D157, D226; controller-mapping 5).");
        }
    }

    // Why a position has no value, for CMP104.
    private static string ReasonOf(HeidenhainBlock writing, CycleEntry entry, string parameter)
    {
        bool toSafe = writing.Block.Find("CYCLE_RETRACT")?.Value is IdentValue { Name: Safe };
        if (entry.NativeOf(Safe) == parameter && toSafe && writing.Block.Find(Safe) is null)
        {
            return "CYCLE_RETRACT=SAFE and the cycle block writes no SAFE (D188)";
        }

        return "neither a word of the cycle block, nor a rule of the compiler, nor the defaults of the catalog entry "
            + "gives its value";
    }

    // The depth of one infeed: PECK, or the whole depth below the surface without it.
    private static decimal? Infeed(Dictionary<string, decimal> words, decimal surface)
    {
        return words.TryGetValue(Peck, out decimal peck) ? peck : DepthBelow(words, surface);
    }

    private static decimal? DepthBelow(Dictionary<string, decimal> words, decimal surface)
    {
        return words.TryGetValue(Depth, out decimal depth) ? Math.Abs(depth - surface) : null;
    }

    // The word the entry maps to a Q parameter; null for a Q parameter no word carries.
    private static string? WordOf(CycleEntry entry, string parameter)
    {
        foreach (KeyValuePair<string, string> mapped in entry.Params)
        {
            if (mapped.Value == parameter)
            {
                return mapped.Key;
            }
        }

        return null;
    }

    // The numbers of the cycle words of the block; null when one is an expression, which the planes relative to the
    // surface cannot take (reported).
    private static Dictionary<string, decimal>? NumbersOf(HeidenhainBlock writing)
    {
        var numbers = new Dictionary<string, decimal>();
        foreach (string key in s_numberWords)
        {
            if (writing.Block.Find(key) is not Word word)
            {
                continue;
            }

            if (HeidenhainNumbers.NumberOf(word.Value) is not decimal number)
            {
                HeidenhainNumbers.ReportValue(writing, word);
                return null;
            }

            numbers[key] = number;
        }

        return numbers;
    }

    // Q parameters carry their sign, Q200=+2 (controllers heidenhain.md 5).
    private static string Signed(HeidenhainBlock writing, string parameter, decimal value)
    {
        return HeidenhainNumbers.Signed(writing, parameter, value);
    }

    private static void Report(HeidenhainBlock writing, CycleEntry entry, string parameter, string reason)
    {
        writing.Error(DiagnosticCodes.HeidenhainCycleParameterWithoutValue,
            $"{parameter} of cycle {entry.Native} ({entry.Name}) has no value: {reason} (controllers heidenhain.md 8 "
            + "rule 7; D164).");
    }
}
