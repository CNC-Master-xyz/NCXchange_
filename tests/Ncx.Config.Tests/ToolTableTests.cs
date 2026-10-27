using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Config.Tests;

/// <summary>
/// The tool table of D10: the tool kind, length and radius per tool, which [machine] tool_table names (machine-config
/// 1), read in the form of the TODO(question) of ToolTable.
/// </summary>
public sealed class ToolTableTests
{
    // Machine-config 1, D10, language 4.4: a tool is found by its number, or by its name for a tool called by name.
    [Fact]
    public void LoadText_ToolsByNumberAndByName_AreFoundByTheirTool()
    {
        var diagnostics = new Diagnostics("mill.tools.toml");

        ToolTable? table = ToolTable.LoadText("""
            [[tool]]
            number = 4
            kind = "TURNING"
            length = 35.5
            radius = 5

            [[tool]]
            name = "DRILL_D8"
            kind = "ROTARY"
            """, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(table);
        ToolData? four = table.Find(new ToolRef(4));
        Assert.Equal("TURNING", four?.Kind);
        Assert.Equal(35.5m, four?.Length);
        Assert.Equal(5m, four?.Radius);
        Assert.Equal("ROTARY", table.Find(new ToolRef("DRILL_D8"))?.Kind);
        Assert.Null(table.Find(new ToolRef(5)));
    }

    // P2-01: a tool without number and name cannot be found, an ERROR on its table; the file gives no table.
    [Fact]
    public void LoadText_ToolWithoutNumberAndName_IsAnError()
    {
        var diagnostics = new Diagnostics("mill.tools.toml");

        ToolTable? table = ToolTable.LoadText("""
            [[tool]]
            kind = "TURNING"
            """, diagnostics);

        Assert.Null(table);
        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(1, error.Line);
    }

    // P2-01: an unknown key is a WARNING that names the nearest known key, and the table still loads.
    [Fact]
    public void LoadText_UnknownKey_IsAWarningAndTheTableLoads()
    {
        var diagnostics = new Diagnostics("mill.tools.toml");

        ToolTable? table = ToolTable.LoadText("""
            [[tool]]
            number = 1
            knd = "ROTARY"
            """, diagnostics);

        Assert.NotNull(table);
        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Contains("kind", warning.Message, StringComparison.Ordinal);
    }

    // D376: [[tool.point]] rows give k, tip_offset, radius and wear per measurement point; wear defaults to 0.
    [Fact]
    public void LoadText_PointRows_AreFoundByTheirPoint()
    {
        var diagnostics = new Diagnostics("mill.tools.toml");

        ToolTable? table = ToolTable.LoadText("""
            [[tool]]
            number = 5
            length = 120
            radius = 8

            [[tool.point]]
            k = 1
            tip_offset = 0
            radius = 0

            [[tool.point]]
            k = 2
            tip_offset = 2
            radius = 8
            wear = -0.01
            """, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(table);
        ToolData five = table.Find(new ToolRef(5))!;
        Assert.Equal(2, five.Points.Count);
        ToolPointData? second = five.FindPoint(2);
        Assert.NotNull(second);
        Assert.Equal(2m, second.TipOffset);
        Assert.Equal(8m, second.Radius);
        Assert.Equal(-0.01m, second.Wear);
        Assert.Equal(0m, five.FindPoint(1)?.Wear);
        Assert.Null(five.FindPoint(3));
    }

    // D376: a tool without rows is point 1 with a = 0 and r = 0, and has no other point.
    [Fact]
    public void FindPoint_ToolWithoutRows_IsPointOneAtTheTip()
    {
        ToolTable table = ToolTable.LoadText("""
            [[tool]]
            number = 4
            """, new Diagnostics("mill.tools.toml"))!;
        ToolData four = table.Find(new ToolRef(4))!;

        Assert.Empty(four.Points);
        Assert.Equal(new ToolPointData { K = 1, TipOffset = 0m, Radius = 0m, Wear = 0m }, four.FindPoint(1));
        Assert.Null(four.FindPoint(2));
    }

    // D376: k counts from 1 and tip_offset is not negative; a row without k is an ERROR, and so is either value
    // out of its range, and the file gives no table.
    [Theory]
    [InlineData("tip_offset = 1")]
    [InlineData("k = 0")]
    [InlineData("k = 2\ntip_offset = -1")]
    [InlineData("k = 2\nradius = -1")]
    public void LoadText_PointRowOutOfRange_IsAnError(string row)
    {
        var diagnostics = new Diagnostics("mill.tools.toml");

        ToolTable? table = ToolTable.LoadText("[[tool]]\nnumber = 5\n\n[[tool.point]]\n" + row + "\n", diagnostics);

        Assert.Null(table);
        Assert.Equal(Severity.Error, Assert.Single(diagnostics.Items).Severity);
    }

    // D265, D376: an unknown key of a point row is a WARNING, as on the tool.
    [Fact]
    public void LoadText_UnknownKeyOfAPointRow_IsAWarning()
    {
        var diagnostics = new Diagnostics("mill.tools.toml");

        ToolTable? table = ToolTable.LoadText("[[tool]]\nnumber = 5\n\n[[tool.point]]\nk = 2\ntipoffset = 1\n",
            diagnostics);

        Assert.NotNull(table);
        Assert.Contains("tip_offset", Assert.Single(diagnostics.Items).Message, StringComparison.Ordinal);
    }

    // D376: the shipped table of the Fanuc mill has two tools, one of them with two points, and loads clean.
    [Fact]
    public void ShippedToolTable_FanucMill_HasTwoToolsOneWithTwoPoints()
    {
        string path = Path.Combine(Fixture.RepositoryRoot(), "machines", "fanuc-mill-30i.tools.toml");
        var diagnostics = new Diagnostics(path);

        ToolTable? table = ToolTable.LoadText(File.ReadAllText(path), diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(table);
        Assert.Equal(2, table.Numbered.Count);
        Assert.Contains(table.Numbered.Values, static tool => tool.Points.Count == 2);
    }
}
