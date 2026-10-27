using Ncx.Core.Catalog;
using Ncx.Core.Model;

namespace Ncx.Core.Parsing;

/// <summary>
/// The block rules of language 5 that the parser applies to a block with all its words: at most one verb (rule 1),
/// axis words under a verb that carries them (rule 2), a key once per block (rule 4), the partner words present
/// (rule 5), each an ERROR with its PAR code (D98). Rule 3 is the virtual machine's, rules 6 and 7 are the writer's.
/// </summary>
internal static class BlockRules
{
    // The verb of the reference point return, which carries bare axis names (language 4.3).
    private const string HomeKey = "HOME";

    // The motion verbs that carry every axis word: the verbs of the blocks that move (language 2 rule 2; D83) but
    // HOME, which carries bare axis names, and RETRACT, which carries none (language 5 rule 2).
    private const string MotionVerbsWithAxisWords = "RAPID, LINE, ARC or CYCLE_CALL";

    // The verbs that carry an axis name: the motion verbs, HOME and the verbs of the frame table (language 5 rule 2).
    private const string VerbsWithAxisNames = "RAPID, LINE, ARC, HOME, CYCLE_CALL, SHIFT, TILT, TILT_AXIS or SETPOS";

    // The verbs of the frame table, which carry their own axis words, the axis names of their rows, and no arc or
    // vector word (language 5 rule 2; 4.2).
    private static readonly string[] s_frameVerbs = ["SHIFT", "TILT", "TILT_AXIS", "SETPOS"];

    // Rule 5: the block words and the verbs or partner words of which one must stand in the same block; FRAME needs a
    // motion verb, the verbs of language 2 rule 2 and RETRACT (language 5 rule 5; 4.2, 4.3, 4.5, 4.8, 4.9; D83).
    // TODO(question): the Scope column of language 4 names a partner for more words than rule 5 lists: NAME with
    // PROGRAM=BEGIN, SUB=BEGIN or START_CHANNEL, NUMBER with PROGRAM=BEGIN, CHANNEL as a header word, the cycle
    // parameters with CYCLE, TOLERANCE:ROTARY only with TOLERANCE (language 4.1, 4.7). Neither rule 5 nor the
    // validation list of virtual machine 5 makes their absence an ERROR of the parser, so only the words of rule 5 are
    // checked here until D116 is answered; the header words are read from the PROGRAM=BEGIN block only.
    private static readonly Dictionary<string, string[]> s_partners = new()
    {
        ["IF"] = ["JUMP", "CALL"],

        // ARG is the argument of the CALL in the same block, which the callee sees as a local variable, and a REPEAT
        // has no callee; TIMES counts the passes of a CALL or of a REPEAT (language 4.9, rows ARG and TIMES; virtual
        // machine 3.6, 5; wave-1 question #57).
        ["ARG"] = ["CALL"],
        ["TIMES"] = ["CALL", "REPEAT"],
        ["WITH"] = ["SYNC"],
        ["FRAME"] = ["RAPID", "LINE", "ARC", "RETRACT", HomeKey, "CYCLE_CALL"],
        ["MOVE"] = ["TILT", "TILT_AXIS"],
        ["ROT"] = ["TILT", "TILT_AXIS"],
        ["POINT"] = [HomeKey],
        ["PHASE"] = ["SPINDLE_SYNC"],
    };

    // The words of the measurement point, valid in a TOOL block or in a block of their own (language 4.4, D372).
    private static readonly string[] s_pointWords = ["TOOL_POINT", "TIP_OFFSET", "POINT_RADIUS"];

    // What a block of the point words may carry beside them: the block skip and a comment, which run nothing (D391).
    private static readonly string[] s_besidePointWords = ["SKIP", "COMMENT"];

    /// <summary>
    /// The verb of the words of a block. A block has at most one verb; a second is an ERROR, and the first stays the
    /// verb (language 5 rule 1). The =RESET form of SHIFT, TILT and TILT_AXIS is no verb (language 4.2, D90).
    /// </summary>
    /// <param name="words">The words of the block in their order.</param>
    /// <param name="line">The line of the block.</param>
    /// <param name="diagnostics">Where the ERROR goes.</param>
    public static Word? FindVerb(IReadOnlyList<Word> words, int line, Diagnostics diagnostics)
    {
        Word? verb = null;
        foreach (Word word in words)
        {
            if (!WordCatalog.IsVerb(word))
            {
                continue;
            }

            if (verb is null)
            {
                verb = word;
                continue;
            }

            diagnostics.Error(line, DiagnosticCodes.TwoVerbs,
                $"{word.ToCanonical()} is a second verb beside {verb.ToCanonical()}; a block has at most one verb "
                + "(language 5 rule 1).");
        }

        return verb;
    }

