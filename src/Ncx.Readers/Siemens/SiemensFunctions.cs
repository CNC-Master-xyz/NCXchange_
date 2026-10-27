using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The M functions of a SINUMERIK block that no other concern reads (controllers siemens.md 1, 5; controller-mapping
/// 1 and 4; machine-config 5; language 4.1, 4.6): M0 and M1 stop the program, the codes of the machine's tables are
/// their words, coolant, named functions and the spindle states, codes compared by number (D105), any other M is
/// MFUNC=n with a WARNING; the H functions of the PLC have no NCX word and stay RAW words of the block.
/// </summary>
internal static class SiemensFunctions
{
    /// <summary>
    /// Reads the M and H words no other concern read.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "H" || SiemensSpindles.NumberOfAddress(word.Address, 'H') is not null)
            {
                // H functions go to the PLC; NCX has no word for them (controllers siemens.md 1).
                block.KeepAsRawWord(word);
            }
            else if (word.Address == "M" && SiemensBlock.CodeOf(word) is string code)
            {
                ReadCode(block, word, code);
            }
            else if (SiemensSpindles.NumberOfAddress(word.Address, 'M') is not null && word.Text.Length > 0)
            {
                ReadExtended(block, word);
            }
        }
    }

    // M0 is STOP=PROGRAM, M1 STOP=OPTIONAL (controller-mapping 1, STOP); a code of a table is its word; any other M is
    // MFUNC=n (controller-mapping 4, MFUNC).
    private static void ReadCode(SiemensBlock block, SourceWord word, string code)
    {
        block.MarkRead(word);
        if (code is "M0" or "M1")
        {
            block.Draft.AddState("STOP", null, new IdentValue(code == "M0" ? "PROGRAM" : "OPTIONAL"));
            return;
        }

        if (ReadTable(block, code, null))
        {
            return;
        }

        long number = (long)word.Number!.Value;
        block.Draft.AddState("MFUNC", null, new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture)));
        block.Draft.Warnings.Add(new SiemensWarning(DiagnosticCodes.SiemensMCodeNotNamed,
            $"{code} is named by no table of the machine and is written as MFUNC={number} (machine-config 5, language "
            + "4.6)."));
    }

    // M1=28 and the like: a table may name the code with its extension; MFUNC takes a number without one, so any other
    // stays a RAW word (controller-mapping 4).
    private static void ReadExtended(SiemensBlock block, SourceWord word)
    {
        string text = word.Address + "=" + word.Text;
        if (ReadTable(block, text, SiemensSpindles.NumberOfAddress(word.Address, 'M')))
        {
            block.MarkRead(word);
            return;
        }

        block.KeepAsRawWord(word);
    }

    // The states of the machine that write the code, as FunctionChoice keeps them (D155): every state that writes it,
    // the [workpiece] selection first, the first in file order where two resources would be switched on (RDR020). The
    // Siemens reader keeps no source state of the tables, so no state is dropped as holding already; the codes of the
    // spindles of the control, M3 to M5 and M19, are SiemensSpindles'. The source state rules out the tables of
    // another channel, where the machine has one channel, and the spindle tables of other spindles, where the code
    // addresses spindle n by its extension, M2=3. False when no state gives a word.
    private static bool ReadTable(SiemensBlock block, string code, int? spindle)
    {
        IReadOnlyList<int> channels = block.Machine.Machine.Channels;
        string? role = spindle is int number ? SiemensSpindles.RoleOf(block, number) : null;
        var context = new FunctionContext
        {
            Channel = channels.Count == 1 ? channels[0] : null,
            AddressedSpindles = role is null ? null : [role],
        };
        IReadOnlyList<FunctionMatch> chosen = FunctionChoice.Choose(block.Templates.FindFunctionsByCode(code),
            context, block.Line, code, block.Diagnostics);
        var words = new List<Word>();
        foreach (FunctionMatch match in chosen)
        {
            if (WordsOf(block, match, code) is not { } matchWords)
            {
                return false;
            }

            words.AddRange(matchWords);
        }

        foreach (Word word in words)
        {
            block.Draft.AddState(word.Key, word.Addr, word.Value);
        }

        return words.Count > 0;
    }

    // The words of a table state (machine-config 5, D154): SPINDLE:role=CW with the role address, ORIENT:role at the
    // angle of the code, SPINDLE_MODE:role, SPINDLE_SYNC=leader,follower of the pair with PHASE at the angle of the
    // code, COOLANT with the channel STANDARD as the bare word (language 4.6), FUNC:name=state, WORKPIECE=role. An
    // ORIENT or PHASE whose template carries no angle is at 0, with the WARNING RDR021 naming the code. Null for a
    // state that gives no word, a spindle speed among them.
    private static List<Word>? WordsOf(SiemensBlock block, FunctionMatch match, string code)
    {
        string? addr = match.Address;
        string state = match.State;
        switch (match.Family)
        {
            case "SPINDLE" when state is "CW" or "CCW" or "OFF" && addr is not null:
                return [new Word
                {
                    Key = "SPINDLE",
                    Addr = SiemensSpindles.Address(block, addr),
                    Value = Ident(state),
                }];
            case "SPINDLE" when state == "ORIENT" && addr is not null:
                return [new Word
                {
                    Key = "ORIENT",
                    Addr = SiemensSpindles.Address(block, addr),
                    Value = AngleOf(block, match, code),
                }];
            case "SPINDLE_MODE":
                return [new Word { Key = "SPINDLE_MODE", Addr = addr, Value = Ident(state) }];
            case "SPINDLE_SYNC" when state == "OFF":
                return [new Word { Key = "SPINDLE_SYNC", Value = Ident(state) }];
            case "SPINDLE_SYNC" when state is "ON" or "PHASE":
                IReadOnlyList<string> pair = block.Machine.SpindleSyncRoles();
                if (pair.Count != 2)
                {
                    return null;
                }

                var sync = new List<Word> { new() { Key = "SPINDLE_SYNC", Value = new ListValue([.. pair]) } };
                if (state == "PHASE")
                {
                    sync.Add(new Word { Key = "PHASE", Value = AngleOf(block, match, code) });
                }

                return sync;
            case "COOLANT":
                return [new Word { Key = "COOLANT", Addr = addr == "STANDARD" ? null : addr, Value = Ident(state) }];
            case "FUNC":
                return [new Word { Key = "FUNC", Addr = addr, Value = Ident(state) }];
            case "WORKPIECE":
                return [new Word { Key = "WORKPIECE", Value = Ident(state) }];
            default:
                return null;
        }
    }

    // The {angle} the code carried; 0 with the WARNING RDR021 where the template has none, the orientation angle set
    // by the machine (D154).
    private static Value AngleOf(SiemensBlock block, FunctionMatch match, string code)
    {
        if (match.Values.TryGetNumber("angle", out decimal angle))
        {
            return SiemensNumbers.Of(angle);
        }

        block.Draft.Warnings.Add(AngleSetByTheMachine(code));
        return new IntegerValue(0, "0");
    }

    /// <summary>
    /// The WARNING RDR021 of a code that orients a spindle or sets a phase without an angle: the reader writes 0, and
    /// the angle is the one the machine sets (D154).
    /// </summary>
    /// <param name="code">The code as the source writes it, M19.</param>
    public static SiemensWarning AngleSetByTheMachine(string code)
    {
        return new SiemensWarning(DiagnosticCodes.FunctionAngleSetByTheMachine,
            $"{code} carries no angle; the reader writes 0, the orientation angle set by the machine (D154).");
    }

    private static IdentValue Ident(string value)
    {
        return new IdentValue(value);
    }
}
