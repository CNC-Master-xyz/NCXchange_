using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// The M codes of a block under [format] m_codes_per_block and m_codes_combinable (D339), the family codes of the
/// spindle tables (D258), strings in custom macro B (D169) and the cylinder interpolation G7.1 (D143, D146).
/// </summary>
public sealed class FanucFunctionsTests
{
    // The named functions M10 and M11 of a clamp and M85 of a door beside the coolant M8 and M9 (machine-config 5).
    private const string Clamp = "[func]\nCLAMP = { ON = \"M10\", OFF = \"M11\" }\nDOOR = { OPEN = \"M85\" }";

    // D339: without m_codes_per_block a block carries one M code, which every control accepts; the others move to
    // the following lines.
    [Fact]
    public void D339_WithoutTheKey_OneMCodePerBlock()
    {
        string body = FanucCompile.Body("SPINDLE=CW RPM=1000 COOLANT=ON");

        Assert.Equal("S1000 M3\nM8\n", body);
    }

    // D339: m_codes_per_block = 3 without a list puts two function codes in one block.
    [Fact]
    public void D339_ThreePerBlock_PutsTwoFunctionCodesInOneBlock()
    {
        Assert.Equal("M10 M8\n", BodyWith("m_codes_per_block = 3", "FUNC:CLAMP=ON COOLANT=ON"));
    }

    // D339: with m_codes_combinable only a listed code is the second M code of a block.
    [Theory]
    [InlineData("m_codes_combinable = [\"M8\"]", "M10 M8\n")]
    [InlineData("m_codes_combinable = [\"M10\"]", "M10\nM8\n")]
    [InlineData("m_codes_combinable = []", "M10\nM8\n")]
    public void D339_Combinable_OnlyAListedCodeFollows(string list, string expected)
    {
        Assert.Equal(expected, BodyWith("m_codes_per_block = 3\n" + list, "FUNC:CLAMP=ON COOLANT=ON"));
    }

    // D339 with the Mori NTX1000SZM programming manual p.253, note 2: a spindle code never shares a block with
    // another M code, a listed one among them.
    [Fact]
    public void D339_SpindleCode_NeverSharesABlock()
    {
        Assert.Equal("S1000 M3\nM8\n",
            BodyWith("m_codes_per_block = 3\nm_codes_combinable = [\"M8\"]", "SPINDLE=CW RPM=1000 COOLANT=ON"));
    }

    // D339: M00 and M01 stand without another M code, whatever the keys say.
    [Fact]
    public void D339_OptionalStop_StandsAlone()
    {
        Assert.Equal("M8\nM1\n", BodyWith("m_codes_per_block = 3", "COOLANT=ON STOP=OPTIONAL"));
    }

    // D339: from the first function that does not fit in the main line, it and every later one stand on the lines
    // after it, so M10, which would fit beside M9, still follows M85.
    [Fact]
    public void D339_FunctionThatDoesNotFit_KeepsTheOrderOfTheLaterOnes()
    {
        Assert.Equal("M9\nM85 M10\n", BodyWith("m_codes_per_block = 3\nm_codes_combinable = [\"M10\"]",
            "COOLANT=OFF FUNC:DOOR=OPEN FUNC:CLAMP=ON"));
    }

    // D339, language 5 rule 3: where the functions do not all fit in a block with a motion, they stand on lines
    // before it, so the spindle and the coolant run before the tool moves.
    [Fact]
    public void D339_MotionWithFunctionsThatDoNotFit_WritesThemBeforeTheMotion()
    {
        Assert.Equal("S1000 M3\nM8\nG1 G17 X5. F100.\n",
            BodyWith("", "LINE X=5 F=100 SPINDLE=CW RPM=1000 COOLANT=ON"));
    }

