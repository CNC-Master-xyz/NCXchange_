using Ncx.Core.Machine;
using static Ncx.Compilers.Tests.Siemens.SiemensCompile;

namespace Ncx.Compilers.Tests.Siemens;

/// <summary>
/// Rule 3 of controllers siemens.md 12 and the Siemens column of controller-mapping 3 and 4: T and M6 per
/// [tool_change], D from OFFSET, SETMS in a block of its own, S2= and M2=3 for the other spindles, the spindle modes,
/// COUPON and COUPOF, the functions of the machine.
/// </summary>
public sealed class SiemensToolTests
{
    // Siemens 12 rule 3: T1 and M6 per [tool_change] of the mill, D from OFFSET; T alone is the preload (controller-
    // mapping 3, PRELOAD and TOOL).
    [Fact]
    public void ToolChangeOnAMill_IsTAndM6ThenDAndThePreload()
    {
        Assert.Equal(Lines("T5 M6", "D5", "T6"), MillBody("TOOL=5 OFFSET=5", "PRELOAD=6"));
    }

    // Controller-mapping 3, TOOL=0: T0 M6 per unload of the mill.
    [Fact]
    public void ToolZero_IsTheUnloadTemplate()
    {
        Assert.Equal(Lines("T5 M6", "T0 M6"), MillBody("TOOL=5", "TOOL=0"));
    }

    // Siemens 12 rule 3 on the turret of millturn1.toml: T3 D3 from change = "T{tool} D{offset}", the D the one
    // register of OFFSET:LEN and OFFSET:RAD (controller-mapping 3; offsets_with_change).
    [Fact]
    public void ToolChangeOnATurret_IsTWithTheDOfTheTemplate()
    {
        Assert.Equal(Lines("T3 D3"), MillTurnBody("TOOL:TURRET1=3 OFFSET:LEN=3 OFFSET:RAD=3"));
    }

    // Controller-mapping 3, OFFSET:LEN: the D of the control is one register; two registers write the length one with
    // a WARNING (D301).
    [Fact]
    public void OffsetLenAndRad_TwoRegisters_WriteTheLengthWithAWarning()
    {
        CompileResult result = Run(Program(MillTurnHeader, "TOOL:TURRET1=3 OFFSET:LEN=3 OFFSET:RAD=13"), MillTurn());

        Assert.Equal(Lines("T3 D3"), Body(result));
        Assert.Equal(["CMP510"], CompilerCodes(result));
    }

    // D301: a block that sets OFFSET:LEN=0 writes D0, because a D of this control always carries the length, and D0
    // drops the radius with it; with COMP still on that loss is a WARNING.
    [Fact]
    public void OffsetLenZero_WithARadiusRegisterStillSet_IsD0WithAWarningWhileCompIsOn()
    {
        CompileResult result = Run(Program(MillTurnHeader, "TOOL:TURRET1=3 OFFSET:LEN=3 OFFSET:RAD=3",
            "COMP=LEFT LINE X=10 F=0.2", "OFFSET:LEN=0"), MillTurn());

        Assert.Equal(Lines("T3 D3", "G1 G41 X10 F0.2", "D0"), Body(result));
        Assert.Equal(["CMP510"], CompilerCodes(result));
    }

    // D301: without a compensation on, the D0 of an OFFSET:LEN=0 block is written without a WARNING.
    [Fact]
    public void OffsetLenZero_WithoutACompensation_IsD0WithoutAWarning()
    {
        CompileResult result = Run(Program(MillTurnHeader, "TOOL:TURRET1=3 OFFSET:LEN=3 OFFSET:RAD=3",
            "OFFSET:LEN=0"), MillTurn());

        Assert.Equal(Lines("T3 D3", "D0"), Body(result));
        Assert.Empty(CompilerCodes(result));
    }

    // Controller-mapping 1, FRAME=MACHINE: G0 G53 Z0 D0, the offset cancelled in the block of the retract.
    [Fact]
    public void OffsetZeroInAMachineFrameRetract_IsD0InTheSameBlock()
    {
        Assert.Equal(Lines("T1 M6", "D1", "G0 G53 Z0 D0"),
            MillBody("TOOL=1 OFFSET=1", "RAPID Z=0 FRAME=MACHINE OFFSET=0"));
    }

