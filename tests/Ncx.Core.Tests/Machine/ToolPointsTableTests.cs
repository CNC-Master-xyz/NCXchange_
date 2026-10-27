using Ncx.Core.Machine;

namespace Ncx.Core.Tests.Machine;

/// <summary>
/// The [tool_points] mapping of D373: tool and measurement point to the Klartext index, the Siemens edge or the Fanuc
/// registers, and null where the form cannot map the point.
/// </summary>
public sealed class ToolPointsTableTests
{
    // D373 (1): indexed_tool writes index k - 1, so point 1 is the plain call and k at most 10.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(10, 9)]
    public void IndexedTool_PointK_IsIndexKMinusOne(int point, int index)
    {
        var table = new ToolPointsTable { Form = ToolPointForm.IndexedTool };

        Assert.Equal(index, table.Map(5, point)?.Index);
    }

    // D373 (1): a Klartext tool has at most nine indices, so point 11 has no mapping.
    [Fact]
    public void IndexedTool_PointEleven_HasNoMapping()
    {
        Assert.Null(new ToolPointsTable { Form = ToolPointForm.IndexedTool }.Map(5, 11));
    }

    // D373 (2): edge writes D k by default, and a row wins over it.
    [Fact]
    public void Edge_WithoutARow_IsDkAndARowWins()
    {
        var table = new ToolPointsTable
        {
            Form = ToolPointForm.Edge,
            Rows = [new ToolPointRow { Tool = 5, Point = 2, Edge = 20 }],
        };

        Assert.Equal(2, table.Map(4, 2)?.Edge);
        Assert.Equal(20, table.Map(5, 2)?.Edge);
    }

    // D373 (2): under edge_numbering = "flat" only a row maps a point.
    [Fact]
    public void Edge_FlatNumberingWithoutARow_HasNoMapping()
    {
        var table = new ToolPointsTable
        {
            Form = ToolPointForm.Edge,
            FlatEdges = true,
            Rows = [new ToolPointRow { Tool = 5, Point = 2, Edge = 20 }],
        };

        Assert.Null(table.Map(5, 1));
        Assert.Equal(20, table.Map(5, 2)?.Edge);
    }

    // D373 (3), (5): registers from a row, else by the shop's step rule register = tool + (k - 1) * step.
    [Fact]
    public void Registers_RowWinsOverTheStepRule()
    {
        var table = new ToolPointsTable
        {
            Form = ToolPointForm.Registers,
            LengthRegisterStep = 100,
            RadiusRegisterStep = 200,
            Rows = [new ToolPointRow { Tool = 5, Point = 2, Length = 12, Radius = 13 }],
        };

        Assert.Equal(new ToolPointRegisters { Length = 12, Radius = 13 }, table.Map(5, 2));
        Assert.Equal(new ToolPointRegisters { Length = 6, Radius = 6 }, table.Map(6, 1));
        Assert.Equal(new ToolPointRegisters { Length = 106, Radius = 206 }, table.Map(6, 2));
    }

    // D373: nothing is derived from the tool number that the machine file does not state.
    [Fact]
    public void Registers_WithoutARowOrARule_HasNoMapping()
    {
        Assert.Null(new ToolPointsTable { Form = ToolPointForm.Registers }.Map(5, 1));
    }

    // D373 (4): tool management writes H99 and D99 for point 1 only; point 2 needs a row.
    [Fact]
    public void Registers_ManagedMapsPointOneOnly()
    {
        var table = new ToolPointsTable { Form = ToolPointForm.Registers, Managed = true };

        Assert.Equal(new ToolPointRegisters { Length = 99, Radius = 99 }, table.Map(5, 1));
        Assert.Null(table.Map(5, 2));
    }
}
