using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The frame of a Klartext program (controllers heidenhain.md 1; 8 rule 1; controller-mapping 1; language 4.1, 4.13):
/// BEGIN PGM name MM from the program name and the units, M30 per program_end for PROGRAM=END, END PGM name MM after
/// it and after the subprograms placed behind it, the sub_end LBL 0 for SUB=END, the comments as ; and * - blocks, and
/// RAW of the controller as the text it keeps. The block numbers are consecutive from 0, [format] block_numbers.
/// </summary>
internal static partial class HeidenhainProgramFrame
{
    // ; starts a comment block, * - a structuring block (controllers heidenhain.md 1; controller-mapping 1, COMMENT and
    // SECTION).
    private const string CommentMark = "; ";
    private const string SectionMark = "* - ";

    // The longest program name of the iTNC 530 (D266).
    private const int MaxNameLength = 25;

    /// <summary>
    /// A comment block of Klartext: ; text.
    /// </summary>
    public static string CommentLine(string text)
    {
        return CommentMark + text;
    }

    /// <summary>
    /// Writes BEGIN PGM name MM from the name of the program and its units (heidenhain 8 rule 1); the file is named
    /// after the program (controllers heidenhain.md 1).
    /// </summary>
    public static void WriteBegin(HeidenhainBlock writing, Section program, ChannelSnapshot state)
    {
        if (UnitsOf(state) is not string units)
        {
            writing.Error(DiagnosticCodes.HeidenhainProgramWithoutUnits,
                "BEGIN PGM needs the units of the program, MM or INCH, and the program sets no UNITS (controllers "
                + "heidenhain.md 8 rule 1).");
            return;
        }

        string name = NameOf(writing, program);
        CheckName(writing, name);
        writing.Line("BEGIN PGM " + name + " " + units);
    }

    /// <summary>
    /// Writes END PGM name MM, after the M30 and the subprograms placed behind it (heidenhain 8 rules 1 and 6), with
    /// the units of its BEGIN PGM: BEGIN PGM name MM opens and END PGM name MM closes (controllers heidenhain.md 1),
    /// and a UNITS that changes them is reported where it stands (TakeUnits).
    /// </summary>
    public static void WriteEndOfProgram(HeidenhainBlock writing, Section program)
    {
        if (UnitsOf(writing.LookAhead.HeaderState(program)) is string units)
        {
            writing.Line("END PGM " + NameOf(writing, program) + " " + units);
        }
    }

    /// <summary>
    /// Writes the structure and the comments of a block: FILE=BEGIN and FILE=END are the start and the end of the file
    /// and write nothing, PROGRAM=BEGIN is BEGIN PGM (WriteBegin), SUB=BEGIN is LBL n, SECTION and COMMENT are the
    /// comment blocks, RAW the text the reader kept.
    /// </summary>
    public static void WriteStart(HeidenhainBlock writing)
    {
        writing.Take("SKIP");
        writing.Take("FILE");
        writing.Take("NCX");
        if (writing.Block.Has("PROGRAM", null, "BEGIN"))
        {
            // Klartext has one program per file, which BEGIN PGM names, no program number and one channel
            // (controller-mapping 1, PROGRAM=BEGIN; 7, CHANNEL).
            writing.Take("PROGRAM");
            writing.Take("NAME");
            writing.Take("NUMBER");
            writing.Take("CHANNEL");
        }
        else if (writing.Block.Has("SUB", null, "BEGIN"))
        {
            HeidenhainSubprograms.WriteBegin(writing);
        }

        TakeUnits(writing);
        WriteComments(writing);
        WriteRaw(writing);
    }