    /// <summary>
    /// Applies rules 2, 4 and 5 of language 5 to a block with all its words and its verb.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="diagnostics">Where an ERROR goes.</param>
    public static void Check(Block block, Diagnostics diagnostics)
    {
        CheckAxisWords(block, diagnostics);
        CheckKeysOnce(block, diagnostics);
        CheckPartners(block, diagnostics);
        CheckPointWords(block, diagnostics);
    }

    // Axis words require a verb that carries them: a motion verb carries every axis word, RETRACT none. SHIFT, TILT,
    // TILT_AXIS and SETPOS carry their own axis words, the axis names of their rows, so CENTER, R, ANGLE, TX TY TZ and
    // NX NY NZ, which name no axis, still require a motion verb under them. HOME carries bare axis names; under any
    // other verb an axis word has a value. A machine axis word counts as an axis word; a native parameter is none
    // (language 5 rule 2; 4.2, 4.3; D93, D94).
    // D117: the four frame verbs take absolute axis words only, each the kinds language 4.2 gives it (decisions.md).
    // TILT carries the spatial angles A, B and C alone; TILT_AXIS carries A, B, C and the machine axis names of the
    // D93 form, the virtual machine reporting an axis that is not rotary; SHIFT and SETPOS carry every absolute axis
    // word. An incremental word under any of the four is PAR012, because a shift is already relative to the frame it
    // stands in and a SETPOS declares a coordinate, so a reader folds a source increment itself.
    private static void CheckAxisWords(Block block, Diagnostics diagnostics)
    {
        bool nativeBlock = WordCatalog.IsNativeParameterAllowed(block);
        Word? verb = block.Verb;
        bool verbCarriesAxisWords = verb?.Definition is { TakesAxisWords: true };
        foreach (Word word in block.Words)
        {
            if (!IsAxisWord(word, nativeBlock))
            {
                continue;
            }

            if (verb is null || !verbCarriesAxisWords)
            {
                string verbs = NamesAnAxis(word) ? VerbsWithAxisNames : MotionVerbsWithAxisWords;
                string rule = verb is null
                    ? $"{word.ToCanonical()} is an axis word and needs a verb that carries it: {verbs}"
                    : $"{verb.Key} carries no axis words, not {word.ToCanonical()}";
                diagnostics.Error(block, DiagnosticCodes.AxisWordWithoutVerb, $"{rule} (language 5 rule 2).");
            }
            else if (s_frameVerbs.Contains(verb.Key) && !NamesAnAxis(word))
            {
                diagnostics.Error(block, DiagnosticCodes.AxisWordWithoutVerb,
                    $"{verb.Key} carries its own axis words, the axis names, not {word.ToCanonical()}; CENTER, R, "
                    + $"ANGLE, TX TY TZ and NX NY NZ require a motion verb: {MotionVerbsWithAxisWords} (language 5 "
                    + "rule 2, 4.2).");
            }
            else if (s_frameVerbs.Contains(verb.Key) && FrameVerbRefusal(verb.Key, word) is string refusal)
            {
                diagnostics.Error(block, DiagnosticCodes.AxisWordWithoutVerb, refusal);
            }
            else if (verb.Key == HomeKey)
            {
                if (!IsAxisName(word) || word.Value is not NoValue)
                {
                    diagnostics.Error(block, DiagnosticCodes.HomeTakesBareAxisNames,
                        $"HOME carries bare axis names, HOME X Z, not {word.ToCanonical()} (language 5 rule 2, 4.3).");
                }
            }
            else if (word.Value is NoValue)
            {
                diagnostics.Error(block, DiagnosticCodes.AxisWordNeedsValue,
                    $"The axis word {word.Key} under {verb.Key} needs a number or an expression; only HOME carries "
                    + "bare axis names (language 5 rule 2, 4.3).");
            }
        }
    }

    // Why a frame verb refuses an axis word that names an axis, null when it carries it (D117): none of the four takes
    // an incremental form, TILT carries the spatial angles A, B and C alone, and TILT_AXIS carries those three and the
    // machine axis names of the D93 form, not a standard linear axis (language 4.2, 5 rule 2).
    private static string? FrameVerbRefusal(string verb, Word word)
    {
        if (!IsAxisName(word))
        {
            return $"{verb} carries absolute axis words only, not {word.ToCanonical()}: a shift stands in the frame "
                + "that is active where it is written and a SETPOS declares a coordinate, so a reader folds a source "
                + "increment itself (language 5 rule 2, 4.2, 4.3; D117).";
        }

        if (verb == "TILT" && !IsSpatialAngle(word))
        {
            return $"TILT carries the spatial angles A, B and C, not {word.ToCanonical()}; the rotary axis angles of "
                + "this machine are TILT_AXIS (language 5 rule 2, 4.2; D117).";
        }

        if (verb == "TILT_AXIS" && !IsSpatialAngle(word) && word.Definition is not null)
        {
            return $"TILT_AXIS carries A, B, C and the machine axis names of the [[axis]] list, not "
                + $"{word.ToCanonical()} (language 5 rule 2, 4.2; D93, D117).";
        }

        return null;
    }

