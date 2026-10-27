using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The positions of a SINUMERIK cycle call that no word of the definition fills by its params: the whole depth in one
/// infeed of CYCLE83 (D356), the distances the machine holds (D358), the reference point of SLOT1 and SLOT2 (D379),
/// and the check of the words an entry cannot write (D362) and of the positions the control needs that stay empty
/// (controllers siemens.md 7, 12 rule 5; controller-mapping 5; machine-config 6).
/// </summary>
internal static partial class SiemensCycles
{
    private const decimal MillimetresPerInch = 25.4m;

    // Gap closed 2026-10-27 (delegated, code wave 2): the positions without an NCX word that the control needs for
    // the cycle to run, reported once with CMP576 where they stay empty: the length, width, feed and VARI of SLOT1,
    // whose ones digit 0 is "reserviert" (SW4.92 p.1053), the opening angle, width, radius and feed of SLOT2
    // (p.1055), the spindle direction and speed of CYCLE830 (p.1121).
    private static readonly Dictionary<string, string[]> s_required = new(StringComparer.Ordinal)
    {
        ["SLOT1"] = ["LENG", "WID", "FFP1", "VARI"],
        ["SLOT2"] = ["AFSL", "WID", "RAD", "FFP1"],
        ["CYCLE830"] = ["_SDAC", "_SV1"],
    };

    // D362: the words of the CYCLE block that the entry maps to no position and no rule of the family carries, once at
    // the block: a WARNING where the native cycle reaches the value from another word (CMP574), nothing where the
    // value is the control's behaviour anyway, the ERROR CMP571 where it is lost; and the positions the control needs
    // that no word fills (CMP576).
    private static void CheckWords(SiemensBlock write, CycleEntry entry)
    {
        IReadOnlyList<Word> words = write.Block.Words;
        foreach (Word word in words)
        {
            if (word.Key is "CYCLE" or "SAFE" || !s_cycleWords.Contains(word.Key)
                || entry.NativeOf(word.Key) is not null || entry.RuleWords.Contains(word.Key))
            {
                continue;
            }

            if (entry.DerivedFrom(word.Key) is string source && Has(words, source))
            {
                write.Warning(DiagnosticCodes.SiemensCycleWordDerived,
                    $"{word.ToCanonical()} is not written: {entry.Native} takes the value from {source}, and the "
                    + $"catalog entry {entry.Name} maps {word.Key} to no position (D362; controller-mapping 5).");
            }
            else if (IsLost(entry, word, words))
            {
                write.Error(DiagnosticCodes.SiemensCycleWordNotWritable,
                    $"{word.ToCanonical()} has no position in {entry.Native} of the cycle catalog, and no rule of the "
                    + "family carries it (machine-config 6; controllers siemens.md 12 rule 5; D279, D362).");
            }
        }

        if (!s_required.TryGetValue(entry.Native, out string[]? required))
        {
            return;
        }

        var empty = new List<string>();
        foreach (string native in required)
        {
            if (Index(entry, native) is not null && !MapsNative(entry, native) && !entry.Fixed.ContainsKey(native))
            {
                empty.Add(native);
            }
        }

        if (empty.Count > 0)
        {
            write.Warning(DiagnosticCodes.SiemensCycleRequiredPositionEmpty,
                $"{string.Join(", ", empty)} of {entry.Native} {(empty.Count == 1 ? "stays" : "stay")} empty: no NCX "
                + $"word of the catalog entry {entry.Name} carries {(empty.Count == 1 ? "it" : "them")}, and the "
                + "control needs a value there (D379, D380; controllers siemens.md 7).");
        }
    }

    // D362: true where an unmapped word of the definition is lost, false where the cycle derives it from another word
    // of the block or where it is the control's own behaviour, CYCLE_DWELL=0 or PECK=0 on a cycle without that motion.
    private static bool IsLost(CycleEntry entry, Word word, IReadOnlyList<Word> words)
    {
        if (entry.DerivedFrom(word.Key) is string source && Has(words, source))
        {
            return false;
        }

        return !(word.Key is "CYCLE_DWELL" or "PECK" && Number(word.Value) == 0);
    }

    // True where a cycle has a reference point of its own in the plane, _CPA and _CPO of SLOT1 and SLOT2 (D379).
    private static bool AtCallPosition(SiemensBlock write)
    {
        CycleSnapshot cycle = write.After.Cycle;
        return cycle.Controller is null && cycle.Name is string name
            && write.Machine.CycleCatalog.Find(name) is CycleEntry entry
            && Index(entry, "_CPA") is not null && Index(entry, "_CPO") is not null;
    }

