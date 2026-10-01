using Ncx.Core.Machine;

namespace Ncx.Core.Expander;

/// <summary>
/// An expansion rule a block triggers, with the table of the machine file it stands in (machine-config 5a).
/// </summary>
/// <param name="Rule">The pre, post, requires and restore of the table.</param>
/// <param name="Source">The table, as a generated block names its origin: "[tool_change]", "[coolant] THROUGH",
/// "cycle PECK".</param>
/// <param name="CoversCycle">True for the rule of a catalog cycle, whose post and @RESTORE stand at the block that
/// ends the cycle and not after the block that defines it (D204).</param>
internal sealed record TriggeredRule(ExpansionRule Rule, string Source, bool CoversCycle = false);
