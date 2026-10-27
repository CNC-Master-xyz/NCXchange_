using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Expressions;
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
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

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
        return _used.Contains(variable.Trim());
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
    /// <param name="variable">The Fanuc variable of an NCX variable name without an ERROR; null where none
    /// maps.</param>
    public static FanucRepeats Of(IReadOnlyList<BlockStep> steps, Func<string, string?> variable)
    {
        var repeats = new FanucRepeats();
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
                repeats.AddUsed(step.Block, variable);
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

    // The variables of a block: the VAR words and every variable of an expression.
    private void AddUsed(Block block, Func<string, string?> variable)
    {
        foreach (Word word in block.Words)
        {
            if (word.Key == "VAR" && word.Addr is string name && variable(name) is string assigned)
            {
                _used.Add(assigned);
            }

            if (word.Value is ExprValue { Tree: ExprNode tree })
            {
                AddUsed(tree, variable);
            }
        }
    }

    private void AddUsed(ExprNode node, Func<string, string?> variable)
    {
        switch (node)
        {
            case VariableNode { Index: null } named when variable(named.Name) is string used:
                _used.Add(used);
                break;
            case UnaryNode unary:
                AddUsed(unary.Operand, variable);
                break;
            case ParenthesesNode parentheses:
                AddUsed(parentheses.Inner, variable);
                break;
            case BinaryNode binary:
                AddUsed(binary.Left, variable);
                AddUsed(binary.Right, variable);
                break;
            case CallNode call:
                foreach (ExprNode argument in call.Arguments)
                {
                    AddUsed(argument, variable);
                }

                break;
        }
    }

    /// <summary>
    /// The Fanuc variable of a V name or a name of [variables] map without a diagnostic, for the check of D280; null
    /// for any other name (FanucExpressions.Variable writes them).
    /// </summary>
    /// <param name="map">[variables] map of the machine.</param>
    /// <param name="templates">The templates of the machine.</param>
    /// <param name="name">The NCX variable name, "V149", "Q49".</param>
    public static string? QuietVariable(IReadOnlyDictionary<string, string> map, TemplateSet templates, string name)
    {
        if (name.Length > 1 && name[0] == 'V' && name.Substring(1).All(char.IsAsciiDigit))
        {
            return "#" + name.Substring(1).TrimStart('0');
        }

        int digits = name.Length;
        while (digits > 0 && char.IsAsciiDigit(name[digits - 1]))
        {
            digits--;
        }

        if (digits == name.Length || !map.TryGetValue(name.Substring(0, digits), out string? native))
        {
            return null;
        }

        var quiet = new Diagnostics("repeat counters");
        Template template = templates.For(native) ?? new Template(native, 1, quiet);
        var values = new TemplateValues();
        values.Set("index", decimal.Parse(name.AsSpan(digits), NumberStyles.None, CultureInfo.InvariantCulture));
        return template.Render(values, new Block { Line = 0, Words = [] }, quiet);
    }
}
