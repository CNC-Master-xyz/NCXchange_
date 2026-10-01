namespace Ncx.Core.Machine;

/// <summary>
/// The optional keys of a function table, the tool change or a catalog cycle, from which the expander makes generated
/// NCX blocks around the triggering block (machine-config 5a, virtual machine 3.10). A function table carries one rule
/// and may name the states it fires on (D198).
/// </summary>
public sealed record ExpansionRule
{
    /// <summary>
    /// states: the states of the function table that trigger the rule, <c>states = ["ON"]</c> on the through-spindle
    /// coolant. Empty means every state of the table triggers it (machine-config 5a, D198). The tool change and a
    /// catalog cycle have no states and leave it empty.
    /// </summary>
    // TODO(question): D198 settles which states fire the one rule of a table, not how a machine that needs two
    // different sequences for one state is written. The DMG chuck has M436 "open with the spindle standing still"
    // and M433 "open with the spindle running", two codes for one state under two conditions, which this shape
    // reaches only through two function entries. Until that is settled a table has one rule.
    public IReadOnlyList<string> States { get; init; } = [];

    /// <summary>
    /// pre: NCX blocks inserted before the block, "HOME Z", "RAPID {position:tool_change} FRAME=MACHINE" (D100).
    /// </summary>
    public IReadOnlyList<string> Pre { get; init; } = [];

    /// <summary>
    /// post: NCX blocks inserted after the block.
    /// </summary>
    public IReadOnlyList<string> Post { get; init; } = [];

    /// <summary>
    /// requires: state conditions that must hold while the function is written, SPINDLE = "OFF".
    /// </summary>
    public IReadOnlyDictionary<string, string> Requires { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// restore: the state variables put back after the block through @RESTORE, "SPINDLE".
    /// </summary>
    public IReadOnlyList<string> Restore { get; init; } = [];
}