    // Controller-mapping 1, SKIP: the change is written from [tool_change] without the skip mark, an ERROR.
    [Fact]
    public void SkipOnAToolChange_IsAnError()
    {
        Assert.Equal(["CMP502"], CompilerCodes(Run(Program(MillHeader, "SKIP TOOL=1"), Mill())));
    }

    // Siemens 12 rule 3: S and M3 of the master spindle, S<n>= and M<n>=3 for the other spindles (controller-mapping 4,
    // SPINDLE:role; millturn1.toml).
    [Fact]
    public void SpindlesOfTheRoles_AreThePlainWordsAndTheExtensions()
    {
        Assert.Equal(Lines("S1500 M3", "S3=6000 M3=3", "M3=5", "M2=70"),
            MillTurnBody("SPINDLE:MAIN=CW RPM:MAIN=1500", "SPINDLE:TOOL=CW RPM:TOOL=6000", "SPINDLE:TOOL=OFF",
                "SPINDLE_MODE:SUB=AXIS"));
    }

    // Siemens 12 rule 3: SETMS(n) in its own block when the master spindle changes, G96 acting on the master spindle
    // (siemens 5; millturn1.toml, [spindle.SUB]); SETMS alone returns before a plain S of the configured one.
    [Fact]
    public void SurfaceSpeedOnTheSubSpindle_SelectsItAsMasterInABlockOfItsOwn()
    {
        Assert.Equal(Lines("SETMS(2)", "G96 S2=200 LIMS[2]=3000", "SETMS", "S1500 M3"),
            MillTurnBody("CSS:SUB=ON VC:SUB=200 RPM_MAX:SUB=3000", "SPINDLE:MAIN=CW RPM:MAIN=1500"));
    }

    // D294, machine-config 5: a spindle's number is the address extension its templates write, else the digits of its
    // resource id, so a SUB spindle S2 whose templates write the plain S and M3 is selected with SETMS(2).
    [Fact]
    public void SpindleNumber_WithoutAnExtensionInItsTemplates_IsTheDigitsOfItsResourceId()
    {
        CompileResult result = Run(Program(MillTurnHeader, "CSS:SUB=ON VC:SUB=200 RPM_MAX:SUB=3000"),
            PlainSubSpindle(text => text));

        Assert.Empty(CompilerCodes(result));
        Assert.Equal(Lines("SETMS(2)", "G96 S200 LIMS=3000"), Body(result));
    }

    // D294: a spindle with neither an extension in its templates nor digits in its resource id has no number, and
    // SETMS for it is the ERROR CMP511, once per text that acts on the master spindle (controllers siemens.md 5, 12
    // rule 3).
    [Fact]
    public void SpindleNumber_WithNeitherAnExtensionNorDigitsInItsId_IsTheErrorCmp511()
    {
        CompileResult result = Run(Program(MillTurnHeader, "CSS:SUB=ON VC:SUB=200 RPM_MAX:SUB=3000"),
            PlainSubSpindle(text => text.Replace("\"S2\"", "\"SUBSPINDLE\"", StringComparison.Ordinal)));

        Assert.Equal(["CMP511"], CompilerCodes(result).Distinct());
    }

    // millturn1.toml with the templates of [spindle.SUB] in the plain words of the master spindle, no extension.
    private static MachineConfig PlainSubSpindle(Func<string, string> change)
    {
        return Variant(MillTurnFile, text =>
        {
            int start = text.IndexOf("\n[spindle.SUB]", StringComparison.Ordinal);
            int end = text.IndexOf("\n[spindle.TOOL]", StringComparison.Ordinal);
            string plain = "\n[spindle.SUB]\nCW = \"M3\"\nCCW = \"M4\"\nOFF = \"M5\"\nRPM = \"S{rpm}\"\n"
                + "VC = \"G96 S{value}\"\nCSS_OFF = \"G97\"\nRPM_MAX = \"LIMS={value}\"\n";
            return change(text[..start] + plain + text[end..]);
        });
    }

