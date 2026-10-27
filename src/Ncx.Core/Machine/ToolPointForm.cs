namespace Ncx.Core.Machine;

/// <summary>
/// form of [tool_points]: how the control selects a measurement point of a tool (machine-config 3, D373).
/// </summary>
public enum ToolPointForm
{
    /// <summary>
    /// indexed_tool, Heidenhain Klartext: point k is the index k-1 of the called tool, TOOL CALL 5.1.
    /// </summary>
    IndexedTool,

    /// <summary>
    /// edge, Siemens: point k is the cutting edge D k of the active tool, or the edge of a [[tool_point]] row.
    /// </summary>
    Edge,

    /// <summary>
    /// registers, Fanuc: point k selects the length register H and the radius register D of a row, of the step
    /// rule or of tool management.
    /// </summary>
    Registers,
}
