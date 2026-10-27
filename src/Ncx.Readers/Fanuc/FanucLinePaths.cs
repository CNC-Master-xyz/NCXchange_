namespace Ncx.Readers.Fanuc;

/// <summary>
/// What the paths into a cycle block say of the "before" line of its entry, as FanucBeforeLineWalk walks them back
/// (D360): a path that reaches it with the line, one without it, one the reader does not see.
/// </summary>
internal sealed class FanucLinePaths
{
    /// <summary>
    /// The index of the cycle block among the blocks of the file; a path back to it decides nothing.
    /// </summary>
    public required int CycleIndex { get; init; }

    /// <summary>
    /// The "before" line of the entry, "M29 S{rpm}".
    /// </summary>
    public required string Before { get; init; }

    /// <summary>
    /// The native code of the entry, "G84"; null where the catalog has no entry.
    /// </summary>
    public string? Native { get; init; }

    /// <summary>
    /// True when a path reaches the cycle block with the line in force.
    /// </summary>
    public bool InForce { get; set; }

    /// <summary>
    /// True when a path reaches the cycle block without the line.
    /// </summary>
    public bool Without { get; set; }

    /// <summary>
    /// True when the reader does not see what runs before a block of a path.
    /// </summary>
    public bool Unseen { get; set; }

    /// <summary>
    /// The lines of the blocks that hold the line, one per path that has it.
    /// </summary>
    public List<int> Lines { get; } = [];

    /// <summary>
    /// The indices of the blocks the paths have passed, so that a loop is walked once.
    /// </summary>
    public HashSet<int> Visited { get; } = [];
}
