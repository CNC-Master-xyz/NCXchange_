using Ncx.Core.Catalog;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The state variables the restore stack keeps (virtual machine 3.10): for a state key, the variable it names by the
/// key that sets it, the words that set its current value again, and what @RESTORE puts back where no word can, an
/// UNKNOWN value (virtual machine 1) or none.
/// </summary>
internal static class RestoreRules
{
    // The coolant channel that a bare COOLANT addresses (virtual machine 2.5, F29).
    private const string DefaultCoolantChannel = "STANDARD";

    // TOLERANCE:ROTARY, the rotary tolerance (language 4.10).
    private const string RotaryAddress = "ROTARY";

    /// <summary>
    /// The state variable a state key names: the key with the resource id, coolant channel or function name its address
    /// resolves to, SPINDLE:S1, COOLANT:STANDARD, FUNC:SUB_CHUCK, or the key alone for a variable of the channel, F.
    /// Null, with an ERROR, for a key the restore stack does not keep or an address that does not resolve (virtual
    /// machine 3.8, 3.10).
    /// </summary>
    /// <param name="pseudoWord">@SAVE or @RESTORE, which the ERROR names.</param>
    /// <param name="key">The state key of the pseudo-word.</param>
    /// <param name="block">The generated block, which the diagnostics name.</param>
    /// <param name="state">The channel state.</param>
    /// <param name="resources">The resolution of roles, channels and functions of the run.</param>
    /// <param name="diagnostics">Where an ERROR goes.</param>
    public static string? Resolve(string pseudoWord, StateKeyValue key, Block block, ChannelState state,
        ResourceResolver resources, Diagnostics diagnostics)
    {
        // A state key names a state variable of the channel by the key that sets it, with the address that word takes
        // (virtual machine 3.10): a spindle role, a coolant channel, a function name, none for a variable of the
        // channel.
        // D202: the restore stack keeps SPINDLE (with the speed, as 3.10 shows), RPM, SPINDLE_MODE, CSS, VC, RPM_MAX,
        // COOLANT, FUNC, F, FEED_MODE, COMP and DIAMETER, and WORKPLANE, TCPM, POLAR, CYLINDER, ROTARY_PATH,
        // ROTARY_FEED and TOLERANCE (with TOLERANCE:ROTARY and TOLERANCE_MODE). TOOL, PRELOAD, OFFSET, CYCLE, ORIGIN,
        // SETPOS, the chain words, SPINDLE_SYNC, PHASE, ORIENT, WORKPIECE, UNITS, variables and positions stay VM681.
        // ORIGIN stays out on purpose: written again it empties the chain, so it would not bring back the frames that
        // Siemens SAVE restores. UNITS too: a restored UNITS would change the unit of values already on the stack.
        // Gap closed 2026-10-27 (delegated): TOLERANCE:ROTARY and TOLERANCE_MODE come back with TOLERANCE and are no
        // state keys of their own.
        switch (key.Key)
        {
            case "SPINDLE" or "RPM" or "CSS" or "VC" or "RPM_MAX":
                return VariableOf(key.Key, resources.ResolveSpindle(key.Addr, block, state, diagnostics));
            case "SPINDLE_MODE":
                return VariableOf(key.Key, resources.ResolveWorkSpindle(key.Addr, block, state, diagnostics));
            case "COOLANT":
                string channel = key.Addr ?? DefaultCoolantChannel;
                return resources.ResolveCoolantChannel(channel, block, state, diagnostics)
                    ? VariableOf(key.Key, channel)
                    : null;
            case "FUNC" when key.Addr is string function:
                return resources.ResolveFunction(function, block, state, diagnostics)
                    ? VariableOf(key.Key, function)
                    : null;
            case "F" or "FEED_MODE" or "COMP" or "DIAMETER" when key.Addr is null:
                return key.Key;
            case "WORKPLANE" or "TCPM" or "POLAR" or "CYLINDER" or "ROTARY_PATH" or "ROTARY_FEED" or "TOLERANCE"
                when key.Addr is null:
                return key.Key;
            default:
                diagnostics.Error(block, DiagnosticCodes.StateKeyNotKept,
                    $"{pseudoWord}={key.ToCanonical()} names no state variable the restore stack keeps; it keeps "
                    + "SPINDLE, RPM, SPINDLE_MODE, CSS, VC and RPM_MAX of a spindle, COOLANT of a channel, FUNC of a "
                    + "function, F, FEED_MODE, COMP, DIAMETER, WORKPLANE, TCPM, POLAR, CYLINDER, ROTARY_PATH, "
                    + "ROTARY_FEED and TOLERANCE (virtual machine 3.10, D202).");
                return null;
        }
    }

