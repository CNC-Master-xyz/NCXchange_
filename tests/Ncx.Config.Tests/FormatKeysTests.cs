using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// The [format] keys of the 2026-10 decisions and the values a file that leaves them out takes from its family:
/// comment_charset (D263), label_max (D271), motion_code (D299), m_codes_per_block and m_codes_combinable (D339),
/// helix_feed (D354).
/// </summary>
public sealed class FormatKeysTests
{
    // D271, D299, D339, D354, D263: every key read as written; the combinable codes are normalized (D105).
    [Fact]
    public void FormatKeys_Written_LoadIntoTheFormat()
    {
        OutputFormat format = LoadFormat("fanuc", """
            comment_charset = "UTF-8"
            label_max = 254
            motion_code = "change"
            m_codes_per_block = 3
            m_codes_combinable = ["M010", "M11"]
            helix_feed = "path"
            """);

        Assert.Equal("UTF-8", format.CommentCharset);
        Assert.Equal(254, format.LabelMax);
        Assert.Equal(MotionCode.Change, format.MotionCode);
        Assert.Equal(3, format.MCodesPerBlock);
        Assert.Equal(["M10", "M11"], format.MCodesCombinable);
        Assert.Equal(HelixFeed.Path, format.HelixFeed);
    }

    // D263: "comment_charset ... takes "ASCII" or "UTF-8"; any other value is a CFG ERROR" (CFG005, D388 15).
    [Fact]
    public void CommentCharset_OtherValue_IsValueNotAllowed()
    {
        Diagnostic error = SingleError("fanuc", "comment_charset = \"LATIN1\"");

        Assert.Equal(DiagnosticCodes.ValueNotAllowed, error.Code);
        Assert.Contains("LATIN1", error.Message, StringComparison.Ordinal);
    }

    // D339: m_codes_per_block is 1 to 5; D271: label_max is a label number, at least 1.
    [Theory]
    [InlineData("m_codes_per_block = 6")]
    [InlineData("m_codes_per_block = 0")]
    [InlineData("label_max = 0")]
    [InlineData("motion_code = \"always\"")]
    [InlineData("helix_feed = \"tangent\"")]
    public void FormatKeys_OutOfRange_IsValueNotAllowed(string line)
    {
        Diagnostic error = SingleError("fanuc", line);

        Assert.Equal(DiagnosticCodes.ValueNotAllowed, error.Code);
    }

    // D263: "Heidenhain numbers blocks from 0 in steps of 1. Siemens uses line_ending LF and max_line_length 512.
    // comment_charset defaults to "ASCII" on every family"; D299: Fanuc "arcs", Siemens "every_block"; D354: Fanuc
    // "arc", Heidenhain and Siemens "path"; D271: 999; D339: 1.
    [Theory]
    [InlineData(Controller.Fanuc, false, 10, 10, 0, MotionCode.Arcs, HelixFeed.Arc)]
    [InlineData(Controller.Heidenhain, true, 0, 1, 0, MotionCode.EveryBlock, HelixFeed.Path)]
    [InlineData(Controller.Siemens, false, 10, 10, 512, MotionCode.EveryBlock, HelixFeed.Path)]
    public void WithFamilyDefaults_KeysLeftOut_TakeTheFamilyValue(
        Controller controller, bool numbered, int start, int step, int longest, MotionCode motion, HelixFeed helix)
    {
        OutputFormat format = new OutputFormat().WithFamilyDefaults(controller);

        Assert.Equal(LineEnding.Lf, format.LineEnding);
        Assert.Equal(numbered, format.BlockNumbers?.Enabled);
        Assert.Equal(start, format.BlockNumbers?.Start);
        Assert.Equal(step, format.BlockNumbers?.Step);
        Assert.Equal(longest, format.MaxLineLength);
        Assert.Equal("ASCII", format.CommentCharset);
        Assert.Equal(999, format.LabelMax);
        Assert.Equal(motion, format.MotionCode);
        Assert.Equal(1, format.MCodesPerBlock);
        Assert.Equal(helix, format.HelixFeed);
    }

    // D263: "A key left out takes its family value" — and a key written keeps its value; block numbers enabled
    // without start or step count from 10 in steps of 10 off Heidenhain.
    [Fact]
    public void WithFamilyDefaults_KeysWritten_KeepTheirValue()
    {
        OutputFormat written = new OutputFormat
        {
            LineEnding = LineEnding.CrLf,
            BlockNumbers = new BlockNumbering { Enabled = true },
            MaxLineLength = 80,
            CommentCharset = "UTF-8",
            MotionCode = MotionCode.Change,
            HelixFeed = HelixFeed.Path,
        };

        OutputFormat format = written.WithFamilyDefaults(Controller.Fanuc);

        Assert.Equal(LineEnding.CrLf, format.LineEnding);
        Assert.Equal(10, format.BlockNumbers?.Start);
        Assert.Equal(10, format.BlockNumbers?.Step);
        Assert.Equal(80, format.MaxLineLength);
        Assert.Equal("UTF-8", format.CommentCharset);
        Assert.Equal(MotionCode.Change, format.MotionCode);
        Assert.Equal(HelixFeed.Path, format.HelixFeed);
    }

    // Loads a clean machine of the controller whose [format] holds the lines.
    private static OutputFormat LoadFormat(string controller, string lines)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(Machine(controller, lines), diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(machine?.Format);
        return machine.Format;
    }

    // Loads a machine with one ERROR in [format] and returns it.
    private static Diagnostic SingleError(string controller, string lines)
    {
        var diagnostics = new Diagnostics("test.toml");
        Assert.Null(MachineConfigLoader.LoadText(Machine(controller, lines), diagnostics));
        return Assert.Single(diagnostics.Items);
    }

    private static string Machine(string controller, string lines)
    {
        return "[machine]\nname = \"Test\"\ncontroller = \"" + controller + "\"\n\n[format]\n" + lines
            + "\n\n[coolant]\nSTANDARD = { ON = \"M8\", OFF = \"M9\" }\n";
    }
}