    // Language 4.11: under CSS RPM is ignored and not written; when CSS goes off the spindle turns at RPM again, G97
    // and S with the last RPM (controller-mapping 4, CSS).
    [Fact]
    public void SurfaceSpeedOff_WritesG97AndTheSpeedOfRpm()
    {
        Assert.Equal(Lines("S1000 M3", "G96 S180 LIMS=3000", "G97 S1200"),
            MillTurnBody("SPINDLE:MAIN=CW RPM:MAIN=1000", "CSS:MAIN=ON VC:MAIN=180 RPM_MAX:MAIN=3000",
                "RPM:MAIN=1200", "CSS:MAIN=OFF"));
    }

    // Siemens 1: X=10 when the value is an expression; the plain S of the templates S{rpm} and G96 S{value} of the
    // master spindle takes the equals sign too, S=R1*2 (machine-config 5; controller-mapping 4, RPM and CSS).
    [Fact]
    public void SpeedAsAnExpression_InThePlainSOfTheTemplate_IsWrittenAfterAnEqualsSign()
    {
        Assert.Equal(Lines("R1=1000", "S=R1*2 M3", "G96 S=R1/10 LIMS=3000"),
            MillTurnBody("VAR:R1=1000", "SPINDLE:MAIN=CW RPM:MAIN={$R1 * 2}",
                "CSS:MAIN=ON VC:MAIN={$R1 / 10} RPM_MAX:MAIN=3000"));
    }

    // Siemens 2 and 5: G94, G95, G96 and G961 are one group, so G94 would switch the constant surface speed off; under
    // FEED_MODE=PER_MIN the surface speed is G961, and the feed motion after it writes no G94 (controller-mapping 4,
    // CSS; D300).
    [Fact]
    public void SurfaceSpeedUnderFeedPerMinute_IsG961AndTheFeedMotionKeepsIt()
    {
        string program = Program("FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=1500 CSS:MAIN=ON VC:MAIN=200 RPM_MAX:MAIN=3000", "LINE X=40 F=100");

        Assert.Equal(Lines("G961 S200 LIMS=3000 M3", "G1 X40 F100"), Body(Run(program, MillTurn())));
    }

    // D342: G97 switches the surface speed off with the feed type G95 and G971 with G94, so the G97 of a CSS_OFF
    // template is written as the code of the feed type the block leaves, and the feed motion after it writes no G94.
    [Fact]
    public void SurfaceSpeedOff_WhenTheBlockLeavesFeedPerMinute_IsG971()
    {
        string program = Program("FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF",
            "SPINDLE:MAIN=CW RPM:MAIN=1500 CSS:MAIN=ON VC:MAIN=200 RPM_MAX:MAIN=3000", "CSS:MAIN=OFF",
            "LINE X=40 F=100");

        Assert.Equal(Lines("G961 S200 LIMS=3000 M3", "G971 S1500", "G1 X40 F100"), Body(Run(program, MillTurn())));
    }

    // Siemens 2 and 5: a FEED_MODE under the constant surface speed is the surface speed code of the new feed type,
    // G961 per minute and G96 per revolution, never G94 or G95, which would switch it off; after G97 the feed type
    // stays (controller-mapping 4, CSS).
    [Fact]
    public void FeedModeChange_UnderSurfaceSpeed_IsTheSurfaceSpeedCodeOfTheFeedType()
    {
        Assert.Equal(
            Lines("S1500 M3", "G96 S200 LIMS=3000", "G1 G961 X40 F100", "G1 G96 X38 F0.2", "G97 S1500", "G1 X36"),
            MillTurnBody("SPINDLE:MAIN=CW RPM:MAIN=1500", "CSS:MAIN=ON VC:MAIN=200 RPM_MAX:MAIN=3000",
                "FEED_MODE=PER_MIN LINE X=40 F=100", "FEED_MODE=PER_REV LINE X=38 F=0.2", "CSS:MAIN=OFF",
                "LINE X=36"));
    }

