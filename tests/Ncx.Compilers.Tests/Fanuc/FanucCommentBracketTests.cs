using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// R1-14, D389 (open): a bracket in the text of a Fanuc comment ends the comment early and the rest of the line runs
/// as code, so until D389 is answered the compiler refuses it with an ERROR instead of writing a broken line.
/// </summary>
public sealed class FanucCommentBracketTests
{
    // D389: SECTION="PROFIL (D10)" would compile to (PROFIL (D10)), whose first ) ends the comment.
    [Theory]
    [InlineData("SECTION=\"PROFIL (D10)\"")]
    [InlineData("COMMENT=\"ROUGH)\"")]
    [InlineData("MESSAGE=\"(CHECK\"")]
    public void CommentWithABracket_IsAnError(string block)
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program(block), FanucCompile.Mill());

        Assert.Equal(DiagnosticCodes.FanucBracketInComment, FanucCompile.ErrorOf(result).Code);
    }

    // D389: the program name stands in parentheses after the O number as well.
    [Fact]
    public void ProgramNameWithABracket_IsAnError()
    {
        string program = FanucCompile.Program().Replace("NAME=\"T\"", "NAME=\"T (1)\"", StringComparison.Ordinal);

        CompileResult result = FanucCompile.Run(program, FanucCompile.Mill());

        Assert.Equal(DiagnosticCodes.FanucBracketInComment, FanucCompile.ErrorOf(result).Code);
    }

    // A comment without a bracket is written as before; quotes pass (R1-14).
    [Fact]
    public void CommentWithoutABracket_IsWritten()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("SECTION=\"PROFIL D10 \\\"FINISH\\\"\""), FanucCompile.Mill()));

        Assert.Contains("(PROFIL D10 \"FINISH\")", text, StringComparison.Ordinal);
    }
}
