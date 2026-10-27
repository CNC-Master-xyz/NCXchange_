using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The M codes of a Fanuc block and the codes the reader keeps as RAW (controllers fanuc.md 1, 8, 9 rule 7;
/// controller-mapping 1, 4, 7, 9; machine-config 5): M0 and M1 as STOP, the M codes of the machine's function tables
/// compared by number (D105), the workpiece codes of [workpiece], the wait marks of [sync] as SYNC with WITH, any other
/// M code as MFUNC with a WARNING; G10, G31, G38, the builder macros of [raw] and every G code the reader does not map
/// stay RAW with a WARNING, never dropped (D5). The NT NURSE branch commands of the builder nakamura are not RAW:
/// FanucMacro reads them as JUMP with IF (controller-mapping 6, 9).
/// </summary>
internal static class FanucBuilder
{
    // The M codes of the controller itself, which the structure pass, the calls and the tool change read; the code of
    // an external call is not among them, because it is a fact of the machine, [format] external_call (D322).
    private static readonly string[] s_ownCodes = ["M0", "M1", "M2", "M30", "M6", "M98", "M99"];

    // The G codes the reader maps in every G-code system (controllers fanuc.md 3, 4; controller-mapping 1, 2).
    private static readonly string[] s_commonCodes =
    [
        "G0", "G1", "G2", "G3", "G4", "G15", "G16", "G17", "G18", "G19", "G28", "G30", "G40", "G41", "G42", "G43",
        "G43.4", "G43.5", "G44", "G49", "G52", "G53", "G53.1", "G54", "G54.1", "G55", "G56", "G57", "G58", "G59",
        "G65", "G66", "G67", "G68", "G68.1", "G68.2", "G69", "G5.1", "G7.1", "G12.1", "G13.1", "G50.1", "G51.1", "G80",
        "G61", "G64", "G96", "G97",
    ];

    // The G codes of a mill and of every G-code system of a lathe (controllers fanuc.md 3, 6).
    private static readonly string[] s_millCodes =
    [
        "G20", "G21", "G73", "G74", "G76", "G81", "G82", "G83", "G84", "G85", "G86", "G87", "G88", "G89", "G90", "G91",
        "G92", "G94", "G95", "G98", "G99",
    ];

    private static readonly string[] s_systemACodes =
    [
        "G20", "G21", "G50", "G70", "G71", "G72", "G73", "G74", "G75", "G76", "G83", "G84", "G85", "G87", "G88", "G89",
        "G90", "G92", "G94", "G98", "G99",
    ];

    private static readonly string[] s_systemBCodes =
    [
        "G20", "G21", "G70", "G71", "G72", "G73", "G74", "G75", "G76", "G77", "G78", "G79", "G83", "G84", "G85", "G87",
        "G88", "G89", "G90", "G91", "G92", "G94", "G95", "G98", "G99",
    ];

    private static readonly string[] s_systemCCodes =
    [
        "G70", "G71", "G77", "G78", "G79", "G83", "G84", "G85", "G87", "G88", "G89", "G90", "G91", "G92", "G94", "G95",
        "G98", "G99",
    ];

    /// <summary>
    /// Why a block is kept as RAW before it is read, for a code NCX cannot express: the data setting of G10, the skip
    /// of G31, the push check of G38, a builder macro of [raw], a G code the reader does not map (controllers fanuc.md
    /// 9 rule 7; controller-mapping 9); null for a block the reader reads.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static string? ReasonToKeepAsRaw(FanucBlock block)
    {
        foreach (SourceWord word in block.Source.Words)
        {
            if (RawEmitter.IsBuilderCode(word, block.Machine.Raw))
            {
                return $"{word.Address}{word.Text} is a builder macro of the [raw] table (controller-mapping 9)";
            }

            string? code = word.Address == "G" ? NativeCode.Of(word) : null;
            if (word.Address == "G" && (code is null || !IsMapped(block, code)))
            {
                return ReasonFor(code ?? "G" + word.Text);
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the M codes of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            if (block.Draft.IsRaw)
            {
                return;
            }

            if (word.Address == "M" && !block.IsRead(word))
            {
                ReadCode(block, word);
            }
        }
    }

    /// <summary>
    /// Tells whether the controller or a table of the machine names an M code, so that no reader rule is asked for it:
    /// the tables are tried first (D40, D66, machine-config 5).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="word">The M word.</param>
    public static bool Names(FanucBlock block, SourceWord word)
    {
        // M200 P of the builder nakamura calls a program as M98 does, which FanucMacro reads (controller-mapping 6, 8),
        // and the external call code the machine names is read there too (D322).
        string? code = NativeCode.Of(word);
        return code is null
            || s_ownCodes.Contains(code)
            || code == FanucMacro.ExternalCallCode(block.Machine)
            || (code == "M200" && FanucMacro.IsBuilderNakamura(block.Machine))
            || FanucFunctions.Of(block, word, out _) is not null
            || WorkpieceOf(block, code) is not null
            || MarkOf(block, word) is not null;
    }

    // An M code: STOP, the word of a function table, WORKPIECE, SYNC, or MFUNC with a WARNING (controller-mapping 1, 4,
    // 7; machine-config 5).
    private static void ReadCode(FanucBlock block, SourceWord word)
    {
        string? code = NativeCode.Of(word);
        if (code is null || word.Number is not decimal number || number != decimal.Truncate(number))
        {
            block.Draft.KeepAsRaw($"M{word.Text} names no M code");
            return;
        }

        block.MarkRead(word);
        if (code is "M0" or "M1")
        {
            // M0 stops, M1 stops when the optional stop switch is on (controllers fanuc.md 1, language 4.1).
            block.Draft.AddState("STOP", null, new IdentValue(code == "M0" ? "PROGRAM" : "OPTIONAL"));
            return;
        }

        if (ReadFunctions(block, word))
        {
            return;
        }

        if (WorkpieceOf(block, code) is string role)
        {
            block.Draft.AddState("WORKPIECE", null, new IdentValue(role));
            return;
        }

        if (MarkOf(block, word) is int mark)
        {
            ReadSync(block, mark);
            return;
        }

        // The PHASE code of [spindle_sync] is named by a table, so no MFUNC (machine-config 5); on a machine without a
        // pair of spindles to couple it stays RAW (D5, D154).
        if (FanucFunctions.IsPhaseCode(block, word))
        {
            block.Draft.KeepAsRaw($"M{word.Text} is the PHASE of [spindle_sync], and the machine names no pair of "
                + "spindles it couples (D154)");
            return;
        }

        // An M code that matches no entry becomes MFUNC=n with a WARNING (machine-config 5, language 4.6), reported
        // when the block is written and not kept as RAW for another word (FanucDraft.Warnings).
        long mfunc = decimal.ToInt64(number);
        string text = mfunc.ToString(CultureInfo.InvariantCulture);
        block.Draft.Warnings.Add((DiagnosticCodes.FanucMCodeNotNamed,
            $"M{text} is no code of the controller or of a table of the machine; it is written as MFUNC={text} "
            + "(machine-config 5)."));
        block.Draft.AddState("MFUNC", null, new IntegerValue(mfunc, text));
    }

    // The states of the function tables an M code writes, the [workpiece] selection first (D155;
    // FanucFunctions.Choose); false when none maps to a word, and the code is read as the workpiece, a mark or MFUNC.
    private static bool ReadFunctions(FanucBlock block, SourceWord word)
    {
        IReadOnlyList<FunctionMatch> chosen = FanucFunctions.Choose(block, word, out SourceWord? parameter);
        var functions = new List<FunctionMatch>();
        string? workpiece = null;
        foreach (FunctionMatch match in chosen)
        {
            if (match.Table is null)
            {
                workpiece ??= match.State;
            }
            else if (FanucFunctions.WordOf(block, match) is not null)
            {
                functions.Add(match);
            }
        }

        if (functions.Count == 0)
        {
            return false;
        }

        if (parameter is not null)
        {
            block.MarkRead(parameter);
        }

        if (workpiece is not null)
        {
            block.Draft.AddState("WORKPIECE", null, new IdentValue(workpiece));
        }

        foreach (FunctionMatch match in functions)
        {
            FanucFunction function = FanucFunctions.WordOf(block, match)!;
            bool orient = function.Key == "ORIENT";
            block.Draft.AddState(function.Key, function.Addr, orient ? AngleOf(block, word, match) : function.Value);
            if (match.State == "PHASE")
            {
                block.Draft.AddState("PHASE", null, AngleOf(block, word, match));
            }

            if (function.SpindleRole is not null)
            {
                block.State.LastSpindle = function.SpindleRole;
                block.Fanuc.Unknowns.Spindle = false;
            }
        }

        return true;
    }

    // D154: ORIENT and PHASE take the angle of the {angle} of their template, or for ORIENT the S or R of the block,
    // M19 S90 (controller-mapping 4, ORIENT); a code without one, M19 or Nakamura M92, gives 0 with the WARNING RDR021,
    // the angle being set by the machine (language 4.5).
    private static Value AngleOf(FanucBlock block, SourceWord word, FunctionMatch match)
    {
        if (match.Values.TryGetNumber("angle", out decimal angle))
        {
            return FanucNumbers.Of(angle);
        }

        SourceWord? written = match.State == "PHASE" ? null : block.Take("S") ?? block.Take("R");
        if (written is not null && FanucMacro.ValueOf(block, written) is Value value)
        {
            return value;
        }

        string key = match.State == "PHASE" ? "PHASE" : "ORIENT";
        block.Diagnostics.Warning(block.Line, DiagnosticCodes.FunctionAngleSetByTheMachine,
            $"M{word.Text} carries no angle, so {key}=0 is written: the orientation angle is set by the machine "
            + "(D154).");
        return new IntegerValue(0, "0");
    }

    // The M code of a [workpiece] template selects the holder the program machines, WORKPIECE=role (controller-mapping
    // 4, WORKPIECE; language 4.10; D40, D57).
    // D222: a template holds the selection code alone, so the reader matches it in whatever block the code stands
    // and reads G54..G59 as ORIGIN where the source writes them (machine-config 5; controller-mapping 1, 4;
    // decisions.md).
    private static string? WorkpieceOf(FanucBlock block, string code)
    {
        if (block.Machine.Workpiece is not WorkpieceConfig workpiece)
        {
            return null;
        }

        foreach (KeyValuePair<string, string> template in workpiece.Templates)
        {
            foreach (string token in template.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.StartsWith('M') && NativeCode.SameCode(token, code))
                {
                    return template.Key;
                }
            }
        }

        return null;
    }

    // The wait codes of the mark range of [sync] are the marks of SYNC (controllers fanuc.md 8; controller-mapping 7;
    // machine-config 5).
    private static int? MarkOf(FanucBlock block, SourceWord word)
    {
        if (block.Machine.Sync?.MarkRange is not MarkRange range || word.Number is not decimal number)
        {
            return null;
        }

        return number >= range.First && number <= range.Last ? decimal.ToInt32(number) : null;
    }

    // SYNC=m with the participating paths of P as WITH, a path list P12 or a bitmask P3 as [sync] paths says; without P
    // all paths wait (controllers fanuc.md 8; controller-mapping 7; language 4.8).
    private static void ReadSync(FanucBlock block, int mark)
    {
        block.Draft.AddState("SYNC", null, new IntegerValue(mark, mark.ToString(CultureInfo.InvariantCulture)));
        SyncConfig sync = block.Machine.Sync!;
        if (sync.Wait?.Contains("{paths}", StringComparison.Ordinal) != true || block.Take("P") is not SourceWord paths)
        {
            return;
        }

        var channels = new List<string>();
        string digits = paths.Text.TrimEnd('.');
        if (sync.Paths == SyncPaths.Bitmask && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture,
            out int mask))
        {
            for (int channel = 1; channel <= 8; channel++)
            {
                if ((mask & (1 << (channel - 1))) != 0)
                {
                    channels.Add(channel.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
        else
        {
            foreach (char channel in digits)
            {
                channels.Add(channel.ToString());
            }
        }

        block.Draft.AddState("WITH", null, new ListValue(channels));
    }

    // Whether the reader maps a G code: the codes of every system and of the machine's system, and the builder codes of
    // the machine's [tool_change] and [transform] templates.
    private static bool IsMapped(FanucBlock block, string code)
    {
        string[] system = block.System switch
        {
            null => s_millCodes,
            GcodeSystem.A => s_systemACodes,
            GcodeSystem.B => s_systemBCodes,
            _ => s_systemCCodes,
        };
        if (s_commonCodes.Contains(code) || system.Contains(code) || FanucMacro.IsBranchCommand(block.Machine, code))
        {
            return true;
        }

        TransformTable? transform = block.Machine.Transform;
        ToolChangeConfig? toolChange = block.Machine.ToolChange;
        string?[] templates =
        [
            toolChange?.Change, toolChange?.Preload, transform?.PolarOn, transform?.PolarOff, transform?.CylinderOn,
            transform?.CylinderOff, transform?.TcpmOn, transform?.TcpmOff,
        ];
        foreach (string? template in templates)
        {
            string? first = template?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (first is not null && first.StartsWith('G') && NativeCode.SameCode(first, code))
            {
                return true;
            }
        }

        return false;
    }

    // The reason a G code the reader does not map is kept as RAW, in the words of the machine (controller-mapping 1,
    // 9).
    private static string ReasonFor(string code)
    {
        return code switch
        {
            "G10" => "G10 writes data of the control, datums and offsets (controller-mapping 9)",
            "G31" => "G31 is a skip move for probing (controller-mapping 9)",
            "G38" => "G38 is a push check (controller-mapping 9)",
            "G5" or "G8" or "G61.1" => $"{code} switches the path smoothing with a tolerance in a parameter "
                + "(controller-mapping 1, TOLERANCE)",
            "G62" or "G63" => $"{code}, the cutting mode of group 13, has no NCX word; G61 and G64 are PATH_MODE "
                + "(D349)",
            "G33" => "G33, thread cutting, has no NCX word",
            _ => $"{code} has no NCX word the Fanuc reader maps (controller-mapping 9)",
        };
    }
}
