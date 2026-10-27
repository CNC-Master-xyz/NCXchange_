using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// REPEAT with the counters of [variables] repeat_counters (D280; controller-mapping 6, REPEAT + TIMES; machine-config
/// 7) and the clearing of a counter by a JUMP that leaves its range (D213).
/// </summary>
public sealed class FanucRepeatTests
{
    // D280: the counter counts the passes, IF GOTO returns to the label while it is not above TIMES, and it is 0 again
    // once the repeat ends; the start of the program clears it.
    [Fact]
    public void Repeat_WithCounters_CountsInTheCounterOfItsDepth()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("LABEL=1", "RAPID X=1 Y=1", "REPEAT=1 TIMES=3"), FanucCompile.MillWithCounters()));

        Assert.Contains("G80 G90 G94 G98\n#149 = 0\n", text, StringComparison.Ordinal);
        Assert.Contains("\nN1\n", text, StringComparison.Ordinal);
        Assert.Contains("\n#149 = #149 + 1\nIF [#149 LE 3] GOTO 1\n#149 = 0\nM30\n", text, StringComparison.Ordinal);
    }

    // D212, D280: a REPEAT without TIMES repeats once.
    [Fact]
    public void Repeat_WithoutTimes_RepeatsOnce()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("LABEL=1", "RAPID X=1 Y=1", "REPEAT=1"), FanucCompile.MillWithCounters()));

        Assert.Contains("IF [#149 LE 1] GOTO 1\n", text, StringComparison.Ordinal);
    }

    // D280: a nested REPEAT counts in the counter of its depth.
    [Fact]
    public void Repeat_Nested_TakesTheSecondCounter()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("LABEL=1", "LABEL=2", "RAPID X=1 Y=1", "REPEAT=2 TIMES=2", "REPEAT=1 TIMES=3"),
            FanucCompile.MillWithCounters()));

        Assert.Contains("IF [#148 LE 2] GOTO 2\n#148 = 0\n", text, StringComparison.Ordinal);
        Assert.Contains("IF [#149 LE 3] GOTO 1\n#149 = 0\n", text, StringComparison.Ordinal);
    }

    // D280: a REPEAT in a subprogram that a repeat calls takes the counter of the depth below the caller's.
    [Fact]
    public void Repeat_InASubprogramCalledInsideARepeat_TakesTheNextCounter()
    {
        string program = FanucCompile.Lines("FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF", "LABEL=1", "CALL=100", "REPEAT=1 TIMES=2",
            "PROGRAM=END", "SUB=BEGIN NAME=\"100\"", "LABEL=5", "RAPID X=1 Y=1", "REPEAT=5 TIMES=4", "SUB=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.MillWithCounters()));

        Assert.Contains("IF [#148 LE 4] GOTO 5\n", text, StringComparison.Ordinal);
        Assert.Contains("IF [#149 LE 2] GOTO 1\n", text, StringComparison.Ordinal);
    }

    // D213: a JUMP that leaves the range of a repeat clears its counter, under the jump's condition.
    [Fact]
    public void Jump_LeavingTheRange_ClearsTheCounter()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("VAR:V1=0", "LABEL=1", "RAPID X=1 Y=1", "IF={$V1 > 2} JUMP=2", "REPEAT=1 TIMES=3",
                "LABEL=2", "RAPID X=2"),
            FanucCompile.MillWithCounters()));

        Assert.Contains("IF [#1 GT 2] THEN #149 = 0\nIF [#1 GT 2] GOTO 2\n", text, StringComparison.Ordinal);
    }

    // D213, D280: a RETURN inside the range of a repeat ends it, so the counter is 0 again before M99 and the next
    // call of the subprogram runs every pass.
    [Fact]
    public void Return_InsideTheRange_ClearsTheCounterBeforeM99()
    {
        string program = FanucCompile.Lines("FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF", "VAR:V100=0", "CALL=100", "CALL=100",
            "PROGRAM=END", "SUB=BEGIN NAME=\"100\"", "LABEL=5", "RAPID X=1 Y=1", "VAR:V100={$V100 + 1}",
            "IF={$V100 < 2} JUMP=6", "RETURN", "LABEL=6", "REPEAT=5 TIMES=4", "SUB=END", "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.MillWithCounters()));

        Assert.Contains("\n#149 = 0\nM99\nN6\n", text, StringComparison.Ordinal);
    }

    // D213: a conditional JUMP=END in a subprogram is the ERROR CMP302 and writes nothing, no clearing either.
    [Fact]
    public void ConditionalEndInASubprogram_WritesNoClearing()
    {
        string program = FanucCompile.Lines("FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF", "VAR:V100=0", "CALL=100", "PROGRAM=END",
            "SUB=BEGIN NAME=\"100\"", "LABEL=5", "RAPID X=1 Y=1", "IF={$V100 > 2} JUMP=END", "REPEAT=5 TIMES=4",
            "SUB=END", "FILE=END");

        CompileResult result = FanucCompile.Run(program, FanucCompile.MillWithCounters());

        Assert.Equal(DiagnosticCodes.FanucConditionalEndInSub, FanucCompile.ErrorOf(result).Code);
        foreach (var file in result.Files)
        {
            Assert.DoesNotContain("THEN", file.Text, StringComparison.Ordinal);
        }
    }

    // D213: a JUMP inside the range leaves the counter as it is.
    [Fact]
    public void Jump_InsideTheRange_KeepsTheCounter()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("VAR:V1=0", "LABEL=1", "IF={$V1 > 2} JUMP=3", "RAPID X=1 Y=1", "LABEL=3",
                "REPEAT=1 TIMES=3"),
            FanucCompile.MillWithCounters()));

        Assert.DoesNotContain("THEN", text, StringComparison.Ordinal);
    }

    // D280: without [variables] repeat_counters a REPEAT is the ERROR CMP314; no variable is guessed.
    [Fact]
    public void Repeat_WithoutTheKey_IsTheErrorCmp314()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("LABEL=1", "RAPID X=1 Y=1", "REPEAT=1 TIMES=3"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucRepeatCounterMissing, error.Code);
    }

    // D280: a nesting deeper than the list is the ERROR CMP314.
    [Fact]
    public void Repeat_DeeperThanTheList_IsTheErrorCmp314()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("LABEL=1", "LABEL=2", "RAPID X=1 Y=1", "REPEAT=2 TIMES=2", "REPEAT=1 TIMES=3"),
            FanucCompile.MillWithCounters("[\"#149\"]")));

        Assert.Equal(DiagnosticCodes.FanucRepeatCounterMissing, error.Code);
    }

    // D280: a counter the program uses itself is the ERROR CMP315.
    [Fact]
    public void Repeat_CounterTheProgramUses_IsTheErrorCmp315()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("VAR:V149=1", "LABEL=1", "RAPID X=1 Y=1", "REPEAT=1 TIMES=3"),
            FanucCompile.MillWithCounters()));

        Assert.Equal(DiagnosticCodes.FanucRepeatCounterInUse, error.Code);
    }
}
