using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The REPEAT blocks the Heidenhain compiler writes as a Q-parameter counter loop instead of CALL LBL n REP k, and the
/// counter of each (D213, D388 (12)). NCX ends a repeat when a JUMP or JUMP=END leaves the part between the label and
/// the REPEAT, and a later arrival at the REPEAT starts it fresh with TIMES passes, while the TNC keeps the REP counter
/// of CALL LBL REP as block state that only GOTO 0 or a new program selection resets. So where a JUMP, a JUMP=END or a
/// RETURN in the part leaves it, the counter is a parameter of [format] counter_registers, set to 0 before the label
/// and counted at the REPEAT; a machine without the key, or a range with no free register, is the ERROR CMP130.
/// Gap closed 2026-10-27 (delegated): a RETURN in the part leaves it as a JUMP does; each counter loop of the compile
/// takes its own register of the range, in block order, so that nested and shared subprogram loops never share one,
/// and skips a register that a word of the file names. A JUMP or REPEAT outside the part whose label lies inside it
/// arrives without passing the reset before the label, so it sets the counter to 0 itself before its jump line.
/// </summary>
internal sealed class HeidenhainRepeats
{
    private readonly Dictionary<Block, string> _counters = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Block, List<string>> _resets = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Block> _refused = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Block, List<string>> _entryResets = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Finds the repeats that need a counter and gives each a register of the range; reports those without one
    /// (CMP130).
    /// </summary>
    public static HeidenhainRepeats Of(LookAhead lookAhead, CounterRange? range, Diagnostics diagnostics)
    {
        var repeats = new HeidenhainRepeats();
        var sections = new Dictionary<Section, List<Block>>();
        var seen = new HashSet<Block>(ReferenceEqualityComparer.Instance);
        foreach (BlockStep step in lookAhead.Steps)
        {
            if (step.Section is not Section section || !seen.Add(step.Block))
            {
                continue;
            }

            if (!sections.TryGetValue(section, out List<Block>? blocks))
            {
                blocks = [];
                sections.Add(section, blocks);
            }

            blocks.Add(step.Block);
        }

        var needing = new List<RepeatPart>();
        foreach (List<Block> blocks in sections.Values)
        {
            blocks.Sort((one, other) => one.Line.CompareTo(other.Line));
            needing.AddRange(PartsLeft(blocks));
        }

        needing.Sort((one, other) => one.Repeat.Line.CompareTo(other.Repeat.Line));
        HashSet<string> named = HeidenhainSpills.NamedParameters(lookAhead);
        int next = range?.First ?? 0;
        foreach (RepeatPart part in needing)
        {
            while (range is not null && next <= range.Last && named.Contains(range.NameOf(next)))
            {
                next++;
            }

            if (range is null || next > range.Last)
            {
                diagnostics.Error(part.Repeat, DiagnosticCodes.HeidenhainRepeatWithoutCounter,
                    "A JUMP leaves the part this REPEAT repeats, so the compiler writes it as a counter loop instead "
                    + "of CALL LBL REP, whose count the control keeps as block state (D213), and "
                    + (range is null
                        ? "the machine names no [format] counter_registers"
                        : "every register of [format] counter_registers is taken or named by the program")
                    + "; the compiler never guesses a register (D388 (12)).");
                repeats._refused.Add(part.Repeat);
                continue;
            }

            string counter = range.NameOf(next++);
            repeats._counters.Add(part.Repeat, counter);
            Add(repeats._resets, part.Label, counter);
            foreach (Block entry in part.Entries)
            {
                Add(repeats._entryResets, entry, counter);
            }
        }

        return repeats;
    }

    /// <summary>
    /// The counter of a REPEAT block written as a counter loop; null for CALL LBL REP.
    /// </summary>
    public string? CounterOf(Block repeat)
    {
        return _counters.TryGetValue(repeat, out string? counter) ? counter : null;
    }

    /// <summary>
    /// Whether a REPEAT needs a counter loop and has no register (CMP130), so that nothing is written for it.
    /// </summary>
    public bool IsRefused(Block repeat)
    {
        return _refused.Contains(repeat);
    }

    /// <summary>
    /// The counters a JUMP or REPEAT block sets to 0 before its jump line, since it enters a counter loop's part from
    /// outside without passing the reset before the label (D213).
    /// </summary>
    public IReadOnlyList<string> ResetsOnEntry(Block jump)
    {
        return _entryResets.TryGetValue(jump, out List<string>? resets) ? resets : [];
    }

    private static void Add(Dictionary<Block, List<string>> map, Block block, string counter)
    {
        if (!map.TryGetValue(block, out List<string>? counters))
        {
            counters = [];
            map.Add(block, counters);
        }

        counters.Add(counter);
    }

    /// <summary>
    /// The counters set to 0 before the LBL of a LABEL block: one per counter loop that repeats to it.
    /// </summary>
    public IReadOnlyList<string> ResetsBefore(Block label)
    {
        return _resets.TryGetValue(label, out List<string>? resets) ? resets : [];
    }

    // The parts LABEL..REPEAT of one section, blocks in text order, that a JUMP to a label outside the part, a
    // JUMP=END or a RETURN leaves (D213).
    private static List<RepeatPart> PartsLeft(List<Block> blocks)
    {
        var parts = new List<RepeatPart>();
        for (int end = 0; end < blocks.Count; end++)
        {
            if (blocks[end].Find("REPEAT") is not Word repeat)
            {
                continue;
            }

            int start = blocks.FindLastIndex(end, block => Names(block, repeat.Value));
            if (start < 0)
            {
                continue;
            }

            for (int index = start; index <= end; index++)
            {
                if (Leaves(blocks, start, end, blocks[index]))
                {
                    parts.Add(new RepeatPart(blocks[start], blocks[end], Entries(blocks, start, end)));
                    break;
                }
            }
        }

        return parts;
    }

    // The JUMP and REPEAT blocks outside the part whose label lies inside it.
    private static List<Block> Entries(List<Block> blocks, int start, int end)
    {
        var entries = new List<Block>();
        for (int index = 0; index < blocks.Count; index++)
        {
            Word? jump = blocks[index].Find("JUMP") ?? blocks[index].Find("REPEAT");
            if ((index >= start && index <= end) || jump is null || jump.Value is IdentValue { Name: "END" })
            {
                continue;
            }

            int target = blocks.FindIndex(candidate => Names(candidate, jump.Value));
            if (target >= start && target <= end)
            {
                entries.Add(blocks[index]);
            }
        }

        return entries;
    }

    private static bool Leaves(List<Block> blocks, int start, int end, Block block)
    {
        if (block.Has("RETURN"))
        {
            return true;
        }

        if (block.Find("JUMP") is not Word jump)
        {
            return false;
        }

        if (jump.Value is IdentValue { Name: "END" })
        {
            return true;
        }

        int target = blocks.FindIndex(candidate => Names(candidate, jump.Value));
        return target < start || target > end;
    }

    // Whether the block is the LABEL of the value (language 4.9).
    private static bool Names(Block block, Value label)
    {
        return block.Find("LABEL")?.Value.ToCanonical() == label.ToCanonical();
    }
}
