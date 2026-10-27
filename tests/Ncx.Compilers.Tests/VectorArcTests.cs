using Ncx.Compilers.Tests.Heidenhain;
using Ncx.Compilers.Tests.Siemens;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// D185: an ARC with a tool vector compiles to Heidenhain (LN has no arc form) and for now to Siemens as the ERROR
/// CMP017, like RAW of another controller: nothing is written.
/// </summary>
public sealed class VectorArcTests
{
    private static readonly string[] s_blocks =
    [
        "TCPM=ON",
        "RAPID X=10 Y=0 Z=100",
        "ARC=CCW X=0 Y=10 CENTER:X=0 CENTER:Y=0 TX=0 TY=0 TZ=1 F=100",
    ];

    [Fact]
    public void Heidenhain_VectorArc_IsCmp017()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(s_blocks));

        Assert.Empty(result.Files);
        Diagnostic error = HeidenhainCompile.Single(result, DiagnosticCodes.VectorArcNotWritable);
        Assert.Equal(Severity.Error, error.Severity);
    }

    [Fact]
    public void Siemens_VectorArc_IsCmp017()
    {
        CompileResult result = SiemensCompile.Run(SiemensCompile.Program(SiemensCompile.MillHeader, s_blocks),
            SiemensCompile.FiveAxis());

        Assert.Empty(result.Files);
        Assert.Contains(DiagnosticCodes.VectorArcNotWritable, SiemensCompile.Codes(result));
    }
}
