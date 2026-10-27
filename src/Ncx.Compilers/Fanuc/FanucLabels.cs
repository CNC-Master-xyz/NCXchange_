using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The labels of one program or subprogram as Fanuc block numbers (controllers fanuc.md 1, 7; controller-mapping 6,
/// LABEL): a numeric LABEL is its own block number, the target of GOTO; the label after the header that the loop of
/// M99 returns to (D210); the block number of the end where a conditional JUMP=END goes.
/// </summary>
internal sealed class FanucLabels
{
    // The words of the header block of a program (D34): the label of the M99 loop stands right after them.
    private static readonly string[] s_headerKeys = ["FEED_MODE", "COMP", "UNITS", "WORKPLANE", "DIAMETER", "CYCLE"];

    private readonly Dictionary<string, int> _numbers = new(StringComparer.Ordinal);

    // The labels a JUMP of the section names.
    private readonly HashSet<string> _jumpTargets = new(StringComparer.Ordinal);

    // What the compiler knows of the paths to a label that a jump reaches, by the walk step of the LABEL block, one per
    // walk of the section (FanucPaths).
    private readonly Dictionary<int, FanucMeeting> _meetings = [];

    // The greatest block number the labels of the O program take or a contour took from them (D277, D278).
    private int _greatest;

    /// <summary>
    /// The labels of no section: FILE=BEGIN and FILE=END.
    /// </summary>
    public static FanucLabels None { get; } = new();

    /// <summary>
    /// The label after the header whose every jump is unconditional, written as M99 (D210); null for none.
    /// </summary>
    public string? LoopLabel { get; private set; }

    /// <summary>
    /// The block number of the end of the program that a conditional JUMP=END names; null when no such jump stands
    /// in it.
    /// </summary>
    public int? EndLabel { get; private set; }

    /// <summary>
    /// The block number of a label; null for a label the section does not have.
    /// </summary>
    /// <param name="label">The label as NCX writes it, "300", "WHILE_12".</param>
    public int? NumberOf(string label)
    {
        return _numbers.TryGetValue(label, out int number) ? number : null;
    }

    /// <summary>
    /// True for a label that a JUMP of the section names, which the control may reach from that jump as well as from
    /// the block before it (language 4.9, JUMP).
    /// </summary>
    /// <param name="label">The label as NCX writes it.</param>
    public bool IsJumpTarget(string label)
    {
        return _jumpTargets.Contains(label);
    }

    /// <summary>
    /// Whether the value of NCX under a key differs by the path on which the control reaches a label, by label and key,
    /// as FanucPaths finds it from the steps of the walk.
    /// </summary>
    public Dictionary<(string Label, string Key), bool> DiffersByPath { get; } = [];

    /// <summary>
    /// What the compiler knows of the paths on which the control reaches the LABEL of a step (virtual machine 1).
    /// </summary>
    /// <param name="label">The step of the LABEL block in its walk.</param>
    public FanucMeeting MeetingAt(BlockStep label)
    {
        if (!_meetings.TryGetValue(label.Index, out FanucMeeting? meeting))
        {
            meeting = new FanucMeeting();
            _meetings.Add(label.Index, meeting);
        }

        return meeting;
    }

    /// <summary>
    /// The next free block number of the O program, above every number its labels take and every number given out
    /// before: the P and Q of a contour written inline and the labels of that contour (D277, D278).
    /// </summary>
    public int Allocate()
    {
        _greatest = Math.Max(_greatest, EndLabel ?? 0) + 1;
        return _greatest;
    }

    /// <summary>
    /// The labels of a contour section written inline in an O program: each takes a free number of the program, so
    /// that no N stands twice in it (D277, D278).
    /// </summary>
    /// <param name="section">The contour section.</param>
    /// <param name="steps">Every step of the run.</param>
    /// <param name="program">The labels of the O program the contour stands in.</param>
    public static FanucLabels Renumbered(Section section, IReadOnlyList<BlockStep> steps, FanucLabels program)
    {
        FanucLabels labels = Of(section, steps);
        foreach (string label in new List<string>(labels._numbers.Keys))
        {
            labels._numbers[label] = program.Allocate();
        }

        return labels;
    }

    /// <summary>
    /// The labels of a section from the blocks of its walks.
    /// </summary>
    /// <param name="section">The program or subprogram.</param>
    /// <param name="steps">Every step of the run.</param>
    public static FanucLabels Of(Section section, IReadOnlyList<BlockStep> steps)
    {
        var labels = new FanucLabels();
        var blocks = new List<Block>();
        foreach (BlockStep step in steps)
        {
            if (step.Section == section && !blocks.Contains(step.Block))
            {
                blocks.Add(step.Block);
            }
        }

        blocks.Sort((first, second) => first.Line.CompareTo(second.Line));
        labels.Number(blocks);
        labels.FindLoop(section, blocks);
        return labels;
    }

    // A numeric LABEL is the block number N that GOTO names (controllers fanuc.md 1, 7). D277: a numeric label keeps
    // its number; a LABEL named by an identifier, as a reader lowers WHILE and IF THEN (controller-mapping 6), and the
    // end that a conditional JUMP=END names take the numbers above the greatest numeric label of their O program, in
    // the order they stand, the end last (fanuc 10 rule 6).
    private void Number(List<Block> blocks)
    {
        var identifiers = new List<string>();
        int greatest = 0;
        bool conditionalEnd = false;
        foreach (Block block in blocks)
        {
            if (block.Find("LABEL")?.Value is IntegerValue number)
            {
                _numbers[number.Text] = (int)number.Number;
                greatest = Math.Max(greatest, (int)number.Number);
            }
            else if (block.Find("LABEL")?.Value is IdentValue name && !identifiers.Contains(name.Name))
            {
                identifiers.Add(name.Name);
            }

            conditionalEnd |= block.Has("JUMP", null, "END") && block.Has("IF");
            if (JumpTargetOf(block) is string target)
            {
                _jumpTargets.Add(target);
            }
        }

        foreach (string identifier in identifiers)
        {
            _numbers[identifier] = ++greatest;
        }

        EndLabel = conditionalEnd ? greatest + 1 : null;
        _greatest = greatest;
    }

    // The loop of a main program: M99 jumps back to its first block, which the reader writes as a LABEL after the
    // header and a JUMP to it (controller-mapping 6, M99 paragraph).
    // D210: the compiler writes such a JUMP back as M99 where every jump to the label is unconditional; M99 takes no
    // condition, so a conditional one keeps the label's N and IF [ ] GOTO (decisions.md; fanuc 10 rule 3), and so does
    // the label of a REPEAT, whose loop is an IF GOTO (D280).
    private void FindLoop(Section section, List<Block> blocks)
    {
        if (section.Kind != SectionKind.Program || blocks.Count < 2)
        {
            return;
        }

        int index = 1;
        if (index < blocks.Count && IsHeader(blocks[index]))
        {
            index++;
        }

        if (index >= blocks.Count || blocks[index].Words.Count != 1 || blocks[index].Find("LABEL") is not Word label)
        {
            return;
        }

        string name = label.Value.ToCanonical();
        foreach (Block block in blocks)
        {
            if (JumpTargetOf(block) == name && (block.Has("IF") || block.Has("REPEAT")))
            {
                return;
            }
        }

        LoopLabel = name;
    }

    /// <summary>
    /// The label a block jumps to: the target of a JUMP, or the label of a REPEAT, whose IF GOTO returns to it (D280);
    /// null for any other block.
    /// </summary>
    public static string? JumpTargetOf(Block block)
    {
        return (block.Find("JUMP") ?? block.Find("REPEAT"))?.Value.ToCanonical();
    }

    // The header block of D34: state words only.
    private static bool IsHeader(Block block)
    {
        foreach (Word word in block.Words)
        {
            if (!s_headerKeys.Contains(word.Key))
            {
                return false;
            }
        }

        return block.Words.Count > 0;
    }
}
