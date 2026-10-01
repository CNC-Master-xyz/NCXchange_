using Ncx.Core.Model;
using static Ncx.Readers.Tests.Fanuc.FanucRead;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The kind of a Fanuc O section (controller-mapping 1 and 6; virtual machine 3.6 and 3.9; language 4.13): an O section
/// that a call of the file enters by its number is a SUB, also when it holds an M30, because a CALL of a program is an
/// ERROR. Every call of controller-mapping 6 that enters an O program of the file names it for the structure pass
/// (SourceStructure.Calls): M98 P, M98 P L, the older form M98 P20100, G65 P and the M200 P of the builder nakamura.
/// </summary>
public sealed class FanucSectionKindTests
{
    // An M30 in a subprogram ends the program from the call, as a Fanuc M30 in a subprogram does: JUMP=END, and the O
    // section stays the SUB its call enters, whichever call of controller-mapping 6 it is (virtual machine 3.6 and 3.9;
    // language 4.13). check finds no CALL of a program.
    [Theory]
    [InlineData("M98 P100", "CALL=100")]
    [InlineData("M98 P0100", "CALL=100")]
    [InlineData("M98 P100 L2", "CALL=100 TIMES=2")]
    [InlineData("M98 P20100", "CALL=100 TIMES=2")]
    [InlineData("G65 P100 A1.", "CALL=100 ARG:V1=1")]
    public void SectionKind_OSectionHoldingM30EnteredByACall_IsASubWhoseM30IsJumpEnd(string call, string written)
    {
        string text = Text("%\nO0001\nG0 X0 Y0 Z10.\n" + call + "\nM30\nO0100\nG0 X5.\n/M30\nM99\nN9 M30\n%\n");

        Assert.Contains(
            Lines(written, "PROGRAM=END", "SUB=BEGIN NAME=100", "RAPID X=5", "SKIP JUMP=END", "RETURN", "JUMP=END",
                "SUB=END", "FILE=END"),
            text, StringComparison.Ordinal);
        AssertFormatsToItself(text);
        Diagnostics check = Check(text, Mill());
        Assert.DoesNotContain(check.Items, diagnostic => diagnostic.Code == Core.Model.DiagnosticCodes.CallOfProgram);
        Assert.False(check.HasErrors, check.ToText());
    }

    // Nakamura M200 P{times}{nnnn} calls a program as M98 does (controller-mapping 6, CALL and TIMES; 8): the O
    // section it enters is a SUB, also when it holds an M30.
    [Fact]
    public void SectionKind_OSectionHoldingM30EnteredByNakamuraM200_IsASub()
    {
        string text = Text("%\nO0001\nM200 P20100\nM30\nO0100\nG0 Z5.\n/M30\nM99\n%\n", Lathe());

        Assert.Contains(
            Lines("CALL=100 TIMES=2", "PROGRAM=END", "SUB=BEGIN NAME=100", "RAPID Z=5", "SKIP JUMP=END", "SUB=END"),
            text, StringComparison.Ordinal);
        Diagnostics check = Check(text, Lathe());
        Assert.DoesNotContain(check.Items, diagnostic => diagnostic.Code == Core.Model.DiagnosticCodes.CallOfProgram);
        Assert.False(check.HasErrors, check.ToText());
    }

    // G65 P calls the program of its digits, P90001 the O90001 of a 30i with eight-digit program numbers; the older
    // form with the count in front of the program number is M98's (controllers fanuc.md 1 and 7; controller-mapping 6).
    // The O section it enters is the SUB, and O0001 stays the program.
    [Fact]
    public void SectionKind_G65WithFiveDigits_EntersTheOSectionOfThoseDigits()
    {
        string text = Text("%\nO0001\nG65 P90001 A1.\nM30\nO90001\nG0 X5.\n/M30\nM99\n%\n");

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN NUMBER=1",
                "FEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF",
                "CALL=90001 ARG:V1=1",
                "PROGRAM=END",
                "SUB=BEGIN NAME=90001",
                "RAPID X=5",
                "SKIP JUMP=END",
                "SUB=END",
                "FILE=END"),
            text);
        Assert.False(Check(text, Mill()).HasErrors, Check(text, Mill()).ToText());
    }

    // M198 P calls the program of its digits from external memory on a machine that names M198, P90001 the O90001;
    // the older form is M98's (controllers fanuc.md 1; controller-mapping 6; D322).
    [Fact]
    public void SectionKind_M198WithFiveDigits_CallsTheExternalProgramOfThoseDigits()
    {
        Assert.Equal(Lines("CALL=\"O90001\""),
            Body("M198 P90001", MillWith("[format]\nexternal_call = \"M198\"")));
    }

    // D322: the code of an external call is the machine's [format] external_call, which the control takes from
    // parameter 6030; on a machine that names M200 the reader reads M200 P as the call.
    [Fact]
    public void ExternalCall_CodeOfTheMachine_IsReadAsTheCall()
    {
        Assert.Equal(Lines("CALL=\"O9010\""),
            Body("M200 P9010", MillWith("[format]\nexternal_call = \"M200\"")));
    }

    // D322: the code sits in parameter 6030 and M198 is only its factory setting, so a machine that names no
    // [format] external_call has no external call at all: M198 P is no CALL, and the block keeps its words as RAW.
    [Fact]
    public void ExternalCall_WithoutTheKeyOfTheMachine_IsNoCall()
    {
        string body = Body("M198 P9010");

        Assert.DoesNotContain("CALL", body, StringComparison.Ordinal);
        Assert.Contains("RAW:FANUC=\"M198 P9010\"", body, StringComparison.Ordinal);
    }

    // D322: the reader never reads M198 as a call on a machine whose wait range covers 198; on the Nakamura lathe
    // M198 is the rendezvous of the two turrets, M100 to M199 of [sync] mark_range.
    [Fact]
    public void ExternalCall_CodeInsideTheWaitRangeOfTheMachine_IsTheSyncMark()
    {
        Assert.Equal(Lines("SYNC=198"), Body("M198", Lathe()));
        Assert.Equal(Lines("SYNC=198"),
            Body("M198", LatheWith("[format]\nexternal_call = \"M198\"")));
    }
}
