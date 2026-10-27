using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Model;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The units of a SINUMERIK file (controllers siemens.md 1, 8, 12 rule 1; controller-mapping 1, 6; machine-config 2;
/// language 4.1, 4.13): %_N_NAME_MPF in front of every program and %_N_NAME_SPF in front of every subprogram, which
/// begins with PROC NAME and its parameters; EXTERN for the subprograms with parameters a unit calls and DEF REAL
/// for the names a unit uses (D261); M30 per
/// program_end at the end of a program, behind the label a JUMP=END goes to; RET or M17 per sub_end at the end of a
/// subprogram; COMMENT and SECTION as comments with a semicolon.
/// </summary>
internal static partial class SiemensProgramFrame
{
    // The words of a STOCK block (D351).
    private static readonly string[] s_stockKeys = ["STOCK", "X", "Y", "Z", "MAX"];

    /// <summary>
    /// The suffix of a main program unit (controllers siemens.md 1).
    /// </summary>
    public const string ProgramSuffix = "_MPF";

    /// <summary>
    /// The suffix of a subprogram unit (controllers siemens.md 1).
    /// </summary>
    public const string SubSuffix = "_SPF";

    /// <summary>
    /// The start of a unit header (controllers siemens.md 1).
    /// </summary>
    public const string HeaderPrefix = "%_N_";

    /// <summary>
    /// Writes FILE=BEGIN and FILE=END, which stand for the start and the end of the file and write nothing, and the
    /// head of a program or subprogram (language 4.1; controller-mapping 1, FILE=BEGIN, PROGRAM=BEGIN, SUB=BEGIN).
    /// </summary>
    /// <returns>True when the block was one of them.</returns>
    public static bool WriteBegin(SiemensBlock write, string fileStem)
    {
        Block block = write.Block;
        if (block.Has("FILE"))
        {
            write.Written("FILE");
            write.Written("NCX");
            return true;
        }

        if (write.Step.Section is not Section section)
        {
            return false;
        }

        if (block.Has("PROGRAM", null, "BEGIN"))
        {
            // %_N_NAME_MPF in front of the program; the channel it runs on is the job's (controller-mapping 7,
            // CHANNEL).
            write.Written("PROGRAM");
            write.Written("NAME");
            write.Written("NUMBER");
            write.Written("CHANNEL");
            string name = write.File.UnitNameOf(section, fileStem, write);
            write.Write(HeaderPrefix + name + ProgramSuffix);
            SiemensFlow.WriteExterns(write, section, fileStem);
            SiemensFlow.WriteDeclarations(write, section);
            SiemensCycles.BeginUnit(write);
            SiemensSpindles.BeginProgram(write);
            return true;
        }

        if (block.Has("SUB", null, "BEGIN"))
        {
            // %_N_NAME_SPF, then PROC NAME at the top of every subprogram (controllers siemens.md 12 rule 1); the
            // subprogram starts without a modal call and with the frame that a RAW line of the file may change
            // unknown (SiemensCycles.BeginUnit, SiemensFrames.ForgetRawFrames; D99).
            write.Written("SUB");
            write.Written("NAME");
            string name = write.File.UnitNameOf(section, fileStem, write);
            write.Write(HeaderPrefix + name + SubSuffix);
            write.Write(SiemensFlow.ProcLine(write, section, name));
            SiemensFlow.WriteExterns(write, section, fileStem);
            SiemensFlow.WriteDeclarations(write, section);
            SiemensCycles.BeginUnit(write);
            SiemensFrames.ForgetRawFrames(write);
            return true;
        }

        return false;
    }

    /// <summary>
    /// PROGRAM=END as program_end, M30, behind the label that a JUMP=END of the program goes to; SUB=END as sub_end,
    /// RET or M17 (controllers siemens.md 12 rule 1; controller-mapping 1, PROGRAM=END and JUMP=END; machine-config 2;
    /// D48, D49).
    /// </summary>
    public static void WriteEnd(SiemensBlock write)
    {
        Block block = write.Block;
        if (block.Has("PROGRAM", null, "END") && write.Step.Section is Section program)
        {
            write.Written("PROGRAM");
            if (write.File.JumpsToEnd(program))
            {
                write.Write(write.File.EndLabelOf(program) + ":");
            }

            if (write.Render(write.Machine.Format?.ProgramEnd, "[format] program_end (machine-config 2, D49)",
                new TemplateValues()) is string end)
            {
                write.Write(end);
            }

            return;
        }

        if (block.Has("SUB", null, "END"))
        {
            write.Written("SUB");
            if (SubEnd(write, write.Step.Section) is string end)
            {
                write.Write(end);
            }
        }
    }

    /// <summary>
    /// The end of a subprogram, for SUB=END and RETURN: sub_end of the machine (machine-config 2). In a subprogram
    /// whose PROC line declares SAVE, a RET of sub_end is M17, the plain end D381 keeps: RET may stand there (D381),
    /// but its one benefit, not interrupting G64, needs a subprogram without SAVE (840D sl NC-Programmierung SW4.92,
    /// p.523; 840D/810D Arbeitsvorbereitung 2006 p.92), and M17 is the return at the end of any subprogram (p.522,
    /// 3.2.2.8); the rest of sub_end stays as it is, and a sub_end without RET is not changed.
    /// </summary>
    public static string? SubEnd(SiemensBlock write, Section? sub)
    {
        string? end = write.Render(write.Machine.Format?.SubEnd, "[format] sub_end (machine-config 2)",
            new TemplateValues());
        return end is not null && sub is not null && write.File.DeclaresSave(sub)
            ? PlainReturn().Replace(end, "M17")
            : end;
    }

    /// <summary>
    /// COMMENT and SECTION as a comment line with a semicolon, in the charset of the machine (controllers siemens.md 1;
    /// controller-mapping 1, COMMENT; language 4.1: SECTION is a plain comment on the other controllers), and MESSAGE
    /// as MSG("text") in a block of its own, MESSAGE="" as MSG() (controllers siemens.md 12 rule 6; D326).
    /// </summary>
    public static void WriteComment(SiemensBlock write)
    {
        foreach (string key in new[] { "COMMENT", "SECTION" })
        {
            if (write.Block.Find(key) is Word word && word.Value is StringValue text)
            {
                write.Written(word);
                write.Write("; " + write.CommentText(text.Content));
            }
        }

        if (write.Block.Find("MESSAGE") is Word message && message.Value is StringValue shown)
        {
            write.Written(message);
            string text = write.CommentText(shown.Content);
            write.Write(text.Length == 0 ? "MSG()" : "MSG(\"" + text + "\")");
        }
    }

    // RET without parameters, the word of sub_end that a SAVE subprogram writes as M17 (D381; SW4.92, p.523).
    [GeneratedRegex(@"(?<![A-Z0-9_$])RET(?![A-Z0-9_$]|\s*\()", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PlainReturn();

    /// <summary>
    /// STOCK through [stock], WORKPIECE(...) in a block of its own (D351; machine-config 5; NC-Programmierung SW 4.92
    /// p.1021-1023): the framework has chosen the template and reported what writes nothing (CMP012 to CMP014).
    /// </summary>
    public static void WriteStock(SiemensBlock write)
    {
        if (write.Block.Verb?.Key != "STOCK")
        {
            return;
        }

        foreach (string key in s_stockKeys)
        {
            write.Written(key);
        }

        if (write.Stock is not StockForm stock)
        {
            return;
        }

        var values = new TemplateValues();
        values.Set("axis", stock.Axis);
        foreach (StockNumber number in stock.Numbers)
        {
            values.Set(number.Name, write.Format(number.Address, number.Value));
        }

        if (write.Render(stock.Template, stock.What, values) is string text)
        {
            write.Write(text);
        }
    }
}
