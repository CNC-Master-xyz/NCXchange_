namespace Ncx.Core.Machine;

/// <summary>
/// A [[tool_point]] row of the machine file: the registers of one point of one tool, which win over the rule of the
/// form (machine-config 3, D373). Edge for the edge form, length and radius for the registers form.
/// </summary>
public sealed record ToolPointRow
{
    /// <summary>
    /// tool: the tool number.
    /// </summary>
    public required int Tool { get; init; }

    /// <summary>
    /// point: the measurement point k, from 1.
    /// </summary>
    public required int Point { get; init; }

    /// <summary>
    /// edge: the D number of the point on Siemens; null when left out.
    /// </summary>
    public int? Edge { get; init; }

    /// <summary>
    /// length: the length register H of the point on Fanuc; null when left out.
    /// </summary>
    public int? Length { get; init; }

    /// <summary>
    /// radius: the radius register D of the point on Fanuc; null when left out.
    /// </summary>
    public int? Radius { get; init; }
}