    // D379: _CPA and _CPO are the call position, the first and second axis of the plane where the block stands after
    // the call: the axis word of the CYCLE_CALL, else the known position. An unknown one is the ERROR CMP572.
    private static bool FillCallPosition(SiemensBlock write, CycleEntry entry, string?[] positions)
    {
        if (Index(entry, "_CPA") is not int first || Index(entry, "_CPO") is not int second)
        {
            return true;
        }

        string[] plane = write.After.Frame.Workplane switch
        {
            Workplane.ZX => ["Z", "X"],
            Workplane.YZ => ["Y", "Z"],
            _ => ["X", "Y"],
        };
        string? a = PositionText(write, plane[0]);
        string? b = PositionText(write, plane[1]);
        if (a is null || b is null)
        {
            write.Error(DiagnosticCodes.SiemensCycleValueMissing,
                $"{entry.Native} takes its reference point _CPA/_CPO at the call position, and the position of "
                + $"{(a is null ? plane[0] : plane[1])} there is not known (D379; controllers siemens.md 12 rule 5).");
            return false;
        }

        positions[first] = a;
        positions[second] = b;
        return true;
    }

    // The value of a plane axis at the call: the axis word of the block, else the position known after it.
    private static string? PositionText(SiemensBlock write, string axis)
    {
        string decimals = SiemensAxes.DecimalsAddress(write, SiemensAxes.LetterOf(write, axis));
        if (write.Block.Find(axis) is Word word && word.Addr is null)
        {
            return SiemensAxes.ValueOf(write, axis, word.Value, decimals);
        }

        if (write.After.Motion.Position.TryGetValue(axis, out AxisPosition position) && position.Known
            && position.Frame == PositionFrame.Workpiece)
        {
            var value = new DecimalValue(position.Value, position.Value.ToString(CultureInfo.InvariantCulture));
            return SiemensAxes.ValueOf(write, axis, value, decimals);
        }

        return null;
    }

    // D356: PECK and CHIP_BREAK without PECK, or with PECK = 0, drill the whole depth in one infeed: FDPR = the
    // distance from DEPTH to SURFACE, or to CLEARANCE where the block gives no SURFACE; FDEP stays empty.
    private static void FillWholeDepth(SiemensBlock write, CycleSnapshot cycle, CycleEntry entry, string?[] positions)
    {
        if (entry.NativeOf("PECK") is not string native || native != "FDPR" || Index(entry, native) is not int index
            || (Parameter(cycle, "PECK") is Value peck && Number(peck) != 0))
        {
            return;
        }

        if (Parameter(cycle, "DEPTH") is not Value depth
            || (Parameter(cycle, "SURFACE") ?? Parameter(cycle, "CLEARANCE")) is not Value start)
        {
            return;
        }

        if (Number(depth) is decimal to && Number(start) is decimal from)
        {
            positions[index] = write.Format(DecimalsAddress(write, cycle), Math.Abs(to - from));
            return;
        }

        string? toText = SiemensExpressions.ValueText(write, depth, native);
        string? fromText = SiemensExpressions.ValueText(write, start, native);
        positions[index] = toText is null || fromText is null ? null : "ABS((" + toText + ")-(" + fromText + "))";
    }

    // D358: the distances the control holds come from the machine, the one source: _VRT from [cycles]
    // chip_break_lift on the CYCLE83 of VARI=0 (position 15), _DIS1 from [cycles] peck_clearance where that key is set
    // (position 17). The keys are millimetres (D357).
    private static void FillHeldDistances(SiemensBlock write, CycleSnapshot cycle, CycleEntry entry,
        string?[] positions)
    {
        CyclesConfig? cycles = write.Machine.Cycles;
        if (cycles is null || !entry.Fixed.TryGetValue("VARI", out decimal vari))
        {
            return;
        }

        bool inch = write.After.Frame.Units == Units.Inch;
        if (vari == 0 && cycles.ChipBreakLift is decimal lift && Index(entry, "_VRT") is int lifted)
        {
            positions[lifted] = write.Format(DecimalsAddress(write, cycle), inch ? lift / MillimetresPerInch : lift);
        }

        if (cycles.PeckClearance is decimal clearance && Index(entry, "_DIS1") is int advanced)
        {
            positions[advanced] = write.Format(DecimalsAddress(write, cycle),
                inch ? clearance / MillimetresPerInch : clearance);
        }
    }

    private static bool MapsNative(CycleEntry entry, string native)
    {
        foreach (string mapped in entry.Params.Values)
        {
            if (mapped == native)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Has(IReadOnlyList<Word> words, string key)
    {
        foreach (Word word in words)
        {
            if (word.Key == key)
            {
                return true;
            }
        }

        return false;
    }
}