    // D99 with siemens 2 and 5: a subprogram is written from an unknown state of the control, and its first feed motion
    // under the caller's constant surface speed writes G96, since G95 would switch the surface speed off.
    [Fact]
    public void FeedMotionInASubprogram_UnderSurfaceSpeed_IsG96()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF
            SPINDLE:MAIN=CW RPM:MAIN=1500 CSS:MAIN=ON VC:MAIN=200 RPM_MAX:MAIN=3000
            RAPID X=42 Z=2
            CALL=FACE
            PROGRAM=END
            SUB=BEGIN NAME=FACE
            LINE X=0 F=0.1
            SUB=END
            FILE=END
            """;

        Assert.EndsWith(Lines("PROC FACE", "G1 G90 G96 X0 F0.1", "RET"), TextOf(Run(program, MillTurn())),
            StringComparison.Ordinal);
    }

    // Siemens 5 and 12 rule 3: what a RAW line leaves active is not known, the master spindle neither, so the plain S
    // after it selects the configured master spindle again with SETMS (language 4.1, RAW).
    [Fact]
    public void MasterSpindle_AfterARawLine_IsSelectedAgain()
    {
        Assert.Equal(Lines("SETMS(2)", "G96 S2=200 M2=3", "STOPRE", "SETMS", "S1000 M3"),
            MillTurnBody("CSS:SUB=ON VC:SUB=200 SPINDLE:SUB=CW", "RAW:SIEMENS=\"STOPRE\"",
                "SPINDLE:MAIN=CW RPM:MAIN=1000"));
    }

    // Siemens 5 and virtual machine 1: the jump back arrives at the label with the master spindle of its own block,
    // SETMS(2) here, so the plain S after the label selects the configured master spindle again.
    [Fact]
    public void MasterSpindle_InALoop_IsSelectedAgainAfterTheLabel()
    {
        Assert.Equal(Lines("R3=0", "S1000 M3", "PASS:", "SETMS", "S1200", "SETMS(2)", "G96 S2=200 M2=3", "R3=R3+1",
                "IF R3<3 GOTOB PASS"),
            MillTurnBody("VAR:R3=0", "SPINDLE:MAIN=CW RPM:MAIN=1000", "LABEL=PASS", "RPM:MAIN=1200",
                "CSS:SUB=ON VC:SUB=200 SPINDLE:SUB=CW", "VAR:R3={$R3 + 1}", "JUMP=PASS IF={$R3 < 3}"));
    }

    // Siemens 5: a machine with one spindle has no other master spindle, so no SETMS stands after a RAW line
    // (machine-config 4; siemens-840dsl-mill.toml, the spindle S1 alone).
    [Fact]
    public void MasterSpindle_OnAMachineWithOneSpindle_IsNeverSelectedAgain()
    {
        Assert.Equal(Lines("S1000 M3", "STOPRE", "S1200"),
            MillBody("SPINDLE=CW RPM=1000", "RAW:SIEMENS=\"STOPRE\"", "RPM=1200"));
    }

    // D99: a subprogram is written from an unknown state of the control, so its first plain S selects the configured
    // master spindle with SETMS in a block of its own (siemens 5).
    [Fact]
    public void MasterSpindleInASubprogram_IsSelectedAtItsFirstUse()
    {
        string program = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="T"
            FEED_MODE=PER_REV COMP=OFF UNITS=MM WORKPLANE=ZX DIAMETER=ON CYCLE=OFF
            CALL=START_MAIN
            PROGRAM=END
            SUB=BEGIN NAME=START_MAIN
            SPINDLE:MAIN=CW RPM:MAIN=800
            SUB=END
            FILE=END
            """;

