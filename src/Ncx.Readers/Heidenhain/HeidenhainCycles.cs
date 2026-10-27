using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The machining cycles of a Klartext program (controllers heidenhain.md 5, 7 rules 7 and 9; controller-mapping 5;
/// language 4.7, 4.7.1; machine-config 6): a CYCL DEF becomes a CYCLE block with the coordinates along the tool axis
/// made absolute from Q203, CYCL CALL, M99 and CYCL CALL PAT become CYCLE_CALL blocks, a PATTERN DEF expands into one
/// call per point. The definition stays active until the next CYCL DEF; there is no cancel word, so the reader writes
/// CYCLE=OFF before the next non-cycle motion (HeidenhainReader) and the definition again before a later call.
/// </summary>
internal static partial class HeidenhainCycles
{
    /// <summary>
    /// Tells whether a cycle number is one of the cycles that act where they stand and are never called: 7, 8, 9, 10,
    /// 19, 32 and 247 (controllers heidenhain.md 2, 3, 5).
    /// </summary>
    /// <param name="number">The cycle number.</param>
    public static bool IsFrameCycle(int number)
    {
        return number is 7 or 8 or 9 or 10 or 19 or 32 or 247;
    }

    /// <summary>
    /// The number of the cycle acting where it stands that a CYCL DEF block defines, 7 of CYCL DEF 7.1; null for every
    /// other CYCL DEF, a cycle whose number the reader cannot read included, which replaces the active machining
    /// definition (controllers heidenhain.md 5).
    /// </summary>
    /// <param name="block">A CYCL DEF block.</param>
    public static int? FrameCycleOf(SourceBlock block)
    {
        return block.Words.Count > 2 && HeidenhainFrameCycles.NumberOf(block.Words[2], out int number, out _)
            && IsFrameCycle(number)
            ? number
            : null;
    }

    /// <summary>
    /// Keeps a CYCL DEF of a machining cycle as RAW (heidenhain 7 rule 9) and makes it the active definition all the
    /// same: the definition stays active until the next CYCL DEF (controllers heidenhain.md 5), so the CYCL CALL, M99
    /// and CYCL CALL PAT after it stay RAW with it and call no definition before it.
    /// </summary>
    /// <param name="block">The block being read, CYCL DEF with its words, all of them read.</param>
    /// <param name="native">The cycle number, "12" of CYCL DEF 12.0; the word after CYCL DEF as written where it is no
    /// cycle number, empty where there is none.</param>
    /// <param name="reason">Why the definition is kept as RAW.</param>
    public static void DefineAsRaw(HeidenhainBlock block, string native, string reason)
    {
        Activate(block, new HeidenhainDefinition { Native = native, RawReason = reason });
    }

    /// <summary>
    /// Reads CYCL DEF n of a machining cycle into its CYCLE block (heidenhain 7 rule 7).
    /// </summary>
    /// <param name="block">The block being read, CYCL DEF with its Q parameters.</param>
    /// <param name="number">The cycle number.</param>
    public static void ReadDefinition(HeidenhainBlock block, int number)
    {
        block.MarkAllRead();
        string native = number.ToString(CultureInfo.InvariantCulture);
        var parameters = new List<SourceWord>();
        foreach (SourceWord word in block.Source.Words)
        {
            if (word.Address.Length > 1 && word.Address[0] == 'Q' && char.IsAsciiDigit(word.Address[1]))
            {
                parameters.Add(word);
            }
        }

        // The reader turns the cycle into the entry of the catalog that maps it; of two entries of one cycle, DRILL
        // and DRILL_DWELL on cycle 200, PECK and CHIP_BREAK on cycle 203, the one its Q parameters name
        // (controller-mapping 5; machine-config 6; heidenhain 7 rule 7, D163).
        CycleEntry? first = block.Machine.CycleCatalog.FindNative(native);
        HeidenhainDefinition definition;
        if (first is null)
        {
            definition = new HeidenhainDefinition
            {
                Native = native,
                RawReason = $"cycle {native} has no entry in the cycle catalog of the machine (machine-config 6)",
            };
        }
        else if (ChosenEntry(block, first, parameters) is not CycleEntry entry)
        {
            definition = Native(block, native, parameters);
        }
        else
        {
            definition = Define(block, entry, native, parameters);
        }

        if (!Activate(block, definition))
        {
            return;
        }

        foreach (Word word in definition.Words)
        {
            block.Draft.AddState(word.Key, word.Addr, word.Value);
        }

        block.Heidenhain.CycleOn = true;
        block.Heidenhain.CycleCalled = false;
    }

    /// <summary>
    /// Reads CYCL CALL, the call at the current position, and CYCL CALL PAT, one call per point of the PATTERN DEF
    /// (heidenhain 7 rule 7). A bare CYCL CALL becomes a bare CYCLE_CALL: the reader writes the axis words the source
    /// block carries and no others (D220).
    /// </summary>
    /// <param name="block">The block being read, whose first words are CYCL CALL.</param>
    public static void ReadCall(HeidenhainBlock block)
    {
        block.MarkLeading(2);
        string form = block.Keyword(2);
        if (form == "POS")
        {
            // D189: the tool-axis coordinate of a CYCL CALL POS block is neither a depth nor a plain move but an
            // additional datum shift on the whole cycle, so the block has no CYCLE_CALL form (decisions.md).
            block.Draft.KeepAsRaw("heidenhain 7 rule 7 reads CYCL CALL, M99 and CYCL CALL PAT as calls; CYCL CALL POS "
                + "is kept as RAW");
            return;
        }

        if (form == "PAT")
        {
            block.MarkLeading(3);
            ReadPatternCall(block);
            return;
        }

        Call(block, block.Draft.Main);
    }

    /// <summary>
    /// Reads M99 in a positioning block, the call at the position of the block (controllers heidenhain.md 5): the rapid
    /// positioning in the plane is the CYCLE_CALL with its axis words (virtual machine 3.3); a positioning at the feed,
    /// in the machine frame or along the tool axis stays a motion of its own, and the bare CYCLE_CALL follows it.
    /// </summary>
    /// <param name="block">The block being read, after its motion.</param>
    public static void ReadM99(HeidenhainBlock block)
    {
        if (block.FindCode("M99") is not SourceWord code)
        {
            return;
        }

        block.MarkRead(code);
        if (block.Draft.IsRaw)
        {
            return;
        }

        HeidenhainDraftBlock main = block.Draft.Main;
        bool plane = block.Heidenhain.PlaneAxes(out _, out _, out string tool);
        bool inPlane = main.Verb == "RAPID" && plane && !main.Has("FRAME", null) && !main.Has(tool, null)
            && !main.Has("I" + tool, null);
        if (main.Verb is null || inPlane)
        {
            Call(block, main);
            return;
        }

        main.PositionsCall = true;
        var call = new HeidenhainDraftBlock();
        block.Draft.After.Insert(0, call);
        Call(block, call);
    }

    /// <summary>
    /// Reads PATTERN DEF with its points POS1 (X+25 Y+33.5 Z+0) ... for the CYCL CALL PAT after it (controllers
    /// heidenhain.md 5): the block writes nothing, its points are the calls. The other forms, and a pattern no CYCL
    /// CALL PAT calls, stay RAW (heidenhain 7 rule 9).
    /// </summary>
    /// <param name="block">The block being read, whose first words are PATTERN DEF.</param>
    public static void ReadPattern(HeidenhainBlock block)
    {
        block.MarkAllRead();
        HeidenhainState state = block.Heidenhain;
        state.Pattern = null;
        if (!CalledLater(block) || !state.PlaneAxes(out string first, out string second, out string tool))
        {
            block.Draft.KeepAsRaw("no CYCL CALL PAT calls this PATTERN DEF in a known working plane; PATTERN DEF "
                + "beyond the expanded calls is RAW (controllers heidenhain.md 7 rule 9)");
            return;
        }

        string body = PatternHead().Replace(block.Content, "");
        var points = new List<HeidenhainPatternPoint>();
        foreach (Match point in PatternPoint().Matches(body))
        {
            if (PointOf(point.Groups[1].Value, first, second, tool, out string? problem)
                is not HeidenhainPatternPoint read)
            {
                block.Draft.KeepAsRaw(problem!);
                return;
            }

            points.Add(read);
        }

        if (points.Count == 0 || PatternPoint().Replace(body, "").Trim().Length > 0)
        {
            block.Draft.KeepAsRaw(
                "a PATTERN DEF of other forms than POS points is RAW (controllers heidenhain.md 7 rule 9)");
            return;
        }

        state.Pattern = points;
    }

    /// <summary>
    /// Tells whether a CYCL CALL or M99 reads into a CYCLE_CALL of the active definition (Call): the definition is
    /// known and written as a CYCLE; one kept RAW keeps its calls RAW, and so does a call of no definition or of one a
    /// caller or a called subprogram defines (controllers heidenhain.md 5; heidenhain 7 rules 7 and 9).
    /// </summary>
    /// <param name="state">The source-side state of the reader.</param>
    public static bool WritesCall(HeidenhainState state)
    {
        return !state.DefinitionUnknown && state.Definition is { RawReason: null };
    }

    // A CYCL DEF of a machining cycle replaces the active definition, which stays active until the next CYCL DEF
    // (controllers heidenhain.md 5). One kept as RAW keeps its block RAW and writes no CYCLE block, so the NCX cycle
    // stays as it was, on or off; false for such a definition.
    private static bool Activate(HeidenhainBlock block, HeidenhainDefinition definition)
    {
        HeidenhainState state = block.Heidenhain;
        state.Definition = definition;
        state.DefinitionUnknown = false;
        block.State.ActiveCycle = definition.Native;
        if (definition.RawReason is string reason)
        {
            block.Draft.KeepAsRaw(reason);
            return false;
        }

        return true;
    }

    // The definition a CYCL CALL, an M99 or a CYCL CALL PAT calls, with the report every call without one owes its
    // source: the RAW of a definition the reader cannot know (virtual machine 3.9), the ERROR RDR302 and the RAW of a
    // call before any CYCL DEF (controllers heidenhain.md 5), and the RAW a definition kept as RAW passes to its calls
    // (heidenhain 7 rule 9). Every path that needs the definition asks here, so no call is ever dropped without a word
    // (D5; language 2 rule 8).
    private static HeidenhainDefinition? CalledDefinition(HeidenhainBlock block)
    {
        HeidenhainState state = block.Heidenhain;
        if (state.DefinitionUnknown)
        {
            block.Draft.KeepAsRaw("the cycle it calls is defined by a caller of the subprogram or in a called "
                + "subprogram, which the reader does not know (virtual machine 3.9)");
            return null;
        }

        if (state.Definition is not HeidenhainDefinition definition)
        {
            block.Diagnostics.Error(block.Line, DiagnosticCodes.HeidenhainCycleCallWithoutDefinition,
                "The block calls a cycle and no CYCL DEF of a machining cycle stands before it; the block is kept as "
                + "RAW (controllers heidenhain.md 5).");
            block.Draft.KeepAsRaw("no cycle is defined");
            return null;
        }

        if (definition.RawReason is not null)
        {
            block.Draft.KeepAsRaw(definition.Native.Length > 0
                ? $"the definition of cycle {definition.Native} it calls is kept as RAW"
                : "the CYCL DEF it calls is kept as RAW");
            return null;
        }

        return definition;
    }

    // A call of the active definition: where NCX has the cycle off, the definition is written again first, since the
    // control keeps it until the next CYCL DEF (controllers heidenhain.md 5; language 4.7). Afterwards the plane axes
    // stand at the call point and the drilling axis at the plane the cycle returns to (virtual machine 3.3).
    private static bool Call(HeidenhainBlock block, HeidenhainDraftBlock target)
    {
        HeidenhainState state = block.Heidenhain;
        if (CalledDefinition(block) is not HeidenhainDefinition definition)
        {
            return false;
        }

        if (state.CycleOn != true)
        {
            var again = new HeidenhainDraftBlock();
            foreach (Word word in definition.Words)
            {
                again.Add(word.Key, word.Addr, word.Value);
            }

            block.Draft.Before.Add(again);
            state.CycleOn = true;
        }

        target.WithVerb("CYCLE_CALL");
        state.CycleCalled = true;
        state.Tangent = null;
        if (state.PlaneAxes(out _, out _, out string tool))
        {
            string retract = FindWord(definition, "CYCLE_RETRACT")?.Value.ToCanonical() ?? "CLEARANCE";
            decimal? level = HeidenhainNumbers.NumberOf(FindWord(definition, retract)?.Value);
            if (level is decimal known)
            {
                block.State.SetPosition(tool, known);
            }
            else
            {
                block.State.ForgetPosition(tool);
            }
        }

        return true;
    }

    // CYCL CALL PAT calls the cycle at every point of the PATTERN DEF, one CYCLE_CALL per point (heidenhain 7 rule 7);
    // the feed between the points, CYCL CALL PAT F, has no NCX word, since a CYCLE_CALL positions at rapid (virtual
    // machine 3.3).
    private static void ReadPatternCall(HeidenhainBlock block)
    {
        block.Take("FMAX");
        HeidenhainState state = block.Heidenhain;
        if (block.Find("F") is not null)
        {
            block.Draft.KeepAsRaw("the feed between the points of CYCL CALL PAT has no NCX word; CYCLE_CALL positions "
                + "at rapid (virtual machine 3.3)");
            return;
        }

        if (state.Pattern is not List<HeidenhainPatternPoint> points
            || !state.PlaneAxes(out string first, out string second, out _))
        {
            block.Draft.KeepAsRaw("CYCL CALL PAT needs the points of a PATTERN DEF that the reader expands "
                + "(controllers heidenhain.md 7 rules 7 and 9)");
            return;
        }

        decimal active = 0m;
        for (int index = 0; index < points.Count; index++)
        {
            HeidenhainDraftBlock target = index == 0 ? block.Draft.Main : new HeidenhainDraftBlock();

            // D254: the surface of a position acts in addition to the Q203 of the cycle, so the CYCLE block is
            // written again before that point's call with SURFACE, CLEARANCE, DEPTH and SAFE shifted by it, and back
            // where the next point differs (language 4.7: a new CYCLE replaces all parameters).
            if (points[index].Surface != active)
            {
                if (Shifted(block, points[index].Surface) is not HeidenhainDraftBlock again)
                {
                    return;
                }

                AddCallBlock(block, index, again);
                active = points[index].Surface;
                state.CycleOn = true;
            }

            if (index > 0)
            {
                block.Draft.After.Add(target);
            }

            if (!Call(block, target))
            {
                return;
            }

            HeidenhainPoint point = points[index].Point;
            target.Add(first, HeidenhainNumbers.Of(point.First)).Add(second, HeidenhainNumbers.Of(point.Second));
            block.State.SetPosition(first, point.First);
            block.State.SetPosition(second, point.Second);
        }

        // The shifted CYCLE block is NCX's, not the control's: the definition of the control is unchanged, so the
        // shifted cycle is switched off and the next call writes the definition again (D254).
        if (active != 0m)
        {
            block.Draft.After.Add(new HeidenhainDraftBlock().Add("CYCLE", null, new IdentValue("OFF")));
            state.CycleOn = false;
            state.CycleCalled = false;
        }
    }

    // The block of an expanded pattern call stands in the main block for the first point and after it for the others.
    private static void AddCallBlock(HeidenhainBlock block, int index, HeidenhainDraftBlock written)
    {
        if (index == 0)
        {
            block.Draft.Before.Add(written);
        }
        else
        {
            block.Draft.After.Add(written);
        }
    }

    // The CYCLE block of the active definition with SURFACE, CLEARANCE, DEPTH and SAFE shifted by the surface of a
    // PATTERN DEF position (D254; controllers heidenhain.md 5); null where a shifted word is no number or the call has
    // no definition to shift, each of which keeps the pattern call RAW with its reason, since this stands before the
    // Call of the first point (D5).
    private static HeidenhainDraftBlock? Shifted(HeidenhainBlock block, decimal surface)
    {
        if (CalledDefinition(block) is not HeidenhainDefinition definition)
        {
            return null;
        }

        var written = new HeidenhainDraftBlock();
        foreach (Word word in definition.Words)
        {
            bool shifted = surface != 0m && word.Addr is null
                && word.Key is "SURFACE" or "CLEARANCE" or "DEPTH" or "SAFE";
            if (!shifted)
            {
                written.Add(word.Key, word.Addr, word.Value);
                continue;
            }

            if (HeidenhainNumbers.NumberOf(word.Value) is not decimal value)
            {
                block.Draft.KeepAsRaw($"the surface of a PATTERN DEF position shifts {word.Key} of the cycle, which "
                    + "is no number here (D254)");
                return null;
            }

            written.Add(word.Key, word.Addr, HeidenhainNumbers.Of(value + surface));
        }

        return written;
    }

    // D163: cycle 200 with Q211 = 0 is DRILL and with any other Q211 DRILL_DWELL with CYCLE_DWELL = Q211; cycle 203
    // with Q213 = 0 retracts fully after every infeed, which is PECK, and with a Q213 of at least the number of
    // infeeds, ceil(|Q201| / Q202), never retracts, which is CHIP_BREAK (decisions.md; heidenhain 7 rule 7). A Q213
    // between the two is the mixed form, which has no NCX cycle: null, so that the definition is written natively as
    // CYCLE:HEIDENHAIN=203 (D94). Every other entry is the one the catalog found.
    private static CycleEntry? ChosenEntry(HeidenhainBlock block, CycleEntry first, List<SourceWord> parameters)
    {
        switch (first.Name)
        {
            case "DRILL" or "DRILL_DWELL":
                decimal dwell = NumberOfParameter(block, parameters, first.NativeOf("CYCLE_DWELL")) ?? 0m;
                return Sibling(block, first, dwell == 0 ? "DRILL" : "DRILL_DWELL");

            case "PECK" or "CHIP_BREAK":
                decimal breaks = NumberOfParameter(block, parameters, "Q213") ?? 0m;
                if (breaks == 0)
                {
                    return Sibling(block, first, "PECK");
                }

                decimal? infeeds = Infeeds(block, first, parameters);
                return infeeds is decimal count && breaks >= count
                    ? Sibling(block, first, "CHIP_BREAK")
                    : null;

            default:
                return first;
        }
    }

    // The number of infeeds of a cycle 203, ceil(|Q201| / Q202); null when the definition gives neither a depth nor an
    // infeed depth to count with (D163).
    private static decimal? Infeeds(HeidenhainBlock block, CycleEntry entry, List<SourceWord> parameters)
    {
        decimal? depth = NumberOfParameter(block, parameters, entry.NativeOf("DEPTH"));
        decimal? peck = NumberOfParameter(block, parameters, entry.NativeOf("PECK"));
        return depth is decimal below && peck is decimal step && step > 0
            ? Math.Ceiling(Math.Abs(below) / step)
            : null;
    }

    // The entry of a name beside the one the catalog found, both being entries of the same native cycle; the one found
    // when the machine's catalog has no such name.
    private static CycleEntry? Sibling(HeidenhainBlock block, CycleEntry first, string name)
    {
        CycleEntry? named = block.Machine.CycleCatalog.Find(name);
        return named is not null && named.Native == first.Native ? named : first;
    }

    // The number a Q parameter of the definition carries, Q211 of a cycle 200; null for a parameter the definition
    // does not write or whose value is no plain number.
    private static decimal? NumberOfParameter(HeidenhainBlock block, List<SourceWord> parameters, string? name)
    {
        return FindParameter(parameters, name) is SourceWord word
            ? HeidenhainNumbers.NumberOf(ValueOf(block, word, out _))
            : null;
    }

    // The CYCLE block of a definition through its catalog entry. A cycle of the built-in drilling family, and a catalog
    // cycle whose every Q parameter the entry maps to a word of the language, is the catalog name with its words, the
    // coordinates along the tool axis made absolute: SURFACE = Q203, CLEARANCE = Q203 + Q200, DEPTH = Q203 + Q201, SAFE
    // = Q203 + Q204 (heidenhain 5; machine-config 6, absolute_from_surface). Any other cycle is written natively,
    // CYCLE:HEIDENHAIN=n with its Q parameters in source order (language 4.7.1, D94; cycles/heidenhain.toml).
    // TODO(question): the catalog maps cycles 204 to 209, 240, 241 and 251 to 257 only partly (wave-1 question #22),
    // and a catalog cycle's own parameter names, LENGTH and WIDTH of RECT_POCKET, are not words the parser knows
    // (wave-1 questions #36 and #59); such a definition is written natively.
    // D164: the reader takes the named entry only when every Q parameter no word of the entry carries holds what the
    // compiler would write for it (HeidenhainCycleMatch), so CAM output that differs only where the cycle does not use
    // a value still converts; otherwise it keeps the cycle as CYCLE:HEIDENHAIN=n (D94), as D5 requires (decisions.md).
    private static HeidenhainDefinition Define(HeidenhainBlock block, CycleEntry entry, string native,
        List<SourceWord> parameters)
    {
        bool family = DrillingFamily.Names.Contains(entry.Name);
        bool languageWords = true;
        foreach (string word in entry.Params.Keys)
        {
            languageWords &= WordCatalog.Lookup(word) is not null;
        }

        if ((!family && !languageWords)
            || HeidenhainCycleMatch.FirstDiffering(block.Machine, entry, parameters) is not null)
        {
            return Native(block, native, parameters);
        }

        var words = new List<Word> { new() { Key = "CYCLE", Value = new IdentValue(entry.Name) } };
        SourceWord? surfaceWord = FindParameter(parameters, entry.NativeOf("SURFACE"));
        Value? surface = surfaceWord is null ? null : ValueOf(block, surfaceWord, out _);
        foreach (KeyValuePair<string, string> parameter in entry.Params)
        {
            if (FindParameter(parameters, parameter.Value) is not SourceWord word)
            {
                continue;
            }

            Value? value = ValueOf(block, word, out string? problem);
            if (value is not null && entry.AbsoluteFromSurface.Contains(parameter.Key))
            {
                value = surface is null ? null : Absolute(block, surface, value, out problem);
                problem ??= $"{parameter.Value} is relative to the surface Q203, which the definition does not give";
            }

            if (value is null)
            {
                return new HeidenhainDefinition { Native = native, RawReason = problem };
            }

            words.Add(new Word { Key = parameter.Key, Value = value });
        }

        AddRetract(block, entry, parameters, words, family);
        return new HeidenhainDefinition { Native = native, Words = words };
    }

    // CYCLE_RETRACT is where the tool ends after each hole (language 4.7; controller-mapping 5): the tool returns to
    // SAFE = Q203 + Q204, the second set-up clearance, or to CLEARANCE (heidenhain 5).
    // D226: the second set-up clearance acts only when it is programmed greater than the first, so a Q204 from 0 up to
    // Q200 ends the tool at the set-up clearance and reads as CYCLE_RETRACT=CLEARANCE without SAFE, and only a Q204
    // greater than Q200 reads as SAFE with CYCLE_RETRACT=SAFE (decisions.md; heidenhain 7 rule 7). Where the two
    // cannot be compared, a Q204 or a Q200 that is no plain number, the Q204 stands and the retract is SAFE.
    private static void AddRetract(HeidenhainBlock block, CycleEntry entry, List<SourceWord> parameters,
        List<Word> words, bool family)
    {
        string? safe = entry.NativeOf("SAFE");
        if (!family && safe is null)
        {
            return;
        }

        bool toSafe = safe is not null && FindParameter(parameters, safe) is not null;
        if (toSafe && ActsAboveTheClearance(block, entry, parameters, safe!) == false)
        {
            toSafe = false;
            words.RemoveAll(word => word.Key == "SAFE" && word.Addr is null);
        }

        words.Add(new Word { Key = "CYCLE_RETRACT", Value = new IdentValue(toSafe ? "SAFE" : "CLEARANCE") });
    }

    // Whether the second set-up clearance Q204 is programmed greater than the set-up clearance Q200 and so acts at all
    // (D226); null where the two cannot be compared.
    private static bool? ActsAboveTheClearance(HeidenhainBlock block, CycleEntry entry, List<SourceWord> parameters,
        string safe)
    {
        decimal? second = NumberOfParameter(block, parameters, safe);
        decimal? first = NumberOfParameter(block, parameters, entry.NativeOf("CLEARANCE"));
        return second is decimal above && first is decimal clearance ? above > clearance : null;
    }

    // CYCLE:HEIDENHAIN=n with every Q parameter as written, in source order (language 4.7.1, D94).
    // D115: a native parameter takes a number or an expression (language 4.7.1, D94), and the control literals
    // (FMAX, FAUTO, FU, FZ, PREDEF, MAX) are allowed only in a catalog's native `defaults` text (D164), so a Q
    // parameter whose value is such a literal has no NCX value and the whole definition is kept as RAW (D5;
    // heidenhain 7 rule 9); no number is invented for it. The mixed cycle 203 that D163 sends here then does not
    // reach CYCLE:HEIDENHAIN=203: the Q208=MAX of docs/spec/examples/sources/BOHREN.h keeps its definition and five
    // calls RAW.
    private static HeidenhainDefinition Native(HeidenhainBlock block, string native, List<SourceWord> parameters)
    {
        var words = new List<Word>
        {
            new() { Key = "CYCLE", Addr = "HEIDENHAIN", Value = new IntegerValue(long.Parse(native,
                CultureInfo.InvariantCulture), native) },
        };
        foreach (SourceWord parameter in parameters)
        {
            if (ValueOf(block, parameter, out string? problem) is not Value value)
            {
                return new HeidenhainDefinition { Native = native, RawReason = problem };
            }

            words.Add(new Word { Key = parameter.Address, Value = value });
        }

        return new HeidenhainDefinition { Native = native, Words = words };
    }

    // The absolute coordinate from the surface and the relative value, NCX = SURFACE + native (machine-config 6).
    private static Value? Absolute(HeidenhainBlock block, Value surface, Value relative, out string? problem)
    {
        problem = null;
        if (HeidenhainNumbers.NumberOf(surface) is decimal from && HeidenhainNumbers.NumberOf(relative) is decimal by)
        {
            return HeidenhainNumbers.Of(from + by);
        }

        return HeidenhainExpression.Parse("(" + TextOf(surface) + ") + (" + TextOf(relative) + ")", block.Line,
            block.Diagnostics.File, out problem);
    }

    private static string TextOf(Value value)
    {
        return value is ExprValue expression ? expression.Text : value.ToCanonical();
    }

    private static Value? ValueOf(HeidenhainBlock block, SourceWord word, out string? problem)
    {
        Value? value = HeidenhainExpression.ValueOf(word.Text, block.Line, block.Diagnostics.File, out problem);
        problem = value is null ? $"{word.Address}={word.Text}: {problem}" : null;
        return value;
    }

    private static SourceWord? FindParameter(List<SourceWord> parameters, string? name)
    {
        return name is null ? null : parameters.Find(parameter => parameter.Address == name);
    }

    private static Word? FindWord(HeidenhainDefinition definition, string key)
    {
        foreach (Word word in definition.Words)
        {
            if (word.Key == key && word.Addr is null)
            {
                return word;
            }
        }

        return null;
    }

    // A point of a PATTERN DEF: the two axes of the plane, and on the tool axis the workpiece surface the position
    // starts at. D254: a surface in Z other than 0 acts in addition to the Q203 of the machining cycle, so the reader
    // keeps it with the point and shifts the cycle by it (decisions.md; controllers heidenhain.md 5).
    private static HeidenhainPatternPoint? PointOf(string coordinates, string first, string second, string tool,
        out string? problem)
    {
        problem = null;
        decimal? x = null;
        decimal? y = null;
        decimal surface = 0m;
        foreach (string coordinate in coordinates.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string axis = coordinate.Substring(0, 1).ToUpperInvariant();
            decimal? value = HeidenhainNumbers.Parse(coordinate.Substring(1));
            x = axis == first ? value : x;
            y = axis == second ? value : y;
            surface = axis == tool && value is decimal height ? height : surface;
            if (value is null || (axis != first && axis != second && axis != tool))
            {
                problem = $"{coordinate} of a PATTERN DEF point has no NCX form; the pattern stays RAW";
                return null;
            }
        }

        problem = x is null || y is null ? "a PATTERN DEF point needs both axes of the plane" : null;
        return x is decimal pointX && y is decimal pointY
            ? new HeidenhainPatternPoint(new HeidenhainPoint(pointX, pointY), surface)
            : null;
    }

    // A CYCL CALL PAT stands after the PATTERN DEF before the next one.
    private static bool CalledLater(HeidenhainBlock block)
    {
        for (SourceBlock? next = block.Heidenhain.NextBlock(block.Source); next is not null;
            next = block.Heidenhain.NextBlock(next))
        {
            string first = next.Words[0].Address;
            if (first == "PATTERN")
            {
                return false;
            }

            if (first == "CYCL" && next.Words.Count > 2 && next.Words[1].Address == "CALL"
                && next.Words[2].Address == "PAT")
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^PATTERN\s+DEF\s*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PatternHead();

    [GeneratedRegex(@"POS[0-9]+\s*\(([^)]*)\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PatternPoint();
}
