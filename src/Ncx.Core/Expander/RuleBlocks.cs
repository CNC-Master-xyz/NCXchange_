using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.Expander;

/// <summary>
/// An expansion rule as generated NCX blocks around the block that triggered it (machine-config 5a, architecture 5.5).
/// </summary>
internal static class RuleBlocks
{
    /// <summary>
    /// Puts the blocks of a rule around the block: before it the @SAVE of every variable of restore, the words that
    /// establish requires and the pre blocks; after it the post blocks and the @RESTORE of every variable of restore
    /// (architecture 5.5). The rule of a catalog cycle covers the cycle while it is active, so its post and @RESTORE
    /// go to the block that ends the cycle instead (D204).
    /// </summary>
    /// <param name="triggered">The rule and the table it stands in.</param>
    /// <param name="expansion">The block the rule fired on.</param>
    /// <param name="generated">The writer of generated blocks.</param>
    /// <param name="machine">The machine file.</param>
    /// <param name="cycleEnds">Where the blocks of a cycle rule's end are kept; null outside the expander's pass.
    /// </param>
    /// <param name="cycleEnd">The block the cycle ends at; null when no end was found, which drops the end blocks.
    /// </param>
    public static void Apply(TriggeredRule triggered, BlockExpansion expansion, GeneratedText generated,
        MachineConfig machine, CycleRuleEnds? cycleEnds = null, CycleEnd? cycleEnd = null)
    {
        ExpansionRule rule = triggered.Rule;
        Block origin = expansion.Origin;
        var before = new List<Block>();
        List<Block> after = [];

        // requires: the expander inserts the words that establish the conditions, preceded by @SAVE of the variables
        // named in restore (machine-config 5a). restore puts back what @SAVE kept, so the @SAVE blocks stand first
        // whenever the rule has a restore list, with or without requires; the flowchart of architecture 5.5 draws them
        // in the requires branch, where every example has them.
        foreach (string variable in rule.Restore)
        {
            Add(before, generated.FromRule("@SAVE=" + StateKeys.WithRole(variable, machine), "restore", triggered,
                origin, GeneratedPlacement.Before));
        }

        // One block per condition, state variable = value, in the order the rule writes them (machine-config 5a).
        foreach (KeyValuePair<string, string> condition in rule.Requires)
        {
            Add(before, generated.FromRule(StateKeys.WithRole(condition.Key, machine) + "=" + condition.Value,
                "requires", triggered, origin, GeneratedPlacement.Before));
        }

        // pre and post: lists of NCX blocks inserted before and after the block (machine-config 5a).
        foreach (string text in rule.Pre)
        {
            Add(before, generated.FromRule(text, "pre", triggered, origin, GeneratedPlacement.Before));
        }

        foreach (string text in rule.Post)
        {
            Add(after, generated.FromRule(text, "post", triggered, origin, GeneratedPlacement.After));
        }

        // restore: the state variables put back after the block through @RESTORE; the virtual machine re-applies the
        // saved value from its own snapshot, so the rule never knows the speed the spindle had (machine-config 5a,
        // virtual machine 3.10).
        foreach (string variable in rule.Restore)
        {
            Add(after, generated.FromRule("@RESTORE=" + StateKeys.WithRole(variable, machine), "restore", triggered,
                origin, GeneratedPlacement.After));
        }

        // D204: the post blocks and the @RESTORE of a catalog cycle's rule stand at the block that ends the cycle, so
        // that the mode or the required state holds at every CYCLE_CALL of the cycle. A cycle the section never ends
        // (the scan found no end) gets no end blocks.
        if (triggered.CoversCycle && cycleEnds is not null)
        {
            if (cycleEnd is not null)
            {
                cycleEnds.Add(cycleEnd, after);
            }

            after = [];
        }

        if (!expansion.Surround(before, after))
        {
            generated.Error(origin, DiagnosticCodes.GeneratedBlockOutsideSection,
                $"The blocks of {triggered.Source} would stand before a BEGIN or after an END block, outside every "
                + "program and subprogram (language 4.13).");
        }
    }

    private static void Add(List<Block> blocks, Block? block)
    {
        if (block is not null)
        {
            blocks.Add(block);
        }
    }
}
