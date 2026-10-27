using Ncx.Core.Machine;

namespace Ncx.Config;

/// <summary>
/// One tool of the tool table: its kind, length and radius (machine-config 1, D10) and its measurement points (D376),
/// every number in the machine's units_default.
/// </summary>
public sealed record ToolData
{
    // A tool without point rows is point 1 with a = 0 and r = 0 (D376).
    private static readonly ToolPointData s_pointOneAtTheTip = new() { K = 1, TipOffset = 0m };

    /// <summary>
    /// kind: the tool kind, ROTARY or TURNING, a key of [tool_change] kind_map that gives the value of {kind}
    /// (machine-config 3, D52); null when left out.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    /// length: the tool length; null when left out.
    /// </summary>
    public decimal? Length { get; init; }

    /// <summary>
    /// radius: the tool radius; null when left out.
    /// </summary>
    public decimal? Radius { get; init; }

    /// <summary>
    /// The [[tool.point]] rows in the order of the file (D376); empty for a tool without rows.
    /// </summary>
    public IReadOnlyList<ToolPointData> Points { get; init; } = [];

    /// <summary>
    /// The row of point k: the row the table gives, point 1 at the tip for a tool without rows, null for any other
    /// point the table does not describe (D376).
    /// </summary>
    public ToolPointData? FindPoint(int k)
    {
        foreach (ToolPointData point in Points)
        {
            if (point.K == k)
            {
                return point;
            }
        }

        return Points.Count == 0 && k == 1 ? s_pointOneAtTheTip : null;
    }
}