    /// <summary>
    /// The entry @SAVE pushes: the words that set the current value of the variable again, with the address of the
    /// state key as written, so that they address what it addressed, and the state keys that are UNKNOWN now; no word
    /// for a value that is none (virtual machine 3.10).
    /// </summary>
    public static RestoreEntry Save(StateKeyValue key, string variable, ChannelState state)
    {
        string? resource = ResourceOf(variable);
        var words = new List<Word>();
        var unknown = new List<string>();
        switch (key.Key)
        {
            case "SPINDLE":
                // A saved SPINDLE:MAIN=CW RPM:MAIN=1500 comes back as those two words (virtual machine 3.10).
                words.Add(WordOf(key, "SPINDLE", DirectionOf(state.Spindles[resource!].Direction)));
                AddNumber(words, unknown, key, "RPM", state.Spindles[resource!].Rpm, resource, state);
                break;
            case "RPM":
                AddNumber(words, unknown, key, "RPM", state.Spindles[resource!].Rpm, resource, state);
                break;
            case "SPINDLE_MODE":
                bool axisMode = state.Spindles[resource!].Mode == SpindleMode.Axis;
                words.Add(WordOf(key, "SPINDLE_MODE", axisMode ? "AXIS" : "SPINDLE"));
                break;
            case "CSS":
                words.Add(WordOf(key, "CSS", OnOrOff(state.Spindles[resource!].Css)));
                break;
            case "VC":
                AddNumber(words, unknown, key, "VC", state.Spindles[resource!].Vc, resource, state);
                break;
            case "RPM_MAX":
                AddNumber(words, unknown, key, "RPM_MAX", state.Spindles[resource!].RpmMax, resource, state);
                break;
            case "COOLANT":
                words.Add(WordOf(key, "COOLANT", OnOrOff(state.Coolant[resource!])));
                break;
            case "FUNC":
                if (state.Functions[resource!] is string functionState)
                {
                    words.Add(WordOf(key, "FUNC", functionState));
                }

                break;
            case "F":
                AddNumber(words, unknown, key, "F", state.Motion.Feed, null, state);
                break;
            case "FEED_MODE":
                words.Add(WordOf(key, "FEED_MODE", state.Motion.FeedMode == FeedMode.PerRev ? "PER_REV" : "PER_MIN"));
                break;
            case "COMP":
                words.Add(WordOf(key, "COMP", CompensationOf(state.Motion.Comp)));
                break;
            case "DIAMETER":
                words.Add(WordOf(key, "DIAMETER", OnOrOff(state.Frame.Diameter)));
                break;
            default:
                SaveFrameKey(words, unknown, key, state);
                break;
        }

        return new RestoreEntry(key, words) { Variable = variable, UnknownKeys = unknown };
    }

    // D202: the frame and path keys, each as the word that sets its current value.
    private static void SaveFrameKey(List<Word> words, List<string> unknown, StateKeyValue key, ChannelState state)
    {
        FrameState frame = state.Frame;
        switch (key.Key)
        {
            case "WORKPLANE":
                words.Add(WordOf(key, "WORKPLANE", frame.Workplane.ToString().ToUpperInvariant()));
                break;
            case "TCPM":
                words.Add(WordOf(key, "TCPM", OnOrOff(frame.Tcpm)));
                break;
            case "POLAR":
                words.Add(WordOf(key, "POLAR", OnOrOff(frame.Polar)));
                break;
            case "CYLINDER" when frame.Cylinder is null && !state.Unknown.Contains("CYLINDER"):
                words.Add(WordOf(key, "CYLINDER", "OFF"));
                break;
            case "CYLINDER":
                AddNumber(words, unknown, key, "CYLINDER", frame.Cylinder, null, state);
                break;
            case "ROTARY_PATH":
                words.Add(WordOf(key, "ROTARY_PATH", frame.RotaryPath == RotaryPath.Full ? "FULL" : "SHORTEST"));
                break;
            case "ROTARY_FEED":
                words.Add(WordOf(key, "ROTARY_FEED", frame.RotaryFeed == RotaryFeed.DegMin ? "DEG_MIN" : "MM_MIN"));
                break;
            case "TOLERANCE":
                SaveTolerance(words, unknown, key, state);
                break;
        }
    }