    /// <summary>
    /// Writes the end of a section: PROGRAM=END as program_end, M30, with the LBL of JUMP=END before it
    /// (controller-mapping 1, JUMP=END; D49); SUB=END as sub_end, LBL 0 (machine-config 2; heidenhain 8 rule 6).
    /// </summary>
    public static void WriteEnd(HeidenhainBlock writing)
    {
        if (writing.Block.Has("PROGRAM", null, "END"))
        {
            writing.Take("PROGRAM");
            if (writing.Step.Section is Section program && writing.LabelNumbers.EndOf(program) is string end)
            {
                writing.Line("LBL " + end);
            }

            writing.Template(writing.Machine.Format?.ProgramEnd, "[format] program_end (machine-config 2, D49)",
                new TemplateValues());
        }
        else if (writing.Block.Has("SUB", null, "END"))
        {
            writing.Take("SUB");

            // D272: the LBL m that a RETURN before the end jumps to stands directly before the one LBL 0.
            if (writing.Step.Section is Section sub && writing.LabelNumbers.ReturnOf(sub) is string back)
            {
                writing.Line("LBL " + back);
            }

            WriteSubEnd(writing);
        }
    }

    /// <summary>
    /// Writes sub_end, LBL 0 on Heidenhain, once per section at its SUB=END; a RETURN before the end jumps to the LBL
    /// before it (machine-config 2; D272).
    /// </summary>
    public static void WriteSubEnd(HeidenhainBlock writing)
    {
        writing.Template(writing.Machine.Format?.SubEnd, "[format] sub_end (machine-config 2)", new TemplateValues());
    }

