using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The M codes of a Fanuc block (controllers fanuc.md 1, 5; controller-mapping 1, 4; machine-config 2, 5): the named
/// functions of [func], the spindles, the coolant channels of [coolant], MFUNC with a WARNING, M0 and M1 for STOP, the
/// workpiece selection of [workpiece], at most [format] m_codes_per_block M codes in one block (D339).
/// </summary>
internal static class FanucFunctions
{
    // D339: M00, M01, M02, M30, M98, M99 and M198 always stand without another M code, as do the [sync] wait codes and
    // the external call of [format] external_call (machine-config 2).
    private static readonly int[] s_aloneCodes = [0, 1, 2, 30, 98, 99, 198];

    // D339: the spindle codes M3, M4 and M5, besides the CW, CCW and OFF codes of the spindle tables.
    private static readonly int[] s_spindleCodes = [3, 4, 5];

    /// <summary>
    /// The functions of the block, in the order the sources write them: the named functions, the spindles with their
    /// S, the coolant, MFUNC, STOP and the workpiece (the TODO(question) of FanucLine).
    /// </summary>
    public static void Write(FanucBlock write)
    {
        Block block = write.Block;
        foreach (Word word in block.Words)
        {
            if (word.Key == "FUNC" && word.Addr is string name)
            {
                write.Written(word);
                string state = word.Value.ToCanonical();
                AddRendered(write, write.Machine.FindFunction(name, state),
                    $"[func] {name} {state} (machine-config 5)");
            }
        }

        FanucToolWords.AddSpindleFunctions(write);
        foreach (Word word in block.Words)
        {
            if (word.Key == "COOLANT")
            {
                write.Written(word);
                string channel = word.Addr ?? "STANDARD";
                string state = word.Value.ToCanonical();
                string? template = write.Machine.Coolant.TryGetValue(channel, out FunctionTable? table)
                    ? table.States.GetValueOrDefault(state)
                    : null;
                AddRendered(write, template, $"[coolant] {channel} {state} (machine-config 5)");
            }
        }

        AddMFunc(write);
        AddStop(write);
        AddWorkpiece(write);
    }

    /// <summary>
    /// Adds the words of a function to the main line, or to a line after it where the main line cannot take its M
    /// codes (D339; machine-config 2): more than m_codes_per_block (1 where the file leaves it out), a code that
    /// stands alone, a second or later code that m_codes_combinable, where set, does not list, or a spindle code next
    /// to another M code. D339 lets a spindle code share a block with a code m_codes_combinable lists; the Mori
    /// NTX1000SZM forbids exactly that (programming manual 2011 p.253, note 2: M03, M04, M13 and M14 never with a
    /// multiple M code function code), and no other list of the documents allows it, so a spindle code never shares
    /// a block with another M code.
    /// </summary>
    public static void Add(FanucBlock write, string words)
    {
        var codes = new List<int>();
        foreach (string written in write.Main.Functions)
        {
            codes.AddRange(MCodes(written));
        }

        List<int> added = MCodes(words);
        if (codes.Count > 0 && added.Count > 0 && !Combine(write, codes, added))
        {
            write.Following.Add(words);
            return;
        }

        write.Main.Function(words);
    }

    // True where the M codes of a line and those of a function stand in one block (D339).
    private static bool Combine(FanucBlock write, List<int> codes, List<int> added)
    {
        int most = write.Machine.Format?.MCodesPerBlock ?? 1;
        var all = new List<int>(codes);
        all.AddRange(added);
        if (all.Count > most)
        {
            return false;
        }

        IReadOnlyList<string>? combinable = write.Machine.Format?.MCodesCombinable;
        for (int index = 0; index < all.Count; index++)
        {
            int code = all[index];
            if (StandsAlone(write, code) || IsSpindleCode(write, code))
            {
                return false;
            }

            if (index > 0 && combinable is not null && !Lists(combinable, code))
            {
                return false;
            }
        }

        return true;
    }

