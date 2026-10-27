namespace Ncx.Core.Machine;

// D115: there is no kind key in 1.0. A catalog parameter that is not a word of language 4.7 takes a number or an
// expression, as a native parameter of D94 does, and the words of 4.7 keep their 4.7 kinds (AXIS the axis name). A
// per-entry `kinds` table (R/I/C/S, after Siemens uc.com) is reserved for later string parameters (machine-config 6).

/// <summary>
/// One entry of a cycle catalog: the NCX cycle name, the native cycle it maps to, the NCX words of its parameters with
/// their native names, and how the native cycle is written (machine-config 6, language 4.7.1). An entry comes from the
/// built-in drilling family, from the catalog file of the controller family or from a [[cycle]] of a machine file, and
/// each overrides the entry of the same name before it (machine-config 6).
/// </summary>
public sealed record CycleEntry
{
    // The words of language 4.7 that an entry answers for by name.
    private const string CycleFWord = "CYCLE_F";
    private const string CycleDwellWord = "CYCLE_DWELL";
    private const string AxisWord = "AXIS";
    private const string ContourWord = "CONTOUR";

    /// <summary>
    /// name: the NCX cycle name, the value of CYCLE in a program, "RECT_POCKET" (language 4.7, 4.7.1).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// native: the native cycle, "G81", "200" for CYCL DEF 200, "CYCLE81"; a cycle number is kept as its text.
    /// </summary>
    public required string Native { get; init; }

    /// <summary>
    /// for_axis: the drilling axis the entry maps its cycle under, "X" for the Fanuc lathe G87 to G89; null for the
    /// entry of the default axis. An entry is found by its name together with this key (machine-config 6, D160).
    /// </summary>
    public string? ForAxis { get; init; }

