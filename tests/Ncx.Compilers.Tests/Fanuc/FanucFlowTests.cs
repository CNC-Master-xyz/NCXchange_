using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Fanuc;

/// <summary>
/// The flow of custom macro B in the Fanuc compiler (controllers fanuc.md 7; controller-mapping 6; language 4.9, 4.12):
/// # variables, GOTO and IF [ ] GOTO, the program end, the M99 loop, M98 and G65.
/// </summary>
public sealed class FanucFlowTests
{
    // PATTERN_LOOP.ncx in custom macro B: Q1 is #101 through [variables] map (machine-config 7, D173), the label the
    // block number N1, the conditional jump IF [#103 LT #102] GOTO 1.
    [Fact]
    public void Loop_WithQVariables_IsIfGotoOverHashVariables()
    {
        string body = FanucCompile.Body(
            "VAR:Q1=10\nVAR:Q3=0\nLABEL=1\nVAR:Q1={$Q1 + 20}\nVAR:Q3={$Q3 + 1}\nJUMP=1 IF={$Q3 < 5}");

        Assert.Equal("#101 = 10\n#103 = 0\nN1\n#101 = #101 + 20\n#103 = #103 + 1\nIF [#103 LT 5] GOTO 1\n", body);
    }

    // Language 4.12 NOT; fanuc 7: custom macro B has no NOT, so the comparison is inverted, as a reader lowers WHILE.
    [Fact]
    public void Condition_WithNot_IsTheInvertedComparison()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLABEL=1\nJUMP=1 IF={NOT ($V1 < 3 AND $V1 >= 0)}");

