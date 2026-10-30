using Ncx.Core.Machine;

namespace Ncx.Compilers.Tests.Heidenhain;

/// <summary>
/// Subprograms as LBL sections after the M30 of every caller, CALL LBL and CALL PGM (controllers heidenhain.md 1; 8
/// rule 6; controller-mapping 1 and 6; language 4.13; D48, D99).
/// </summary>
public sealed class HeidenhainSubprogramTests
{
    // Heidenhain 8 rule 6: the subprogram is LBL n ... LBL 0 after the M30 of the program that calls it, written from
    // an unknown target state, so that R0 and the feed stand at its first line (D99); END PGM follows it.
    [Fact]
    public void Rule6_Subprogram_IsALblSectionAfterTheM30OfTheProgramThatCallsIt()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"",
            "UNITS=MM WORKPLANE=XY",
            "RAPID X=0 Y=0 Z=2",
            "CALL=100",
            "PROGRAM=END",
            "SUB=BEGIN NAME=100",
            "LINE IX=30 F=800",
            "RAPID Z=5",
            "SUB=END",
            "FILE=END"));

        Assert.Equal(
            "0 BEGIN PGM T MM\r\n1 L X+0 Y+0 Z+2 R0 FMAX\r\n2 CALL LBL 100\r\n3 M30\r\n4 LBL 100\r\n"
            + "5 L IX+30 R0 F800\r\n6 L Z+5 FMAX\r\n7 LBL 0\r\n8 END PGM T MM\r\n",
            HeidenhainCompile.TextOf(result));
    }

    // Heidenhain 8 rule 6, language 4.13, D99: labels are program-local in Klartext, so the section stands in the file
    // of every program that calls it.
    [Fact]
    public void Rule6_SubprogramCalledByTwoPrograms_StandsInTheFileOfEach()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"A\"", "UNITS=MM", "RAPID X=0 Y=0 Z=2", "CALL=100", "PROGRAM=END",
            "PROGRAM=BEGIN NAME=\"B\"", "UNITS=MM", "RAPID X=0 Y=0 Z=2", "CALL=100", "PROGRAM=END",
            "SUB=BEGIN NAME=100", "RAPID Z=5", "SUB=END",
            "FILE=END"));

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.Equal(["A.h", "B.h"], [result.Files[0].Name, result.Files[1].Name]);
        foreach (CompiledFile file in result.Files)
        {
            Assert.Equal(["CALL LBL 100", "M30", "LBL 100", "L Z+5 R0 FMAX", "LBL 0"],
                HeidenhainCompile.LinesOf(file.Text).GetRange(2, 5));
        }
    }

    // Controller-mapping 6, CALL and TIMES; virtual machine 3.9: CALL LBL has no count, so a CALL with TIMES=3 is the
    // call three times in sequence.
    // Row W3-3 (S-5): a Heidenhain machine file without program_layout takes the family default file_per_program,
    // one program per file (heidenhain 1, 8 rule 6; language 4.13; machine-config 2), as the decimal separator takes
    // the comma; a written key still wins.
    [Fact]
    public void Rule6_MachineFileWithoutProgramLayout_WritesOneFilePerProgram()
    {
        MachineConfig machine = HeidenhainCompile.MillWith(
            "program_layout = \"file_per_program\"", "# program_layout left out");
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"A\"", "UNITS=MM", "RAPID X=0 Y=0 Z=2", "PROGRAM=END",
            "PROGRAM=BEGIN NAME=\"B\"", "UNITS=MM", "RAPID X=0 Y=0 Z=2", "PROGRAM=END",
            "FILE=END"), machine);

        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.Equal(["A.h", "B.h"], [result.Files[0].Name, result.Files[1].Name]);
    }

    [Fact]
    public void CallWithTimes_IsTheCallOnceForEveryPass()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Lines(
            "FILE=BEGIN NCX=1",
            "PROGRAM=BEGIN NAME=\"T\"", "UNITS=MM", "RAPID X=0 Y=0 Z=2", "CALL=100 TIMES=3", "PROGRAM=END",
            "SUB=BEGIN NAME=100", "RAPID IX=10", "SUB=END",
            "FILE=END"));

        Assert.Equal("L X+0 Y+0 Z+2 R0 FMAX\nCALL LBL 100\nCALL LBL 100\nCALL LBL 100",
            HeidenhainCompile.Body(result));
    }

    // Controller-mapping 6, CALL + ARG: CALL PGM name with the Q parameters set before the call.
    [Fact]
    public void CallOfAnotherFile_IsCallPgmWithItsArgumentsBefore()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("CALL=\"PART2\" ARG:Q1=5"));

        Assert.Equal("Q1 = 5\nCALL PGM PART2", HeidenhainCompile.Body(result));
    }

    // Controller-mapping 6, CALL + ARG: an argument that names no Q parameter has no Klartext form, CMP101.
    [Fact]
    public void ArgumentWithoutAQParameter_IsTheErrorCmp101()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program("CALL=\"PART2\" ARG:A=5"));

        Assert.Empty(result.Files);
        Assert.Equal(4, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainWordWithoutKlartext).Line);
    }

    // D149: an ARG name is a local of the call in NCX, and the caller reads its own value after it, while the Q
    // parameter set before CALL PGM survives the call on the control: a caller that reads it after the call is CMP125.
    [Fact]
    public void Call_WhoseArgumentTheCallerReadsAfterIt_IsTheErrorCmp125()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "VAR:Q1=2", "CALL=\"PART2\" ARG:Q1=5", "RAPID X={$Q1}"));

        Assert.Equal(5, HeidenhainCompile.Single(result, DiagnosticCodes.HeidenhainArgumentReadAfterCall).Line);
    }

    // D149: a caller that assigns the name again before it reads it reads its own value.
    [Fact]
    public void Call_WhoseArgumentTheCallerAssignsBeforeItReadsIt_ReportsNothing()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "CALL=\"PART2\" ARG:Q1=5", "VAR:Q1=3", "RAPID X={$Q1}"));

        Assert.DoesNotContain(DiagnosticCodes.HeidenhainArgumentReadAfterCall, HeidenhainCompile.Codes(result));
    }

    // D149: QL is local to a program, so a QL argument of CALL PGM never reaches the caller's QL: no CMP125.
    [Fact]
    public void CallPgm_WhoseQlArgumentTheCallerReadsAfterIt_ReportsNothing()
    {
        CompileResult result = HeidenhainCompile.Run(HeidenhainCompile.Program(
            "VAR:QL1=2", "CALL=\"PART2\" ARG:QL1=5", "RAPID X={$QL1}"));

        Assert.DoesNotContain(DiagnosticCodes.HeidenhainArgumentReadAfterCall, HeidenhainCompile.Codes(result));
    }
}