    // A, B or C written bare, the spatial angles of TILT and the standard rotary names of TILT_AXIS (language 4.2).
    private static bool IsSpatialAngle(Word word)
    {
        return word.Addr is null && word.Definition is not null && word.Key is "A" or "B" or "C";
    }

    // The axis words of rule 2: X and IX for every standard axis, CENTER, R, ANGLE, TX TY TZ, NX NY NZ by their catalog
    // entry, and a key of the machine-axis form outside a CYCLE:controller=n block (language 5 rule 2; D93, D94).
    private static bool IsAxisWord(Word word, bool nativeBlock)
    {
        if (word.Definition is not null)
        {
            return word.Definition.IsAxisWord;
        }

        return !nativeBlock && WordCatalog.TryMachineAxis(word.Key, out _);
    }

    // An axis word that names an axis: X Y Z A B C and IX to IC, whose key is the axis name or I and the name, and a
    // machine axis word of the D93 form; CENTER, R, ANGLE, TX TY TZ and NX NY NZ name none (language 4.3, rows X=10.5
    // and IX=5; 5 rule 2; D93).
    private static bool NamesAnAxis(Word word)
    {
        if (word.Definition is not null)
        {
            return WordCatalog.IsStandardAxis(word.Key);
        }

        return WordCatalog.TryMachineAxis(word.Key, out _);
    }

    // An axis name: X Y Z A B C, or a machine axis name of the D93 form; not the incremental form, I followed by the
    // name, and no other axis word (language 3, KEY; 4.3; D93).
    private static bool IsAxisName(Word word)
    {
        if (word.Addr is not null)
        {
            return false;
        }

        if (word.Definition is not null)
        {
            return WordCatalog.IsStandardAxis(word.Key) && !word.Key.StartsWith('I');
        }

        return WordCatalog.TryMachineAxis(word.Key, out MachineAxisWord? machineAxis) && !machineAxis.IsIncremental;
    }

    // A key may appear once per block; keys with different addresses are different words (language 5 rule 4).
    private static void CheckKeysOnce(Block block, Diagnostics diagnostics)
    {
        for (int index = 1; index < block.Words.Count; index++)
        {
            Word word = block.Words[index];
            for (int earlier = 0; earlier < index; earlier++)
            {
                Word other = block.Words[earlier];
                if (other.Key == word.Key && other.Addr == word.Addr)
                {
                    string name = word.Addr is null ? word.Key : word.Key + ":" + word.Addr;
                    diagnostics.Error(block, DiagnosticCodes.DuplicateKey,
                        $"{name} stands twice in the block; a key appears once per block, and only a different address "
                        + "makes a different word (language 5 rule 4).");
                    break;
                }
            }
        }
    }

    // IF, ARG, TIMES, WITH, FRAME, MOVE, ROT, POINT and PHASE are block words that need their verb or partner word in
    // the same block (language 5 rule 5).
    private static void CheckPartners(Block block, Diagnostics diagnostics)
    {
        foreach (Word word in block.Words)
        {
            if (!s_partners.TryGetValue(word.Key, out string[]? partners) || HasAny(block, partners))
            {
                continue;
            }

            diagnostics.Error(block, DiagnosticCodes.PartnerWordMissing,
                $"{word.Key} needs {JoinChoices(partners)} in the same block (language 5 rule 5).");
        }
    }

    // TOOL_POINT, TIP_OFFSET and POINT_RADIUS are valid in a TOOL block or in a block of their own (language 4.4,
    // D372). A block of their own carries nothing but the point words, SKIP and a COMMENT (decisions.md D391), because
    // a point change takes effect with the next tool-axis motion and a block that moves or switches something else
    // would hide where it starts.
    private static void CheckPointWords(Block block, Diagnostics diagnostics)
    {
        Word? pointWord = null;
        Word? otherWord = null;
        foreach (Word word in block.Words)
        {
            if (s_pointWords.Contains(word.Key))
            {
                pointWord ??= word;
            }
            else if (!s_besidePointWords.Contains(word.Key))
            {
                otherWord ??= word;
            }
        }

        if (pointWord is null || otherWord is null || block.Has("TOOL"))
        {
            return;
        }

        diagnostics.Error(block, DiagnosticCodes.PointWordOutsideToolBlock,
            $"{pointWord.ToCanonical()} stands beside {otherWord.ToCanonical()}; the point words are valid in a TOOL "
            + "block or in a block of their own (language 4.4, D372).");
    }

    private static bool HasAny(Block block, string[] keys)
    {
        foreach (string key in keys)
        {
            if (block.Has(key))
            {
                return true;
            }
        }

        return false;
    }

    // "JUMP or CALL", "TILT or TILT_AXIS", "RAPID, LINE, ... or CYCLE_CALL".
    private static string JoinChoices(string[] choices)
    {
        if (choices.Length == 1)
        {
            return choices[0];
        }

        return string.Join(", ", choices, 0, choices.Length - 1) + " or " + choices[^1];
    }
}