    // TOLERANCE comes back with TOLERANCE:ROTARY and TOLERANCE_MODE (D202); TOLERANCE=OFF alone when none is active,
    // since TOLERANCE_MODE without an active TOLERANCE is an ERROR (virtual machine 5).
    private static void SaveTolerance(List<Word> words, List<string> unknown, StateKeyValue key, ChannelState state)
    {
        ToleranceState tolerance = state.Frame.Tolerance;
        if (tolerance.Value is null && !state.Unknown.Contains("TOLERANCE"))
        {
            words.Add(WordOf(key, "TOLERANCE", "OFF"));
            return;
        }

        AddNumber(words, unknown, key, "TOLERANCE", tolerance.Value, null, state);
        if (state.Unknown.Contains(BlockContext.StateKey("TOLERANCE", RotaryAddress)))
        {
            unknown.Add(BlockContext.StateKey("TOLERANCE", RotaryAddress));
        }
        else if (tolerance.Rotary is decimal rotary)
        {
            words.Add(new Word
            {
                Key = "TOLERANCE",
                Addr = RotaryAddress,
                Value = NumberValues.Of(rotary),
                Definition = WordCatalog.Lookup("TOLERANCE"),
            });
        }

        words.Add(WordOf(key, "TOLERANCE_MODE", tolerance.Mode == ToleranceMode.Rough ? "ROUGH" : "FINISH"));
    }

    /// <summary>
    /// What @RESTORE puts back besides the words it re-applies: a state key that was UNKNOWN is UNKNOWN again (virtual
    /// machine 1), and a value that was none is none again, since no word sets none (virtual machine 2.2, 2.4, 2.5).
    /// </summary>
    public static void PutBack(RestoreEntry entry, ChannelState state)
    {
        foreach (string unknownKey in entry.UnknownKeys)
        {
            state.Unknown.Add(unknownKey);
        }

        // D202: a TOLERANCE saved without TOLERANCE:ROTARY has none again; TOLERANCE=v leaves the rotary value as it
        // stands, so it is cleared before the words are re-applied.
        if (entry.Key.Key == "TOLERANCE" && !HasRotaryTolerance(entry))
        {
            state.Frame.Tolerance = state.Frame.Tolerance with { Rotary = null };
            state.Unknown.Remove(BlockContext.StateKey("TOLERANCE", RotaryAddress));
        }

        if (entry.Words.Count > 0 || entry.UnknownKeys.Count > 0)
        {
            return;
        }

        string? resource = ResourceOf(entry.Variable);
        state.Unknown.Remove(BlockContext.StateKey(entry.Key.Key, resource));
        switch (entry.Key.Key)
        {
            case "VC":
                state.Spindles[resource!].Vc = null;
                break;
            case "RPM_MAX":
                state.Spindles[resource!].RpmMax = null;
                break;
            case "FUNC":
                state.Functions[resource!] = null;
                break;
            case "F":
                state.Motion.Feed = null;
                break;
        }
    }

    private static bool HasRotaryTolerance(RestoreEntry entry)
    {
        foreach (Word word in entry.Words)
        {
            if (word.Key == "TOLERANCE" && word.Addr == RotaryAddress)
            {
                return true;
            }
        }

        return entry.UnknownKeys.Contains(BlockContext.StateKey("TOLERANCE", RotaryAddress));
    }

    // A number of the state as the word that sets it. A value from an expression is UNKNOWN in STATIC mode and no word
    // sets it again, so its state key is kept instead (virtual machine 1); none gives no word.
    private static void AddNumber(List<Word> words, List<string> unknown, StateKeyValue key, string wordKey,
        decimal? number, string? resource, ChannelState state)
    {
        string stateKey = BlockContext.StateKey(wordKey, resource);
        if (state.Unknown.Contains(stateKey))
        {
            unknown.Add(stateKey);
            return;
        }

        if (number is decimal value)
        {
            words.Add(WordOf(key, wordKey, NumberValues.Of(value)));
        }
    }

    private static Word WordOf(StateKeyValue key, string wordKey, string identifier)
    {
        return WordOf(key, wordKey, new IdentValue(identifier));
    }

    private static Word WordOf(StateKeyValue key, string wordKey, Value value)
    {
        return new Word { Key = wordKey, Addr = key.Addr, Value = value, Definition = WordCatalog.Lookup(wordKey) };
    }

    // SPINDLE:S1, the key with the resource the address resolved to; null when it resolved to none.
    private static string? VariableOf(string key, string? resource)
    {
        return resource is null ? null : BlockContext.StateKey(key, resource);
    }

    // The resource id, coolant channel or function name of a variable, S1 of SPINDLE:S1; null for F.
    private static string? ResourceOf(string variable)
    {
        int colon = variable.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? null : variable.Substring(colon + 1);
    }

    private static string DirectionOf(SpindleDirection direction)
    {
        return direction switch
        {
            SpindleDirection.Clockwise => "CW",
            SpindleDirection.Counterclockwise => "CCW",
            _ => "OFF",
        };
    }

    private static string CompensationOf(Compensation compensation)
    {
        return compensation switch
        {
            Compensation.Left => "LEFT",
            Compensation.Right => "RIGHT",
            _ => "OFF",
        };
    }

    private static string OnOrOff(bool on)
    {
        return on ? "ON" : "OFF";
    }
}
