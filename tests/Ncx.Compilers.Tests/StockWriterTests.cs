using Ncx.Compilers.Tests.Fanuc;
using Ncx.Compilers.Tests.Heidenhain;
using Ncx.Compilers.Tests.Siemens;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// D351: STOCK through the [stock] templates of the machine, BLK FORM on the iTNC 530 and WORKPIECE(...) on the
/// 840D sl; a form without a template is the INFO CMP012, a stock the form cannot express the INFO CMP013, and a
/// program with a RAW blank of the target writes the RAW text alone with the WARNING CMP014 (machine-config 5).
/// </summary>
public sealed class StockWriterTests
{
    private const string Box = "STOCK=BOX X=0 Y=0 Z=-40 MAX:X=100 MAX:Y=100.5 MAX:Z=0";

    [Fact]
    public void Heidenhain_Box_IsBlkFormWithSigns()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("ORIGIN=1", Box, "RAPID Z=50"));

        Assert.Contains("BLK FORM 0.1 Z X+0 Y+0 Z-40\nBLK FORM 0.2 X+100 Y+100,5 Z+0\n",
            HeidenhainCompile.Body(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Heidenhain_Cylinder_IsCmp012()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "ORIGIN=1", "STOCK=CYLINDER X=-50 Y=-50 Z=-40 MAX:X=50 MAX:Y=50 MAX:Z=0", "RAPID Z=50"));

        Diagnostic info = HeidenhainCompile.Single(result, DiagnosticCodes.StockFormWithoutTemplate);
        Assert.Equal(Severity.Info, info.Severity);
        Assert.DoesNotContain("BLK FORM", HeidenhainCompile.TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Heidenhain_RawBlankBesideStock_WritesTheRawAloneWithCmp014()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "RAW:HEIDENHAIN=\"BLK FORM 0.1 Z X+0 Y+0 Z-20\"", "ORIGIN=1", Box, "RAPID Z=50"));

        string text = HeidenhainCompile.TextOf(result);
        Assert.Contains("BLK FORM 0.1 Z X+0 Y+0 Z-20", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Z-40", text, StringComparison.Ordinal);
        Assert.Equal(Severity.Warning,
            HeidenhainCompile.Single(result, DiagnosticCodes.StockBesideRawBlank).Severity);
    }

    [Fact]
    public void Siemens_BoxAndCentredCylinder_AreWorkpiece()
    {
        Assert.Equal(SiemensCompile.Lines("G54", "WORKPIECE(,,,\"BOX\",240,0,-40,-40,0,0,100,100.5)", "G0 Z50"),
            SiemensCompile.MillBody("ORIGIN=1", Box, "RAPID Z=50"));
        Assert.Equal(SiemensCompile.Lines("G54", "WORKPIECE(,,,\"CYLINDER\",192,0,-40,-40,100)", "G0 Z50"),
            SiemensCompile.MillBody("ORIGIN=1", "STOCK=CYLINDER X=-50 Y=-50 Z=-40 MAX:X=50 MAX:Y=50 MAX:Z=0",
                "RAPID Z=50"));
    }

    [Fact]
    public void Siemens_CylinderOffTheDatum_IsCmp013()
    {
        CompileResult result = SiemensCompile.Run(SiemensCompile.Program(SiemensCompile.MillHeader, "ORIGIN=1",
            "STOCK=CYLINDER X=0 Y=0 Z=-40 MAX:X=100 MAX:Y=100 MAX:Z=0", "RAPID Z=50"), SiemensCompile.Mill());

        Assert.Contains(DiagnosticCodes.StockNotExpressible, SiemensCompile.Codes(result));
        Assert.DoesNotContain("WORKPIECE", SiemensCompile.TextOf(result), StringComparison.Ordinal);
    }

    // A BOX template without {axis} is written about Z: under WORKPLANE=ZX it is CMP013.
    [Fact]
    public void Siemens_BoxUnderZx_IsCmp013()
    {
        CompileResult result = SiemensCompile.Run(SiemensCompile.Program(SiemensCompile.MillHeader, "WORKPLANE=ZX",
            "ORIGIN=1", Box, "RAPID Z=50"), SiemensCompile.Mill());

        Assert.Contains(DiagnosticCodes.StockNotExpressible, SiemensCompile.Codes(result));
        Assert.DoesNotContain("WORKPIECE", SiemensCompile.TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Fanuc_Box_IsCmp012()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program("ORIGIN=1", Box, "RAPID Z=50"),
            FanucCompile.Mill());

        Diagnostic info = Assert.Single(result.Diagnostics.Items,
            diagnostic => diagnostic.Code == DiagnosticCodes.StockFormWithoutTemplate);
        Assert.Equal(Severity.Info, info.Severity);
    }
}
