using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// [tool_points] and the [[tool_point]] rows of the machine file (machine-config 3, D373): the form, its keys, and
/// CFG010 for a row without its tool or point and for a register that serves as a length and a radius under offset
/// memory A or B.
/// </summary>
public sealed class ToolPointsLoaderTests
{
    private const string Head = """
        [machine]
        name = "Test mill"
        controller = "fanuc"

        [coolant]
        STANDARD = { ON = "M8", OFF = "M9" }

        """;

    // D373: the registers form with its keys and the rows.
    [Fact]
    public void Registers_KeysAndRows_AreRead()
    {
        MachineConfig machine = LoadClean(Head + """
            [tool_points]
            form = "registers"
            offset_memory = "C"
            length_register_step = 100
            radius_register_step = 200
            point_comment = true

            [[tool_point]]
            tool = 5
            point = 2
            length = 12
            radius = 12
            """);

        ToolPointsTable? table = machine.ToolPoints;
        Assert.NotNull(table);
        Assert.Equal(ToolPointForm.Registers, table.Form);
        Assert.Equal("C", table.OffsetMemory);
        Assert.Equal(100, table.LengthRegisterStep);
        Assert.Equal(200, table.RadiusRegisterStep);
        Assert.True(table.PointComment);
        Assert.Equal(new ToolPointRow { Tool = 5, Point = 2, Length = 12, Radius = 12 }, Assert.Single(table.Rows));
    }

    // D373 (1), (2): the forms of Klartext and Siemens with their keys.
    [Fact]
    public void IndexedToolAndEdge_KeysAreRead()
    {
        MachineConfig klartext = LoadClean(Head + "[tool_points]\nform = \"indexed_tool\"\npoint_change = \"call\"\n");
        MachineConfig siemens = LoadClean(Head + "[tool_points]\nform = \"edge\"\nedge_numbering = \"flat\"\n");

        Assert.True(klartext.ToolPoints?.PointChangeByCall);
        Assert.Equal(ToolPointForm.Edge, siemens.ToolPoints?.Form);
        Assert.True(siemens.ToolPoints?.FlatEdges);
    }

    // A machine file without [tool_points] has no mapping (D373, CMP016 at compile time).
    [Fact]
    public void WithoutTheTable_HasNoMapping()
    {
        Assert.Null(LoadClean(Head).ToolPoints);
    }

    // D373: a form the loader does not know is an ERROR.
    [Fact]
    public void UnknownForm_IsAnError()
    {
        Diagnostics diagnostics = Report(Head + "[tool_points]\nform = \"turret\"\n");

        Assert.Equal(Severity.Error, Assert.Single(diagnostics.Items).Severity);
    }

    // D373: a row without its tool or its point is CFG010.
    [Theory]
    [InlineData("point = 2\nedge = 20")]
    [InlineData("tool = 5\nedge = 20")]
    public void RowWithoutToolOrPoint_IsCfg010(string row)
    {
        Diagnostics diagnostics = Report(Head + "[tool_points]\nform = \"edge\"\n\n[[tool_point]]\n" + row + "\n");

        Assert.Equal(DiagnosticCodes.ToolPointConflict, Assert.Single(diagnostics.Items).Code);
    }

    // P2-01: a tool that is no whole number is the wrong type, CFG003, and nothing else.
    [Fact]
    public void RowWithAFractionalTool_IsCfg003Only()
    {
        Diagnostics diagnostics =
            Report(Head + "[tool_points]\nform = \"edge\"\n\n[[tool_point]]\ntool = 1.5\npoint = 2\n");

        Assert.Equal(DiagnosticCodes.WrongType, Assert.Single(diagnostics.Items).Code);
    }

    // D373 (3): under offset memory A or B, H and D share one pool, so one number as a length and as a radius is
    // CFG010, also within one row.
    [Theory]
    [InlineData("A", "tool = 5\npoint = 1\nlength = 5\nradius = 5")]
    [InlineData("B", "tool = 5\npoint = 1\nlength = 5\nradius = 25\n\n[[tool_point]]\ntool = 6\npoint = 1\nlength=25")]
    public void RegisterAsLengthAndRadiusUnderMemoryAOrB_IsCfg010(string memory, string rows)
    {
        Diagnostics diagnostics = Report(Head + "[tool_points]\nform = \"registers\"\noffset_memory = \"" + memory
            + "\"\n\n[[tool_point]]\n" + rows + "\n");

        Assert.Equal(DiagnosticCodes.ToolPointConflict, Assert.Single(diagnostics.Items).Code);
    }

    // D373 (3): the shared-pool CFG010 stands on the radius line of the row that reuses the register, not on the first
    // line of the file.
    [Fact]
    public void RegisterAsLengthAndRadius_IsReportedOnTheRadiusLineOfTheRow()
    {
        Diagnostics diagnostics = Report(Head + "[tool_points]\nform = \"registers\"\noffset_memory = \"A\"\n\n"
            + "[[tool_point]]\ntool = 5\npoint = 1\nlength = 5\nradius = 5\n");

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.ToolPointConflict, error.Code);
        Assert.Equal(15, error.Line);
    }

    // D373 (3): under memory C, and without the key, length = radius is legal.
    [Theory]
    [InlineData("offset_memory = \"C\"\n")]
    [InlineData("")]
    public void LengthEqualsRadiusUnderMemoryCOrWithoutTheKey_IsAccepted(string memory)
    {
        LoadClean(Head + "[tool_points]\nform = \"registers\"\n" + memory
            + "\n[[tool_point]]\ntool = 5\npoint = 1\nlength = 5\nradius = 5\n");
    }

    private static Diagnostics Report(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfigLoader.LoadText(toml, diagnostics);
        return diagnostics;
    }

    private static MachineConfig LoadClean(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(toml, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(machine);
        return machine;
    }
}
