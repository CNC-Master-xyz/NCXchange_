namespace Ncx.Analytics.ToolVectors;

/// <summary>
/// A rotary axis of [[axis]] as the tool-vector convention applies it (implementation 14, P4-03): named A, B or C, it
/// turns the tool vector about the machine X, Y or Z axis; a head axis turns the tool, a table axis the workpiece, the
/// axis of a tool spindle nothing (D325).
/// </summary>
internal sealed record ConventionAxis
{
    /// <summary>
    /// ncx: the name of the axis in the programs and in the position store, A, C2 (machine-config 4, D93).
    /// </summary>
    public required string NcxName { get; init; }

    /// <summary>
    /// id: the machine's own name of the axis, A1.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The machine axis it turns about: "X" for A, "Y" for B, "Z" for C.
    /// </summary>
    public required string About { get; init; }

    /// <summary>
    /// owner: the resource the axis belongs to (machine-config 4); null for an axis without owner.
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// True for a table axis, owned by a work spindle or a table, which turns the workpiece; false for a head axis,
    /// which turns the tool.
    /// </summary>
    public required bool Table { get; init; }

    /// <summary>
    /// True for the axis of a tool spindle, the spindle's own rotation (the C-axis mode of a tool spindle), which turns
    /// no tool vector (D325).
    /// </summary>
    public bool SpindleRotation { get; init; }

    /// <summary>
    /// True when the axis turns the tool relative to the workpiece while the holder holds it: a head axis always, a
    /// table axis only when that holder owns it, as the axis of another holder turns another part (language 4.10: the
    /// program machines the part held by the WORKPIECE resource), the axis of a tool spindle never (D325).
    /// </summary>
    /// <param name="workpieceHolder">The resource id of the workpiece holder of the block; null when the machine names
    /// none.</param>
    public bool AppliesWhile(string? workpieceHolder)
    {
        return !SpindleRotation && (!Table || Owner == workpieceHolder);
    }

    /// <summary>
    /// What the axis turns, as the report writes it: "the workpiece of TABLE1", "the tool of H1", "the tool, no owner",
    /// "nothing, the own rotation of the tool spindle S1".
    /// </summary>
    public string Turns()
    {
        if (Owner is null)
        {
            return "the tool, no owner";
        }

        if (SpindleRotation)
        {
            return "nothing, the own rotation of the tool spindle " + Owner;
        }

        return (Table ? "the workpiece of " : "the tool of ") + Owner;
    }
}
