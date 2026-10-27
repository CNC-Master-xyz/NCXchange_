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
            // D278: a later G70 repeats only P and Q.
            write.Write(CycleLine(code, written));
            return true;
        }

        // D278: the blocks are written from an unknown motion state, so the first writes G00 or G01 and every later
        // one its own code and words; what the program has active otherwise (plane, feed mode, G80) stands.
        var entryState = new TargetState();
        entryState.TakeOver(write.Target);
        entryState.Forget(FanucCodes.Motion);
        entryState.Forget("F");
        if (contours.Inline(contour, entryState) is not InlinedSection inlined
            || !Checks(write, entry, inlined, contour))
        {
            return false;
        }

        List<string> lines = FirstBlockWords(write, entry, inlined);
        int greatest = GreatestLabel(write, program);
        int first = contours.Next(program, greatest);
        int last = lines.Count > 1 ? contours.Next(program, greatest) : first;
        var range = new FanucContourRange(first, last);
        contours.Add(program, contour, range);
        write.Write(CycleLine(code, range));
        for (int index = 0; index < lines.Count; index++)
        {
            string number = index == 0 ? "N" + Number(first) + " "
                : index == lines.Count - 1 ? "N" + Number(last) + " " : "";
            write.Write(number + lines[index]);
        }

        // D278: after the section every code its blocks wrote is unknown, since the control does not run them where
        // they stand.
        foreach (string key in inlined.WrittenKeys)
        {
            write.Target.Forget(key);
        }

        write.MakeUnknown(FanucCodes.Motion);
        return true;
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

    // The lines of the section, the first without the word of a plane axis its first motion names but does not move,
    // so that G71 and G72 read type I where only the infeed axis moves (D278).
    private static List<string> FirstBlockWords(FanucBlock write, CycleEntry entry, InlinedSection inlined)
    {
        var lines = new List<string>(inlined.Lines);
        Block? first = FirstMotion(inlined);
        if (entry.Native is not ("G71" or "G72") || first is null || lines.Count == 0)
        {
            return lines;
        }

        string other = entry.Native == "G71" ? "Z" : "X";
        if (first.Find(other) is null || Moves(write, first, other))
        {
            return lines;
        }

        string letter = FanucAxes.LetterOf(write, other, incremental: false) ?? other;
        var kept = new List<string>();
        foreach (string token in lines[0].Split(' '))
        {
            if (!(token.StartsWith(letter, StringComparison.Ordinal) && token.Length > letter.Length
                && (char.IsAsciiDigit(token[letter.Length]) || token[letter.Length] is '-' or '.' or '[' or '#')))
            {
                kept.Add(token);
            }
        }

        lines[0] = string.Join(" ", kept);
        return lines;
    }

    // The greatest block number the labels of the program take (D277): its numeric labels, the numbers its
    // identifier labels and the end of a conditional JUMP=END take.
    private static int GreatestLabel(FanucBlock write, Section program)
    {
        int greatest = write.Labels.EndLabel ?? 0;
        foreach (BlockStep step in write.Steps)
        {
            if (step.Section == program && step.Block.Find("LABEL") is Word label
                && write.Labels.NumberOf(label.Value.ToCanonical()) is int number)
            {
                greatest = Math.Max(greatest, number);
            }
        }

        return greatest;
    }
}
