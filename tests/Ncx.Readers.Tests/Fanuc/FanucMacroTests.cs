using Ncx.Core.Model;
using Ncx.Core.Writing;
using Ncx.Tests.Fixtures;
using static Ncx.Readers.Tests.Fanuc.FanucRead;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The Fanuc column of controller-mapping 6, variables and control flow (controllers fanuc.md 1, 7; language 4.9,
/// 4.12): #n to Vn, [ ] to { }, the operators and functions, IF GOTO, IF THEN, WHILE DO END, GOTO, G65, M98, M99, the
/// system variables, and what stays RAW.
/// </summary>
public sealed class FanucMacroTests
{
    // #100 is V100 (D33); an assignment is VAR (controller-mapping 6).
    [Fact]
    public void Hash100_IsV100()
    {
        Assert.Equal(Lines("VAR:V100=5", "VAR:V101={$V100 + 20}"), Body("#100=5\n#101=#100+20"));
    }

    // [ ] are the parentheses of an NCX expression, SIN[ ] SIN( ) (controller-mapping 6, functions).
    [Fact]
    public void BracketsAndFunctions_AreTheNcxExpression()
    {
        Assert.Equal(Lines("VAR:V1={($V2 + 3.5) * SIN($V3)}"), Body("#1=[#2+3.5]*SIN[#3]"));
    }

    // FIX is INT, FUP the rounding away from zero (phase 3, P3-02: FIX and FUP to INT forms).
    [Fact]
    public void FixAndFup_AreIntForms()
    {
        Assert.Equal(
            Lines("VAR:V1={INT($V2)}", "VAR:V3={(INT($V4) + SGN(FRAC($V4)))}"),
            Body("#1=FIX[#2]\n#3=FUP[#4]"));
    }

    // ATAN[a]/[b] is the arc tangent of a over b, in the argument order of NCX ATAN2 (D119).
    [Fact]
    public void AtanOfTwoBrackets_IsAtan2()
    {
        Assert.Equal(Lines("VAR:V1={ATAN2($V2, $V3)}"), Body("#1=ATAN[#2]/[#3]"));
    }

    // D119 puts the range conversion of the two-argument ATAN on the Fanuc reader as well as on the Fanuc compiler,
    // and makes it wait on a machine-file key that says which range the control uses; no key carries that, so the
    // reader writes the value unconverted. A deferral of a settled decision is recorded, like the compiler's in row 60
    // of wave 3: the open-questions table names the reader's marker and the decision it waits on (00-method 4).
    [Fact]
    public void D119Rows_TheRangeConversionOfTheFanucReader_AreRecordedAsTheOpenQuestionItIs()
    {
        string path = Path.Combine(Fixture.RepositoryRoot(), "docs", "implementation", "03-open-questions.md");
        string? row = Array.Find(File.ReadAllLines(path),
            line => line.Contains("`src/Ncx.Readers/Fanuc/FanucExpression.cs:272`", StringComparison.Ordinal));

        Assert.NotNull(row);
        Assert.Contains("D119", row, StringComparison.Ordinal);
        Assert.Contains("range", row, StringComparison.Ordinal);
    }

    // IF [ ] GOTO n is JUMP=n with IF, EQ is == (controller-mapping 6; language 4.9).
    [Fact]
    public void IfGoto_IsJumpWithIf()
    {
        Assert.Equal(
            Lines("JUMP=30 IF={$V503 == 0}", "LABEL=30", "RAPID X=1"),
            Body("IF [#503 EQ 0] GOTO 30\nN30 G0 X1."));
    }

    // GOTO n is JUMP=n to the block Nn, which gets its LABEL (controller-mapping 6).
    [Fact]
    public void Goto_IsJumpToTheLabelOfItsBlock()
    {
        Assert.Equal(Lines("JUMP=20", "LABEL=20", "RAPID X=1"), Body("GOTO 20\nN20 G0 X1."));
    }

    // IF [ ] THEN #n = e is lowered to a jump over the assignment (phase 3, P3-02).
    [Fact]
    public void IfThen_IsLoweredToAJumpOverTheAssignment()
    {
        Assert.Equal(
            Lines("JUMP=IF_3 IF={NOT ($V1 > 0)}", "VAR:V2=1", "LABEL=IF_3"),
            Body("IF [#1 GT 0] THEN #2=1"));
    }

    // WHILE [ ] DOm ... ENDm is lowered to LABEL, JUMP and IF (controller-mapping 6, structured loops).
    [Fact]
    public void WhileDoEnd_IsLoweredToLabelsAndJumps()
    {
        Assert.Equal(
            Lines(
                "VAR:V1=0",
                "LABEL=WHILE_4",
                "JUMP=WHILE_4_END IF={NOT ($V1 < 3)}",
                "VAR:V1={$V1 + 1}",
                "JUMP=WHILE_4",
                "LABEL=WHILE_4_END"),
            Body("#1=0\nWHILE [#1 LT 3] DO1\n#1=#1+1\nEND1"));
    }

    // G65 P9010 A1. B2. Z-5. calls with arguments; the letters are the locals #1, #2, #26 of the callee, V1, V2, V26
    // (controller-mapping 6, CALL + ARG; language 4.9; controllers fanuc.md 7).
    [Fact]
    public void G65_IsCallWithTheLocalVariablesOfItsLetters()
    {
        string text = Text("%\nO0001\nG65 P9010 A1. B2. Z-5.\nM30\nO9010\n#3=#1+#2\nM99\n%\n");

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1",
                "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
                "CALL=9010 ARG:V1=1 ARG:V2=2 ARG:V26=-5",
                "PROGRAM=END",
                "SUB=BEGIN NAME=9010",
                "VAR:V3={$V1 + $V2}",
                "SUB=END",
                "FILE=END"),
            text);
        AssertFormatsToItself(text);
    }

    // M98 P L calls L times; the older form M98 P40100 calls program 0100 four times (controller-mapping 6).
    [Fact]
    public void M98_IsCallWithTimes_AlsoInTheOlderForm()
    {
        string text = Text("%\nO0001\nM98 P100 L4\nM98 P40100\nM30\nO0100\nG91 G1 X10. F100\nM99\n%\n");

        Assert.Contains("\nCALL=100 TIMES=4\nCALL=100 TIMES=4\nPROGRAM=END\nSUB=BEGIN NAME=100\n", text,
            StringComparison.Ordinal);
    }

    // Nakamura M200 P{times}{nnnn} calls a program from the memory card, the same as M98 for NCX: CALL with TIMES from
    // the leading digits, and the called O program of the file is a SUB (controller-mapping 6, CALL and TIMES; 8).
    [Fact]
    public void M200_OfTheBuilderNakamura_IsACallWithTimes()
    {
        NcxProgram program = Program("%\nO0001\nM200 P20100\nM200 P8889\nM30\nO0100\nG0 X1.\nM99\n%\n", Lathe());
        string text = NcxWriter.Write(program);

        Assert.Contains(
            "\nCALL=100 TIMES=2\nCALL=\"O8889\"\nPROGRAM=END\nSUB=BEGIN NAME=100\nRAPID X=1\nSUB=END\n", text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(DiagnosticCodes.FanucMCodeNotNamed, Codes(program));
        AssertFormatsToItself(text);
    }

    // On a machine of another builder M200 is no call: the block stays RAW, and no WARNING says it were written as
    // MFUNC (controller-mapping 8; machine-config 5).
    [Fact]
    public void M200_OnAMachineOfAnotherBuilder_IsKeptAsRawWithoutAnMfuncWarning()
    {
        NcxProgram program = FramedProgram("M200 P20100");

        Assert.Equal(Lines("RAW:FANUC=\"M200 P20100\""), BodyOf(NcxWriter.Write(program)));
        Assert.DoesNotContain(DiagnosticCodes.FanucMCodeNotNamed, Codes(program));
    }

    // D148: a subprogram call opens no local-variable level, so an M98 subprogram shares the caller's #1..#33; the
    // locals it reads stand on the CALL as ARG:Vn={$Vn}, which is how a CALL of NCX gives a callee its V1..V33
    // (decisions.md; fanuc 7; virtual machine 3.6).
    [Fact]
    public void SharedLocals_AnM98SubprogramThatReadsThem_StandOnTheCallAsArg()
    {
        string text = Text("%\nO0001\n#1=5\n#2=7\nM98 P100\nM30\nO0100\nG90 G1 X#1 F100\nM99\n%\n");
        Assert.Contains("CALL=100 ARG:V1={$V1}\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ARG:V2", text, StringComparison.Ordinal);
    }

    // D148: a local the M98 subprogram writes back for the caller is one the caller sees on the control, while the
    // CALL of NCX restores the caller's V1..V33 at the return, so the write does not reach the block of the caller
    // that reads it and the reader reports the call (decisions.md; virtual machine 3.6; RDR106).
    [Fact]
    public void SharedLocals_AnM98SubprogramThatWritesThemBackForTheCaller_IsReported()
    {
        NcxProgram program = Program("%\nO0001\nM98 P100\nG90 G1 X#1 F100\nM30\nO0100\n#1=5\nM99\n%\n");
        Assert.Contains("RDR106", Codes(program));
        Assert.DoesNotContain("ARG:V1", NcxWriter.Write(program), StringComparison.Ordinal);
    }

    // D148 conditions the ARG on the subprogram reading the caller's locals and the report on its writing them
    // "back for the caller": a subprogram that uses #1 as its own loop counter, assigning it before every read and
    // leaving it behind for no block of the caller, does neither, so its CALL carries no ARG of a local nothing
    // assigned (which the virtual machine would report as the ERROR VM901, D38) and no warning (decisions.md).
    [Fact]
    public void SharedLocals_AnM98SubprogramWhoseCounterIsItsOwn_StandOnNoArgAndAreNoWarning()
    {
        NcxProgram program = Program(
            "%\nO0001\nG90 G0 X0\nM98 P100\nM30\nO0100\n#1=0\nWHILE[#1 LT 3]DO1\nG1 X[#1] F100\n#1=#1+1\n"
            + "END1\nM99\n%\n");
        string text = NcxWriter.Write(program);
        Assert.Contains("CALL=100\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ARG:V1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("RDR106", Codes(program));
    }

    // D148: a G65 macro call raises the local-variable level, so its callee's #1..#33 are vacant apart from the
    // letter arguments and no ARG of the caller's locals stands on the CALL (fanuc 7; decisions.md).
    [Fact]
    public void SharedLocals_AG65MacroCallWhoseCalleeReadsThem_StandOnNoArg()
    {
        string text = Text("%\nO0001\n#1=5\nG65 P9010 A1.\nM30\nO9010\nG90 G1 X#2 F100\nM99\n%\n");
        Assert.Contains("CALL=9010 ARG:V1=1\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ARG:V2", text, StringComparison.Ordinal);
    }

    // A program the file does not hold is an external program, called by its file name (language 4.9, CALL).
    [Fact]
    public void M98_OfAProgramNotInTheFile_IsAnExternalCall()
    {
        Assert.Equal(Lines("CALL=\"O8889\""), Body("M98 P8889"));
    }

    // M99 in the main program loops to a label after the header (controller-mapping 6).
    [Fact]
    public void M99_InTheMainProgram_LoopsToTheStart()
    {
        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1",
                "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
                "LABEL=START",
                "RAPID X=1",
                "JUMP=START",
                "PROGRAM=END",
                "FILE=END"),
            Text("%\nO0001\nG0 X1.\nM99\n%\n"));
    }

    // The system variables are the SYS_ names of [system_variables], with their index (language 4.12, D51).
    [Fact]
    public void SystemVariables_MappedByTheMachine_AreTheirSysNames()
    {
        Assert.Equal(Lines("VAR:V1={$SYS_POS_X}", "VAR:V2={$SYS_WEAR_Z[99]}"), Body("#1=#5041\n#2=#11099"));
    }

    // GOTO n to the block of the M30 is JUMP=END, the early end of language 4.9 (controller-mapping 1, JUMP=END).
    [Fact]
    public void GotoToTheM30Block_IsJumpEnd()
    {
        string text = Text("%\nO0001\nIF [#1 EQ 0] GOTO 30\nG0 X1.\nN30 M30\n%\n");

        Assert.Equal(Lines("JUMP=END IF={$V1 == 0}", "RAPID X=1"), BodyOf(text));
        AssertFormatsToItself(text);
    }

    // A block-skipped /M30 is not the end of the program: SKIP JUMP=END, and PROGRAM=END stands at the real end
    // (controller-mapping 6; language 4.13).
    [Fact]
    public void SlashM30_IsSkipJumpEnd()
    {
        string text = Text("%\nO0001\nG0 X1.\n/M30\nG0 X2.\nM30\n%\n");

        Assert.Equal(Lines("RAPID X=1", "SKIP JUMP=END", "RAPID X=2"), BodyOf(text));
        Assert.Contains("\nRAPID X=2\nPROGRAM=END\nFILE=END\n", text, StringComparison.Ordinal);
    }

    // The NT NURSE branch command G480 I{n} of the builder nakamura jumps to Nn unconditionally: JUMP=n, and Nn gets
    // its LABEL (controller-mapping 6, JUMP + IF; controller-mapping 9: the branch commands are not RAW).
    [Fact]
    public void G480_OfTheBuilderNakamura_IsAJumpToTheLabelOfI()
    {
        Assert.Equal(Lines("JUMP=20", "LABEL=20", "RAPID X=1"), Body("G480 I20.\nN20 G0 X1.", Lathe()));
    }

    // G471 to G476 D Q I{n} jump when D compares with Q: equal, not equal, >=, <=, >, < (controller-mapping 6).
    [Fact]
    public void G471ToG476_CompareDWithQ_AreJumpsWithIf()
    {
        Assert.Equal(
            Lines(
                "JUMP=20 IF={$V1 == 2}",
                "JUMP=20 IF={$V1 != 2}",
                "JUMP=20 IF={$V1 >= 2}",
                "JUMP=20 IF={$V1 <= 2}",
                "JUMP=20 IF={$V1 > 2}",
                "JUMP=20 IF={$V1 < 2}",
                "LABEL=20",
                "RAPID X=1"),
            Body(
                "G471 D#1 Q2. I20.\nG472 D#1 Q2. I20.\nG473 D#1 Q2. I20.\nG474 D#1 Q2. I20.\nG475 D#1 Q2. I20.\n"
                + "G476 D#1 Q2. I20.\nN20 G0 X1.",
                Lathe()));
    }

    // G481 to G486 Q R I{n} jump when the variable #q compares with the variable #r (controller-mapping 6).
    [Fact]
    public void G481ToG486_CompareTheVariablesOfQAndR_AreJumpsWithIf()
    {
        Assert.Equal(
            Lines(
                "JUMP=20 IF={$V1 == $V2}",
                "JUMP=20 IF={$V1 != $V2}",
                "JUMP=20 IF={$V1 >= $V2}",
                "JUMP=20 IF={$V1 <= $V2}",
                "JUMP=20 IF={$V1 > $V2}",
                "JUMP=20 IF={$V1 < $V2}",
                "LABEL=20",
                "RAPID X=1"),
            Body(
                "G481 Q1. R2. I20.\nG482 Q1. R2. I20.\nG483 Q1. R2. I20.\nG484 Q1. R2. I20.\nG485 Q1. R2. I20.\n"
                + "G486 Q1. R2. I20.\nN20 G0 X1.",
                Lathe()));
    }

    // The branch commands are the NT NURSE macros of the builder nakamura (controller-mapping 6, 8); on a machine of
    // another builder G480 has no NCX word and stays RAW (controller-mapping 9).
    [Fact]
    public void G480_OnAMachineOfAnotherBuilder_IsKeptAsRaw()
    {
        Assert.Equal(Lines("RAW:FANUC=\"G480 I20.\"", "RAPID X=1"), Body("G480 I20.\nN20 G0 X1."));
    }

    // D221: M99 Pn in a section that no block of the file calls returns to the block Nn of that same section, which
    // reads as JUMP=n with the LABEL=n the structure pass writes there (controller-mapping 6, the paragraph after the
    // table; decisions.md).
    [Fact]
    public void ReturnToABlock_InASectionNoBlockOfTheFileCalls_IsAJumpToThatBlock()
    {
        Assert.Equal(Lines("LABEL=30", "RAPID X=1", "JUMP=30"), Body("N30 G0 X1.\nM99 P30"));
    }

    // D211 with D221: the loop an M99 Pn closes in a section no block of the file calls is an end the source writes,
    // as a plain M99 loop is, so a program whose last block it is gets PROGRAM=END without the WARNING RDR010
    // (decisions.md; controller-mapping 1, the PROGRAM=END row).
    [Fact]
    public void MissingProgramEnd_WithAnM99PnLoopAsTheLastBlock_IsNoWarning()
    {
        NcxProgram program = Program("%\nO0001\nN30 G0 X1.\nM99 P30\n%\n");
        string text = NcxWriter.Write(program);
        Assert.Contains("LABEL=30\nRAPID X=1\nJUMP=30\nPROGRAM=END\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("RDR010", Codes(program));
    }

    // D221: in a subprogram a block of the file calls, the P of M99 names a block of the caller, which no JUMP of NCX
    // reaches, so the block stays RAW even where the subprogram carries that block number itself (D5).
    [Fact]
    public void ReturnToABlock_InASubprogramTheFileCalls_StaysRaw()
    {
        string text = Text("%\nO0001\nM98 P100\nM30\nO0100\nN30 G0 X1.\nM99 P30\n%\n");
        Assert.Contains("RAW:FANUC=\"M99 P30\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("JUMP=30", text, StringComparison.Ordinal);
    }

    // D221: whether a block of the file calls a section is a fact of the whole file, not of the blocks read so far,
    // so the subprogram reads the same way where its caller stands after it (controller-mapping 6; decisions.md).
    [Fact]
    public void ReturnToABlock_InASubprogramTheFileCallsFurtherDown_StaysRaw()
    {
        string text = Text("%\nO0100\nN30 G0 X1.\nM99 P30\nO0001\nM98 P100\nM30\n%\n");
        Assert.Contains("RAW:FANUC=\"M99 P30\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("JUMP=30", text, StringComparison.Ordinal);
        Assert.DoesNotContain("LABEL=30", text, StringComparison.Ordinal);
    }

    // An unmapped system variable, an indirect variable, M99 P and G66 keep their blocks as RAW (language 4.12;
    // controller-mapping 6, 9).
    [Fact]
    public void UnmappedAndUnexpressibleMacroForms_AreKeptAsRaw()
    {
        Assert.Equal(
            Lines(
                "RAW:FANUC=\"#1=#5025\"",
                "RAW:FANUC=\"#[#1]=5\"",
                "RAW:FANUC=\"M99 P30\"",
                "RAW:FANUC=\"G66 P9010 A1.\""),
            Body("#1=#5025\n#[#1]=5\nM99 P30\nG66 P9010 A1."));
    }
}
