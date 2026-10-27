using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config;

// D265: the tool table is TOML made of [[tool]] tables with number (or name, for a tool that a program calls by name,
// language 4.4), kind (a key of [tool_change] kind_map, D52), length and radius, in the manner of the [[axis]] and
// [[resource]] tables of the machine file; an unknown key is a WARNING (decisions.md; machine-config 1).

/// <summary>
/// The tool table of D10, the optional file that [machine] tool_table names: the tool kind, length and radius per
/// tool (machine-config 1). A compiler takes {kind} from it (machine-config 3, D52); without it, or for a tool it does
/// not describe, {kind} takes the default and the compiler writes a warning block at the head of the program (D10).
/// </summary>
public sealed record ToolTable
{
    // The file is made of [[tool]] tables, each with these keys.
    private static readonly string[] s_tables = ["tool"];
    private static readonly string[] s_toolKeys = ["number", "name", "kind", "length", "radius", "point"];

    // D376: a [[tool]] carries optional [[tool.point]] rows with these keys.
    private static readonly string[] s_pointKeys = ["k", "tip_offset", "radius", "wear"];

    /// <summary>
    /// The tools by number, TOOL=4.
    /// </summary>
    public IReadOnlyDictionary<int, ToolData> Numbered { get; init; } = new Dictionary<int, ToolData>();

    /// <summary>
    /// The tools by name, TOOL="DRILL_D8" (language 4.4).
    /// </summary>
    public IReadOnlyDictionary<string, ToolData> Named { get; init; } = new Dictionary<string, ToolData>();

    /// <summary>
    /// The data of a tool; null when the table does not describe it.
    /// </summary>
    /// <param name="tool">The tool by number or by name.</param>
    public ToolData? Find(ToolRef tool)
    {
        if (tool.Number is int number)
        {
            return Numbered.TryGetValue(number, out ToolData? numbered) ? numbered : null;
        }

        return tool.Name is string name && Named.TryGetValue(name, out ToolData? named) ? named : null;
    }

    /// <summary>
    /// Loads a tool table from its text; its mistakes are diagnostics on their lines, a wrong type or a tool without
    /// number and name an ERROR, an unknown key a WARNING (P2-01).
    /// </summary>
    /// <param name="text">The TOML text of the tool table.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The table, or null when the file has an ERROR.</returns>
    public static ToolTable? LoadText(string text, Diagnostics diagnostics)
    {
        int errorsBefore = TomlDocument.ErrorCount(diagnostics);
        TomlDocument? document = TomlDocument.Parse(text, diagnostics);
        if (document is null)
        {
            return null;
        }

        ConfigTable root = document.Root("machine-config 1, D10");
        root.WarnUnknownTables(s_tables, s_tables);
        var numbered = new Dictionary<int, ToolData>();
        var named = new Dictionary<string, ToolData>(StringComparer.Ordinal);
        foreach (ConfigTable tool in root.Tables("tool"))
        {
            // Every tool is found by its number, or by its name when a program calls it by name (language 4.4).
            tool.WarnUnknownKeys(s_toolKeys);
            var data = new ToolData
            {
                Kind = tool.Text("kind"),
                Length = tool.Number("length"),
                Radius = tool.Number("radius"),
                Points = PointsOf(tool),
            };
            if (tool.Integer("number") is int number)
            {
                numbered[number] = data;
            }
            else if (tool.Text("name") is string name)
            {
                named[name] = data;
            }
            else
            {
                tool.MissingKey("number");
            }
        }

        if (TomlDocument.ErrorCount(diagnostics) > errorsBefore)
        {
            return null;
        }

        return new ToolTable { Numbered = numbered, Named = named };
    }

    // D376: the [[tool.point]] rows of a tool, k from 1 and required, tip_offset not negative, radius and wear 0 when
    // left out; the register radius is not negative either, as POINT_RADIUS is not (D372). A value out of its range is
    // an ERROR on its line.
    private static List<ToolPointData> PointsOf(ConfigTable tool)
    {
        var points = new List<ToolPointData>();
        foreach (ConfigTable row in tool.Tables("point"))
        {
            row.WarnUnknownKeys(s_pointKeys);
            int? k = row.RequiredInteger("k");
            decimal tipOffset = row.Number("tip_offset") ?? 0m;
            decimal radius = row.Number("radius") ?? 0m;
            if (k is < 1)
            {
                OutOfRange(row, "k", "an integer from 1");
            }

            if (tipOffset < 0m)
            {
                OutOfRange(row, "tip_offset", "not negative");
            }

            if (radius < 0m)
            {
                OutOfRange(row, "radius", "not negative");
            }

            if (k is int point)
            {
                points.Add(new ToolPointData
                {
                    K = point,
                    TipOffset = tipOffset,
                    Radius = radius,
                    Wear = row.Number("wear") ?? 0m,
                });
            }
        }

        return points;
    }

    private static void OutOfRange(ConfigTable row, string key, string range)
    {
        row.Diagnostics.Error(row.LineOf(key), DiagnosticCodes.ValueNotAllowed,
            $"{key} of {row.Name} must be {range} (machine-config 1, D376).");
    }
}
