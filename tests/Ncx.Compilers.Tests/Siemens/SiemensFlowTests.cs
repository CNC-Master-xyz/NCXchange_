using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// The flow of controllers siemens.md 8 and the Siemens column of controller-mapping 6, as the compiler writes it: the
/// labels NAME:, IF ... GOTOF chains, the calls, REPEAT with P, the variables and the expressions.
/// </summary>
public sealed class SiemensFlowTests
{
    // Controller-mapping 6, JUMP + IF: IF cond GOTOF label forward, GOTOB backward, the labels NAME: at the block
    // start; the structured loops are never reconstructed.
    [Fact]
    public void JumpWithIf_BackAndForward_IsIfGotobAndGotof()
    {
        Assert.Equal(Lines("R1=0", "BACK:", "R1=R1+1", "IF R1<3 GOTOB BACK", "GOTOF DONE", "DONE:"),
            MillBody("VAR:R1=0", "LABEL=BACK", "VAR:R1={$R1 + 1}", "JUMP=BACK IF={$R1 < 3}", "JUMP=DONE",
                "LABEL=DONE"));
    }

    // Siemens 8: GOTOB jumps backward, and a jump to the label of its own block runs the block again, whose label line
    // stands before the IF line (controller-mapping 6, JUMP + IF; language 4.9).
    [Fact]
    public void JumpWithIf_ToTheLabelOfItsOwnBlock_IsGotob()
    {
        Assert.Equal(Lines("R1=0", "AGAIN:", "R1=R1+1", "IF R1<3 GOTOB AGAIN"),
            MillBody("VAR:R1=0", "LABEL=AGAIN VAR:R1={$R1 + 1} IF={$R1 < 3} JUMP=AGAIN"));
    }

    // Virtual machine 1: the STATIC walk does not follow the jump back, which arrives at the label with the modal codes
    // of its own block, G95 here, so the modal words after the label stand again (language 2 rule 2).
    [Fact]
    public void Label_ReachedByAJump_WritesTheModalWordsAgain()
    {
        Assert.Equal(Lines("R1=0", "G1 X0 F100", "BACK:", "G1 G90 G94 X1 F100", "G1 G95 X2 F0.1", "R1=R1+1",
                "IF R1<3 GOTOB BACK"),
            MillBody("VAR:R1=0", "LINE X=0 F=100", "LABEL=BACK", "LINE X=1 F=100", "FEED_MODE=PER_REV LINE X=2 F=0.1",
                "VAR:R1={$R1 + 1}", "JUMP=BACK IF={$R1 < 3}"));
    }

    // Controllers siemens.md 8: a label begins with two letters and is no statement of the language; an integer label
    // and a label named like a statement are written with LABEL_ in front.
    [Fact]
    public void Label_OfAnIntegerAndOfAStatement_IsWrittenWithAPrefix()
    {
        Assert.Equal(Lines("GOTOF LABEL_10", "LABEL_10:", "GOTOF LABEL_LOOP", "LABEL_LOOP:"),
            MillBody("JUMP=10", "LABEL=10", "JUMP=LOOP", "LABEL=LOOP"));
    }

