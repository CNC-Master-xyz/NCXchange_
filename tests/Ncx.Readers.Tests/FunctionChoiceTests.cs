using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Tests;

/// <summary>
/// The reader chooses among the states that write one native code with its source state: it drops the states the
/// source rules out, writes every remaining state that does not already hold, and falls back to the first state in
/// file order (machine-config 5, architecture 7, D155).
/// </summary>
public sealed class FunctionChoiceTests
{
    private const string Source = "PART.NC";

    // D155: M9 on the Nakamura is the OFF of every coolant channel; the reader writes COOLANT=OFF for each channel that
    // was on and drops the OFF of a channel that is not on.
    [Fact]
    public void Choose_OffOfSeveralChannels_WritesTheOffOfEachChannelThatWasOn()
    {
        FunctionMatch[] matches =
        [
            Match("COOLANT:STANDARD", "OFF"), Match("COOLANT:LOW_PRESSURE", "OFF"), Match("COOLANT:THROUGH", "OFF"),
        ];
        var context = new FunctionContext
        {
            Holding = new Dictionary<string, string> { ["COOLANT:STANDARD"] = "ON", ["COOLANT:THROUGH"] = "ON" },
        };

        IReadOnlyList<FunctionMatch> chosen = Choose(matches, context, out Diagnostics diagnostics);

        Assert.Equal(["COOLANT:STANDARD=OFF", "COOLANT:THROUGH=OFF"], Keys(chosen));
        Assert.Empty(diagnostics.Items);
    }

    // D155: a table bound to another channel is ruled out (D56).
    [Fact]
    public void Choose_TableOfAnotherChannel_IsRuledOut()
    {
        FunctionMatch[] matches = [Match("FUNC:DOOR", "OPEN", channel: 1), Match("FUNC:SUB_CHUCK", "OPEN", channel: 2)];

        IReadOnlyList<FunctionMatch> chosen = Choose(matches, new FunctionContext { Channel = 2 }, out _);

        Assert.Equal(["FUNC:SUB_CHUCK=OPEN"], Keys(chosen));
    }

    // D155: a spindle the source does not address is ruled out, the work spindle of the side [workpiece] did not
    // select (Doosan P11 after M134).
    [Fact]
    public void Choose_SpindleTheSourceDoesNotAddress_IsRuledOut()
    {
        FunctionMatch[] matches = [Match("SPINDLE:MAIN", "CW"), Match("SPINDLE:SUB", "CW")];

        IReadOnlyList<FunctionMatch> chosen =
            Choose(matches, new FunctionContext { AddressedSpindles = ["SUB"] }, out Diagnostics diagnostics);

        Assert.Equal(["SPINDLE:SUB=CW"], Keys(chosen));
        Assert.Empty(diagnostics.Items);
    }

    // D155: a state that already holds is not written again; M5 on millturn1 adds SPINDLE_MODE:MAIN=SPINDLE only when
    // the spindle was in AXIS mode.
    [Theory]
    [InlineData("AXIS", 2)]
    [InlineData("SPINDLE", 1)]
    public void Choose_StateThatHolds_IsNotWrittenAgain(string mode, int count)
    {
        FunctionMatch[] matches = [Match("SPINDLE:MAIN", "OFF"), Match("SPINDLE_MODE:MAIN", "SPINDLE")];
        var context = new FunctionContext
        {
            Holding = new Dictionary<string, string> { ["SPINDLE:MAIN"] = "CW", ["SPINDLE_MODE:MAIN"] = mode },
        };

        IReadOnlyList<FunctionMatch> chosen = Choose(matches, context, out _);

        Assert.Equal(count, chosen.Count);
        Assert.Equal("SPINDLE:MAIN=OFF", chosen[0].Key);
    }

    // D155: when nothing remains, the reader writes the first state in file order.
    [Fact]
    public void Choose_NothingRemains_IsTheFirstInFileOrder()
    {
        FunctionMatch[] matches = [Match("COOLANT:STANDARD", "OFF"), Match("COOLANT:LOW_PRESSURE", "OFF")];
        var context = new FunctionContext { Holding = new Dictionary<string, string>() };

        IReadOnlyList<FunctionMatch> chosen = Choose(matches, context, out Diagnostics diagnostics);

        Assert.Equal(["COOLANT:STANDARD=OFF"], Keys(chosen));
        Assert.Empty(diagnostics.Items);
    }

    // D155: two resources of one word would be switched on, M3 of MAIN and TOOL on the Mori Seiki: the first state in
    // file order, with the WARNING RDR020 that names the others.
    [Fact]
    public void Choose_TwoResourcesSwitchedOn_IsTheFirstWithAWarningNamingTheOther()
    {
        FunctionMatch[] matches = [Match("SPINDLE:MAIN", "CW"), Match("SPINDLE:TOOL", "CW")];

        IReadOnlyList<FunctionMatch> chosen = Choose(matches, new FunctionContext(), out Diagnostics diagnostics);

        Assert.Equal(["SPINDLE:MAIN=CW"], Keys(chosen));
        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(DiagnosticCodes.FunctionCodeOfSeveralResources, warning.Code);
        Assert.Equal(12, warning.Line);
        Assert.Contains("SPINDLE:TOOL=CW", warning.Message, StringComparison.Ordinal);
    }

    // D155: the [workpiece] selection comes first and is written with the state of its spindle mode, the Doosan M34
    // that gives WORKPIECE=MAIN and SPINDLE_MODE:MAIN=SPINDLE when the C axis was on.
    [Fact]
    public void Choose_WorkpieceSelectionAndSpindleMode_WritesBoth()
    {
        FunctionMatch workpiece = new() { Word = "WORKPIECE", State = "MAIN", Values = new TemplateValues() };
        FunctionMatch[] matches = [workpiece, Match("SPINDLE_MODE:MAIN", "SPINDLE")];
        var context = new FunctionContext
        {
            Holding = new Dictionary<string, string> { ["SPINDLE_MODE:MAIN"] = "AXIS" },
        };

        IReadOnlyList<FunctionMatch> chosen = Choose(matches, context, out Diagnostics diagnostics);

        Assert.Equal(["WORKPIECE=MAIN", "SPINDLE_MODE:MAIN=SPINDLE"], Keys(chosen));
        Assert.Empty(diagnostics.Items);
    }

    // No state writes the code: nothing is chosen.
    [Fact]
    public void Choose_NoMatch_IsEmpty()
    {
        Assert.Empty(Choose([], new FunctionContext(), out _));
    }

    private static IReadOnlyList<FunctionMatch> Choose(
        IReadOnlyList<FunctionMatch> matches, FunctionContext context, out Diagnostics diagnostics)
    {
        diagnostics = new Diagnostics(Source);
        return FunctionChoice.Choose(matches, context, 12, "M3", diagnostics);
    }

    // A state of a function table, bound to a channel when one is given.
    private static FunctionMatch Match(string word, string state, int? channel = null)
    {
        return new FunctionMatch
        {
            Word = word,
            State = state,
            Table = new FunctionTable { Channel = channel },
            Values = new TemplateValues(),
        };
    }

    private static List<string> Keys(IReadOnlyList<FunctionMatch> matches)
    {
        var keys = new List<string>();
        foreach (FunctionMatch match in matches)
        {
            keys.Add(match.Key);
        }

        return keys;
    }
}