    // BEGIN PGM gives the units of the whole program (controllers heidenhain.md 1; controller-mapping 1, UNITS), the
    // LBL sections of its subprograms included, which stand between its M30 and its END PGM (heidenhain 8 rules 1, 6):
    // a UNITS that keeps the units of the BEGIN PGM of the file being written writes nothing, one that changes them has
    // no Klartext form, in the program and in the walk of a subprogram it calls alike.
    private static void TakeUnits(HeidenhainBlock writing)
    {
        if (writing.Take("UNITS") is not Word units || ProgramOf(writing) is not Section program)
        {
            return;
        }

        if (UnitsOf(writing.After) != UnitsOf(writing.LookAhead.HeaderState(program)))
        {
            writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
                $"{units.ToCanonical()} changes the units that BEGIN PGM gives the program {NameOf(writing, program)}, "
                + "which Klartext keeps for the whole program and the LBL sections in its file (controllers "
                + "heidenhain.md 1; 8 rules 1 and 6).");
        }
    }

    // The program whose file the block is written into (program_layout = "file_per_program", D48): its own program,
    // or, in the walk of a subprogram, the program whose walk entered it, the last PROGRAM=BEGIN before it in walk
    // order (architecture 8, D99). The walk of a subprogram that no program calls follows the PROGRAM=END of the last
    // program: under file_per_program it stands in the first program's file (D264, CompilerBase.FilePerProgram), under
    // one_file it belongs to none, and its UNITS has no BEGIN PGM to be compared with.
    internal static Section? ProgramOf(HeidenhainBlock writing)
    {
        if (writing.Step.Section is Section { Kind: SectionKind.Program } own)
        {
            return own;
        }

        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        for (int index = writing.Step.Index - 1; index >= 0; index--)
        {
            Block block = steps[index].Block;
            if (block.Has("PROGRAM", null, "END"))
            {
                return writing.Machine.Format?.ProgramLayout == ProgramLayout.FilePerProgram
                    ? FirstProgram(steps) : null;
            }

            if (block.Has("PROGRAM", null, "BEGIN"))
            {
                return steps[index].Section;
            }
        }

        return null;
    }

    // The first program of the file, whose file holds the subprograms no program calls (D264).
    private static Section? FirstProgram(IReadOnlyList<BlockStep> steps)
    {
        foreach (BlockStep step in steps)
        {
            if (step.Block.Has("PROGRAM", null, "BEGIN"))
            {
                return step.Section;
            }
        }

        return null;
    }

    // SECTION as the structuring block * - text, COMMENT as the comment block ; text, both in the charset of the
    // machine (controller-mapping 1, COMMENT and SECTION; machine-config 2). MESSAGE has no Klartext form of its own,
    // so its text is written as a comment, as SECTION is on the controllers without * - (language 4.1; D326).
    private static void WriteComments(HeidenhainBlock writing)
    {
        if (writing.Take("SECTION")?.Value is StringValue section)
        {
            writing.Line(SectionMark + writing.CommentText(section.Content));
        }

        if (writing.Take("COMMENT")?.Value is StringValue comment)
        {
            writing.Line(CommentMark + writing.CommentText(comment.Content));
        }

        if (writing.Take("MESSAGE")?.Value is StringValue message)
        {
            writing.Line(CommentMark + writing.CommentText(message.Content));
        }
    }

    // RAW of the controller is written as the reader kept it (language 4.1, D5), without the block number of the
    // source: the compiler numbers the blocks consecutively itself (heidenhain 8 rule 1; heidenhain 1: the numbers are
    // part of the text, and LBL, not block numbers, are the jump targets). RAW of another controller stopped the
    // compile before (CMP001).
    private static void WriteRaw(HeidenhainBlock writing)
    {
        foreach (Word raw in writing.TakeAll("RAW"))
        {
            if (raw.Value is StringValue text)
            {
                writing.Line(SourceBlockNumber().Replace(text.Content, ""));
            }
        }
    }

    private static string? UnitsOf(ChannelSnapshot state)
    {
        return state.Frame.Units switch
        {
            Units.Mm => "MM",
            Units.Inch => "INCH",
            _ => null,
        };
    }

    // The name of the program, the final name the framework gives its file (D266).
    private static string NameOf(HeidenhainBlock writing, Section program)
    {
        return writing.ProgramName(program);
    }

    // D266: a WARNING when the name breaks the control's limits: a character outside A-Z a-z 0-9 _ -, or more than 25
    // characters on the iTNC 530 (decisions.md D266).
    private static void CheckName(HeidenhainBlock writing, string name)
    {
        bool characters = name.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
        bool itnc530 = string.Equals(writing.Machine.Machine.Dialect, "iTNC530", StringComparison.OrdinalIgnoreCase);
        bool length = !itnc530 || name.Length <= MaxNameLength;
        if (characters && length)
        {
            return;
        }

        writing.Warning(DiagnosticCodes.HeidenhainProgramNameLimits,
            $"The program name \"{name}\" breaks the limits of the control: "
            + (characters ? "" : "it has characters outside A-Z a-z 0-9 _ -")
            + (characters || length ? "" : ", and ")
            + (length ? "" : $"it is longer than the {MaxNameLength} characters of the iTNC 530")
            + " (controllers heidenhain.md 1; D266).");
    }

    // The block number in front of a Klartext block, 2 of "2 BLK FORM 0.1 Z X0 Y0 Z-20".
    [GeneratedRegex(@"^\s*[0-9]+(?=[\s/])\s*", RegexOptions.CultureInvariant)]
    private static partial Regex SourceBlockNumber();

    /// <summary>
    /// STOCK through [stock], BLK FORM 0.1 Z X+0 Y+0 Z-40 and BLK FORM 0.2 with their signs (D351; machine-config 5;
    /// iTNC 530 Klartext 340490 p.62-63, 114-115): the corners and DIST with their sign, R and L without, as the
    /// cylinder blank of the TNC 640 writes them (Klartext 340590-05 p.103). The framework has chosen the template and
    /// reported what writes nothing (CMP012 to CMP014).
    /// </summary>
    public static void WriteStock(HeidenhainBlock writing)
    {
        if (writing.Block.Verb?.Key != "STOCK")
        {
            return;
        }

        writing.TakeAll("STOCK");
        writing.TakeAll("X");
        writing.TakeAll("Y");
        writing.TakeAll("Z");
        writing.TakeAll("MAX");
        if (writing.Stock is not StockForm stock)
        {
            return;
        }

        var values = new TemplateValues();
        values.Set("axis", stock.Axis);
        foreach (StockNumber number in stock.Numbers)
        {
            bool unsigned = number.Name is "radius" or "diameter" or "length";
            values.Set(number.Name, unsigned
                ? writing.Numbers.Format(number.Address, number.Value, writing.Block)
                : HeidenhainNumbers.Signed(writing, number.Address, number.Value));
        }

        writing.Template(stock.Template, stock.What, values);
    }
}