    // Controller-mapping 1, JUMP=END: GOTOF to a label followed by M30, which the program end carries.
    [Fact]
    public void JumpToEnd_GoesToTheLabelBeforeM30()
    {
        CompileResult result = Run(Program(MillHeader, "VAR:R1=1", "JUMP=END IF={$R1 == 1}", "RAPID X=10"), Mill());

        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G71", "R1=1", "IF R1==1 GOTOF PROGRAM_END", "G0 X10",
            "PROGRAM_END:", "M30"), TextOf(result));
    }

    // Language 4.9 and virtual machine 3.6: JUMP=END in a subprogram ends the program from the call, which the
    // parameterized return RET("<end label>",,$P_STACK) does, since an M30 in a subprogram only returns to the caller
    // (D344). The condition stands as a jump over the return, as it does over a conditional CALL.
    [Fact]
    public void JumpToEndInASubprogram_WithACondition_IsTheParameterizedReturnUnderAJumpOverIt()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="MAIN"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            VAR:R1=1
            CALL=CHECK
            RAPID X=10
            PROGRAM=END
            SUB=BEGIN NAME=CHECK
            JUMP=END IF={$R1 == 1}
            SUB=END
            FILE=END
            """;
        CompileResult result = Run(program, Mill());

        Assert.Empty(CompilerCodes(result));
        Assert.Contains("IF NOT (R1==1) GOTOF SKIP_CALL_9", TextOf(result));
        Assert.Contains("RET(\"PROGRAM_END\",,$P_STACK)", TextOf(result));
        Assert.Contains("PROGRAM_END:", TextOf(result));
    }

    // D344 and the 840D sl manual (NC-Programmierung SW4.92, p.523): RET "can only be used in subprograms that were
    // not defined with the attribute SAVE", and the parameterized form is no exception the manual names (p.524-526),
    // so JUMP=END in a subprogram whose RAW PROC line declares SAVE, as the reader keeps PROC NAME SAVE, is the ERROR
    // CMP597 naming the block.
    [Theory]
    [InlineData("PROC CHECK SAVE")]
    [InlineData("N5 PROC CHECK SAVE")]
    [InlineData("PROC CHECK SBLOF SAVE DISPLOF")]
    public void JumpToEndInASubprogram_DeclaredSave_IsTheErrorCmp597(string proc)
    {
        string program = $$"""
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="MAIN"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            VAR:R1=1
            CALL=CHECK
            RAPID X=10
            PROGRAM=END
            SUB=BEGIN NAME=CHECK
            RAW:SIEMENS="{{proc}}"
            JUMP=END IF={$R1 == 1}
            SUB=END
            FILE=END
            """;
        CompileResult result = Run(program, Mill());

        Assert.Equal(["CMP597"], CompilerCodes(result));
    }

    // D344: the CMP597 of a SAVE subprogram stands after a parameter list too, PROC CHECK(REAL WERT1) SAVE with the
    // parameter its call gives (NC-Programmierung SW4.92, p.511-512, p.523).
    [Fact]
    public void JumpToEndInASubprogram_DeclaredSaveAfterParameters_IsTheErrorCmp597()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="MAIN"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            CALL=CHECK ARG:WERT1=1
            RAPID X=10
            PROGRAM=END
            SUB=BEGIN NAME=CHECK
            RAW:SIEMENS="PROC CHECK(REAL WERT1) SAVE"
            JUMP=END IF={$WERT1 == 1}
            SUB=END
            FILE=END
            """;

        Assert.Equal(["CMP597"], CompilerCodes(Run(program, Mill())));
    }

    // D344: SAVE inside the name of a subprogram, SAVE_CHECK, is no attribute, so the parameterized return stands
    // (NC-Programmierung SW4.92, p.511, p.523).
    [Fact]
    public void JumpToEndInASubprogram_SaveInsideTheName_IsTheParameterizedReturn()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="MAIN"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            CALL=SAVE_CHECK
            RAPID X=10
            PROGRAM=END
            SUB=BEGIN NAME=SAVE_CHECK
            RAW:SIEMENS="PROC SAVE_CHECK"
            JUMP=END
            SUB=END
            FILE=END
            """;
        CompileResult result = Run(program, Mill());

        Assert.Empty(CompilerCodes(result));
        Assert.Contains("RET(\"PROGRAM_END\",,$P_STACK)", TextOf(result), StringComparison.Ordinal);
        Assert.Contains("PROC SAVE_CHECK", LinesOf(TextOf(result)));
    }

    // D344: a RAW PROC line with SBLOF or DISPLOF and without SAVE leaves the parameterized return as it is
    // (NC-Programmierung SW4.92, p.512, p.523).
    [Theory]
    [InlineData("PROC CHECK SBLOF")]
    [InlineData("PROC CHECK DISPLOF")]
    public void JumpToEndInASubprogram_ProcWithoutSave_IsTheParameterizedReturn(string proc)
    {
        string program = $$"""
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="MAIN"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            CALL=CHECK
            RAPID X=10
            PROGRAM=END
            SUB=BEGIN NAME=CHECK
            RAW:SIEMENS="{{proc}}"
            JUMP=END
            SUB=END
            FILE=END
            """;
        CompileResult result = Run(program, Mill());

        Assert.Empty(CompilerCodes(result));
        Assert.Contains("RET(\"PROGRAM_END\",,$P_STACK)", TextOf(result), StringComparison.Ordinal);
    }

    // D344: the compiler itself never declares a subprogram SAVE, its PROC line is PROC NAME with the parameters
    // alone (controllers siemens.md 8, 12 rule 1), so the RET it writes stands in a SAVE subprogram only where a RAW
    // line of the file declares it.
    [Fact]
    public void ProcLine_OfASubprogramWithAndWithoutParameters_NeverDeclaresSave()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="MAIN"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            CALL=CHECK
            CALL=POCK ARG:DEPTH=5
            PROGRAM=END
            SUB=BEGIN NAME=CHECK
            JUMP=END
            SUB=END
            SUB=BEGIN NAME=POCK
            LINE Z={-$DEPTH} F=100
            SUB=END
            FILE=END
            """;
        CompileResult result = Run(program, Mill());

        Assert.Empty(CompilerCodes(result));
        Assert.DoesNotContain("SAVE", TextOf(result), StringComparison.Ordinal);
    }

    // Siemens 8: the comparisons bind weaker than AND and OR on the control, so they stand in parentheses; NOT binds
    // stronger than a comparison (language 4.12).
    [Fact]
    public void Condition_WithAndAndNot_PutsTheComparisonsInParentheses()
    {
        Assert.Equal(Lines("R1=1 R2=2", "IF (R1>0) AND NOT (R2==5) GOTOF DONE", "DONE:"),
            MillBody("VAR:R1=1 VAR:R2=2", "JUMP=DONE IF={$R1 > 0 AND NOT $R2 == 5}", "LABEL=DONE"));
    }

    // Controller-mapping 6, functions: TRUNC for INT, MINVAL and MAXVAL for MIN and MAX, POT for the square, <> for !=.
    [Fact]
    public void Expression_Functions_AreTheSinumerikFunctions()
    {
        Assert.Equal(Lines("R1=2", "R2=TRUNC(R1/3)+MINVAL(R1,4)+POT(R1)", "IF R2<>1 GOTOF DONE", "DONE:"),
            MillBody("VAR:R1=2", "VAR:R2={INT($R1 / 3) + MIN($R1, 4) + $R1 ^ 2}", "JUMP=DONE IF={$R2 != 1}",
                "LABEL=DONE"));
    }

    // Siemens 8: the SINUMERIK language has no power other than the square and no sign function, an ERROR.
    [Fact]
    public void Expression_PowerOfThree_IsAnError()
    {
        Assert.Equal(["CMP590"], CompilerCodes(Run(Program(MillHeader, "VAR:R1=2", "VAR:R2={$R1 ^ 3}"), Mill())));
    }

    // D173: an entry of [variables] map is a template of the variable's number, so Q = "R{index}" writes Q1 as R1,
    // the spelling the manual's worked assignments use; a name no entry covers is an ERROR that names the variable.
    [Fact]
    public void Variable_OfAnotherController_IsMappedOrAnError()
    {
        CompileResult mapped = Run(Program(MillHeader, "VAR:Q1=5", "RAPID X={$Q1}"), Mill());
        Assert.Equal(Lines("R1=5", "G0 X=R1"), Body(mapped));
        Assert.Equal(["CMP591"], CompilerCodes(Run(Program(MillHeader, "VAR:Q1=5"),
            Replaced(MillFile, "map = { Q = \"R{index}\" }", "# no map:"))));
    }

    // D173: an R parameter is a field of type REAL, so no QS string fits one, and the Heidenhain names QL5, QR5 and
    // QS5, which no SINUMERIK program declares either, are the same ERROR.
    [Theory]
    [InlineData("QL5")]
    [InlineData("QR5")]
    [InlineData("QS5")]
    public void VariableMap_HeidenhainNameNoEntryCovers_IsTheErrorCmp591(string name)
    {
        Assert.Equal(["CMP591"], CompilerCodes(Run(Program(MillHeader, "VAR:" + name + "=5"), Mill())));
    }

    // D261: a name the control does not have already is declared with DEF REAL in the definition part of the unit
    // that uses it; an R parameter needs no declaration, since the control predefines it as an array of REAL.
    [Fact]
    public void Declarations_OfTheNamesAUnitUses_StandInItsDefinitionPart()
    {
        CompileResult result = Run(Program(MillHeader, "VAR:LEN=10 VAR:R1=1", "RAPID X={$LEN + $R1}"), Mill());

        Assert.Equal(Lines("%_N_T_MPF", "DEF REAL LEN", "G17 G40 G90 G94 G71", "LEN=10 R1=1", "G0 X=LEN+R1", "M30"),
            TextOf(result));
    }

    // D261: a name a RAW DEF of the unit declares itself is not declared a second time, which would be alarm 12200.
    [Fact]
    public void Declarations_OfANameARawDefOfTheUnitDeclares_AreLeftOut()
    {
        CompileResult result = Run(Program(MillHeader, "RAW:SIEMENS=\"DEF REAL LEN\"", "VAR:LEN=10"), Mill());

        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G71", "DEF REAL LEN", "LEN=10", "M30"), TextOf(result));
    }

    // D261: a DEF is local to its unit, so a name two units use is an ERROR unless [variables] declared of the machine
    // names it, which makes the control declare it outside the program.
    [Fact]
    public void Declarations_OfANameSeveralUnitsUse_AreAnErrorUnlessTheMachineDeclaresIt()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            VAR:XMW_1=10
            CALL=SHARED
            PROGRAM=END
            SUB=BEGIN NAME=SHARED
            RAPID X={$XMW_1}
            SUB=END
            FILE=END
            """ + "\n";

        Assert.Equal(["CMP595", "CMP595"], CompilerCodes(Run(program, Mill())));
        CompileResult declared = Run(program, Replaced(MillFile, "call_depth = 8",
            "call_depth = 8\ndeclared = [\"XMW_1\"]"));
        Assert.DoesNotContain("DEF REAL XMW_1", TextOf(declared), StringComparison.Ordinal);
    }

    // Controller-mapping 6, CALL and TIMES: NAME P3 repeats the call three times.
    [Fact]
    public void Call_WithTimes_IsNameP()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            RAPID X=0
            CALL=DRILL_ROW TIMES=3
            PROGRAM=END
            SUB=BEGIN NAME=DRILL_ROW
            RAPID IX=10
            SUB=END
            FILE=END
            """;

        Assert.Contains("\nDRILL_ROW P3\n", TextOf(Run(program, Mill())), StringComparison.Ordinal);
    }

    // Controller-mapping 6, structured loops: the compiler writes IF ... GOTOF chains, a CALL under IF a jump over it.
    [Fact]
    public void CallWithIf_IsAJumpOverTheCall()
    {
        Assert.Equal(Lines("R1=1", "IF NOT (R1==1) GOTOF SKIP_CALL_5", "EXTCALL(\"O9010\")", "SKIP_CALL_5:"),
            MillBody("VAR:R1=1", "CALL=\"O9010\" IF={$R1 == 1}"));
    }

    // Controller-mapping 6: a call under IF is a jump over it, so the label after the call is reached with the modal
    // call the subprogram leaves armed and without it; the call at a position after it arms it again (siemens 7; D99).
    [Fact]
    public void CallWithIf_OfASubprogramThatLeavesAModalCall_ArmsItAgainAfterTheCall()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            VAR:R1=1
            CYCLE=DRILL SURFACE=0 CLEARANCE=2 DEPTH=-10
            CALL=HOLE IF={$R1 == 1}
            CYCLE_CALL X=20
            CYCLE=OFF
            PROGRAM=END
            SUB=BEGIN NAME=HOLE
            CYCLE_CALL X=10
            SUB=END
            FILE=END
            """;

        Assert.Equal(Lines("%_N_T_MPF", "G17 G40 G90 G94 G71", "R1=1", "IF NOT (R1==1) GOTOF SKIP_CALL_6", "HOLE",
                "SKIP_CALL_6:", "MCALL CYCLE81(2,0,2,-10)", "G0 X20", "MCALL", "M30", "%_N_HOLE_SPF", "PROC HOLE",
                "MCALL CYCLE81(2,0,2,-10)", "G0 G90 X10", "RET"),
            TextOf(Run(program, Mill())));
    }

    // Controller-mapping 6, CALL: a program outside the file with EXTCALL (CALL="name").
    [Fact]
    public void CallOfAProgramOutsideTheFile_IsExtcall()
    {
        Assert.Equal(Lines("EXTCALL(\"O9010\")"), MillBody("CALL=\"O9010\""));
    }

    // Controller-mapping 6, REPEAT + TIMES: REPEAT LABEL P=n, the blocks from the label to the REPEAT n more times.
    [Fact]
    public void Repeat_WithTimes_IsRepeatWithP()
    {
        Assert.Equal(Lines("G0 X0", "ROW:", "G0 X=IC(10)", "REPEAT ROW P=2"),
            MillBody("RAPID X=0", "LABEL=ROW", "RAPID IX=10", "REPEAT=ROW TIMES=2"));
    }

    // Language 4.9, RETURN: in a subprogram the return, sub_end (machine-config 2).
    [Fact]
    public void Return_InASubprogram_IsRet()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF
            CALL=SIDE
            PROGRAM=END
            SUB=BEGIN NAME=SIDE
            VAR:R1=1
            RETURN
            SUB=END
            FILE=END
            """;

        Assert.EndsWith(Lines("PROC SIDE", "R1=1", "RET", "RET"), TextOf(Run(program, Mill())),
            StringComparison.Ordinal);
    }
}