    /// <summary>
    /// params: the NCX word of each parameter and the native name that carries it, DEPTH = "Q201" (machine-config 6).
    /// </summary>
    public IReadOnlyDictionary<string, string> Params { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// absolute_from_surface: the words whose native value is relative to the surface while the NCX value is
    /// absolute, NCX = SURFACE + native, the DEPTH = Q203 + Q201 of Heidenhain (machine-config 6, heidenhain 5).
    /// </summary>
    public IReadOnlyList<string> AbsoluteFromSurface { get; init; } = [];

    /// <summary>
    /// modal: the native cycle stays active after its block and every following block with a position calls it
    /// again, as Fanuc G81..G89 and G90, G92, G94 do; false for a block that runs once where it stands (machine-config
    /// 6, language 4.7.1).
    /// </summary>
    public bool Modal { get; init; }

    /// <summary>
    /// contour: the native words that carry the contour of CONTOUR=name. Two are the first and the last block of the
    /// contour range (Fanuc P and Q), one names the contour subprogram (Siemens CYCLE95 NPP); empty for a cycle
    /// without a contour (machine-config 6, D65).
    /// </summary>
    public IReadOnlyList<string> Contour { get; init; } = [];

    /// <summary>
    /// signature: every native parameter in the order the control writes it, also those without an NCX word; empty
    /// for Fanuc entries, whose parameters are address words of the cycle block (machine-config 6).
    /// </summary>
    public IReadOnlyList<string> Signature { get; init; } = [];

    /// <summary>
    /// before: one native line that belongs to the cycle itself and stands directly before the cycle block, the
    /// Fanuc "M29 S{rpm}" that makes a G84 rigid tapping; the compiler writes it there and the reader folds it into
    /// the cycle (machine-config 6, D256). Null when the entry has no such line, and empty on a machine whose
    /// parameter makes the cycle so without one.
    /// </summary>
    public string? Before { get; init; }

    /// <summary>
    /// dwell_native: the native cycle the entry is written as where the block carries a CYCLE_DWELL other than 0,
    /// its own native having no dwell: the Fanuc mill G89 of REAM, whose G85 takes no P, so the P that params maps is
    /// the P of the G89; the reader reads that native back as this entry (D177). Null for an entry without one.
    /// </summary>
    public string? DwellNative { get; init; }

    /// <summary>
    /// channel: the channel whose compilation the [[cycle]] table of a machine file overrides the entry for, as the
    /// channel key of a function table does (machine-config 5, D56); the table of the compiled channel is taken first,
    /// then the one without the key (D361). Null on the entry for every channel and in a catalog file, which ignores
    /// the key with CFG157 (D377).
    /// </summary>
    public int? Channel { get; init; }

    /// <summary>
    /// available: false where the machine cannot run the cycle on the channel compiled, or on the whole machine
    /// without channel; the compiler reports CMP004 and writes nothing for it, no before line and no substitute
    /// (D377). True by default.
    /// </summary>
    public bool Available { get; init; } = true;

    /// <summary>
    /// fixed: native parameters the entry always writes with this value, which tell two entries of one native cycle
    /// apart, VARI = 1 for PECK on CYCLE83 (machine-config 6, controller-mapping 5).
    /// </summary>
    public IReadOnlyDictionary<string, decimal> Fixed { get; init; } = new Dictionary<string, decimal>();

    /// <summary>
    /// defaults: the value the compiler writes at a position of the signature that no NCX word and no fixed value
    /// fills, by native name, a number or the control's literal as text, Q208 = "MAX" (D164). On Siemens it records the
    /// control's own default of a position, which the reader assumes for an empty position while the compiler leaves
    /// that position empty (VARI of CYCLE83, D181). A distance the machine holds in a [cycles] key is no default
    /// (D358).
    /// </summary>
    public IReadOnlyDictionary<string, string> Defaults { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// title: the text the Heidenhain compiler writes after CYCL DEF n, "BOHREN"; null where the entry has none and
    /// the compiler writes the number alone, the control adding the name in its dialog language (D270).
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// pre, post, requires and restore of the entry, from which the expander makes generated blocks around the cycle
    /// block (machine-config 5a); null when the entry has none of the four keys.
    /// </summary>
    public ExpansionRule? Rule { get; init; }

    /// <summary>
    /// The controller family whose reader and compiler rules apply to the entry, the family of the catalog it belongs
    /// to; null for an entry of a machine file that names no controller. RuleWords follows from it (D157).
    /// </summary>
    public Controller? Family { get; init; }

    /// <summary>
    /// The NCX words that a rule of the reader and the compiler of the controller family carries instead of a native
    /// parameter. D157: they follow from the params of the entry, with no key of their own, for every entry, the
    /// built-in family included (controller-mapping 5): on Fanuc an entry that maps CLEARANCE = "R" carries SURFACE,
    /// SAFE and CYCLE_RETRACT, and one whose native is the tapping code (G84, the lathe G88; D359) PITCH as well; on
    /// Heidenhain an entry that maps SAFE = "Q204" carries CYCLE_RETRACT, on Siemens one that maps SAFE = "RTP". AXIS
    /// is a rule word of every entry that maps DEPTH, the depth along the drilling axis, and not AXIS itself.
    /// </summary>
    public IReadOnlyList<string> RuleWords => RuleWordsOf(Family, Params, Native);

    /// <summary>
    /// The keys of machine-config 6 and 5a that the file of the entry wrote, in file order; a later entry of the same
    /// name overrides exactly these (OverriddenBy). Empty for an entry built in code.
    /// </summary>
    public IReadOnlyList<string> WrittenKeys { get; init; } = [];

    /// <summary>
    /// The native name of CYCLE_F, the plunge feed of the cycle (language 4.7, D29); null when the entry does not map
    /// it. IsAddressWord says where it stands.
    /// </summary>
    public string? CycleF => NativeOf(CycleFWord);

    /// <summary>
    /// The native name of CYCLE_DWELL, the dwell at the bottom (language 4.7, D29); null when the entry does not map
    /// it.
    /// </summary>
    public string? CycleDwell => NativeOf(CycleDwellWord);

    /// <summary>
    /// The native name of AXIS, the drilling axis, Siemens _AXN (D59); null when the rule of the family takes the
    /// drilling axis from the plane or the tool axis (controller-mapping 5).
    /// </summary>
    public string? Axis => NativeOf(AxisWord);

    /// <summary>
    /// The NCX words of the entry: the words its params map, the words a rule of its family carries, and CONTOUR when
    /// it carries a contour (machine-config 6, language 4.7, D65).
    /// </summary>
    public IReadOnlyList<string> Words
    {
        get
        {
            // The words of the cycle block come from the native parameters or from a rule of the family, SURFACE of a
            // Fanuc drilling cycle is R minus the clearance (controller-mapping 5); a contour is named by CONTOUR=name
            // on the cycle block (language 4.7, D65).
            var words = new List<string>(Params.Keys);
            foreach (string word in RuleWords)
            {
                if (!words.Contains(word))
                {
                    words.Add(word);
                }
            }

            if (Contour.Count > 0 && !words.Contains(ContourWord))
            {
                words.Add(ContourWord);
            }

            return words;
        }
    }

    /// <summary>
    /// Whether the entry covers these native words of a source block: of two entries on one native cycle the reader
    /// takes the first in catalog order that does, so that a Fanuc lathe G83 with a peck depth Q is PECK and one
    /// without it DRILL (machine-config 6, D160). A word the params map, a word the entry fixes and a position of
    /// the signature, which the control takes whether or not an NCX word carries it, are all covered.
    /// </summary>
    /// <param name="nativeWords">The native names the source block carries beside the cycle code.</param>
    public bool CoversNativeWords(IEnumerable<string> nativeWords)
    {
        foreach (string word in nativeWords)
        {
            bool mapped = false;
            foreach (string native in Params.Values)
            {
                if (native == word)
                {
                    mapped = true;
                    break;
                }
            }

            if (!mapped && !Fixed.ContainsKey(word) && !Signature.Contains(word))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The native name that carries an NCX word, DEPTH to "Q201"; null for a word the entry does not map.
    /// </summary>
    /// <param name="word">The NCX word, DEPTH.</param>
    public string? NativeOf(string word)
    {
        return Params.TryGetValue(word, out string? native) ? native : null;
    }

    /// <summary>
    /// Whether a mapped word is written as an address word of its own rather than as a position of the signature:
    /// every parameter of a Fanuc entry, the modal F that Siemens CYCLE81..CYCLE83 use (machine-config 6); false for a
    /// word the entry does not map.
    /// </summary>
    /// <param name="word">The NCX word, CYCLE_F.</param>
    public bool IsAddressWord(string word)
    {
        // A native name in params that the signature does not list is an address word of its own; Fanuc entries leave
        // the signature out, because their parameters are address words of the cycle block (machine-config 6).
        string? native = NativeOf(word);
        return native is not null && !Signature.Contains(native);
    }

    /// <summary>
    /// The NCX value of a word from its native value: absolute for a word of absolute_from_surface, as it stands for
    /// every other word (machine-config 6).
    /// </summary>
    /// <param name="word">The NCX word, DEPTH.</param>
    /// <param name="native">The native value, Q201 = -20.</param>
    /// <param name="surface">The surface the native value is relative to, the NCX SURFACE (Q203, RFP).</param>
    public decimal ToNcx(string word, decimal native, decimal surface)
    {
        // Native Q201 is relative to Q203, NCX DEPTH is absolute: NCX = surface + native (machine-config 6; heidenhain
        // 5, CLEARANCE = Q203 + Q200; siemens 7, CLEARANCE = RFP + SDIS).
        return AbsoluteFromSurface.Contains(word) ? surface + native : native;
    }

    /// <summary>
    /// The native value of a word from its NCX value: relative to the surface for a word of absolute_from_surface, as
    /// it stands for every other word (machine-config 6).
    /// </summary>
    /// <param name="word">The NCX word, DEPTH.</param>
    /// <param name="ncx">The NCX value, DEPTH=-15.</param>
    /// <param name="surface">The NCX SURFACE the native value is written relative to.</param>
    public decimal ToNative(string word, decimal ncx, decimal surface)
    {
        // The compiler writes the relative value back, native = NCX - surface (machine-config 6, heidenhain 8.7).
        return AbsoluteFromSurface.Contains(word) ? ncx - surface : ncx;
    }

    /// <summary>
    /// This entry overridden by a later entry of the same name: the keys the later entry wrote replace those of this
    /// entry, the keys it left out stay (machine-config 6).
    /// </summary>
    /// <param name="entry">The later entry, from the catalog file or the machine file.</param>
    public CycleEntry OverriddenBy(CycleEntry entry)
    {
        // D140: key by key. A key the later entry writes replaces the earlier value (a table key such as params as a
        // whole), keys it leaves out stay, each of pre, post, requires and restore is its own key, and native stays
        // required on every entry (machine-config 6), so the Doosan PECK of name, native and pre keeps the address
        // words of G83.
        var writtenKeys = new List<string>(WrittenKeys);
        foreach (string key in entry.WrittenKeys)
        {
            if (!writtenKeys.Contains(key))
            {
                writtenKeys.Add(key);
            }
        }

        return new CycleEntry
        {
            Name = Name,
            ForAxis = ForAxis,
            Native = entry.Writes("native") ? entry.Native : Native,
            Params = entry.Writes("params") ? entry.Params : Params,
            AbsoluteFromSurface = entry.Writes("absolute_from_surface")
                ? entry.AbsoluteFromSurface
                : AbsoluteFromSurface,
            Modal = entry.Writes("modal") ? entry.Modal : Modal,
            Contour = entry.Writes("contour") ? entry.Contour : Contour,
            Signature = entry.Writes("signature") ? entry.Signature : Signature,
            Fixed = entry.Writes("fixed") ? entry.Fixed : Fixed,
            Defaults = entry.Writes("defaults") ? entry.Defaults : Defaults,
            Title = entry.Writes("title") ? entry.Title : Title,
            Before = entry.Writes("before") ? entry.Before : Before,
            DwellNative = entry.Writes("dwell_native") ? entry.DwellNative : DwellNative,
            Available = entry.Writes("available") ? entry.Available : Available,
            Rule = OverriddenRule(entry),
            Family = Family ?? entry.Family,
            WrittenKeys = writtenKeys,
        };
    }

    // D157: the rule words of the family from the params of the entry, in the order SURFACE, SAFE, CYCLE_RETRACT,
    // AXIS, PITCH (controller-mapping 5).
    private static List<string> RuleWordsOf(
        Controller? family, IReadOnlyDictionary<string, string> parameters, string native)
    {
        var words = new List<string>();
        bool Maps(string word, string name) => parameters.TryGetValue(word, out string? mapped) && mapped == name;
        if (family == Controller.Fanuc && Maps("CLEARANCE", "R"))
        {
            // R is the CLEARANCE plane; SURFACE is R minus the clearance or 0, SAFE the initial level of G98, and
            // CYCLE_RETRACT the G98 or G99 of the cycle block (controller-mapping 5).
            AddUnmapped(words, parameters, "SURFACE");
            AddUnmapped(words, parameters, "SAFE");
            words.Add("CYCLE_RETRACT");
        }
        else if ((family == Controller.Heidenhain && Maps("SAFE", "Q204"))
            || (family == Controller.Siemens && Maps("SAFE", "RTP")))
        {
            // Heidenhain: CYCLE_RETRACT = SAFE when Q204 > Q200, else CLEARANCE (D226, D388); Siemens: the cycle
            // always returns to RTP (controller-mapping 5).
            words.Add("CYCLE_RETRACT");
        }

        if (family is not null && parameters.ContainsKey("DEPTH"))
        {
            AddUnmapped(words, parameters, "AXIS");
        }

        // The tapping pitch of a Fanuc G84 (G88 on the lathe) is F / S under G94 and F under G95 (controller-mapping
        // 5, D157, D359).
        if (family == Controller.Fanuc && native is "G84" or "G88")
        {
            AddUnmapped(words, parameters, "PITCH");
        }

        return words;
    }

    private static void AddUnmapped(List<string> words, IReadOnlyDictionary<string, string> parameters, string word)
    {
        if (!parameters.ContainsKey(word))
        {
            words.Add(word);
        }
    }

    // Whether the file of the entry wrote a key of machine-config 6 or 5a.
    private bool Writes(string key)
    {
        return WrittenKeys.Contains(key);
    }

    // The four keys of the expansion rule override one by one like the other keys: a later post leaves the pre of this
    // entry in place (machine-config 5a, 6).
    private ExpansionRule? OverriddenRule(CycleEntry entry)
    {
        bool writesRule = entry.Writes("pre") || entry.Writes("post") || entry.Writes("requires")
            || entry.Writes("restore");
        if (!writesRule)
        {
            return Rule;
        }

        ExpansionRule earlier = Rule ?? new ExpansionRule();
        ExpansionRule later = entry.Rule ?? new ExpansionRule();
        return new ExpansionRule
        {
            Pre = entry.Writes("pre") ? later.Pre : earlier.Pre,
            Post = entry.Writes("post") ? later.Post : earlier.Post,
            Requires = entry.Writes("requires") ? later.Requires : earlier.Requires,
            Restore = entry.Writes("restore") ? later.Restore : earlier.Restore,
        };
    }
}
