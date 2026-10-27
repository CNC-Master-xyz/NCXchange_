using System.Text;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The cycles of Klartext (controllers heidenhain.md 5; 8 rule 7; controller-mapping 5; language 4.7, 4.7.1;
/// machine-config 6): a CYCLE block as CYCL DEF n with the Q parameters in the control's order from the signature of
/// its catalog entry, a native cycle CYCLE:HEIDENHAIN=n with its Q parameters in source order, CYCLE_CALL as CYCL CALL
/// or as the positioning block with M99, CYCLE=OFF as nothing (the definition stays until the next CYCL DEF), and DWELL
/// as cycle 9.
/// </summary>
internal static class HeidenhainCycles
{
    // A definition continues on its next lines, each indented, every line but the last ending with ~ (controllers
    // heidenhain.md 1, 5).
    private const string Indent = "    ";
    private const string Continues = " ~";

    // The words of a cycle block (language 4.7; 5 rule 6, bucket 13).
    private static readonly string[] s_cycleWords =
        ["SURFACE", "CLEARANCE", "DEPTH", "SAFE", "CYCLE_RETRACT", "PECK", "CYCLE_F", "CYCLE_DWELL", "PITCH",
            "CONTOUR", "AXIS"];

    /// <summary>
    /// Tells whether a line continues a cycle definition, indented under its first line.
    /// </summary>
    public static bool IsContinuation(string line)
    {
        return line.StartsWith(Indent, StringComparison.Ordinal);
    }

    /// <summary>
    /// A block continued over several lines: the head, then each value on a line of its own, every line but the last
    /// ending with ~ (controllers heidenhain.md 1, 5): "CYCL DEF 247 INIT. REF.PKT ~" and "    Q339=+1".
    /// </summary>
    public static string Continued(string head, IReadOnlyList<string> values)
    {
        var text = new StringBuilder(head);
        foreach (string value in values)
        {
            text.Append(Continues).Append('\n').Append(Indent).Append(value);
        }

        return text.ToString();
    }

    /// <summary>
    /// Writes the cycle a CYCLE block defines: CYCL DEF with the Q parameters of its catalog entry in the control's
    /// order (heidenhain 8 rule 7), or the native cycle with its Q parameters in source order (language 4.7.1, D94);
    /// nothing for CYCLE=OFF, since the definition stays active until the next CYCL DEF (controllers heidenhain.md 5).
    /// </summary>
    public static void WriteDefinition(HeidenhainBlock writing)
    {
        if (writing.Block.Find("CYCLE") is not Word cycle)
        {
            return;
        }

        writing.MarkWritten(cycle);
        if (cycle.Addr is not null)
        {
            WriteNative(writing, cycle);
            return;
        }

        if (cycle.Value is IdentValue { Name: "OFF" })
        {
            return;
        }

        foreach (string key in s_cycleWords)
        {
            writing.Take(key);
        }

        // D178: the catalog carries the control's own order for every cycle of heidenhain 5; an entry that still has
        // no signature, one a machine file adds, cannot be written in the control's order and is a CMP ERROR
        // (decisions.md).
        string name = cycle.Value.ToCanonical();
        CycleEntry? entry = writing.Machine.CycleCatalog.Find(name);
        if (entry is null || entry.Signature.Count == 0)
        {
            writing.Error(DiagnosticCodes.HeidenhainCycleWithoutSignature,
                $"CYCLE={name}: the cycle catalog of the machine gives {(entry is null ? "no entry" : "no signature")} "
                + $"for it, so CYCL DEF{(entry is null ? "" : " " + entry.Native)} has no Q parameters in the "
                + "control's order (controllers heidenhain.md 8 rule 7; machine-config 6; D178).");
            return;
        }

        if (!DrillsAlongTheToolAxis(writing) || !CheckUnwritten(writing, entry)
            || HeidenhainCycleValues.Of(writing, entry) is not List<string> values)
        {
            return;
        }

        // D355: PECK is the infeed counted from SURFACE, and so is Q202 of cycle 203, an infeed below the surface
        // coordinate Q203 to which the depth Q201 is relative too (iTNC 530 Zyklen 670388-12, cycle 203, connector
        // p.87, 89-90), so the compiler writes PECK unchanged.
        writing.Line(Continued(Head(writing, entry.Native, entry.Title), values));
    }

    /// <summary>
    /// Writes CYCLE_CALL: CYCL CALL at the current position, or the positioning block of its axis words at rapid with
    /// M99 (heidenhain 8 rule 7; controllers heidenhain.md 5; virtual machine 3.3).
    /// </summary>
    public static void WriteCall(HeidenhainBlock writing)
    {
        writing.Take("CYCLE_CALL");
        if (HeidenhainAxes.Of(writing.Block).Count == 0)
        {
            // CYCL CALL runs with the compensation of the program, which only an L block switches (language 4.4;
            // controllers heidenhain.md 2).
            HeidenhainCompensation.Check(writing, "The CYCLE_CALL, written as CYCL CALL,");
            writing.Line("CYCL CALL");
            return;
        }

        HeidenhainMotion.WriteStraight(writing, rapid: true, call: "M99");
    }

    /// <summary>
    /// Writes DWELL as cycle 9: CYCL DEF 9.0 VERWEILZEIT, 9.1 V.ZEIT 1,5, the seconds (controller-mapping 1, DWELL;
    /// controllers heidenhain.md 5).
    /// </summary>
    public static void WriteDwell(HeidenhainBlock writing)
    {
        if (writing.Take("DWELL") is not Word dwell)
        {
            return;
        }

        if (HeidenhainNumbers.NumberOf(dwell.Value) is not decimal seconds)
        {
            HeidenhainNumbers.ReportValue(writing, dwell);
            return;
        }

        writing.Line("CYCL DEF 9.0 VERWEILZEIT\nCYCL DEF 9.1 V.ZEIT "
            + writing.Numbers.Format("V.ZEIT", seconds, writing.Block));
    }

    // CYCLE:HEIDENHAIN=251 Q215=0 Q218=60: the native cycle with every native parameter in source order, each a number
    // or a Q parameter with its sign (language 4.7.1, D94; controllers heidenhain.md 5).
    private static void WriteNative(HeidenhainBlock writing, Word cycle)
    {
        var values = new List<string>();
        foreach (Word word in writing.Block.Words)
        {
            if (word.Definition is not null)
            {
                continue;
            }

            writing.MarkWritten(word);
            string? value = HeidenhainNumbers.SignedValue(writing, word.Key, word.Value);
            if (value is null)
            {
                HeidenhainNumbers.ReportValue(writing, word);
                return;
            }

            values.Add(word.Key + "=" + value);
        }

        string native = cycle.Value.ToCanonical();
        writing.Line(Continued(Head(writing, native, null), values));
    }

    // D270: CYCL DEF n with the `title` of the catalog entry, CYCL DEF 200 BOHREN, and CYCL DEF n alone where the
    // entry has none, with no diagnostic: the control adds the name in its dialog language when it reads the program
    // in (decisions.md; machine-config 6). A native cycle takes the title of the catalog's entry of its number.
    private static string Head(HeidenhainBlock writing, string native, string? title)
    {
        title ??= writing.Machine.CycleCatalog.FindNative(native)?.Title;
        return title is null ? "CYCL DEF " + native : "CYCL DEF " + native + " " + title;
    }

    // The drilling axis of a Klartext cycle is the tool axis of the TOOL CALL (controller-mapping 5, AXIS).
    private static bool DrillsAlongTheToolAxis(HeidenhainBlock writing)
    {
        if (writing.Block.Find("AXIS") is not Word axis)
        {
            return true;
        }

        string toolAxis = HeidenhainAxes.ToolAxis(writing.After.Frame.Workplane);
        if (axis.Value.ToCanonical() == toolAxis)
        {
            return true;
        }

        writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
            $"{axis.ToCanonical()}: the drilling axis of a Klartext cycle is the tool axis {toolAxis} of the TOOL CALL "
            + "(controller-mapping 5, AXIS; D59).");
        return false;
    }

    // D362: a word of the cycle block that the entry maps to no Q parameter and no rule word carries is the WARNING
    // CMP105 where the native cycle reaches the same value from another word of the block (the CYCLE_F of TAP, whose
    // cycle 207 takes its feed from the pitch Q239), is dropped silently where the value is the behaviour the control
    // has anyway (CYCLE_DWELL=0, PECK=0 on a cycle without that motion), and is the ERROR CMP122 where the value would
    // be lost; each stands once, at the CYCLE block (decisions.md; machine-config 6, controller-mapping 5). False after
    // an ERROR.
    private static bool CheckUnwritten(HeidenhainBlock writing, CycleEntry entry)
    {
        bool written = true;
        foreach (string key in s_cycleWords)
        {
            if (writing.Block.Find(key) is not Word word || entry.Params.ContainsKey(key)
                || entry.RuleWords.Contains(key))
            {
                continue;
            }

            if (entry.DerivedFrom(key) is string from && writing.Block.Find(from) is not null)
            {
                writing.Warning(DiagnosticCodes.HeidenhainCycleWordNotWritten,
                    $"{word.ToCanonical()} is not written: cycle {entry.Native} of the catalog entry {entry.Name} has "
                    + $"no Q parameter for it and takes the value from {from} (D362; machine-config 6).");
            }
            else if (key is "CYCLE_DWELL" or "PECK" && HeidenhainNumbers.NumberOf(word.Value) == 0m)
            {
                continue;
            }
            else
            {
                writing.Error(DiagnosticCodes.HeidenhainCycleWordLost,
                    $"{word.ToCanonical()}: cycle {entry.Native} of the catalog entry {entry.Name} has no Q parameter "
                    + "for it and takes its value from no other word, so the value would be lost (D362; "
                    + "machine-config 6).");
                written = false;
            }
        }

        return written;
    }
}
