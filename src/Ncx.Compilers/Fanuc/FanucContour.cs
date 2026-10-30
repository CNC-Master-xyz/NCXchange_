using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The multiple repetitive cycles G70 to G73 of a Fanuc lathe with their CONTOUR (D278; controllers fanuc.md 6;
/// language 4.7.1, 4.13): the contour section is written inline, directly after the first G70 to G73 of the program
/// that names it, and never as an O program (D388 item 4); a later cycle of the program repeats only P and Q.
/// </summary>
internal static class FanucContour
{
    // The words a block of a contour may carry besides its verb: the axis words, the feed, the arc words, the dwell
    // and a comment (D278: G04, G00 to G03, and a LABEL or JUMP inside the range).
    private static readonly string[] s_contourWords =
        ["F", "R", "CENTER", "TURN", "DWELL", "LABEL", "JUMP", "IF", "COMMENT", "SKIP"];

    // The multiple repetitive cycles in the numbers of systems A and B, and of system C (D174).
    private static readonly string[] s_repetitive = ["G70", "G71", "G72", "G73", "G74", "G75", "G76"];
    private static readonly string[] s_repetitiveOfSystemC = ["G72", "G73", "G74", "G75", "G76", "G77", "G78"];

    /// <summary>
    /// The code of a one-shot cycle of the catalog, G70 to G76 in the numbers of the machine's G-code system (D174);
    /// null on a mill, which has none.
    /// </summary>
    /// <param name="write">The block being written.</param>
    /// <param name="native">The native of the catalog, in the numbers of system A.</param>
    public static string? CodeOf(FanucBlock write, string native)
    {
        int index = Array.IndexOf(s_repetitive, native);
        return write.System switch
        {
            null => null,
            GcodeSystem.C when index >= 0 => s_repetitiveOfSystemC[index],
            _ => native,
        };
    }

    /// <summary>
    /// Writes a G70 to G73 with its contour (D278): the first cycle of the program that names the section writes
    /// "G71 Pp Qq" and the blocks of the section inline after it, the first and the last with the block numbers p and
    /// q, which follow D277 after the greatest numeric label of the program; a later one writes P and Q alone.
    /// False where the contour cannot be written, with the ERROR that says why.
    /// </summary>
    /// <param name="write">The block of the CYCLE_CALL.</param>
    /// <param name="entry">The catalog entry, which carries a contour.</param>
    /// <param name="cycle">The cycle of NCX.</param>
    /// <param name="code">The code in the numbers of the machine's G-code system.</param>
    public static bool Write(FanucBlock write, CycleEntry entry, CycleSnapshot cycle, string code)
    {
        if (write.Contours is not FanucContours contours || write.Step.Section is not Section program)
        {
            return false;
        }

        string? name = ContourName(cycle);
        if (name is null || SectionOf(write, name) is not Section contour)
        {
            write.Error(DiagnosticCodes.FanucCycleWordNotMapped,
                $"CONTOUR of CYCLE={entry.Name} names no SUB section of the file, so the contour cannot be written "
                + "(language 4.7.1; D278).");
            return false;
        }

        if (contours.Find(program, contour) is FanucContourRange written)
        {
            // D278: a later G70 repeats only P and Q, with the modes of the range in front of it; the control runs
            // the blocks P to Q, whose codes and F take effect (B-64484EN-1 p.76, p.281), so afterwards they are
            // unknown as after the first copy.
            WriteModes(write, written.Modes);
            WriteFirstBlock(write, entry, cycle, code);
            write.Write(CycleLine(code, written) + FanucCycles.SecondBlockWords(write, entry, cycle));
            ForgetAfter(write, written.Forgotten);
            return true;
        }

        // D278: the contour's own labels take free numbers of this O program (D277), so no N stands twice in it.
        contours.SetLabels(contour, FanucLabels.Renumbered(contour, write.Steps, write.Labels));
        Dictionary<string, string> modes = ModesOf(write, contour);
        if (contours.Inline(contour, EntryState(write, contour, modes)) is not InlinedSection inlined
            || !Checks(write, entry, inlined, contour))
        {
            return false;
        }

        var lines = new List<string>(inlined.Lines);
        int motion = lines.FindIndex(IsMotionLine);
        if (motion < 0 || !IsLinearLine(lines[motion]))
        {
            write.Error(DiagnosticCodes.FanucContourFirstBlockNotLinear,
                $"The first motion line of the CONTOUR {contour.Name} of CYCLE={entry.Name} is \""
                + (motion < 0 ? "none" : lines[motion]) + "\", and the first block of a Fanuc contour writes G00 or "
                + "G01 (controllers fanuc.md 6; D278).");
            return false;
        }

        // P names the line of the first motion: a bare label line in front of it gives that line its number, since
        // a label line alone is a block of its own that P must not name.
        int? labelBefore = null;
        for (int index = motion - 1; index >= 0; index--)
        {
            if (IsBareLabel(lines[index]))
            {
                labelBefore ??= OwnNumber(lines[index]);
                lines.RemoveAt(index);
                motion--;
            }
        }

        lines[motion] = WithoutUnmovedAxis(write, entry, inlined, lines[motion]);
        int first = OwnNumber(lines[motion]) ?? labelBefore ?? write.Labels.Allocate();
        if (OwnNumber(lines[motion]) is null)
        {
            lines[motion] = "N" + Number(first) + " " + lines[motion];
        }

        int lastIndex = lines.Count - 1;
        int last = lastIndex == motion ? first : OwnNumber(lines[lastIndex]) ?? write.Labels.Allocate();
        if (lastIndex != motion && OwnNumber(lines[lastIndex]) is null)
        {
            lines[lastIndex] = "N" + Number(last) + " " + lines[lastIndex];
        }

        var forgotten = new List<string>(inlined.WrittenKeys) { "F" };
        forgotten.AddRange(modes.Keys);
        var range = new FanucContourRange(first, last) { Modes = modes, Forgotten = forgotten };
        contours.Add(program, contour, range);
        WriteModes(write, modes);
        WriteFirstBlock(write, entry, cycle, code);
        write.Write(CycleLine(code, range) + FanucCycles.SecondBlockWords(write, entry, cycle));
        foreach (string line in lines)
        {
            write.Write(line);
        }

        ForgetAfter(write, forgotten);
        return true;
    }

    // D409: the first block of G71, G72 or G73, U(d) R(e), stands directly before the block with P and Q.
    private static void WriteFirstBlock(FanucBlock write, CycleEntry entry, CycleSnapshot cycle, string code)
    {
        if (FanucCycles.FirstBlockLine(write, entry, cycle, code) is string first)
        {
            write.Write(first);
        }
    }

    // D278 (B-64484EN-1 p.90): the modes the range runs under stand in front of the G7x line, where the program's
    // target state differs, since between P and Q only G00 to G03, G04, macro branches and repeats and the drawing
    // dimensions may stand (PS0325, p.91).
    private static void WriteModes(FanucBlock write, IReadOnlyDictionary<string, string> modes)
    {
        var codes = new List<string>();
        foreach (KeyValuePair<string, string> mode in modes)
        {
            if (write.Target.ActiveOf(mode.Key) != mode.Value)
            {
                codes.Add(mode.Value);
                write.Target.Set(mode.Key, mode.Value);
            }
        }

        if (codes.Count > 0)
        {
            write.Write(string.Join(" ", codes));
        }
    }

    // D278: after the cycle the codes and F the blocks of the range wrote, the motion and the modes of the range are
    // unknown, since the control does not run the blocks where they stand in the text.
    private static void ForgetAfter(FanucBlock write, IReadOnlyList<string> forgotten)
    {
        foreach (string key in forgotten)
        {
            write.Target.Forget(key);
        }

        write.MakeUnknown(FanucCodes.Motion);
        write.MakeUnknown("F");
    }

    // The plane, the feed mode and G90/G91 of the contour's NCX blocks, as the walk of the section has them; G90/G91
    // only outside system A, which has no such codes.
    private static Dictionary<string, string> ModesOf(FanucBlock write, Section contour)
    {
        var modes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (FirstStepOf(write, contour) is BlockStep first)
        {
            modes[FanucCodes.Plane] = FanucCodes.PlaneCode(first.Before.Frame.Workplane);
            modes[FanucCodes.FeedMode] = FanucCodes.FeedModeCode(first.Before.Motion.FeedMode, write.System);
        }

        if (write.System != GcodeSystem.A)
        {
            modes[FanucCodes.Distance] = "G90";
        }

        return modes;
    }

    // D278: the blocks of the contour are written from an unknown motion state, so the first writes G00 or G01 and
    // its words. Gap closed 2026-10-27 (delegated): D278's "from an unknown state" cannot hold for the plane, the feed
    // mode and G90/G91, because only G00 to G03, G04, macro branches and repeats and the drawing dimensions may stand
    // between P and Q (B-64484EN-1 p.90; PS0325, p.91): those are pinned to the contour's values and written in front
    // of the cycle line (WriteModes), and the cycle, the compensation and the units are pinned to the contour's
    // values; the length offset and the origin stay as the program has them. The range then holds G00 to G03, G04,
    // axis words and F only.
    private static TargetState EntryState(FanucBlock write, Section contour, IReadOnlyDictionary<string, string> modes)
    {
        var state = new TargetState();
        state.TakeOver(write.Target);
        state.Forget(FanucCodes.Motion);
        state.Forget("F");
        foreach (KeyValuePair<string, string> mode in modes)
        {
            state.Set(mode.Key, mode.Value);
        }

        state.Set(FanucCodes.Cycle, "G80");
        if (FirstStepOf(write, contour) is BlockStep first)
        {
            state.Set(FanucCodes.Comp, FanucCodes.CompCode(first.Before.Motion.Comp));
            if (FanucCodes.UnitsCode(first.Before.Frame.Units, write.System) is string units)
            {
                state.Set(FanucCodes.Units, units);
            }
        }

        return state;
    }

    // The first block of the contour's walk after its SUB=BEGIN.
    private static BlockStep? FirstStepOf(FanucBlock write, Section contour)
    {
        foreach (BlockStep step in write.Steps)
        {
            if (step.Section == contour && !step.Block.Has("SUB", null, "BEGIN"))
            {
                return step;
            }
        }

        return null;
    }

    // A line that carries a motion code G00 to G03 (with or without its leading zero).
    private static bool IsMotionLine(string line)
    {
        return MotionCodeOf(line) is not null;
    }

    private static bool IsLinearLine(string line)
    {
        return MotionCodeOf(line) is "G0" or "G1";
    }

    private static string? MotionCodeOf(string line)
    {
        foreach (string token in line.Split(' '))
        {
            if (token is "G0" or "G00" or "G1" or "G01" or "G2" or "G02" or "G3" or "G03")
            {
                return token.Length == 3 ? string.Concat("G", token.AsSpan(2)) : token;
            }
        }

        return null;
    }

    // A line that is a block number alone, "N7", the line of a LABEL block.
    private static bool IsBareLabel(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Length > 1 && trimmed[0] == 'N' && trimmed.Substring(1).All(char.IsAsciiDigit);
    }

    // The block number a line carries itself; null for none.
    private static int? OwnNumber(string line)
    {
        string first = line.TrimStart().Split(' ')[0];
        return first.Length > 1 && first[0] == 'N' && first.Substring(1).All(char.IsAsciiDigit)
            ? int.Parse(first.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture)
            : null;
    }

    private static string CycleLine(string code, FanucContourRange range)
    {
        return $"{code} P{Number(range.First)} Q{Number(range.Last)}";
    }

    private static string Number(int number)
    {
        return number.ToString(CultureInfo.InvariantCulture);
    }

    private static string? ContourName(CycleSnapshot cycle)
    {
        foreach (Word word in cycle.Parameters)
        {
            if (word.Key == "CONTOUR" && word.Addr is null)
            {
                return word.Value is IdentValue ident ? ident.Name : word.Value.ToCanonical();
            }
        }

        return null;
    }

    // The SUB section of a name, as the steps of the run hold it.
    private static Section? SectionOf(FanucBlock write, string name)
    {
        foreach (BlockStep step in write.Steps)
        {
            if (step.Section is Section section && section.Kind == SectionKind.Sub && section.Name == name)
            {
                return section;
            }
        }

        return null;
    }

    // D278: a section holding anything but G04, G00 to G03 motion, or a LABEL or JUMP whose target is inside the range
    // is a CMP ERROR, a CALL, a cycle or a macro call among it; the first block writes G00 or G01, and for G71 and
    // G72 the plane axes its first motion moves: X alone (Z alone for G72) for type I, both for type II, which only a
    // machine with [cycles] contour_type_ii takes, and a first block of G71 that does not move X is PS0325.
    private static bool Checks(FanucBlock write, CycleEntry entry, InlinedSection inlined, Section contour)
    {
        var labels = new List<string>();
        foreach (BlockStep step in inlined.Steps)
        {
            if (step.Block.Find("LABEL") is Word label)
            {
                labels.Add(label.Value.ToCanonical());
            }
        }

        bool ok = true;
        foreach (BlockStep step in inlined.Steps)
        {
            if (ForbiddenWord(write, step.Block, labels) is string word)
            {
                write.Error(DiagnosticCodes.FanucContourBlockNotAllowed,
                    $"{word} on line {step.Block.Line} of the CONTOUR {contour.Name}: the contour of "
                    + $"CYCLE={entry.Name} holds only G04, G00 to G03 motion and a LABEL or JUMP inside the range, "
                    + "no CALL, cycle or macro call (controllers fanuc.md 6; D278).");
                ok = false;
            }
        }

        if (!ok)
        {
            return false;
        }

        Block? first = FirstMotion(inlined);
        if (first is null || first.Verb?.Key is not ("RAPID" or "LINE"))
        {
            write.Error(DiagnosticCodes.FanucContourFirstBlockNotLinear,
                $"The first block of the CONTOUR {contour.Name} of CYCLE={entry.Name} is no RAPID or LINE, and the "
                + "first block of a Fanuc contour writes G00 or G01 (controllers fanuc.md 6; D278).");
            return false;
        }

        if (entry.Native is not ("G71" or "G72"))
        {
            return true;
        }

        string along = entry.Native == "G71" ? "X" : "Z";
        string other = along == "X" ? "Z" : "X";
        if (!Moves(write, first, along))
        {
            write.Error(DiagnosticCodes.FanucContourFirstBlockWithoutInfeed,
                $"The first block of the CONTOUR {contour.Name} of {entry.Native} does not move {along}, which the "
                + "control rejects with alarm PS0325 (controllers fanuc.md 6; D278).");
            return false;
        }

        if (Moves(write, first, other) && write.Machine.Cycles?.ContourTypeII != true)
        {
            write.Error(DiagnosticCodes.FanucContourTypeIINotDeclared,
                $"The first block of the CONTOUR {contour.Name} of {entry.Native} moves both X and Z, which makes it "
                + "the type II pocketing cycle, and the machine declares no [cycles] contour_type_ii (controllers "
                + "fanuc.md 6; D278, D388 item 11).");
            return false;
        }

        return true;
    }

    // The first word of a block that a contour may not hold, as the program writes it; null for none.
    private static string? ForbiddenWord(FanucBlock write, Block block, List<string> labels)
    {
        if (block.Verb is Word verb && verb.Key is not ("RAPID" or "LINE" or "ARC"))
        {
            return verb.ToCanonical();
        }

        foreach (Word word in block.Words)
        {
            if (ReferenceEquals(word, block.Verb) || IsAxisWord(write, word.Key)
                || s_contourWords.Contains(word.Key))
            {
                continue;
            }

            return word.ToCanonical();
        }

        if (block.Find("JUMP") is Word jump && !labels.Contains(jump.Value.ToCanonical()))
        {
            return jump.ToCanonical();
        }

        return null;
    }

    private static bool IsAxisWord(FanucBlock write, string key)
    {
        string axis = key.StartsWith('I') && key.Length > 1 ? key.Substring(1) : key;
        return write.Machine.ResolveAxis(axis) is not null;
    }

    private static Block? FirstMotion(InlinedSection inlined)
    {
        foreach (BlockStep step in inlined.Steps)
        {
            if (step.Block.Verb is not null)
            {
                return step.Block;
            }
        }

        return null;
    }

    // Whether the first block moves an axis from where the cycle starts: an incremental word other than 0, or an
    // absolute word other than the position at the cycle call (a diameter X counted as the program writes it).
    private static bool Moves(FanucBlock write, Block block, string axis)
    {
        if (block.Find("I" + axis) is Word increment)
        {
            return FanucFunctions.NumberOf(increment.Value) is not 0m;
        }

        if (block.Find(axis) is not Word word)
        {
            return false;
        }

        if (FanucFunctions.NumberOf(word.Value) is not decimal target
            || !write.After.Motion.Position.TryGetValue(axis, out AxisPosition start) || !start.Known)
        {
            return true;
        }

        bool diameter = axis == "X" && write.Machine.ResolveAxis("X")?.Programming == Programming.Diameter;
        return (diameter ? target / 2m : target) != start.Value;
    }

    // The line of the first motion without the word of a plane axis it names but does not move, so that G71 and G72
    // read type I where only the infeed axis moves (D278).
    private static string WithoutUnmovedAxis(FanucBlock write, CycleEntry entry, InlinedSection inlined, string line)
    {
        Block? first = FirstMotion(inlined);
        if (entry.Native is not ("G71" or "G72") || first is null)
        {
            return line;
        }

        string other = entry.Native == "G71" ? "Z" : "X";
        if (first.Find(other) is null || Moves(write, first, other))
        {
            return line;
        }

        string letter = FanucAxes.LetterOf(write, other, incremental: false) ?? other;
        var kept = new List<string>();
        foreach (string token in line.Split(' '))
        {
            if (!(token.StartsWith(letter, StringComparison.Ordinal) && token.Length > letter.Length
                && (char.IsAsciiDigit(token[letter.Length]) || token[letter.Length] is '-' or '.' or '[' or '#')))
            {
                kept.Add(token);
            }
        }

        return string.Join(" ", kept);
    }
}
