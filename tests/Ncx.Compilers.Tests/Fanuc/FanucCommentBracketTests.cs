using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// D390 (R1-14): the first ) ends a Fanuc comment and the rest of the line runs as code, so the compiler writes ( as
/// [ and ) as ] in comment text, with a WARNING per comment that names the replacement.
/// </summary>
public sealed class FanucCommentBracketTests
{
    // D390: SECTION="PROFIL (D10)" is written (PROFIL [D10]), with CMP312.
    [Theory]
    [InlineData("SECTION=\"PROFIL (D10)\"", "(PROFIL [D10])")]
    [InlineData("COMMENT=\"ROUGH)\"", "(ROUGH])")]
    [InlineData("MESSAGE=\"(CHECK\"", "([CHECK)")]
    public void CommentWithABracket_IsWrittenWithSquareBracketsAndWarns(string block, string line)
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program(block), FanucCompile.Mill());

        Assert.Contains(line + "\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Diagnostic warning = Assert.Single(FanucCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.FanucBracketInComment, warning.Code);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Contains(line.Substring(1, line.Length - 2), warning.Message, StringComparison.Ordinal);
    }

    // D390: the program name stands in parentheses after the O number as well.
    [Fact]
    public void ProgramNameWithABracket_IsWrittenWithSquareBrackets()
    {
        string program = FanucCompile.Program().Replace("NAME=\"T\"", "NAME=\"T (1)\"", StringComparison.Ordinal);

        CompileResult result = FanucCompile.Run(program, FanucCompile.Mill());

        Assert.Contains("O0001 (T [1])\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Diagnostic warning = Assert.Single(FanucCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.FanucBracketInComment, warning.Code);
    }

    // A comment without a bracket is written as before and warns nothing; quotes pass (R1-14).
    [Fact]
    public void CommentWithoutABracket_IsWrittenAsBefore()
    {
        CompileResult result = FanucCompile.Run(
            FanucCompile.Program("SECTION=\"PROFIL D10 \\\"FINISH\\\"\""), FanucCompile.Mill());

        Assert.Contains("(PROFIL D10 \"FINISH\")", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Assert.Empty(FanucCompile.CompilerDiagnostics(result));
    }
}
