using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.Expander;

/// <summary>
/// Which expansion rules of the machine configuration a block triggers (machine-config 5, 5a): the rule of the tool
/// change for a TOOL word, of every function table one of its words sets, of the catalog entry of its cycle.
/// </summary>
internal static class ExpansionRules
{
    // The coolant channel that a bare COOLANT addresses (virtual machine 2.5, F29).
    private const string DefaultCoolantChannel = "STANDARD";

    /// <summary>
    /// The rules a block triggers, each table's rule once, in the canonical order of the words that trigger them
    /// (language 5 rule 6).
    /// </summary>
    public static IReadOnlyList<TriggeredRule> Of(Block block, MachineConfig machine)
    {
        // The blocks of several rules on one block nest: the rules in canonical order of their triggering words, each
        // table's rule once, the first outermost; the rewriters' blocks stand inside them, each rewriter inside the
        // one before; generated blocks are not expanded again (D199; architecture 5.5).
        var triggered = new List<TriggeredRule>();
        foreach (Word word in CanonicalOrder.Sort(block))
        {
            if (RuleOf(word, machine) is TriggeredRule rule && !Contains(triggered, rule.Source))
            {
                triggered.Add(rule);
            }
        }

        return triggered;
    }

    // Every function table, the tool change and every catalog cycle may carry pre, post, requires and restore, and a
    // function table also states (machine-config 5a, D198): TOOL triggers the rule of [tool_change]; SPINDLE, RPM,
    // ORIENT, CSS, VC and RPM_MAX the rule of the [spindle.ROLE] table of their spindle, whose states they set
    // (machine-config 5); SPINDLE_MODE the rule of [spindle_mode.ROLE], SPINDLE_SYNC and PHASE the rule of
    // [spindle_sync], COOLANT the rule of its [coolant] channel, FUNC the rule of its [func] entry, CYCLE the rule of
    // the catalog entry of its cycle. D198: a rule that names states fires only on those; without the key every state
    // of the table fires it, so COOLANT:THROUGH=OFF stops and restarts the spindle only when the table says so.
    private static TriggeredRule? RuleOf(Word word, MachineConfig machine)
    {
        string? state = FunctionStates.Of(word);
        switch (word.Key)
        {
            case "TOOL":
                return Triggered(machine.ToolChange?.Rule, "[tool_change]", state: null);
            case "SPINDLE" or "RPM" or "ORIENT" or "CSS" or "VC" or "RPM_MAX":
                string? spindleRole = word.Addr ?? StateKeys.RoleOfDefaultSpindle(machine);
                return spindleRole is null
                    ? null
                    : Triggered(TableOf(machine.SpindleTables, spindleRole)?.Rule, "[spindle." + spindleRole + "]",
                        state);
            case "SPINDLE_MODE":
                string? modeRole = word.Addr ?? StateKeys.RoleOfDefaultSpindle(machine);
                return modeRole is null
                    ? null
                    : Triggered(TableOf(machine.SpindleModeTables, modeRole)?.Rule,
                        "[spindle_mode." + modeRole + "]", state);
            case "SPINDLE_SYNC" or "PHASE":
                return Triggered(machine.SpindleSync?.Rule, "[spindle_sync]", state);
            case "COOLANT":
                string channel = word.Addr ?? DefaultCoolantChannel;
                return Triggered(TableOf(machine.Coolant, channel)?.Rule, "[coolant] " + channel, state);
            case "FUNC" when word.Addr is string function:
                return Triggered(TableOf(machine.Functions, function)?.Rule, "[func] " + function, state);
            // D204: the rule of a catalog cycle covers the cycle while it is active. It fires on the block that names
            // the cycle, never on a CYCLE_CALL block, and its post and @RESTORE stand at the block that ends the
            // cycle (decisions.md; machine-config 5a, language 4.7.1, CycleRuleEnds).
            case "CYCLE":
                return CycleEntryOf(word, machine) is CycleEntry entry
                    ? Triggered(entry.Rule, "cycle " + entry.Name, state: null, coversCycle: true)
                    : null;
            default:
                return null;
        }
    }

    // A cycle is named by CYCLE=name, a name of the cycle catalog (language 4.7.1), or written natively as
    // CYCLE:<controller>=n, which the catalog of that controller family passes through and finds by its native cycle,
    // n compared as the catalog writes it (CycleCatalog.FindNative, D144). CYCLE=OFF names no cycle.
    private static CycleEntry? CycleEntryOf(Word word, MachineConfig machine)
    {
        CycleCatalog catalog = machine.CycleCatalog;
        if (word.Addr is null)
        {
            return word.Value is IdentValue name && name.Name != "OFF" ? catalog.Find(name.Name) : null;
        }

        return catalog.PassesThrough(word.Addr) ? catalog.FindNative(CycleCatalog.NativeOf(word.Value)) : null;
    }

    private static FunctionTable? TableOf(IReadOnlyDictionary<string, FunctionTable> tables, string name)
    {
        return tables.TryGetValue(name, out FunctionTable? table) ? table : null;
    }

    // D198: a rule that names states fires only on the states it names, because on a real machine the states of one
    // table carry different conditions; a rule without the key fires on every state of its table, and the tool change
    // and a catalog cycle have no state at all.
    private static TriggeredRule? Triggered(ExpansionRule? rule, string source, string? state, bool coversCycle = false)
    {
        if (rule is null)
        {
            return null;
        }

        if (rule.States.Count == 0)
        {
            return new TriggeredRule(rule, source, coversCycle);
        }

        foreach (string named in rule.States)
        {
            if (named == state)
            {
                return new TriggeredRule(rule, source, coversCycle);
            }
        }

        return null;
    }

    private static bool Contains(List<TriggeredRule> triggered, string source)
    {
        foreach (TriggeredRule rule in triggered)
        {
            if (rule.Source == source)
            {
                return true;
            }
        }

        return false;
    }
}