        Assert.EndsWith(Lines("PROC START_MAIN", "SETMS", "S800 M3", "RET"), TextOf(Run(program, MillTurn())),
            StringComparison.Ordinal);
    }

    // D172: a tool called by name, and the preload of one, take change_named and preload_named with {name}.
    [Fact]
    public void ToolByName_IsWrittenWithTheNamedTemplates()
    {
        Assert.Equal(Lines("T=\"DRILL_D8\" M6", "T=\"MILL_D10\""),
            MillBody("TOOL=\"DRILL_D8\"", "PRELOAD=\"MILL_D10\""));
    }

    // D172: a tool called by name on a machine that takes names but has no named template is the ERROR CMP008.
    [Fact]
    public void ToolByName_WithoutChangeNamed_IsTheErrorCmp008()
    {
        MachineConfig machine = Variant("machines/siemens-840dsl-mill.toml", text => text
            .Replace("change_named =", "# change_named =", StringComparison.Ordinal)
            .Replace("preload_named =", "# preload_named =", StringComparison.Ordinal));

        Assert.Equal(["CMP008", "CMP008"],
            CompilerCodes(Run(Program(MillHeader, "TOOL=\"DRILL_D8\"", "PRELOAD=\"MILL_D10\""), machine)));
    }

    // D172: a numeric tool keeps the numeric change template.
    [Fact]
    public void ToolByNumber_KeepsTheNumericTemplate()
    {
        Assert.Equal(Lines("T5 M6"), MillBody("TOOL=5"));
    }

    // D172: a tool name with a special character or of more than 31 characters is the WARNING CMP515 (840D sl
    // NC-Programmierung SW4.92 p.67; Bedienhandbuch Fräsen 2013 p.554-555), for TOOL and PRELOAD.
    [Theory]
    [InlineData("TOOL=\"DRILL D8\"")]
    [InlineData("TOOL=\"BOHRER_Ø8\"")]
    [InlineData("PRELOAD=\"DRILL.8\"")]
    [InlineData("TOOL=\"A234567890123456789012345678901X\"")]
    public void ToolName_WithASpecialCharacterOrTooLong_IsTheWarningCmp515(string block)
    {
        Assert.Contains("CMP515", CompilerCodes(Run(Program(MillHeader, block), Mill())));
    }

    // D172: letters of either case, digits, _ and - pass, as the corpus writes T="SF8-ST-NL30-SL21" (siemens.md 1).
    [Theory]
    [InlineData("TOOL=\"DRILL_D8\"")]
    [InlineData("TOOL=\"SF8-ST-NL30-SL21\"")]
    [InlineData("PRELOAD=\"Bohrer\"")]
    public void ToolName_OfLettersDigitsUnderscoreAndHyphen_HasNoCmp515(string block)
    {
        Assert.DoesNotContain("CMP515", CompilerCodes(Run(Program(MillHeader, block), Mill())));
    }

    // Controller-mapping 4, ORIENT: SPOS= of the configured master spindle.
    [Fact]
    public void Orient_IsSposOfTheTemplate()
    {
        Assert.Equal(Lines("SPOS=90", "SPOS[2]=45"), MillTurnBody("ORIENT:MAIN=90", "ORIENT:SUB=45"));
    }

    // Controller-mapping 4, SPINDLE_SYNC: COUPON(S2,S1) of [spindle_sync], with PHASE the angle, COUPOF off
    // (millturn1.toml; D154).
    [Fact]
    public void SpindleSync_IsCouponWithThePhaseAndCoupof()
    {
        Assert.Equal(Lines("COUPON(S2,S1)", "COUPOF(S2,S1)", "COUPON(S2,S1,113.5)"),
            MillTurnBody("SPINDLE_SYNC=MAIN,SUB", "SPINDLE_SYNC=OFF", "SPINDLE_SYNC=MAIN,SUB PHASE=113.5"));
    }

    // D154: [spindle_sync] couples the default spindle with the other work spindle; another pair is an ERROR.
    [Fact]
    public void SpindleSync_OfAnotherPair_IsAnError()
    {
        Assert.Equal(["CMP512"],
            CompilerCodes(Run(Program(MillTurnHeader, "SPINDLE_SYNC=SUB,MAIN"), MillTurn())));
    }

    // Controller-mapping 4, COOLANT and FUNC: the M codes of the tables of the machine in the main line; MFUNC with a
    // WARNING (language 4.6); STOP as M0 and M1 (controller-mapping 1).
    [Fact]
    public void Functions_AreTheMCodesOfTheTables()
    {
        CompileResult result = Run(Program(MillTurnHeader, "COOLANT=ON FUNC:SUB_CHUCK=OPEN", "MFUNC=123",
            "STOP=OPTIONAL", "STOP=PROGRAM"), MillTurn());

        Assert.Equal(Lines("M8 M68", "M123", "M1", "M0"), Body(result));
        Assert.Equal(["CMP513"], CompilerCodes(result));
    }

    // Language 4.10, WORKPIECE, and D222: millturn1.toml names no selection code, so WORKPIECE writes nothing and
    // the datum of the side comes from the ORIGIN of the program.
    [Fact]
    public void WorkpieceSub_WithoutATemplate_WritesNothingAndTheOriginWritesTheDatum()
    {
        Assert.Equal(Lines("G55"), MillTurnBody("WORKPIECE=SUB", "ORIGIN=2"));
    }
}
