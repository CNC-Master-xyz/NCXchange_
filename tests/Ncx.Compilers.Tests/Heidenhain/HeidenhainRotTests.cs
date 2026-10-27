using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// The transformation type of a tilted plane in Klartext: the compiler always writes COORD ROT or TABLE ROT through
/// the {rot} of [transform] rot, an absent ROT as TABLE ROT, since NCX defaults to TABLE and the TNC 640 to COORD ROT
/// (language 4.2; machine-config 5; D191, D269).
/// </summary>
public sealed class HeidenhainRotTests
{
    // A table with an A and a C axis, PLANE SPATIAL with {move} and {rot}, the rot map of machine-config 5.
    private const string TiltingTable = """
        [transform]
        TILT_ON = "PLANE SPATIAL SPA{a} SPB{b} SPC{c} {move} {rot}"
        TILT_AXIS_ON = "PLANE AXIAL A{a} C{c} {move}"
        TILT_OFF = "PLANE RESET STAY"
        move = { TURN = "TURN FMAX", MOVE = "MOVE ABST50 FMAX", STAY = "STAY" }
        rot = { TABLE = "TABLE ROT", COORD = "COORD ROT" }

        [[axis]]
        id = "A1"
        ncx = "A"
        letter = "A"
        kind = "rotary"
        owner = "TABLE1"

        [[axis]]
        id = "C1"
        ncx = "C"
        letter = "C"
        kind = "rotary"
        owner = "TABLE1"
        """;

    // D191, D269: an absent ROT means TABLE in NCX, and the compiler writes it, since the control's default is COORD.
    [Fact]
    public void Tilt_WithoutRot_WritesTableRot()
    {
        CompileResult result = HeidenhainCompile.Run(
            HeidenhainCompile.Program("TILT A=0 B=45 C=0 MOVE=TURN", "TILT=RESET"),
            HeidenhainCompile.MillAnd(TiltingTable));

        Assert.Equal("PLANE SPATIAL SPA0 SPB45 SPC0 TURN FMAX TABLE ROT\nPLANE RESET STAY",
            HeidenhainCompile.Body(result));
    }

    // D269: ROT=COORD is COORD ROT.
    [Fact]
    public void Tilt_WithRotCoord_WritesCoordRot()
    {
        CompileResult result = HeidenhainCompile.Run(
            HeidenhainCompile.Program("TILT A=0 B=45 C=0 MOVE=STAY ROT=COORD"),
            HeidenhainCompile.MillAnd(TiltingTable));

        Assert.Equal("PLANE SPATIAL SPA0 SPB45 SPC0 STAY COORD ROT", HeidenhainCompile.Body(result));
        Assert.DoesNotContain(DiagnosticCodes.HeidenhainRotNotWritten, HeidenhainCompile.Codes(result));
    }

    // D269: a ROT on a TILT_AXIS block has no effect: an INFO, and nothing is written for it.
    [Fact]
    public void TiltAxis_WithRot_IsTheInfoCmp128AndWritesNoRot()
    {
        CompileResult result = HeidenhainCompile.Run(
            HeidenhainCompile.Program("TILT_AXIS A=10 C=90 MOVE=STAY ROT=COORD"),
            HeidenhainCompile.MillAnd(TiltingTable));

        Assert.Equal("PLANE AXIAL A10 C90 STAY", HeidenhainCompile.Body(result));
        Diagnostic info = HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainRotWithoutEffect);
        Assert.Equal(Severity.Info, info.Severity);
        Assert.Equal(4, info.Line);
    }

    // D269: on a machine without any rotary axis no tilt frees one, so ROT has no effect: an INFO, not written.
    [Fact]
    public void Tilt_WithRotOnAMachineWithoutRotaryAxis_IsTheInfoCmp128AndWritesNoRot()
    {
        var mill = HeidenhainCompile.MillAnd("""
            [transform]
            TILT_ON = "PLANE SPATIAL SPA{a} SPB{b} SPC{c} {move} {rot}"
            move = { STAY = "STAY" }
            rot = { TABLE = "TABLE ROT", COORD = "COORD ROT" }
            """);

        CompileResult result = HeidenhainCompile.Run(
            HeidenhainCompile.Program("TILT A=0 B=45 C=0 MOVE=STAY ROT=TABLE"), mill);

        Assert.Equal("PLANE SPATIAL SPA0 SPB45 SPC0 STAY", HeidenhainCompile.Body(result));
        Assert.Equal(Severity.Info,
            HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainRotWithoutEffect).Severity);
    }

    // Language 4.2; D269: a machine without the rot map ignores a ROT with a WARNING.
    [Fact]
    public void Tilt_WithRotOnAMachineWithoutTheMap_IsTheWarningCmp108()
    {
        string withoutMap = TiltingTable.Replace("rot = { TABLE = \"TABLE ROT\", COORD = \"COORD ROT\" }\n", "",
            StringComparison.Ordinal).Replace(" {rot}", "", StringComparison.Ordinal);

        CompileResult result = HeidenhainCompile.Run(
            HeidenhainCompile.Program("TILT A=0 B=45 C=0 MOVE=STAY ROT=TABLE"), HeidenhainCompile.MillAnd(withoutMap));

        Assert.Equal("PLANE SPATIAL SPA0 SPB45 SPC0 STAY", HeidenhainCompile.Body(result));
        Assert.Equal(Severity.Warning,
            HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainRotNotWritten).Severity);
    }

    // A machine without the map and a TILT without ROT: nothing to report.
    [Fact]
    public void Tilt_WithoutRotOnAMachineWithoutTheMap_ReportsNothing()
    {
        string withoutMap = TiltingTable.Replace("rot = { TABLE = \"TABLE ROT\", COORD = \"COORD ROT\" }\n", "",
            StringComparison.Ordinal).Replace(" {rot}", "", StringComparison.Ordinal);

        CompileResult result = HeidenhainCompile.Run(
            HeidenhainCompile.Program("TILT A=0 B=45 C=0 MOVE=STAY"), HeidenhainCompile.MillAnd(withoutMap));

        Assert.Equal("PLANE SPATIAL SPA0 SPB45 SPC0 STAY", HeidenhainCompile.Body(result));
        Assert.Empty(result.Diagnostics.Items);
    }
}