    // M00, M01, M02, M30, M98, M99, M198, a wait code of [sync] within its mark_range and the external call (D339).
    private static bool StandsAlone(FanucBlock write, int code)
    {
        if (s_aloneCodes.Contains(code))
        {
            return true;
        }

        SyncConfig? sync = write.Machine.Sync;
        bool waitIsM = sync?.Wait is string wait && wait.StartsWith("M{mark", StringComparison.Ordinal);
        if (waitIsM && sync?.MarkRange is MarkRange range && code >= range.First && code <= range.Last)
        {
            return true;
        }

        return write.Machine.Format?.ExternalCall is string call && MCodes(call).Contains(code);
    }

    // M3, M4, M5 and the CW, CCW and OFF codes of every spindle table (D339; machine-config 5).
    private static bool IsSpindleCode(FanucBlock write, int code)
    {
        if (s_spindleCodes.Contains(code))
        {
            return true;
        }

        foreach (FunctionTable table in write.Machine.SpindleTables.Values)
        {
            foreach (string state in new[] { "CW", "CCW", "OFF" })
            {
                if (table.States.GetValueOrDefault(state) is string template && MCodes(template).Contains(code))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // True where m_codes_combinable lists the code, M10 or M010 alike.
    private static bool Lists(IReadOnlyList<string> combinable, int code)
    {
        foreach (string listed in combinable)
        {
            if (MCodes(listed).Contains(code))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddRendered(FanucBlock write, string? template, string what)
    {
        if (write.Render(template, what, new TemplateValues()) is string text && text.Length > 0)
        {
            Add(write, text);
        }
    }

    // MFUNC=n is the M code by number, for a function the machine configuration does not name; the compiler warns
    // (language 4.6, MFUNC).
    private static void AddMFunc(FanucBlock write)
    {
        if (write.Block.Find("MFUNC") is not Word mfunc)
        {
            return;
        }

        write.Written(mfunc);
        string number = mfunc.Value.ToCanonical();
        write.Warning(DiagnosticCodes.FanucMFuncWritten,
            $"MFUNC={number} is written as M{number}, a function that no table of the machine names (language 4.6).");
        Add(write, "M" + number);
    }

    // STOP=PROGRAM is M0, STOP=OPTIONAL M1 (controllers fanuc.md 1; controller-mapping 1, STOP).
    private static void AddStop(FanucBlock write)
    {
        if (write.Block.Find("STOP") is Word stop)
        {
            write.Written(stop);
            Add(write, stop.Value.ToCanonical() == "OPTIONAL" ? "M1" : "M0");
        }
    }

    // WORKPIECE=role from the [workpiece] template of the role (machine-config 5, D57). The template holds the
    // selection code alone and never the datum, which is ORIGIN, so ORIGIN=1 WORKPIECE=MAIN of one block stands as
    // G54 M428 on one line; a control without a selection code names no template and WORKPIECE then writes nothing
    // (machine-config 5, D222).
    private static void AddWorkpiece(FanucBlock write)
    {
        if (write.Block.Find("WORKPIECE") is not Word workpiece)
        {
            return;
        }

        write.Written(workpiece);
        string role = workpiece.Value.ToCanonical();
        if (write.Machine.Workpiece?.Templates.GetValueOrDefault(role) is not string template)
        {
            return;
        }

        if (write.Render(template, $"[workpiece] {role} (machine-config 5)", new TemplateValues()) is string text
            && text.Length > 0)
        {
            Add(write, text);
        }
    }

    // The M codes among the words of a function, as numbers: M3, not the S or the P that stands with it.
    private static List<int> MCodes(string words)
    {
        var codes = new List<int>();
        foreach (string word in words.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length > 1 && word[0] == 'M' && word.Skip(1).All(char.IsAsciiDigit)
                && int.TryParse(word.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int code))
            {
                codes.Add(code);
            }
        }

        return codes;
    }

    /// <summary>
    /// A number as a whole number of its text, for the placeholders of the templates.
    /// </summary>
    public static decimal? NumberOf(Value value)
    {
        return value is IntegerValue or DecimalValue
            ? decimal.Parse(value.ToCanonical(), CultureInfo.InvariantCulture)
            : null;
    }
}
