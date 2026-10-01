using Ncx.Core.Model;

namespace Ncx.Core.Expander;

/// <summary>
/// Where the post blocks and the @RESTORE of a catalog cycle's expansion rule stand (machine-config 5a, D204). The
/// rule covers the cycle while it is active, so they stand at the block that ends the cycle: directly after a
/// <c>CYCLE=OFF</c>, and directly before every other block that ends it (the next <c>CYCLE</c>, a <c>TOOL</c>, the
/// <c>PROGRAM=END</c> or the <c>SUB=END</c> of the section; virtual machine 4, cycle row). The end is found in the
/// text of the section, without virtual machine state (architecture 5.5), and a <c>JUMP</c> out of the range skips
/// them like any block it jumps over.
/// </summary>
internal sealed class CycleRuleEnds
{
    private readonly Dictionary<int, List<Block>> _before = [];
    private readonly Dictionary<int, List<Block>> _after = [];

    /// <summary>
    /// The block that ends the cycle a block defines, and whether the rule's blocks stand after it (a
    /// <c>CYCLE=OFF</c>) or before it (every other end); null when the block stands in no section.
    /// </summary>
    /// <param name="program">The parsed program.</param>
    /// <param name="index">The block that names the cycle.</param>
    /// <param name="section">The program or subprogram that block stands in.</param>
    public static CycleEnd? EndOf(NcxProgram program, int index, Section? section)
    {
        if (section is null)
        {
            return null;
        }

        for (int end = index + 1; end <= section.LastBlock; end++)
        {
            Block block = program.Blocks[end];
            if (block.Find("CYCLE") is Word cycle)
            {
                // CYCLE=OFF ends the cycle in its own block, so the rule's blocks follow it; the next CYCLE replaces
                // the cycle, so they stand before that block (virtual machine 4, cycle row).
                bool off = cycle.Addr is null && cycle.Value is IdentValue { Name: "OFF" };
                return new CycleEnd(end, off && end != section.LastBlock);
            }

            // TOOL ends the cycle with a WARNING, and the PROGRAM=END or SUB=END of the section ends it too; the
            // blocks stand before the block that ends it (virtual machine 4, cycle row; language 4.13).
            if (block.Has("TOOL") || end == section.LastBlock)
            {
                return new CycleEnd(end, false);
            }
        }

        return null;
    }

    /// <summary>
    /// Keeps the blocks of a cycle rule for the block that ends the cycle.
    /// </summary>
    /// <param name="end">The end the scan found.</param>
    /// <param name="blocks">The post blocks and the @RESTORE of the rule, in their order.</param>
    public void Add(CycleEnd end, IReadOnlyList<Block> blocks)
    {
        Dictionary<int, List<Block>> at = end.AfterTheBlock ? _after : _before;
        if (!at.TryGetValue(end.Index, out List<Block>? kept))
        {
            kept = [];
            at[end.Index] = kept;
        }

        kept.AddRange(blocks);
    }

    /// <summary>
    /// The blocks that stand before this block, outside everything its own rules generate; empty for every block no
    /// cycle ends at.
    /// </summary>
    /// <param name="index">The block of the file.</param>
    public IReadOnlyList<Block> Before(int index)
    {
        return _before.TryGetValue(index, out List<Block>? blocks) ? blocks : [];
    }

    /// <summary>
    /// The blocks that stand after this block; empty for every block no cycle ends at.
    /// </summary>
    /// <param name="index">The block of the file.</param>
    public IReadOnlyList<Block> After(int index)
    {
        return _after.TryGetValue(index, out List<Block>? blocks) ? blocks : [];
    }
}

/// <summary>
/// The block a cycle ends at and where the rule's blocks stand (D204).
/// </summary>
/// <param name="Index">The block of the file that ends the cycle.</param>
/// <param name="AfterTheBlock">True after a <c>CYCLE=OFF</c>, false before every other end.</param>
internal sealed record CycleEnd(int Index, bool AfterTheBlock);
