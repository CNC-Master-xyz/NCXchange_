using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// The measurement points in Klartext (D373 indexed_tool, D375, D393): point k is the index k-1 of the called tool,
/// TOOL CALL 5.1, never DL, DR or DR2; a point change of the tool in the spindle is an indexed TOOL CALL with the
/// WARNING CMP123 unless point_change = "call"; a point beyond the nine indices is the ERROR CMP124 (D388); a program
/// without point words compiles as before.
/// </summary>
public sealed class HeidenhainToolPointTests
{
    // D373 (1), D375: point 2 of tool 5 is TOOL CALL 5.1, and a and r are not written.
    [Fact]
    public void PointTwo_IsTheIndexOneOfTheCalledTool()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=5 TOOL_POINT=2 TIP_OFFSET=2 POINT_RADIUS=8 RPM=1592"));

        Assert.Equal("TOOL CALL 5.1 Z S1592", HeidenhainCompile.Body(result));
    }

    // D373 (1): point 1 is the plain call.
    [Fact]
    public void PointOne_IsThePlainCall()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOOL=5 TOOL_POINT=1 RPM=1592"));

        Assert.Equal("TOOL CALL 5 Z S1592", HeidenhainCompile.Body(result));
    }

    // D373 (1): a point change of the tool in the spindle is an indexed TOOL CALL with the axis and the speed, and
    // the WARNING CMP123 names the change macro.
    [Fact]
    public void PointChangeOfTheToolInTheSpindle_IsAnIndexedCallWithCmp123()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "TOOL=5 RPM=1592", "TOOL_POINT=2 TIP_OFFSET=2"));

        Assert.Equal("TOOL CALL 5 Z S1592\nTOOL CALL 5.1 Z S1592", HeidenhainCompile.Body(result));
        Diagnostic warning = HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainToolPoint);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Contains("MP7480.0", warning.Message, StringComparison.Ordinal);
    }

    // D373: point_change = "call" says the builder's macro accepts it, and the WARNING goes.
    [Fact]
    public void PointChangeWithPointChangeCall_DoesNotWarn()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOOL=5 RPM=1592", "TOOL_POINT=2"),
            HeidenhainCompile.MillWith("form = \"indexed_tool\"", "form = \"indexed_tool\"\npoint_change = \"call\""));

        Assert.Equal("TOOL CALL 5 Z S1592\nTOOL CALL 5.1 Z S1592", HeidenhainCompile.Body(result));
        Assert.DoesNotContain(DiagnosticCodes.HeidenhainToolPoint, HeidenhainCompile.Codes(result));
    }

    // D373 (1), D388 (C5): point 11 is beyond the nine indices, the ERROR CMP124; CMP123 stays the WARNING of the
    // point change.
    [Fact]
    public void PointEleven_IsAnErrorCmp124()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOOL=5 TOOL_POINT=11 RPM=1592"));

        Diagnostic error = HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainToolPointBeyondIndices);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Contains("Point 11 of tool 5", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(DiagnosticCodes.HeidenhainToolPoint, HeidenhainCompile.Codes(result));
    }

    // D373 (1): point 10 is the last index, TOOL CALL 5.9, and no ERROR.
    [Fact]
    public void PointTen_IsTheLastIndex()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOOL=5 TOOL_POINT=10 RPM=1592"));

        Assert.Equal("TOOL CALL 5.9 Z S1592", HeidenhainCompile.Body(result));
        Assert.DoesNotContain(DiagnosticCodes.HeidenhainToolPointBeyondIndices, HeidenhainCompile.Codes(result));
    }

    // D375: a program without point words compiles as before.
    [Fact]
    public void ProgramWithoutPointWords_IsThePlainCall()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("TOOL=5 RPM=1592"));

        Assert.Equal("TOOL CALL 5 Z S1592", HeidenhainCompile.Body(result));
    }
}