    // D339: the spindle stop and the coolant OFF stand before the named functions, and a spindle start after them,
    // so a clamp never opens while its spindle turns.
    [Fact]
    public void D339_StopsBeforeTheNamedFunctions_StartsAfterThem()
    {
        Assert.Equal("S1000 M3\nM5\nM9\nM11\nM10\nS2000 M3\n",
            BodyWith("", "SPINDLE=CW RPM=1000", "FUNC:CLAMP=OFF SPINDLE=OFF COOLANT=OFF",
                "SPINDLE=CW RPM=2000 FUNC:CLAMP=ON"));
    }

    // D258: where the role's table lacks the key, VC is G96 S, CSS_OFF G97 and RPM_MAX G92 S on a mill.
    [Fact]
    public void D258_TableWithoutTheKeys_WritesTheFamilyCodes()
    {
        string machine = FanucCompile.Mill().Replace("VC = \"G96 S{value}\"\n", "", StringComparison.Ordinal)
            .Replace("CSS_OFF = \"G97\"\n", "", StringComparison.Ordinal)
            .Replace("RPM_MAX = \"G92 S{value}\"\n", "", StringComparison.Ordinal);

        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("RPM_MAX=3000", "CSS=ON VC=200", "CSS=OFF"), machine));

        Assert.Contains("\nG92 S3000\nG96 S200\nG97\n", text, StringComparison.Ordinal);
    }

    // D258: RPM_MAX is G50 S in G-code system A.
    [Fact]
    public void D258_SystemA_RpmMaxIsG50()
    {
        string machine = FanucCompile.Lathe().Replace("RPM_MAX = \"G50 S{value}\"\n", "", StringComparison.Ordinal);

        string text = FanucCompile.TextOf(FanucCompile.Run(FanucCompile.Program("RPM_MAX=2500")
            .Replace(" WORKPLANE=XY", " WORKPLANE=ZX", StringComparison.Ordinal), machine));

        Assert.Contains("\nG50 S2500\n", text, StringComparison.Ordinal);
    }

    // D169: == of two strings has no form in custom macro B, CMP316.
    [Fact]
    public void D169_StringComparison_IsTheErrorCmp316()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("VAR:V1=\"A\"", "VAR:V2=\"B\"", "IF={$V1 == $V2} JUMP=END"), FanucCompile.Mill());

        Assert.Contains(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucStringComparison);
    }

    // D143: under G7.1 an arc takes R only, so ARC with CENTER under CYLINDER is the ERROR CMP317.
    [Fact]
    public void D143_CenterUnderCylinder_IsTheErrorCmp317()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("CYLINDER=30", "LINE Z=0 C=0 F=100", "ARC=CW Z=10 C=10 CENTER:Z=10 CENTER:C=0"),
            FanucCompile.Mill(extra: FanucCompile.TableRotaryAxis));

        Assert.Contains(FanucCompile.CompilerDiagnostics(result),
            diagnostic => diagnostic.Code == DiagnosticCodes.FanucCenterUnderCylinder);
    }

    // D146: G7.1 names the rotary axis of the workpiece holder by its letter, and the C word under CYLINDER is
    // written with that letter, never the NCX letter.
    [Fact]
    public void D146_Cylinder_WritesTheLetterOfTheRotaryAxis()
    {
        string axes = FanucCompile.TableRotaryAxis.Replace("ncx = \"C\"\nletter = \"C\"", "ncx = \"A\"\nletter = \"A\"",
            StringComparison.Ordinal);

        string body = FanucCompile.Body("CYLINDER=30\nLINE Z=0 C=10 F=100\nCYLINDER=OFF", axes);

        Assert.Equal("G7.1 A30.\nG1 G17 Z0. A10. F100.\nG7.1 A0\n", body);
    }

    // The body of a program on the mill with further [format] keys and the CLAMP function.
    private static string BodyWith(string options, params string[] blocks)
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(FanucCompile.Program(blocks),
            FanucCompile.Mill(FanucCompile.SourceHabits + "\n" + options, Clamp)));
        const string Start = "G80 G90 G94 G98\n";
        int start = text.IndexOf(Start, StringComparison.Ordinal) + Start.Length;
        return text.Substring(start, text.IndexOf("M30\n", StringComparison.Ordinal) - start);
    }
}
