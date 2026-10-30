using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The LBL numbers of the Klartext programs of a compile (controllers heidenhain.md 1; machine-config 2, label_max;
/// language 4.9, 4.13; D271, D272). A numeric LABEL or SUB name that is unique, not 0 and within 1 to label_max keeps
/// its number; every other one, a duplicate, 0, an identifier, a string or a number out of range, gets the next free
/// number of the range in block order, and a range without a free number is the ERROR CMP129. LBL "NAME" is never
/// written, and LBL 0 only as the end of a subprogram.
/// Gap closed 2026-10-27 (delegated): of two equal numeric labels the first in block order keeps its number and the
/// later one is renumbered; the next free number is the lowest number of the range no label has. The subprograms are
/// numbered once for the whole compile, since the text of a SUB section stands in every file that calls it (D99): their
/// names, the labels in them and their return labels (D272) share one numbering, and the labels of each program and
/// its JUMP=END label take numbers the subprograms do not have. Where a subprogram holds a JUMP=END, the end labels of
/// all programs take one number of the subprograms' numbering, so that the SUB's text does not depend on its caller.
/// </summary>
internal sealed class HeidenhainLabelNumbers
{
    // The first label number; LBL 0 ends a subprogram (controllers heidenhain.md 1).
    private const long First = 1;

    // The key of the JUMP=END labels and of the one end label all programs share when a subprogram jumps to the end.
    private const string EndPrefix = "END ";
    private const string SharedEnd = "END *";

    private readonly Dictionary<string, long> _numbers = new(StringComparer.Ordinal);
    private readonly long _max;

    private HeidenhainLabelNumbers(long max)
    {
        _max = max;
    }

    /// <summary>
    /// Numbers the labels of every program and subprogram the walk of the compile writes; reports a label without a
    /// free number (CMP129).
    /// </summary>
    /// <param name="lookAhead">Every step of the run.</param>
    /// <param name="max">The largest label number, [format] label_max (D271).</param>
    /// <param name="diagnostics">The diagnostics of the compile.</param>
    public static HeidenhainLabelNumbers Of(LookAhead lookAhead, long max, Diagnostics diagnostics)
    {
        var numbers = new HeidenhainLabelNumbers(max);
        var subs = new List<HeidenhainLabelKey>();
        var programs = new Dictionary<Section, List<HeidenhainLabelKey>>();
        foreach (HeidenhainLabelKey key in KeysOf(lookAhead))
        {
            if (key.Program is null)
            {
                subs.Add(key);
            }
            else if (programs.TryGetValue(key.Program, out List<HeidenhainLabelKey>? keys))
            {
                keys.Add(key);
            }
            else
            {
                programs.Add(key.Program, [key]);
            }
        }

        // A JUMP=END in a subprogram jumps to the end label of whichever program calls it, and the SUB's text is one
        // for every caller (D99): then every program's end label takes one number from the subprograms' numbering.
        List<HeidenhainLabelKey> ends = [];
        if (EndInSubprogram(lookAhead))
        {
            foreach (List<HeidenhainLabelKey> keys in programs.Values)
            {
                ends.AddRange(keys.FindAll(key => key.Key.StartsWith(EndPrefix, StringComparison.Ordinal)));
                keys.RemoveAll(key => key.Key.StartsWith(EndPrefix, StringComparison.Ordinal));
            }

            if (ends.Count > 0)
            {
                subs.Add(ends[0] with { Key = SharedEnd });
                subs.Sort((one, other) => one.Block.Line.CompareTo(other.Block.Line));
            }
        }

        var taken = new HashSet<long>();
        numbers.Assign(subs, taken, diagnostics);
        if (numbers._numbers.TryGetValue(SharedEnd, out long shared))
        {
            foreach (HeidenhainLabelKey end in ends)
            {
                numbers._numbers[end.Key] = shared;
            }
        }

        foreach (List<HeidenhainLabelKey> keys in programs.Values)
        {
            numbers.Assign(keys, new HashSet<long>(taken), diagnostics);
        }

        return numbers;
    }

    /// <summary>
    /// The LBL number of a LABEL, or of the target of a JUMP or REPEAT, of a section; null when it has none.
    /// </summary>
    public string? LabelOf(Section? section, Value label)
    {
        return Find(LabelKey(section, label));
    }

    /// <summary>
    /// The LBL number of a subprogram, by its NAME or the value of a CALL; null when it has none.
    /// </summary>
    public string? SubOf(Value name)
    {
        return Find(SubKey(name));
    }

    /// <summary>
    /// The LBL number of the contour subprogram of cycle 27 for a CYLINDER=r block (D394); null when it has none.
    /// </summary>
    public string? CylinderOf(Block block)
    {
        return Find(CylinderKey(block));
    }

    /// <summary>
    /// The LBL number before the M30 of a program that JUMP=END jumps to; null when it has none.
    /// </summary>
    public string? EndOf(Section program)
    {
        return Find(EndKey(program));
    }

    /// <summary>
    /// The LBL number directly before the LBL 0 of a subprogram that a RETURN before its end jumps to (D272); null
    /// when it has none.
    /// </summary>
    public string? ReturnOf(Section sub)
    {
        return Find(ReturnKey(sub));
    }

    /// <summary>
    /// Tells whether a RETURN of a subprogram stands directly before its SUB=END, where it writes nothing (D272).
    /// </summary>
    public static bool EndsTheSection(LookAhead lookAhead, BlockStep step)
    {
        IReadOnlyList<BlockStep> steps = lookAhead.Steps;
        int next = step.Index + 1;
        return next < steps.Count && steps[next].Block.Has("SUB", null, "END") && !step.Block.Has("IF");
    }

    // Every label the compile writes, once per block, in block order: LABEL and the NAME of SUB=BEGIN, the JUMP=END
    // label of a program before its M30, the return label of a subprogram before its LBL 0.
    private static List<HeidenhainLabelKey> KeysOf(LookAhead lookAhead)
    {
        var keys = new List<HeidenhainLabelKey>();
        var seen = new HashSet<Block>(ReferenceEqualityComparer.Instance);
        var ends = new HashSet<Section>();
        var returns = new HashSet<Section>();
        Section? program = null;
        foreach (BlockStep step in lookAhead.Steps)
        {
            Block block = step.Block;
            Section? section = step.Section;
            if (section is { Kind: SectionKind.Program })
            {
                program = section;
            }

            if (block.Has("JUMP", null, "END") || (block.Has("RETURN") && section is { Kind: SectionKind.Program }))
            {
                if (program is not null)
                {
                    ends.Add(program);
                }
            }
            else if (block.Has("RETURN") && section is { Kind: SectionKind.Sub } sub
                && !EndsTheSection(lookAhead, step))
            {
                returns.Add(sub);
            }

            if (!seen.Add(block))
            {
                continue;
            }

            if (block.Find("LABEL") is Word label)
            {
                Section? owner = section is { Kind: SectionKind.Program } ? section : null;
                keys.Add(new HeidenhainLabelKey(LabelKey(section, label.Value), label.Value, block, owner));
            }

            // D394: the contour subprogram of cycle 27 for a CYLINDER section of a program takes the next free label.
            if (block.Find("CYLINDER") is { Value: not IdentValue } && section is { Kind: SectionKind.Program })
            {
                keys.Add(new HeidenhainLabelKey(CylinderKey(block), null, block, section));
            }

            if (block.Has("SUB", null, "BEGIN") && block.Find("NAME") is Word name)
            {
                keys.Add(new HeidenhainLabelKey(SubKey(name.Value), name.Value, block, null));
            }
        }

        foreach (BlockStep step in lookAhead.Steps)
        {
            if (step.Section is not Section section || !seen.Remove(step.Block))
            {
                continue;
            }

            if (step.Block.Has("PROGRAM", null, "END") && ends.Contains(section))
            {
                keys.Add(new HeidenhainLabelKey(EndKey(section), null, step.Block, section));
            }
            else if (step.Block.Has("SUB", null, "END") && returns.Contains(section))
            {
                keys.Add(new HeidenhainLabelKey(ReturnKey(section), null, step.Block, null));
            }
        }

        keys.Sort((one, other) => one.Block.Line.CompareTo(other.Block.Line));
        return keys;
    }

    // Whether a subprogram's walk holds a JUMP=END.
    private static bool EndInSubprogram(LookAhead lookAhead)
    {
        foreach (BlockStep step in lookAhead.Steps)
        {
            if (step.Section is { Kind: SectionKind.Sub } && step.Block.Has("JUMP", null, "END"))
            {
                return true;
            }
        }

        return false;
    }

    private static string LabelKey(Section? section, Value label)
    {
        return "LABEL " + (section?.FirstBlock.ToString(CultureInfo.InvariantCulture) ?? "-") + " "
            + label.ToCanonical();
    }

    private static string SubKey(Value name)
    {
        string text = name is StringValue named ? named.Content : name.ToCanonical();
        return "SUB " + text;
    }

    private static string CylinderKey(Block block)
    {
        return "CYLINDER " + block.Line.ToString(CultureInfo.InvariantCulture);
    }

    private static string EndKey(Section program)
    {
        return EndPrefix + program.FirstBlock.ToString(CultureInfo.InvariantCulture);
    }

    private static string ReturnKey(Section sub)
    {
        return "RETURN " + sub.FirstBlock.ToString(CultureInfo.InvariantCulture);
    }

    private string? Find(string key)
    {
        return _numbers.TryGetValue(key, out long number) ? number.ToString(CultureInfo.InvariantCulture) : null;
    }

    // The keepers first, each numeric label that is not 0, within the range and not taken by one before it; then the
    // others in block order with the lowest free number (D271).
    private void Assign(List<HeidenhainLabelKey> keys, HashSet<long> taken, Diagnostics diagnostics)
    {
        var others = new List<HeidenhainLabelKey>();
        foreach (HeidenhainLabelKey key in keys)
        {
            if (_numbers.ContainsKey(key.Key))
            {
                continue;
            }

            if (key.Value is IntegerValue number && number.Number >= First && number.Number <= _max
                && taken.Add(number.Number))
            {
                _numbers.Add(key.Key, number.Number);
            }
            else
            {
                others.Add(key);
            }
        }

        long next = First;
        foreach (HeidenhainLabelKey key in others)
        {
            if (_numbers.ContainsKey(key.Key))
            {
                continue;
            }

            while (taken.Contains(next))
            {
                next++;
            }

            if (next > _max)
            {
                diagnostics.Error(key.Block, DiagnosticCodes.HeidenhainNoFreeLabel,
                    $"{What(key)} needs an LBL number, and every number from 1 to label_max {_max} is taken in its "
                    + "Klartext program (controllers heidenhain.md 1; machine-config 2; D271).");
                continue;
            }

            taken.Add(next);
            _numbers.Add(key.Key, next);
        }
    }

    private static string What(HeidenhainLabelKey key)
    {
        if (key.Value is null)
        {
            return key.Key.StartsWith("END", StringComparison.Ordinal)
                ? "The label of JUMP=END before the M30"
                : key.Key.StartsWith("CYLINDER", StringComparison.Ordinal)
                    ? "The contour label of cycle 27"
                    : "The return label before LBL 0";
        }

        return key.Key.StartsWith("SUB", StringComparison.Ordinal)
            ? $"SUB NAME={key.Value.ToCanonical()}"
            : $"LABEL={key.Value.ToCanonical()}";
    }
}
