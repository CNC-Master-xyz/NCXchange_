using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The REPEAT ranges of a file and their counters (D213, D280; controller-mapping 6, REPEAT + TIMES; machine-config
/// 7): one variable of [variables] repeat_counters per nesting depth, the first for the outermost repeat. A repeat
/// inside a subprogram counts the ranges around the CALL of it as well, so that a called repeat never takes the
/// counter of the repeat that calls it.
/// </summary>
internal sealed class FanucRepeats
{
    private readonly List<FanucRepeatRange> _ranges = [];

    // The Fanuc variables the program uses itself, which no counter may be (D280).
    private FanucRepeatVariables? _variables;

    /// <summary>
    /// The ranges of a file without REPEAT.
    /// </summary>
    public static FanucRepeats None { get; } = new();

    /// <summary>
    /// How many counters the file needs: the deepest nesting plus one, 0 without REPEAT.
    /// </summary>
    public int Counters { get; private set; }

    /// <summary>
    /// The range of a REPEAT block; null where its label is not in its section.
    /// </summary>
    public FanucRepeatRange? RangeOf(Section? section, Block repeat)
    {
        return _ranges.Find(range => range.Section == section && range.Last == repeat.Line);
    }

    /// <summary>
    /// True where the program uses a Fanuc variable itself, as VAR or in an expression.
    /// </summary>
    /// <param name="variable">The variable as the machine file writes it, "#149".</param>
    public bool Uses(string variable)
    {
        return _variables?.Uses(variable) == true;
    }

    /// <summary>
    /// The ranges a jump leaves: those that hold the jump and not its target, innermost first (D213).
    /// </summary>
    /// <param name="section">The section of the JUMP.</param>
    /// <param name="jump">The line of the JUMP block.</param>
    /// <param name="target">The line of the target label; null for JUMP=END, which leaves every range.</param>
    public List<FanucRepeatRange> LeftBy(Section? section, int jump, int? target)
    {
        var left = new List<FanucRepeatRange>();
        foreach (FanucRepeatRange range in _ranges)
        {
            if (range.Section == section && range.Holds(jump) && (target is not int line || !range.Holds(line)))
            {
                left.Add(range);
            }
        }

        left.Sort((first, second) => second.Depth.CompareTo(first.Depth));
        return left;
    }

    /// <summary>
    /// The ranges of a file from the blocks of its walks, with the variables it uses.
    /// </summary>
    /// <param name="steps">Every step of the run.</param>
    /// <param name="machine">The machine, whose [variables] map names the variables of the other families.</param>
    /// <param name="templates">The templates of the machine.</param>
    public static FanucRepeats Of(IReadOnlyList<BlockStep> steps, MachineConfig machine, TemplateSet templates)
    {
        var repeats = new FanucRepeats { _variables = new FanucRepeatVariables(machine, templates) };
        var blocks = new Dictionary<Section, List<Block>>();
        foreach (BlockStep step in steps)
        {
            if (step.Section is not Section section)
            {
                continue;
            }

            if (!blocks.TryGetValue(section, out List<Block>? list))
            {
                list = [];
                blocks.Add(section, list);
            }

            if (!list.Contains(step.Block))
            {
                list.Add(step.Block);
                repeats._variables.Add(step.Block);
            }
        }

        var local = new List<FanucRepeatRange>();
        foreach (KeyValuePair<Section, List<Block>> section in blocks)
        {
            local.AddRange(RangesOf(section.Key, section.Value));
        }

        Dictionary<Section, int> bases = BaseDepths(blocks, local);
        foreach (FanucRepeatRange range in local)
        {
            int depth = bases.GetValueOrDefault(range.Section) + Around(local, range.Section, range.First, range.Last);
            repeats._ranges.Add(range with { Depth = depth });
            repeats.Counters = Math.Max(repeats.Counters, depth + 1);
        }

        return repeats;
    }

    // The ranges of one section, each with the number of other ranges around it.
    private static List<FanucRepeatRange> RangesOf(Section section, List<Block> blocks)
    {
        var ranges = new List<FanucRepeatRange>();
        foreach (Block block in blocks)
        {
            if (block.Find("REPEAT") is not Word repeat)
            {
                continue;
            }

            string label = repeat.Value.ToCanonical();
            Block? first = blocks.Find(other => other.Find("LABEL")?.Value.ToCanonical() == label
                && other.Line <= block.Line);
            if (first is not null)
            {
                ranges.Add(new FanucRepeatRange(section, label, first.Line, block.Line, 0));
            }
        }

        return ranges;
    }

    // The ranges of a section that hold the lines first..last and are not that range itself.
    private static int Around(List<FanucRepeatRange> ranges, Section section, int first, int last)
    {
        int count = 0;
        foreach (FanucRepeatRange range in ranges)
        {
            bool same = range.First == first && range.Last == last;
            if (range.Section == section && !same && range.First <= first && last <= range.Last)
            {
                count++;
            }
        }

        return count;
    }

    // The depth a subprogram starts at: the deepest CALL of it, the ranges around the CALL and the depth its own
    // section starts at; a recursion stops after one pass per section.
    private static Dictionary<Section, int> BaseDepths(Dictionary<Section, List<Block>> blocks,
        List<FanucRepeatRange> ranges)
    {
        var bases = new Dictionary<Section, int>();
        for (int pass = 0; pass < blocks.Count; pass++)
        {
            bool changed = false;
            foreach (KeyValuePair<Section, List<Block>> caller in blocks)
            {
                foreach (Block block in caller.Value)
                {
                    if (block.Find("CALL")?.Value is not Value call || call is StringValue)
                    {
                        continue;
                    }

                    string name = call.ToCanonical();
                    int depth = bases.GetValueOrDefault(caller.Key)
                        + Around(ranges, caller.Key, block.Line, block.Line);
                    foreach (Section callee in blocks.Keys)
                    {
                        if (callee.Kind == SectionKind.Sub && callee.Name == name
                            && depth > bases.GetValueOrDefault(callee))
                        {
                            bases[callee] = depth;
                            changed = true;
                        }
                    }
                }
            }

            if (!changed)
            {
                break;
            }
        }

        return bases;
    }
}
