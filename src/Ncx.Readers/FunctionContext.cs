namespace Ncx.Readers;

/// <summary>
/// What the source state of a reader says about a native code it maps to the states of the function tables: the
/// channel it reads, the spindles the source addresses and what holds before the block (architecture 7, D155). The
/// reader of a controller family fills it from its own source state.
/// </summary>
public sealed record FunctionContext
{
    /// <summary>
    /// The channel the program being read belongs to, 2 for the lower unit of the Nakamura; null when the reader does
    /// not know it, and then no channel binding rules a state out (D56).
    /// </summary>
    public int? Channel { get; init; }

    /// <summary>
    /// The spindle roles the source addresses for this code: the side [workpiece] selected, the P11 of the Doosan MX,
    /// the master spindle of SETMS (controller-mapping 4); null when the source names none, and then no spindle is
    /// ruled out.
    /// </summary>
    public IReadOnlyCollection<string>? AddressedSpindles { get; init; }

    /// <summary>
    /// The state that holds before the block per word of a table, "COOLANT:STANDARD" = "ON", "SPINDLE_MODE:MAIN" =
    /// "AXIS"; a word left out is not on. Null when the reader keeps no such state, and then no state is dropped as
    /// holding already or as the OFF of a resource that is not on.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Holding { get; init; }
}
