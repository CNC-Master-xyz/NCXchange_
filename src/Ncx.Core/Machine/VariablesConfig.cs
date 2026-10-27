namespace Ncx.Core.Machine;

/// <summary>
/// [variables]: what reading an unassigned variable does, how variable names compile to another family, and the
/// limits of INTERPRETED mode (machine-config 7, virtual machine 3.6).
/// </summary>
public sealed record VariablesConfig
{
    /// <summary>
    /// unassigned: reading an unassigned variable is an ERROR unless the file sets unassigned = 0 (virtual machine
    /// 3.6, D38).
    /// </summary>
    public UnassignedVariable Unassigned { get; init; } = UnassignedVariable.Error;

    /// <summary>
    /// map: variable prefix to native prefix, Q = "#1" compiles Q1 to #101; empty when left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> Map { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// declared: the names the machine declares outside the program, the Siemens GUD and the builder's variables
    /// (XMW_1, GC1_XDREMI); the Siemens compiler never declares one of them with a DEF, which would be an alarm, and
    /// declares every other name a unit uses (machine-config 7, D261). Empty when left out.
    /// </summary>
    public IReadOnlyList<string> Declared { get; init; } = [];

    /// <summary>
    /// block_cap: the blocks INTERPRETED mode executes before the ERROR "possible endless loop"; 1 000 000 when left
    /// out (virtual machine 3.6).
    /// </summary>
    public long BlockCap { get; init; } = 1_000_000;

    /// <summary>
    /// call_depth: how deep calls and repeats may nest; 8 when left out (virtual machine 3.6, 3.9).
    /// </summary>
    public int CallDepth { get; init; } = 8;

    /// <summary>
    /// repeat_counters: the variables the Fanuc compiler counts a REPEAT in, one per nesting depth, ["#199", "#198"];
    /// empty when left out, and the compiler reports an ERROR for a REPEAT that needs one (machine-config 7, D280).
    /// </summary>
    public IReadOnlyList<string> RepeatCounters { get; init; } = [];
}
