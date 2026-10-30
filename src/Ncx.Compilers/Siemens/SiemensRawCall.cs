using System.Text.RegularExpressions;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// Whether a RAW line calls a subprogram or a builder cycle, which may change the frame on the control as it may arm a
/// modal call (D420, D343): CALL, PCALL, EXTCALL, an L number, or a name of two letters or more, with or without
/// parentheses, that stands at the head of the line and is no frame-neutral instruction (controllers siemens.md 2, 7,
/// 8; machine-builders 2).
/// </summary>
internal static partial class SiemensRawCall
{
    // The instructions that call a program by what follows them (controllers siemens.md 8).
    private static readonly HashSet<string> s_callInstructions = new(StringComparer.OrdinalIgnoreCase)
    {
        "CALL", "PCALL", "EXTCALL", "ISOCALL",
    };

    // D420: the frame-neutral instructions, which leave the frame known after them: the statements of the language
    // (siemens 8, 9), MSG, STOPRE, WORKPIECE, SETAL and GROUP_BEGIN, the RAW blocks the reader already counts as
    // changing nothing it follows; the commands of the G groups of siemens 2 that are names, and the RAW words of the
    // reader beside them; the transformations of siemens 6, which are no frame; the spindle and coupling instructions
    // of siemens 5; the synchronized actions and interrupts; the programmable tool offsets. MCALL arms a modal call,
    // which D343 follows, and calls nothing itself. A frame instruction (TRANS, CYCLE800, TOFRAME) is no call either:
    // it names the frame, which SiemensFrames.KeysNamedBy makes unknown.
    private static readonly HashSet<string> s_neutral = new(StringComparer.OrdinalIgnoreCase)
    {
        "IF", "ELSE", "ENDIF", "LOOP", "ENDLOOP", "FOR", "ENDFOR", "WHILE", "ENDWHILE", "REPEAT", "REPEATB", "UNTIL",
        "CASE", "GOTO", "GOTOF", "GOTOB", "GOTOC", "GOTOS", "PROC", "RET", "DEF", "EXTERN", "DEFINE", "SAVE", "MCALL",
        "MSG", "STOPRE", "WORKPIECE", "SETAL", "GROUP_BEGIN", "GROUP_END", "GROUP_ADDEND", "INIT", "START", "WAITE",
        "WAITM", "WAITMC", "SETM", "CLEARM", "M_TO", "CIP", "CT", "ASPLINE", "BSPLINE", "CSPLINE", "POLY", "INVCW",
        "INVCCW", "REPOSL", "REPOSQ", "REPOSH", "REPOSA", "REPOSQA", "REPOSHA", "STARTFIFO", "STOPFIFO", "FIFOCTRL",
        "SUPA", "CFC", "CFTCP", "CFIN", "NORM", "KONT", "KONTC", "KONTT", "BRISK", "SOFT", "DRIVE", "BRISKA", "SOFTA",
        "DRIVEA", "CUT2D", "CUT2DF", "CUT3DC", "CUT3DF", "CUT3DFF", "CUT3DCC", "CUT3DCCD", "CUT2DD", "CUT2DFD", "FFWOF",
        "FFWON", "ORIWKS", "ORIMKS", "DIAMOF", "DIAMON", "DIAM90", "DIAMCYCOF", "COMPOF", "COMPON", "COMPCURV",
        "COMPCAD", "COMPSURF", "SPATH", "UPATH", "CP", "PTP", "PTPG0", "ORIEULER", "ORIRPY", "ORIVIRT1", "ORIVIRT2",
        "ORIAXPOS", "ORIRPY2", "ORIVECT", "ORIAXES", "ORIPATH", "ORIPLANE", "ORICONCW", "ORICONCCW", "ORICONIO",
        "ORICONTO", "ORICURVE", "ORIPATHS", "ORIRESET", "DYNNORM", "DYNPOS", "DYNROUGH", "DYNSEMIFIN", "DYNFINISH",
        "RTLION", "RTLIOF", "ORISON", "ORISOF", "ORIC", "ORID", "CPRECON", "CPRECOF", "CUTCONON", "CUTCONOF", "WALIMON",
        "WALIMOF", "CDON", "CDOF", "CDOF2", "SBLON", "SBLOF", "DISPLON", "DISPLOF", "TRAORI", "TRAFOOF", "TRACYL",
        "TRANSMIT", "TRAANG", "SPOS", "SPOSA", "SETMS", "LIMS", "SPCON", "SPCOF", "COUPON", "COUPOF", "COUPDEF",
        "COUPDEL", "COUPRES", "FGROUP", "FGREF", "WHEN", "WHENEVER", "EVERY", "FROM", "DO", "ID", "IDS", "SETINT",
        "CLRINT", "ENABLE", "DISABLE", "TOFFON", "TOFFOF", "FNORM", "FLIN", "FCUB",
    };

    /// <summary>
    /// True where the RAW line calls a subprogram or a builder cycle (D420).
    /// </summary>
    /// <param name="raw">The text of the RAW block.</param>
    public static bool IsCall(string raw)
    {
        // The head of the line: the first word after the skip mark, the block number and a label.
        Match head = Head().Match(raw);
        if (!head.Success)
        {
            return false;
        }

        string name = head.Groups["name"].Value;
        string after = head.Groups["after"].Value;
        if (s_callInstructions.Contains(name) || LNumber().IsMatch(name))
        {
            return true;
        }

        // An assignment or an indexed address, R1=5 or ACC[X]=50, is no call; neither is an address letter with its
        // value (G4, M30), whose name has one letter, nor a standard machining cycle of siemens 7, CYCLE81 and the
        // pattern and pocket cycles, which leave the frame as they found it.
        bool assignment = after is "=" or "[";
        return !assignment && TwoLetters().IsMatch(name) && !s_neutral.Contains(name)
            && !StandardCycle().IsMatch(name);
    }

    // The head of a line: an optional skip mark, block number and label, then a name and the character after it.
    [GeneratedRegex(
        @"^\s*(/[0-9]?\s*)?(N[0-9]+\s+)?([A-Za-z_]\w*:\s*)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?<after>[=\[]?)",
        RegexOptions.CultureInvariant)]
    private static partial Regex Head();

    // A subprogram called by its number, L100 (controllers siemens.md 8).
    [GeneratedRegex("^L[0-9]+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LNumber();

    // A name of the form a subprogram takes: two letters, or an underscore and a letter, first (siemens 8).
    [GeneratedRegex("^([A-Za-z]{2}|_[A-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex TwoLetters();

    // The standard machining cycles of siemens 7, which work in the frame they find and leave it as it was.
    [GeneratedRegex("^(CYCLE[0-9]+|POCKET[0-9]+|SLOT[0-9]+|LONGHOLE|HOLES[0-9]+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex StandardCycle();
}
