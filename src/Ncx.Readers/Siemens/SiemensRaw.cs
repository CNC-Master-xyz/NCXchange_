using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The SINUMERIK statements NCX keeps as whole RAW blocks, verbatim with a WARNING, in their place (controllers
/// siemens.md 11 rule 8; controller-mapping 9; D5): STOPRE, WORKPIECE, SETAL, the synchronized actions, the
/// interrupts, the axis exchange between channels, the marks set without waiting, the file functions, PCALL and
/// ISOCALL, the frame arithmetic, the programmable tool offsets, the coupling definitions, the writes to system
/// variables such as $P_UIFR, and the recipe block of the STAMA post. The RAW modal words of the G groups are
/// SiemensGroups, the RAW words of a motion block SiemensMotion.
/// </summary>
internal static class SiemensRaw
{
    // The statements of controller-mapping 9 and siemens 11 rule 8, each with the reason of its RAW.
    private static readonly Dictionary<string, string> s_statements = Statements();

    // The RAW blocks that change nothing the reader follows: a message, a stop of the block preparation, the blank of
    // the simulation, an alarm.
    private static readonly HashSet<string> s_harmless = new(StringComparer.Ordinal)
    {
        "MSG", "STOPRE", "WORKPIECE", "SETAL", "<",
    };

    /// <summary>
    /// Keeps a block whose statement NCX has no word for as RAW, with the reason; MSG becomes MESSAGE where the word
    /// can carry it (D326).
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        foreach (SourceWord word in block.Source.Words)
        {
            if (word.Address == "MSG" && ReadMessage(block, word))
            {
                // Only the MSG word is read: the other words of the block are the other concerns', and what none of
                // them reads keeps the block RAW, so that nothing is dropped (D5; controller-mapping 9).
                continue;
            }

            if (Reason(word) is not string reason)
            {
                continue;
            }

            block.MarkAllRead();
            block.Draft.KeepAsRaw(reason);
            block.Draft.RawKeepsState = s_harmless.Contains(word.Address);
            return;
        }
    }

    // MSG("text") is MESSAGE="text" and MSG() is MESSAGE="", an operator message the virtual machine executes nothing
    // for (language 4.1; controller-mapping 1, COMMENT; D326). MSG("text",1) forces a main-run block of its own and
    // interrupts continuous path, and an MSG built with << has no plain text: both have no MESSAGE form and stay RAW.
    private static bool ReadMessage(SiemensBlock block, SourceWord word)
    {
        if (!word.Text.StartsWith('('))
        {
            return false;
        }

        List<string> arguments = SiemensArguments.Split(word.Text);
        if (arguments.Count > 1 || (arguments.Count == 1 && !IsPlainString(arguments[0])))
        {
            return false;
        }

        block.MarkRead(word);
        string text = arguments.Count == 0 ? "" : SiemensArguments.StringOf(arguments[0])!;
        block.Draft.Main.Add("MESSAGE", new StringValue(text));
        return true;
    }

    // One string literal and nothing else: the quotes stand at the ends and nowhere between, so a text built with the
    // concatenation operator ("A"<<R1<<"B") is none (controllers siemens.md 8).
    private static bool IsPlainString(string argument)
    {
        string trimmed = argument.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
            && !trimmed.AsSpan(1, trimmed.Length - 2).Contains('"');
    }

    // Why a word keeps its block RAW; null for a word the other concerns read.
    private static string? Reason(SourceWord word)
    {
        if (word.Address == "<")
        {
            return "the recipe block of the STAMA post is not NC syntax "
                + "(machine-builders 2, STAMA; controller-mapping 9)";
        }

        if (word.Address == "?")
        {
            return $"{word.Text} is no SINUMERIK word the reader reads (controllers siemens.md 1)";
        }

        if (word.Address.StartsWith('$') && word.Text.Length > 0 && !word.Text.StartsWith('('))
        {
            return $"{word.Address}= writes a system variable, a data write NCX keeps as RAW ($P_UIFR writes the datum "
                + "table; controller-mapping 1, ORIGIN, and 9)";
        }

        return s_statements.TryGetValue(word.Address, out string? reason) ? reason : null;
    }

    private static Dictionary<string, string> Statements()
    {
        var statements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // D326: MSG is MESSAGE where the word can carry it (ReadMessage); this reason is left for the forms it
            // cannot, MSG("text",1) and a text built with <<.
            ["MSG"] = "MSG with an execution argument, or with a text built from values, has no MESSAGE form and "
                + "stays RAW:SIEMENS (language 4.1, MESSAGE; decisions.md D326)",
            ["STOPRE"] = "STOPRE stops the block preparation until the block is executed (controllers siemens.md 8; "
                + "controller-mapping 6)",
            ["WORKPIECE"] = "WORKPIECE defines the blank for the simulation, header information like BLK FORM "
                + "(controllers siemens.md 3; controller-mapping 9)",
            ["SETAL"] = "SETAL raises an alarm (controllers siemens.md 8; controller-mapping 6)",
            ["GOTOC"] = "GOTOC jumps without an alarm when the label is missing (controller-mapping 6, JUMP)",
            ["RETB"] = "RETB returns to a block of the caller (controller-mapping 6, SUB=BEGIN)",
            ["PCALL"] = "PCALL calls a subprogram with a path (controller-mapping 6, CALL)",
            ["ISOCALL"] = "ISOCALL calls an ISO program (controller-mapping 6, CALL)",
            ["CALLPATH"] = "CALLPATH extends the search path of the calls (controllers siemens.md 8)",
        };
        Add(statements, "the synchronized actions are motion-synchronous and beyond the reach of a block-by-block "
            + "reader (controllers siemens.md 8; controller-mapping 9)", "ID", "IDS", "WHEN", "WHENEVER", "EVERY",
            "FROM", "DO", "CANCEL");
        Add(statements, "the interrupt routines and the fast retract have no NCX word (controllers siemens.md 8; "
            + "controller-mapping 9)", "SETINT", "LIFTFAST", "ALF", "DISABLE", "ENABLE", "CLRINT");
        Add(statements, "the axis exchange between channels has no NCX word in 1.0 (controllers siemens.md 9; "
            + "controller-mapping 7)", "GET", "GETD", "RELEASE", "AXTOCHAN", "WAITP");
        Add(statements, "SETM and CLEARM set and clear marks without waiting (controller-mapping 7, SYNC)", "SETM",
            "CLEARM");
        Add(statements, "the file functions have no NCX word (controllers siemens.md 8)", "WRITE", "READ", "DELETE",
            "ISFILE");
        Add(statements, "the frame arithmetic and MEAFRAME have no NCX word (controllers siemens.md 4)", "CTRANS",
            "CROT", "CMIRROR", "CSCALE", "CFINE", "MEAFRAME");
        Add(statements, "the programmable tool offsets and the orientable tool carriers have no NCX word "
            + "(controllers siemens.md 5; controller-mapping 3, OFFSET)", "TOFFL", "TOFF", "TOFFR", "TOFFLR", "TCARR",
            "TCOABS", "TCOFR");
        Add(statements, "COUPDEF, COUPDEL and COUPONC define, delete and keep a coupling that NCX writes with "
            + "SPINDLE_SYNC and PHASE only (controllers siemens.md 5; controller-mapping 4, SPINDLE_SYNC)", "COUPDEF",
            "COUPDEL", "COUPONC");
        Add(statements, "WAITS waits for spindle positions that SPOSA started (controller-mapping 4, ORIENT)", "WAITS");
        return statements;
    }

    private static void Add(Dictionary<string, string> statements, string reason, params string[] words)
    {
        foreach (string word in words)
        {
            statements[word] = reason;
        }
    }
}