        Assert.Equal("#1 = 0\nN1\nIF [[#1 GE 3] OR [#1 LT 0]] GOTO 1\n", body);
    }

    // D33, language 4.9: Vn is #n; the functions take their argument in brackets, INT as FIX (fanuc 7).
    [Fact]
    public void Variables_VNamesAndFunctions_AreHashVariablesWithBrackets()
    {
        string body = FanucCompile.Body("VAR:V2=4\nVAR:V1={$V2 * SIN(30) + INT(2.5)}");

        Assert.Equal("#2 = 4\n#1 = [#2 * SIN[30]] + FIX[2.5]\n", body);
    }

    // D404: under the factory setting of parameter 6004#0 NAT, [variables] atan_nat = false, the two-argument ATAN of
    // the control runs 0 to 360, so NCX ATAN2 is written mapped back to -180 to 180: exactly 180 becomes -180, the
    // same direction (B-64490EN-05 p.340-341, cnc-manuals pages 340-341).
    [Fact]
    public void Atan2_UnderTheFactoryNat_IsMappedToTheNcxRange()
    {
        string body = FanucCompile.Body("VAR:V2=1\nVAR:V3=1\nVAR:V1={ATAN2($V2, $V3)}");

        Assert.Equal("#2 = 1\n#3 = 1\n#1 = [ATAN[#2]/[#3] - 360 * FIX[ATAN[#2]/[#3] / 180]]\n", body);
    }

    // D404: with atan_nat = true the control's range is the NCX range, and ATAN2 is written plain; ASIN follows the
    // same bit.
    [Fact]
    public void Atan2AndAsin_UnderAtanNat_AreWrittenPlain()
    {
        string toml = FanucCompile.Mill().Replace("[variables]\n", "[variables]\natan_nat = true\n",
            StringComparison.Ordinal);
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("VAR:V2=1", "VAR:V3=1", "VAR:V1={ATAN2($V2, $V3)}", "VAR:V4={ASIN(0.5)}"), toml));

        Assert.Contains("#1 = ATAN[#2]/[#3]\n", text, StringComparison.Ordinal);
        Assert.Contains("#4 = ASIN[0.5]\n", text, StringComparison.Ordinal);
    }

    // D404: ASIN under NAT = 0 gives 270 to 0 to 90, and the compiler maps it back to -90 to 90 the same way.
    [Fact]
    public void Asin_UnderTheFactoryNat_IsMappedToTheNcxRange()
    {
        string body = FanucCompile.Body("VAR:V1={ASIN(-0.5)}");

        Assert.Equal("#1 = [ASIN[-0.5] - 360 * FIX[ASIN[-0.5] / 180]]\n", body);
    }

    // D51, machine-config 7: a SYS_ name is the native variable of [system_variables], with its index.
    [Fact]
    public void SystemVariables_AreTheNativeVariablesOfTheMachine()
    {
        string body = FanucCompile.Body("VAR:V100={$SYS_POS_X}\nVAR:V101={$SYS_WEAR_Z[99]}");

        Assert.Equal("#100 = #5041\n#101 = #11099\n", body);
    }

    // Controller-mapping 6, CALL and ARG; fanuc 7: G65 with the letters of the standard table.
    [Fact]
    public void Call_WithArguments_IsG65WithLetters()
    {
        string body = FanucCompile.Body("CALL=\"O9010\" ARG:V1=1 ARG:V2=2.5 ARG:V26=-5");

        Assert.Equal("G65 P9010 A1. B2.5 Z-5.\n", body);
    }

    // D148, the M98 half: a subprogram call opens no local-variable level, so the reader writes the caller's locals
    // an M98 subprogram reads on the CALL as the pass-through ARG:Vn={$Vn}; that shape is written as the subprogram
    // call again, because M98 shares those very locals and carries no arguments at all (controllers fanuc.md 7). The
    // NCX line is the one FanucMacroTests.SharedLocals_AnM98SubprogramThatReadsThem_StandOnTheCallAsArg reads from
    // M98 P100. The block is reported, since a G65 source writes the same NCX text (CMP311, the test below).
    [Fact]
    public void SharedLocalsArg_OfACallOfTheFile_IsTheM98SubprogramCall()
    {
        CompileResult result = FanucCompile.Run(CallerAndSub("VAR:V1=5", "CALL=100 ARG:V1={$V1}"),
            FanucCompile.Mill());

        Assert.Contains("\n#1 = 5\nM98 P0100\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Assert.Equal(DiagnosticCodes.FanucCallFormOfTheSharedLocals,
            Assert.Single(FanucCompile.CompilerDiagnostics(result)).Code);
    }

    // Hard rule 1: the same NCX text is what a G65 macro call of the caller's own locals converts to - G65 P0100 A#1
    // over a subprogram that reads #1 gives the very CALL=100 ARG:V1={$V1} that M98 P100 over it gives, the two .ncx
    // files being identical - so no reading of the block writes both sources back. The compiler writes the M98 that
    // D148 gives the reader and reports the block, because virtual machine 3.6 gives a CALL the local-variable level
    // of a G65: the G65 source is changed into the other call, and a write of #1..#33 in the subprogram reaches the
    // caller where NCX restores it. Row 196 of ../../../docs/implementation/03-open-questions.md.
    [Fact]
    public void PassThroughArg_ThatAG65SourceConvertsToAsWell_IsTheWarningCmp311OnTheM98Block()
    {
        CompileResult result = FanucCompile.Run(CallerAndSub("VAR:V1=5", "CALL=100 ARG:V1={$V1}"),
            FanucCompile.Mill());

        Diagnostic warning = Assert.Single(FanucCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.FanucCallFormOfTheSharedLocals, warning.Code);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Contains("M98 P0100", warning.Message, StringComparison.Ordinal);
        Assert.Contains("ARG:V1={$V1}", warning.Message, StringComparison.Ordinal);
        Assert.Contains("G65", warning.Message, StringComparison.Ordinal);
    }

    // The same for a call of a program outside the file, which the external call code of [format] external_call
    // writes (D322): the ARG words are dropped there too, so the block carries the same report.
    [Fact]
    public void PassThroughArg_OfACallOutsideTheFile_IsTheWarningCmp311OnTheExternalCallBlock()
    {
        CompileResult result = FanucCompile.Run(FanucCompile.Program("VAR:V1=5", "CALL=\"O9010\" ARG:V1={$V1}"),
            FanucCompile.Mill());

        Assert.Contains("\nM98 P9010\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Assert.Equal(DiagnosticCodes.FanucCallFormOfTheSharedLocals,
            Assert.Single(FanucCompile.CompilerDiagnostics(result)).Code);
    }

    // D148, the M98 half: the shared locals are #1..#33, and the letter table of G65 has no letter for #27 or #33, so
    // the pass-through of one of them is a block only the subprogram call writes; deciding by the presence of ARG made
    // it the ERROR CMP306 and lost a program that converts and compiles without one (controllers fanuc.md 7). The open
    // call form of CMP311 is reported in its place, and it is a WARNING, so the program still compiles.
    [Fact]
    public void SharedLocalsArg_OfALocalWithoutAG65Letter_IsTheM98SubprogramCallWithoutAnError()
    {
        CompileResult result = FanucCompile.Run(
            CallerAndSub("VAR:V27=5", "VAR:V33=7", "CALL=100 ARG:V27={$V27} ARG:V33={$V33} TIMES=2"),
            FanucCompile.Mill());

        Assert.Contains("\nM98 P0100 L2\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Diagnostic warning = Assert.Single(FanucCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.FanucCallFormOfTheSharedLocals, warning.Code);
        Assert.Equal(Severity.Warning, warning.Severity);
    }

    // Controller-mapping 6 (CALL and ARG) and fanuc 7 give the letter arguments to G65: an ARG that carries an
    // argument value, a number or an expression that is not the pass-through of the callee's own local, is the macro
    // call. The first line is the one FanucMacroTests.SharedLocals_AG65MacroCallWhoseCalleeReadsThem_StandOnNoArg
    // reads from G65 P9010 A1.
    [Fact]
    public void ArgumentValue_OfACallOfTheFile_IsTheG65MacroCall()
    {
        CompileResult result = FanucCompile.Run(
            CallerAndSub("VAR:V1=5", "CALL=100 ARG:V1=1", "CALL=100 ARG:V2={$V1 + 1}", "CALL=100 ARG:V3={$V1}"),
            FanucCompile.Mill());

        Assert.Contains("\nG65 P0100 A1.\nG65 P0100 B[#1 + 1]\nG65 P0100 C#1\n", FanucCompile.TextOf(result),
            StringComparison.Ordinal);
        Assert.Empty(FanucCompile.CompilerDiagnostics(result));
    }

    // Controller-mapping 6 (CALL and ARG) maps a CALL with ARG to G65 Pnnnn A1 B2, and D148's ANSWER evidence says
    // only a macro call can take arguments at all, so a block that carries an argument value is the macro call even
    // where a pass-through ARG:Vn={$Vn} stands next to it: G65 P0100 A#1 B5., which forwards the caller's #1 into the
    // callee's A and sets B to 5, is ordinary Fanuc and converts to this NCX line. Nothing about it is ambiguous, so
    // it carries no diagnostic either; the WARNING CMP311 is for the pure pass-through shape, which is the one both
    // calls write.
    [Fact]
    public void ArgumentValue_NextToAPassThroughWord_IsTheG65MacroCallWithoutADiagnostic()
    {
        CompileResult result = FanucCompile.Run(
            CallerAndSub("VAR:V1=5", "CALL=100 ARG:V1={$V1} ARG:V2=5"), FanucCompile.Mill());

        Assert.Contains("\nG65 P0100 A#1 B5.\n", FanucCompile.TextOf(result), StringComparison.Ordinal);
        Assert.Empty(FanucCompile.CompilerDiagnostics(result));
    }

    // Hard rule 1: the TODO(question) of the call form is recorded in the open-questions table, with the decision it
    // waits on (00-method 4).
    [Fact]
    public void CallFormQuestion_OfTheFanucCompiler_IsRecordedInTheOpenQuestions()
    {
        string path = Path.Combine(Ncx.Tests.Fixtures.Fixture.RepositoryRoot(), "docs", "implementation",
            "03-open-questions.md");
        string? row = Array.Find(File.ReadAllLines(path),
            line => line.Contains("`src/Ncx.Compilers/Fanuc/FanucFlow.cs:", StringComparison.Ordinal)
                && line.Contains("CMP311", StringComparison.Ordinal));

        Assert.NotNull(row);
        Assert.Contains("D148", row, StringComparison.Ordinal);
        Assert.DoesNotContain("CMP310", row, StringComparison.Ordinal);
        Assert.Contains("an argument value is the `G65`", row, StringComparison.Ordinal);
    }

    // D322: a CALL of a program outside the file writes the code of [format] external_call, M98 where the machine
    // names none.
    [Fact]
    public void ExternalCall_WithTheCodeOfTheMachine_WritesThatCode()
    {
        string machine = FanucCompile.Mill(
            options: FanucCompile.SourceHabits + "\nexternal_call = \"M198\"");

        string text = FanucCompile.TextOf(FanucCompile.Run(FanucCompile.Program("CALL=\"O9010\""), machine));
        string plain = FanucCompile.Body("CALL=\"O9010\"");

        Assert.Contains("\nM198 P9010\n", text, StringComparison.Ordinal);
        Assert.Equal("M98 P9010\n", plain);
    }

    // Language 4.9 JUMP=END, controller-mapping 1: a conditional end jumps to the block number of the program end,
    // which the end carries.
    [Fact]
    public void JumpToEnd_Conditional_JumpsToTheNumberOfTheEnd()
    {
        string text = FanucCompile.TextOf(FanucCompile.Run(
            FanucCompile.Program("VAR:V1=1", "LABEL=5", "JUMP=END IF={$V1 > 0}", "JUMP=5"), FanucCompile.Mill()));

        Assert.EndsWith("#1 = 1\nN5\nIF [#1 GT 0] GOTO 6\nGOTO 5\nN6 M30\n%\n", text, StringComparison.Ordinal);
    }

    // Controllers fanuc.md 1 (N numbers matter as jump targets), machine-config 2 (block_numbers): the number of a
    // label is given to no other line, so GOTO 20 names one block (D277).
    [Fact]
    public void Label_UnderBlockNumbers_KeepsItsNumberForItself()
    {
        string machine = FanucCompile.Mill().Replace("block_numbers = { enabled = false }",
            "block_numbers = { enabled = true, start = 10, step = 10 }", StringComparison.Ordinal);

        string text = FanucCompile.TextOf(FanucCompile.Run(FanucCompile.Program(
            "VAR:V1=0", "LABEL=20", "RAPID X=1 Y=1", "VAR:V1={$V1 + 1}", "JUMP=20 IF={$V1 < 3}"), machine));

        Assert.Equal(
            "%\nO0001 (T)\nN10 G21\nN30 G0 G40\nN40 G80 G90 G94 G98\nN50 #1 = 0\nN20\nN60 G80\n"
            + "N70 G0 G17 G90 X1. Y1.\nN80 #1 = #1 + 1\nN90 IF [#1 LT 3] GOTO 20\nN100 M30\n%\n", text);
    }

    // Virtual machine 1 (the STATIC walk does not follow JUMP), controllers fanuc.md 10 rule 1: a jump reaches the
    // label with the modal codes of the block it jumps from, here the G0 of the RAPID, so the LINE after the label
    // writes G1, the plane, G90, G94 and F again, with G80 before it (D247).
    [Fact]
    public void Label_ThatAJumpReaches_WritesTheModalCodesOfTheNextMotionAgain()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE Z=-1 F=100\nLABEL=10\nLINE X=1\nRAPID Z=5"
            + "\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nG1 G17 Z-1. F100.\nN10\nG80\nG1 G17 G90 G94 X1. F100.\nG0 Z5.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Virtual machine 1 (the STATIC walk does not follow JUMP), language 4.3 (F is modal): the control reaches the
    // label with F100 from the block before it and with F200 from the jump, and keeps either, so the line after the
    // label writes no F of the walk.
    [Fact]
    public void Label_ThatAJumpReachesWithAnotherFeed_LeavesTheFeedToTheControl()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE Z=-1 F=100\nLABEL=10\nLINE X=1\nLINE X=2 F=200"
            + "\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nG1 G17 Z-1. F100.\nN10\nG80\nG1 G17 G90 G94 X1.\nX2. F200.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Virtual machine 1, language 4.2 (WORKPLANE is modal): the jump reaches the label in the plane ZX and the block
    // before it in XY, so the line after the label writes no plane of the walk, while the feed, the same on both
    // paths, stands again.
    [Fact]
    public void Label_ThatAJumpReachesInAnotherPlane_LeavesThePlaneToTheControl()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE X=0 Y=0 F=100\nLABEL=10\nLINE X=1\nWORKPLANE=ZX\nLINE Z=1"
            + "\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nG1 G17 X0. Y0. F100.\nN10\nG80\nG1 G90 G94 X1. F100.\nG18 Z1.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Virtual machine 1, D218 (the F of a cycle block is the control's feed): the control has the feed of the cycle
    // where NCX has F100, so F100 stands before the label, and the control keeps the feed of each path after it.
    [Fact]
    public void Label_WhereTheControlHasTheFeedOfACycle_WritesTheFeedOfNcxBeforeIt()
    {
        string body = FanucCompile.Body("LINE X=0 Z=5 F=100\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=565\nCYCLE_CALL"
            + "\nCYCLE=OFF\nVAR:V1=0\nLABEL=10\nLINE X=1\nLINE X=2 F=200\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("G1 G17 X0. Z5. F100.\nG81 G99 Z-10. R2. F565.\nG80\n#1 = 0\nF100.\nN10\nG80\n"
            + "G1 G17 G90 G94 X1.\nX2. F200.\n#1 = #1 + 1\nIF [#1 LT 3] GOTO 10\n", body);
    }

    // Virtual machine 1, D218: at the jump the control has the feed of the cycle where NCX has F200, so F200 stands
    // before the GOTO, and the line after the label, which takes the feed of the path, runs at it.
    [Fact]
    public void Jump_WhereTheControlHasTheFeedOfACycle_WritesTheFeedOfNcxBeforeTheGoto()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE X=0 Z=5 F=100\nLABEL=10\nLINE X=1\nLINE X=2 F=200"
            + "\nCYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=565\nCYCLE_CALL\nCYCLE=OFF\nVAR:V1={$V1 + 1}"
            + "\nJUMP=10 IF={$V1 < 3}");

        Assert.EndsWith("G81 G99 Z-10. R2. F565.\nG80\n#1 = #1 + 1\nF200.\nIF [#1 LT 3] GOTO 10\n", body,
            StringComparison.Ordinal);
    }

    // Virtual machine 1, controllers fanuc.md 5 and 10 rule 2: the main spindle starts after the label with the speed
    // of the path, 2000 from the jump, which the control does not hold there, and an S alone after the M code of the
    // driven tool would belong to the driven tool: CMP309, never the 1000 of the walk.
    [Fact]
    public void Jump_WithASpeedTheControlCannotTake_IsTheErrorCmp309()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=1000",
            "VAR:V1=0",
            "LABEL=10",
            "SPINDLE:MAIN=CW",
            "RAPID X=50 Z=2",
            "SPINDLE:MAIN=OFF",
            "RPM:MAIN=2000",
            "SPINDLE:TOOL=CW RPM:TOOL=500",
            "VAR:V1={$V1 + 1}",
            "JUMP=10 IF={$V1 < 3}",
            "PROGRAM=END",
            "FILE=END");

        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(program, FanucCompile.Lathe()));

        Assert.Equal(DiagnosticCodes.FanucModalValueNotHeld, error.Code);
    }

    // Virtual machine 1, language 4.5 (RPM is modal): the jump reaches the label with RPM 2000 of the main spindle,
    // which the control cannot take after the M code of the driven tool, but the start after the label states its RPM
    // on every path, so no block takes the speed of a path and the program compiles.
    [Fact]
    public void Jump_WithASpeedStatedAgainAfterTheLabel_Compiles()
    {
        string program = FanucCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "VAR:V1=0",
            "LABEL=10",
            "SPINDLE:MAIN=CW RPM:MAIN=1000",
            "RAPID X=50 Z=2",
            "SPINDLE:MAIN=OFF",
            "RPM:MAIN=2000",
            "SPINDLE:TOOL=CW RPM:TOOL=500",
            "VAR:V1={$V1 + 1}",
            "JUMP=10 IF={$V1 < 3}",
            "PROGRAM=END",
            "FILE=END");

        string text = FanucCompile.TextOf(FanucCompile.Run(program, FanucCompile.Lathe()));

        Assert.Contains("\nN10\nS1000 M3\n", text, StringComparison.Ordinal);
        Assert.Contains("\nS500 M88\n#1 = #1 + 1\nIF [#1 LT 3] GOTO 10\n", text, StringComparison.Ordinal);
    }

    // Virtual machine 1, language 4.4 (OFFSET:RAD is modal), controllers fanuc.md 4 (D stands with G41 and G42): the
    // jump reaches the label with the radius register 1 and the block before it with none, and the tool call after the
    // label states the register on every path, so no block takes the register of a path: the program compiles, as the
    // loop of a Heidenhain program with TOOL CALL in it does.
    [Fact]
    public void Jump_WithARadiusRegisterStatedAgainAfterTheLabel_Compiles()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLABEL=10\nTOOL=1 OFFSET:LEN=1 OFFSET:RAD=1\nRAPID X=0 Y=0 Z=5"
            + "\nLINE Z=-1 F=100\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nN10\nG80\nT1 M6\nG0 G17 G90 G43 X0. Y0. Z5. H1\nG1 G94 Z-1. F100.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Virtual machine 1, controllers fanuc.md 4: a forward jump reaches the label with the radius register 1, the block
    // before it with 2, and the control holds neither, which no line of its own can hold; no block after the label
    // takes the register, so the program compiles.
    [Fact]
    public void Jump_ForwardWithARadiusRegisterNoBlockTakes_Compiles()
    {
        string body = FanucCompile.Body("VAR:V1=0\nTOOL=1 OFFSET:LEN=1 OFFSET:RAD=1\nRAPID X=0 Y=0 Z=5"
            + "\nJUMP=10 IF={$V1 < 3}\nOFFSET:RAD=2\nLABEL=10\nLINE X=1 F=100");

        Assert.EndsWith("IF [#1 LT 3] GOTO 10\nN10\nG80\nG1 G17 G90 G94 X1. F100.\n", body, StringComparison.Ordinal);
    }

    // Virtual machine 1, language 4.4, D53: the same forward jump, and the block after the label starts the
    // compensation without stating the register, whose value differs by path while the control holds neither: CMP309
    // at that block, never the register of the walk.
    [Fact]
    public void Jump_ForwardWithARadiusRegisterABlockTakes_IsTheErrorCmp309AtThatBlock()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(FanucCompile.Program("VAR:V1=0",
            "TOOL=1 OFFSET:LEN=1 OFFSET:RAD=1", "RAPID X=0 Y=0 Z=5", "JUMP=10 IF={$V1 < 3}", "OFFSET:RAD=2",
            "LABEL=10", "LINE X=1 F=100 COMP=LEFT"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucModalValueNotHeld, error.Code);
        Assert.Equal(10, error.Line);
    }

    // Language 4.11 (RPM is ignored while CSS is on), controllers fanuc.md 4 (G96 S is the cutting speed): the label
    // and the jump are reached under CSS with RPM 1000 and 2000, and an S alone would be a cutting speed, so none
    // stands; no block after the label takes the RPM.
    [Fact]
    public void Label_ReachedUnderCss_WritesNoSpeedAlone()
    {
        string body = FanucCompile.Body("SPINDLE=CW RPM=1000\nCSS=ON VC=140\nVAR:V1=0\nLABEL=10\nLINE X=1 F=100"
            + "\nRPM=2000\nLINE X=2\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("S1000 M3\nG96 S140\n#1 = 0\nN10\nG80\nG1 G17 G90 G94 X1. F100.\nX2.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Language 4.11, controllers fanuc.md 4: the jump is reached under CSS with RPM 2000, where S2000 alone would be
    // the cutting speed 2000; the block after the label states CSS=ON before any block writes S.
    [Fact]
    public void Jump_UnderCss_WritesNoSpeedAlone()
    {
        string body = FanucCompile.Body("SPINDLE=CW RPM=1000\nVAR:V1=0\nLABEL=10\nLINE X=1 F=100\nCSS=ON VC=140"
            + "\nLINE X=2\nRPM=2000\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("S1000 M3\n#1 = 0\nN10\nG80\nG1 G17 G90 G94 X1. F100.\nG96 S140\nX2.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Language 4.11, controllers fanuc.md 4, virtual machine 1: the control reaches the label under G97 from the block
    // before it and under G96 from the jump, so the S2000 of RPM=2000 after the label would be the cutting speed 2000
    // on the path of the jump: CMP309 at that block, and no S alone before the GOTO.
    [Fact]
    public void Label_ReachedUnderCssOnOnePath_RpmAfterItIsTheErrorCmp309()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(FanucCompile.Program("SPINDLE=CW RPM=1000",
            "VAR:V1=0", "LABEL=10", "LINE X=1 F=100", "RPM=2000", "CSS=ON VC=140", "LINE X=2", "VAR:V1={$V1 + 1}",
            "JUMP=10 IF={$V1 < 3}"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucModalValueNotHeld, error.Code);
        Assert.Equal(8, error.Line);
    }

    // Controller-mapping 1, SKIP; D53; virtual machine 1: the skipped block before the label leaves F50 on one path and
    // F100 on the other, which the control holds, and the jump arrives with F100: the line after the label writes no
    // F, never the F100 of the walk on the path that skips the block.
    [Fact]
    public void Label_AfterASkippedBlockThatLeftTheFeedToTheControl_KeepsItThere()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE X=0 F=50\nSKIP F=100\nLABEL=10\nLINE X=1\nVAR:V1={$V1 + 1}"
            + "\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nG1 G17 X0. F50.\n/F100.\nN10\nG80\nG1 G17 G90 G94 X1.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Controller-mapping 1, SKIP; D53; virtual machine 1: the skipped block before the jump leaves F50 on one path and
    // F100 on the other, while the block before the label has F100 as well: the line after the label writes no F.
    [Fact]
    public void Jump_AfterASkippedBlockThatLeftTheFeedToTheControl_KeepsItThere()
    {
        string body = FanucCompile.Body("VAR:V1=0\nLINE X=0 F=100\nLABEL=10\nLINE X=1\nLINE X=2 F=50\nSKIP F=100"
            + "\nVAR:V1={$V1 + 1}\nJUMP=10 IF={$V1 < 3}");

        Assert.Equal("#1 = 0\nG1 G17 X0. F100.\nN10\nG80\nG1 G17 G90 G94 X1.\nX2. F50.\n/F100.\n#1 = #1 + 1\n"
            + "IF [#1 LT 3] GOTO 10\n", body);
    }

    // Controller-mapping 6, D210: the jump back to the label after the header is the M99 of the main program, with the
    // block skip where the block has SKIP.
    [Fact]
    public void Loop_ToTheLabelAfterTheHeader_IsM99()
    {
        string body = FanucCompile.Body("LABEL=START\nRAPID X=1 Y=1\nSKIP JUMP=START");

        Assert.Equal("G17 X1. Y1.\n/M99\n", body);
    }

    // Fanuc 7: FRAC has no form in custom macro B.
    [Fact]
    public void Expression_WithFrac_IsTheErrorCmp401()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("VAR:V1={FRAC(2.5)}"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucExpressionNotWritable, error.Code);
    }

    // Language 4.9, machine-config 7: a name that neither the V names nor [variables] map cover has no Fanuc variable.
    [Fact]
    public void Variable_NotMapped_IsTheErrorCmp400()
    {
        Diagnostic error = FanucCompile.ErrorOf(FanucCompile.Run(
            FanucCompile.Program("VAR:DEPTH=5"), FanucCompile.Mill()));

        Assert.Equal(DiagnosticCodes.FanucVariableNotMapped, error.Code);
    }

    // A file of one program that calls the subprogram 100 of the file, and that section, so that the call has the
    // target virtual machine 3.6 asks for (VM072); the mill of the sources writes it as one file.
    private static string CallerAndSub(params string[] blocks)
    {
        var lines = new List<string>
        {
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\" NUMBER=1",
            "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
        };
        lines.AddRange(blocks);
        lines.Add("PROGRAM=END");
        lines.Add("SUB=BEGIN NAME=100");
        lines.Add("LINE X=10 F=100");
        lines.Add("SUB=END");
        lines.Add("FILE=END");
        return FanucCompile.Lines(lines.ToArray());
    }
}
