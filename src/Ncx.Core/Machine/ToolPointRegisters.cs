namespace Ncx.Core.Machine;

/// <summary>
/// What a compiler writes to select one measurement point of a tool, as the [tool_points] mapping gives it (D373,
/// D375): the Klartext index, the Siemens edge, or the Fanuc length and radius registers.
/// </summary>
public sealed record ToolPointRegisters
{
    /// <summary>
    /// The index of the called tool, TOOL CALL 5.1; 0 for the plain call of point 1 (indexed_tool).
    /// </summary>
    public int? Index { get; init; }

    /// <summary>
    /// The cutting edge D (edge).
    /// </summary>
    public int? Edge { get; init; }

    /// <summary>
    /// The length register H (registers); null when the mapping gives none.
    /// </summary>
    public int? Length { get; init; }

    /// <summary>
    /// The radius register D (registers); null when the mapping gives none.
    /// </summary>
    public int? Radius { get; init; }
}
