using Ncx.Core.Machine;
using Ncx.Core.Model;
using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// The measurement points on SINUMERIK (D373 edge, D375, D393): a named point writes the cutting edge D k or the edge
/// of its row, never TOFFL, TOFF, TOFFR or DL; a program without point words compiles as before.
/// </summary>
public sealed class SiemensToolPointTests
{
    // The form of the shipped mill, with flat edge numbers and the edge of point 2 of tool 5 in a row (D373 (2)).
    private const string FlatEdges = "form = \"edge\"\nedge_numbering = \"flat\"\n\n[[tool_point]]\ntool = 5\n"
        + "point = 2\nedge = 20\n";

    // D373 (2): point 2 of the tool is the cutting edge D2, and a and r are not written (D375).
    [Fact]
    public void PointTwo_IsTheCuttingEdgeD2()
    {
        Assert.Equal(Lines("T5 M6", "D2"), MillBody("TOOL=5 TOOL_POINT=2 TIP_OFFSET=2 POINT_RADIUS=8"));
    }

    // D372, D373: a point change without a tool change switches the edge of the active tool, D2 then D1.
    [Fact]
    public void PointChangeWithoutToolChange_SwitchesTheEdge()
    {
        string body = MillBody("TOOL=5 TOOL_POINT=2", "TOOL_POINT=1");

        Assert.Equal(Lines("T5 M6", "D2", "D1"), body);
        Assert.DoesNotContain("TOFF", body, StringComparison.Ordinal);
    }

    // D375: a program without point words compiles as before, no D where no OFFSET stands.
    [Fact]
    public void ProgramWithoutPointWords_WritesNoEdge()
    {
        Assert.Equal(Lines("T5 M6"), MillBody("TOOL=5"));
    }

    // D373 (2): a row wins over D k; under flat numbering it is the only source of an edge.
    [Fact]
    public void FlatNumbering_TheRowGivesTheEdge()
    {
        MachineConfig machine = Replaced(MillFile, "form = \"edge\"", FlatEdges);

        string body = Body(Run(Program(MillHeader, "TOOL=5 TOOL_POINT=2"), machine));

        Assert.Equal(Lines("T5 M6", "D20"), body);
    }

    // D373 (2), CMP514: under flat numbering a point without a row has no edge, an ERROR.
    [Fact]
    public void FlatNumbering_PointWithoutARow_IsCmp514()
    {
        MachineConfig machine = Replaced(MillFile, "form = \"edge\"", FlatEdges);

        CompileResult result = Run(Program(MillHeader, "TOOL=6 TOOL_POINT=2"), machine);

        Assert.Contains(DiagnosticCodes.SiemensToolPointNotMapped, CompilerCodes(result));
    }
}
