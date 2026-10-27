using System.Globalization;

namespace ThroughCoolantSourceRule.Tests;

/// <summary>
/// The reader rule of D66: source blocks in, the NCX blocks the rule writes for them out
/// (architecture 9; machine-config 5a).
/// </summary>
public sealed class ThroughCoolantSourceRuleTests
{
    // D66: the three blocks a clutch machine writes for COOLANT:THROUGH=ON are the one word
    // again, on the line of the first of them.
    [Fact]
    public void Fold_M5M51M3S1500_IsOneThroughCoolantWord()
    {
        var builder = new NcxBuilder(new Diagnostics("part.nc"));

        int folded = ThroughCoolantSourceRule.Fold(Source(10, "M5"), [Source(11, "M51"), Source(12, "M3 S1500")],
            builder);

        Assert.Equal(3, folded);
        Assert.Equal(["10: COOLANT:THROUGH=ON"], Written(builder));
    }

    // The block numbers of the source are no part of what the blocks do.
    [Fact]
    public void Fold_BlocksWithBlockNumbers_AreFoldedAsWell()
    {
        var builder = new NcxBuilder(new Diagnostics("part.nc"));

        int folded = ThroughCoolantSourceRule.Fold(Source(10, "N10 M5"),
            [Source(11, "N20 M51"), Source(12, "N30 M3 S1500")], builder);

        Assert.Equal(3, folded);
        Assert.Equal(["10: COOLANT:THROUGH=ON"], Written(builder));
    }

    // A stop that another code follows is a stop: nothing is folded and nothing written.
    [Fact]
    public void Fold_StopFollowedByAnotherCode_FoldsNothing()
    {
        var builder = new NcxBuilder(new Diagnostics("part.nc"));

        int folded = ThroughCoolantSourceRule.Fold(Source(10, "M5"), [Source(11, "M8"), Source(12, "M3 S1500")],
            builder);

        Assert.Equal(0, folded);
        Assert.Empty(Written(builder));
    }

    // A stop that comes with a move is no part of the sequence, since folding it would lose
    // the move.
    [Fact]
    public void Fold_StopWithAMove_FoldsNothing()
    {
        var builder = new NcxBuilder(new Diagnostics("part.nc"));

        int folded = ThroughCoolantSourceRule.Fold(Source(10, "G0 Z50 M5"),
            [Source(11, "M51"), Source(12, "M3 S1500")], builder);

        Assert.Equal(0, folded);
        Assert.Empty(Written(builder));
    }

    // D232: Read folds the block it is offered with the blocks the reader shows it after it,
    // and claims the three.
    [Fact]
    public void Read_M5WithM51AndM3Following_ClaimsTheThreeBlocks()
    {
        var builder = new NcxBuilder(new Diagnostics("part.nc"));
        var state = new SourceState(null, new Dictionary<string, int>());
        SourceBlock stop = Source(10, "M5") with { Following = [Source(11, "M51"), Source(12, "M3 S1500")] };

        int claimed = new ThroughCoolantSourceRule().Read(stop, state, builder);

        Assert.Equal(3, claimed);
        Assert.Equal(["10: COOLANT:THROUGH=ON"], Written(builder));
    }

    // D231, D232: the Fanuc reader offers the rule the M5 its spindle table names, with the
    // two blocks after it, and the rule's word stands in place of the three.
    [Fact]
    public void Read_ByTheFanucReader_FoldsTheBlocksTheTablesName()
    {
        var machineDiagnostics = new Diagnostics("clutch.toml");
        Ncx.Core.Machine.MachineConfig machine = Ncx.Config.MachineConfigLoader.LoadText(ClutchMill,
            machineDiagnostics)!;
        Assert.True(machineDiagnostics.Items.Count == 0, machineDiagnostics.ToText());
        var options = new ReadOptions { Rules = [new ThroughCoolantSourceRule()] };

        NcxProgram program = new Ncx.Readers.Fanuc.FanucReader().Read(
            new SourceFile("O0001.nc", "%\nO0001\nG90 G21 G17 G94\nM3 S1500\nM5\nM51\nM3 S1500\nM30\n%\n"),
            machine, options);

        string text = NcxWriter.Write(program);
        Assert.EndsWith("\nSPINDLE=CW RPM=1500\nCOOLANT:THROUGH=ON\nPROGRAM=END\nFILE=END\n", text,
            StringComparison.Ordinal);
    }

    // A mill whose spindle and coolant tables name M3, M5 and M51 (machine-config 5).
    private const string ClutchMill = """
        [machine]
        name = "Clutch mill"
        controller = "fanuc"
        channels = [1]
        units_default = "MM"
        default_spindle = "S1"
        default_holder = "H1"

        [roles]
        MAIN = "S1"

        [[resource]]
        id = "S1"
        type = "tool_spindle"

        [[resource]]
        id = "H1"
        type = "tool_holder"
        spindle = "S1"

        [[axis]]
        id = "Z1"
        ncx = "Z"
        letter = "Z"
        kind = "linear"

        [spindle.MAIN]
        CW = "M3"
        CCW = "M4"
        OFF = "M5"

        [coolant]
        STANDARD = { ON = "M8", OFF = "M9" }
        THROUGH = { ON = "M51", OFF = "M9" }
        """;

    // A source block as a tokenizer reads it: "M3 S1500" is the words M3 and S1500.
    private static SourceBlock Source(int line, string text)
    {
        var words = new List<SourceWord>();
        foreach (string written in text.Split(' '))
        {
            string digits = written.Substring(1);
            words.Add(new SourceWord
            {
                Address = written.Substring(0, 1),
                Text = digits,
                Number = decimal.Parse(digits, CultureInfo.InvariantCulture),
            });
        }

        return new SourceBlock { Line = line, Text = text, Words = words };
    }

    // The NCX blocks the rule wrote, each as its line and its canonical text.
    private static List<string> Written(NcxBuilder builder)
    {
        var written = new List<string>();
        foreach (Block block in builder.Build().Blocks)
        {
            written.Add(block.Line.ToString(CultureInfo.InvariantCulture) + ": " + NcxWriter.WriteBlock(block));
        }

        return written;
    }
}
