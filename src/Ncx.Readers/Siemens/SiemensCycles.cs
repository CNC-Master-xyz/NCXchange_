using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The cycles of a SINUMERIK program (controllers siemens.md 7, 11 rule 5; controller-mapping 5; language 4.7;
/// machine-config 6): a cycle call with its positional parameters through the entry of the cycle catalog into a CYCLE
/// block with the coordinates along the drilling axis absolute, SAFE the RTP it returns to, _AXN as AXIS, the modal F
/// as CYCLE_F; MCALL CYCLE81(...) makes it modal and every following block with a position calls it, CYCLE_CALL, MCALL
/// alone is CYCLE=OFF; a direct CYCLE81(...) is CYCLE= and one CYCLE_CALL at the current position; the patterns HOLES1
/// and HOLES2 are SiemensPatterns.
/// </summary>
internal static partial class SiemensCycles
{
    private const decimal MillimetresPerInch = 25.4m;

    // The trailing mode integers the 840D sl added; omitted they mean the old behaviour (controllers siemens.md 7).
    private static readonly string[] s_modes = ["_GMODE", "_DMODE", "_AMODE"];

    /// <summary>
    /// Reads MCALL and the cycle calls of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        if (block.Take("MCALL") is not null)
        {
            ReadModalCall(block);
            return;
        }

        if (FiresModalCall(block))
        {
            CallWithoutMotion(block);
            return;
        }

        foreach (SourceWord word in block.Unread())
        {
            if (!word.Text.StartsWith('('))
            {
                continue;
            }

            if (word.Address is "HOLES1" or "HOLES2")
            {
                SiemensPatterns.Read(block, word);
                return;
            }

            if (word.Address is "CYCLE801" or "CYCLE802")
            {
                // D341: the full sl signatures of CYCLE801 and CYCLE802 are in siemens 7, and the reader expands them
                // into CYCLE_CALL blocks as it expands HOLES1 and HOLES2 (decisions.md).
                SiemensPatterns.Read(block, word);
                return;
            }

            if (block.Machine.CycleCatalog.FindNative(word.Address) is not null)
            {
                ReadDirectCall(block, word);
                return;
            }
        }
    }

    /// <summary>
    /// A block with a position under MCALL calls the modal cycle there: CYCLE_CALL with its axis words (controllers
    /// siemens.md 7; controller-mapping 5, CYCLE_CALL).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="axes">The axis words of the block.</param>
    public static void CallAt(SiemensBlock block, List<SourceWord> axes)
    {
        SiemensCycle cycle = block.Facts.ModalCycle!;
        if (block.MachineFrame is not null)
        {
            block.Draft.KeepAsRaw("a position in the machine frame under MCALL is not read as a call of the cycle");
            return;
        }

        SiemensDraftBlock main = block.Draft.Main.WithVerb("CYCLE_CALL");
        foreach (SourceWord word in axes)
        {
            if (!SiemensMotion.AddAxis(block, main, word))
            {
                return;
            }
        }

        // The F of a position block is the feed of the control, which the cycle and the next feed motion use
        // (controller-mapping 5, CYCLE_F); an F with no NCX value keeps the block RAW (D5), whose F then leaves the
        // feed of the control unknown (SiemensReader.Write).
        if (block.Take("F") is SourceWord feed)
        {
            if (SiemensExpression.ValueOf(block, feed.Text, out string? problem) is not Value value)
            {
                block.Draft.KeepAsRaw(problem!);
                return;
            }

            value = SiemensMotion.FeedInNcxUnits(block, value, feed); // D292: a G70/G71 cycle feed as ControlFeed
            block.Facts.ControlFeed = value;
            cycle = WithFeed(block, cycle, value, block.Draft.Before);
        }

        Returned(block, cycle);
    }

    // D387: an F on a position block under MCALL is both the feed of the cycle and the path feed: a cycle that takes
    // the modal F as its CYCLE_F drills this hole and the later ones of the range with it, so the CYCLE block stands
    // again with that CYCLE_F, all other words unchanged, directly before the CYCLE_CALL. A cycle with a feed of its
    // own (CYCLE85 FFR, CYCLE84) takes only the path feed.
    // Gap closed 2026-10-27 (delegated, code wave 2): an F equal to the CYCLE_F the cycle has already writes no CYCLE
    // block again, since nothing changes (N11: the compiler writes the F on the position block, X.. Y.. F..); an F
    // with an NCX expression value, F=R1, is re-emitted as CYCLE_F={$R1} as well, which the compiler writes back as
    // F=R1 on the position block, rather than keeping the old CYCLE_F (which loses the feed) or the block RAW.
    private static SiemensCycle WithFeed(SiemensBlock block, SiemensCycle cycle, Value feed,
        List<SiemensDraftBlock> target)
    {
        CycleEntry? entry = null;
        if (cycle.Words.Count > 0 && cycle.Words[0].Value is IdentValue name)
        {
            entry = block.Machine.CycleCatalog.Find(name.Name);
        }

        if (entry is null || !entry.IsAddressWord("CYCLE_F"))
        {
            return cycle;
        }

        var words = new List<Word>();
        bool replaced = false;
        foreach (Word word in cycle.Words)
        {
            if (word.Key == "CYCLE_F")
            {
                if (word.Value.ToCanonical() == feed.ToCanonical())
                {
                    return cycle;
                }

                words.Add(word with { Value = feed });
                replaced = true;
            }
            else
            {
                words.Add(word);
            }
        }

        if (!replaced)
        {
            words.Add(new Word { Key = "CYCLE_F", Value = feed });
        }

        var definition = new SiemensDraftBlock();
        foreach (Word word in words)
        {
            definition.Add(word.Key, word.Addr, word.Value);
        }

        target.Add(definition);
        SiemensCycle redefined = cycle with { Words = words };
        block.Facts.ModalCycle = redefined;
        return redefined;
    }

    /// <summary>
    /// True where the MCALL of the next block of the unit arms a cycle for a pattern the reader cannot expand: the F
    /// of this block is the drilling feed of every hole of that pattern and stays RAW with the whole modal range, and
    /// the compiler writes no bare F between an armed MCALL and the pattern, because an F with G0 or G1 active fires
    /// the modal cycle (controllers siemens.md 7; D259).
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static bool NextArmsRawRange(SiemensBlock block)
    {
        SourceBlock? next = block.Siemens.Units.NextInUnit(block.Source);
        return next is not null && HasMcall(next) && RangeKeepsAPattern(block, next);
    }

    // True where the modal range an MCALL block arms holds a pattern the reader cannot expand: a look-ahead within
    // the unit up to the next MCALL, which ends or replaces the modal call (controllers siemens.md 7; D259).
    private static bool RangeKeepsAPattern(SiemensBlock block, SourceBlock mcall)
    {
        SourceBlock? next = block.Siemens.Units.NextInUnit(mcall);
        while (next is not null && !HasMcall(next))
        {
            foreach (SourceWord word in next.Words)
            {
                if (word.Address is "HOLES1" or "HOLES2" or "CYCLE801" or "CYCLE802" && word.Text.StartsWith('(')
                    && !SiemensPatterns.CanExpand(word))
                {
                    return true;
                }
            }

            next = block.Siemens.Units.NextInUnit(next);
        }

        return false;
    }

    // True where this bare MCALL ends a modal range the reader kept RAW: a look-back within the unit to the MCALL
    // block before it, which must arm a call, and the same look-ahead over the range between the two (D259).
    private static bool EndsRawRange(SiemensBlock block)
    {
        SourceBlock? armed = block.Siemens.Units.PreviousInUnit(block.Source);
        while (armed is not null && !HasMcall(armed))
        {
            armed = block.Siemens.Units.PreviousInUnit(armed);
        }

        return armed is not null && Arms(armed) && RangeKeepsAPattern(block, armed);
    }

    // True where an MCALL block arms a call, MCALL CYCLE81(...), and does not end one.
    private static bool Arms(SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address is not ("N" or ":" or "MCALL"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMcall(SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "MCALL")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the next block defines a cycle that takes the modal F as its feed, MCALL CYCLE81(...) or a direct
    /// call: the F before it is the CYCLE_F (controller-mapping 5, CYCLE_F).
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static bool DefinesNext(SiemensBlock block)
    {
        SourceBlock? next = block.Siemens.Units.NextInUnit(block.Source);
        if (next is null)
        {
            return false;
        }

        foreach (SourceWord word in next.Words)
        {
            if (word.Text.StartsWith('(') && block.Machine.CycleCatalog.FindNative(word.Address) is CycleEntry entry
                && entry.CycleF == "F" && entry.IsAddressWord("CYCLE_F"))
            {
                return true;
            }
        }

        return false;
    }

    // MCALL CYCLE81(...) makes the cycle modal, MCALL alone ends it; MCALL with another subprogram has no NCX word
    // (controllers siemens.md 7; controller-mapping 5, CYCLE_CALL and CYCLE=OFF).
    private static void ReadModalCall(SiemensBlock block)
    {
        SourceWord? call = null;
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address is not ("N" or ":"))
            {
                call = word;
                break;
            }
        }

        SiemensFacts facts = block.Facts;
        if (call is null)
        {
            // D259: the MCALL alone that ends a modal range the reader kept RAW stays RAW with it, so that the
            // pattern and its drilling cycle travel together; a look-back within the unit finds the MCALL that armed
            // the range. A bare MCALL under a caller the reader does not know ends no range it kept RAW and stays the
            // lossless CYCLE=OFF (controllers siemens.md 7, 11 rule 5; D259).
            if (EndsRawRange(block))
            {
                block.Draft.KeepAsRaw("MCALL alone ends the modal call of a pattern the reader kept as RAW, and the "
                    + "range travels together (controllers siemens.md 7; D259)");
                return;
            }

            block.Draft.AddState("CYCLE", null, new IdentValue("OFF"));
            facts.ModalCycle = null;
            facts.Unknown.Remove(SiemensFacts.Cycle);
            return;
        }

        block.MarkRead(call);
        if (!call.Text.StartsWith('(') || block.Machine.CycleCatalog.FindNative(call.Address) is null)
        {
            block.Draft.KeepAsRaw($"MCALL {call.Address} makes a subprogram modal, which NCX has no word for; "
                + "CYCLE_CALL calls a cycle (controllers siemens.md 7)");
            return;
        }

        // D259: a pattern the reader cannot expand keeps the whole modal range RAW, since RAW keeps source text NCX
        // cannot express (D5) and here that text is the MCALL range, not the pattern alone.
        if (RangeKeepsAPattern(block, block.Source))
        {
            block.Draft.KeepAsRaw($"MCALL {call.Address} arms the cycle of a pattern the reader cannot expand, and "
                + "the pattern drills nothing without it (controllers siemens.md 7; D259)");
            facts.MakeUnknown(SiemensFacts.Cycle);
            return;
        }

        if (Define(block, call, modal: true) is not SiemensCycle cycle)
        {
            return;
        }

        foreach (Word word in cycle.Words)
        {
            block.Draft.Main.Add(word.Key, word.Addr, word.Value);
        }

        facts.ModalCycle = cycle;
        facts.Unknown.Remove(SiemensFacts.Cycle);
    }

    // CYCLE81(...) alone calls the cycle once at the current position: CYCLE=, a CYCLE_CALL and CYCLE=OFF, since only
    // MCALL is modal (controller-mapping 5, CYCLE_CALL; D224).
    private static void ReadDirectCall(SiemensBlock block, SourceWord call)
    {
        block.MarkRead(call);
        if (Define(block, call, modal: false) is not SiemensCycle cycle)
        {
            return;
        }

        foreach (Word word in cycle.Words)
        {
            block.Draft.Main.Add(word.Key, word.Addr, word.Value);
        }

        block.Draft.After.Add(new SiemensDraftBlock().WithVerb("CYCLE_CALL"));
        Returned(block, cycle);

        // A direct call leaves an armed MCALL armed (SW4.92 p.539: only MCALL alone or another MCALL ends it), so
        // inside a modal range the CYCLE block of the modal cycle stands again after the call instead of CYCLE=OFF
        // (D224 for a cycle called once; D387 for the definition written again).
        if (block.Facts.ModalCycle is SiemensCycle modal)
        {
            var definition = new SiemensDraftBlock();
            foreach (Word word in modal.Words)
            {
                definition.Add(word.Key, word.Addr, word.Value);
            }

            block.Draft.After.Add(definition);
            return;
        }

        block.Draft.After.Add(new SiemensDraftBlock().Add("CYCLE", new IdentValue("OFF")));
    }

    // SW4.92 p.540: under an armed MCALL a block that programs F or S with G0 or G1 active calls the modal cycle at the
    // position reached, without any path motion. Such a block with no position becomes a CYCLE_CALL there, after its
    // own F or S, with the CYCLE block written again where the F changes the CYCLE_F (D387).
    private static bool FiresModalCall(SiemensBlock block)
    {
        if (block.Facts.ModalCycle is null || block.Facts.MotionCode is not ("G0" or "G1")
            || block.Facts.IsUnknown(SiemensFacts.Motion))
        {
            return false;
        }

        bool programs = false;
        foreach (SourceWord word in block.Source.Words)
        {
            if (word.Address == "F" || SiemensSpindles.IsSpeed(word))
            {
                programs = true;
            }
        }

        if (!programs)
        {
            return false;
        }

        foreach (SourceWord word in block.Unread())
        {
            if (word.Address is not ("N" or ":" or "F" or "FB"))
            {
                return false;
            }
        }

        return true;
    }

    private static void CallWithoutMotion(SiemensBlock block)
    {
        SiemensCycle cycle = block.Facts.ModalCycle!;
        if (block.MachineFrame is not null)
        {
            return;
        }

        if (block.Find("F") is SourceWord feed)
        {
            if (SiemensExpression.ValueOf(block, feed.Text, out string? problem) is not Value value)
            {
                block.Draft.KeepAsRaw(problem!);
                return;
            }

            cycle = WithFeed(block, cycle, value, block.Draft.After);
        }

        block.Draft.After.Add(new SiemensDraftBlock().WithVerb("CYCLE_CALL"));
        Returned(block, cycle);
    }

    // The program units of the block: UNITS=INCH after G70 or G700 (controller-mapping 1, UNITS).
    private static bool Inch(SiemensBlock block)
    {
        return block.Facts.Written.TryGetValue("UNITS", out string? units) && units == "INCH";
    }

    /// <summary>
    /// The CYCLE block of a call through its catalog entry: CYCLE=name, the words its parameters map, CLEARANCE = RFP
    /// + SDIS, DEPTH = DP or RFP + DPR, SAFE = RTP with CYCLE_RETRACT=SAFE, AXIS from _AXN, CYCLE_F from the modal F;
    /// null, with the block kept RAW, where the reader cannot read the call (controller-mapping 5; machine-config 6).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="call">The call word with its arguments.</param>
    /// <param name="modal">True for MCALL, whose positions come from the blocks that follow.</param>
    public static SiemensCycle? Define(SiemensBlock block, SourceWord call, bool modal)
    {
        CycleCatalog catalog = block.Machine.CycleCatalog;
        List<string> arguments = SiemensArguments.Split(call.Text);
        IReadOnlyList<string> signature = catalog.FindNative(call.Address)!.Signature;
        var numbers = new Dictionary<string, decimal>(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Count && index < signature.Count; index++)
        {
            if (SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[index])) is decimal number)
            {
                numbers[signature[index]] = number;
            }
        }

        // D164, D181: a position the source leaves empty takes the control's default, which the entry records in
        // defaults where the reader needs it to tell two entries apart (VARI of CYCLE83 is 0); the mode integers are
        // read only where an entry fixes them, the depth mode of CYCLE830 and SLOT1 included (D379, D380), and a call
        // that sets any other mode stays RAW.
        CycleEntry? entry = catalog.FindNative(call.Address, Selecting(call.Address, numbers));
        string? problem = entry is null
            ? NoEntry(catalog, call.Address)
            : arguments.Count > entry.Signature.Count
                ? $"{call.Address} has more parameters than the signature of its catalog entry"
                : ModeSet(entry, arguments) ?? NativeOnly(block, entry, arguments, modal);
        if (problem is not null)
        {
            block.Draft.KeepAsRaw(problem);
            return null;
        }

        var reading = new CycleReading(block, entry!, arguments,
            DepthIsIncremental(call.Address, numbers));
        return reading.Words();
    }

    // Why no entry of the native cycle fits the call: the values its fixed values ask for, VARI of CYCLE83, NUM and
    // _DMODE of SLOT1 (machine-config 6, fixed).
    private static string NoEntry(CycleCatalog catalog, string native)
    {
        var forms = new List<string>();
        foreach (CycleEntry entry in catalog.Entries)
        {
            if (entry.Native != native || entry.Fixed.Count == 0)
            {
                continue;
            }

            var values = new List<string>();
            foreach (KeyValuePair<string, decimal> fixedValue in entry.Fixed)
            {
                values.Add(fixedValue.Key + "=" + fixedValue.Value.ToString(CultureInfo.InvariantCulture));
            }

            forms.Add(entry.Name + " " + string.Join(" ", values));
        }

        return $"{native} sets none of the values that select an entry of the cycle catalog "
            + $"({string.Join("; ", forms)}) (machine-config 6; D181)";
    }

    // The drilling axis stands at the plane the cycle returns to after a call (virtual machine 3.3).
    private static void Returned(SiemensBlock block, SiemensCycle cycle)
    {
        if (cycle.ReturnPlane is decimal plane)
        {
            block.State.SetPosition(cycle.Axis, plane);
        }
        else
        {
            block.State.ForgetPosition(cycle.Axis);
        }

        block.Facts.Tangent = null;
    }

    // A mode integer the entry does not fix and the call sets: the reader keeps it RAW (D181).
    private static string? ModeSet(CycleEntry entry, List<string> arguments)
    {
        foreach (string mode in s_modes)
        {
            int index = IndexOf(entry.Signature, mode);
            decimal? value = index >= 0 && index < arguments.Count && arguments[index].Length > 0
                ? SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[index]))
                : 0;
            if (value != 0 && !entry.Fixed.ContainsKey(mode) && !ModeAccepted(entry.Native, mode, value))
            {
                return $"{mode} of {entry.Native} sets a mode that no NCX word carries (D181)";
            }
        }

        return null;
    }

    private static int IndexOf(IReadOnlyList<string> list, string name)
    {
        for (int index = 0; index < list.Count; index++)
        {
            if (list[index] == name)
            {
                return index;
            }
        }

        return -1;
    }

    // The reading of one call against its entry.
    private sealed class CycleReading(SiemensBlock block, CycleEntry entry, List<string> arguments, bool incremental)
    {
        private readonly List<Word> _words = [new() { Key = "CYCLE", Value = new IdentValue(entry.Name) }];
        private readonly HashSet<string> _used = new(StringComparer.Ordinal);

        public SiemensCycle? Words()
        {
            Value? surface = Argument("SURFACE");
            if (surface is null)
            {
                return Raw($"{entry.Native} gives no reference plane RFP, which its coordinates count from");
            }

            foreach (KeyValuePair<string, string> parameter in entry.Params)
            {
                if (!Add(parameter.Key, parameter.Value, surface))
                {
                    return null;
                }
            }

            // The cycle always returns to RTP: CYCLE_RETRACT=SAFE with SAFE = RTP (controller-mapping 5); D157: an
            // entry that maps SAFE to RTP carries CYCLE_RETRACT as a rule word.
            if (entry.NativeOf("SAFE") == "RTP")
            {
                _words.Add(new Word { Key = "CYCLE_RETRACT", Value = new IdentValue("SAFE") });
            }

            WarnNotCarried();
            string axis = AxisName() ?? SiemensPlane.Of(block.Facts.WorkingPlane).Tool;
            return new SiemensCycle
            {
                Native = entry.Native,
                Words = _words,
                Axis = axis,
                ReturnPlane = SiemensNumbers.NumberOf(Find("SAFE")),
            };
        }

        private bool Add(string word, string native, Value surface)
        {
            _used.Add(native);
            if (word == "CYCLE_F" && entry.IsAddressWord(word))
            {
                // The modal F before the call is the feed of the cycle (controller-mapping 5, CYCLE_F; D29).
                if (block.Facts.ControlFeed is Value feed)
                {
                    _words.Add(new Word { Key = word, Value = feed });
                }

                return true;
            }

            Value? value = word == "SURFACE" ? surface : Argument(word);
            string? problem = null;
            if (word == "DEPTH" && value is null)
            {
                value = Relative(surface, out problem);
            }
            else if (word == "DEPTH" && incremental && value is not null)
            {
                // D380, D379: an incremental _DP counts from RFP away from the retract plane, as DPR does (D334).
                value = Shift(surface, SiemensNumbers.NumberOf(value) is decimal amount
                    ? SiemensNumbers.Of(Math.Abs(amount))
                    : value, towardsTheRetractPlane: false);
                if (value is null)
                {
                    return false;
                }
            }
            else if (word == "PITCH" && value is null && !MetricPitch(out value))
            {
                return false;
            }

            if (value is null)
            {
                if (word is "CLEARANCE" or "SAFE" or "DEPTH")
                {
                    Raw(problem
                        ?? $"{entry.Native} leaves {native} empty, whose default the documents do not give (D181)");
                    return false;
                }

                return true;
            }

            if (entry.AbsoluteFromSurface.Contains(word))
            {
                value = Shift(surface, value, towardsTheRetractPlane: true);
            }
            else if (word == "AXIS")
            {
                value = AxisValue(value);
            }
            else if (word == "CYCLE_DWELL")
            {
                value = Dwell(value);
            }

            if (value is null)
            {
                return false;
            }

            _words.Add(new Word { Key = word, Value = value });
            return true;
        }

        // DEPTH is DP, or RFP less the unsigned DPR, which counts from the reference plane away from the retract
        // plane (controller-mapping 5; D334).
        private Value? Relative(Value surface, out string? problem)
        {
            problem = null;
            string name = IndexOf(entry.Signature, "DPR") >= 0 ? "DPR" : "_DPR";
            int index = IndexOf(entry.Signature, name);
            if (index < 0 || index >= arguments.Count || arguments[index].Length == 0)
            {
                return null;
            }

            _used.Add(name);
            Value? relative = SiemensExpression.ValueOf(block, arguments[index], out problem);
            return relative is null ? null : Shift(surface, relative, towardsTheRetractPlane: false);
        }

        // _AXN 1, 2, 3 are the first, second and third geometry axis of the plane (controller-mapping 5, AXIS; D59).
        private IdentValue? AxisValue(Value value)
        {
            var plane = SiemensPlane.Of(block.Facts.WorkingPlane);
            string? axis = SiemensNumbers.NumberOf(value) switch
            {
                1 => plane.First,
                2 => plane.Second,
                3 => plane.Tool,
                _ => null,
            };
            if (axis is null)
            {
                Raw($"_AXN of {entry.Native} names no geometry axis 1, 2 or 3 (controller-mapping 5, AXIS)");
                return null;
            }

            return new IdentValue(axis);
        }

        // MPIT is the ISO metric thread size, whose ISO 261 coarse pitch is PITCH, the sign giving the hand as the sign
        // of PIT does (D181; DrillingFamily.IsoCoarsePitch; SW4.92 p.1089); false, with the block RAW, for a size
        // that is no ISO coarse size. True with no value where the call gives neither.
        private bool MetricPitch(out Value? value)
        {
            value = null;
            int index = IndexOf(entry.Signature, "MPIT");
            if (index < 0 || index >= arguments.Count || arguments[index].Length == 0)
            {
                return true;
            }

            _used.Add("MPIT");
            if (SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[index])) is not decimal size
                || DrillingFamily.IsoCoarsePitch(size) is not decimal pitch)
            {
                Raw($"MPIT={arguments[index]} of {entry.Native} is no ISO metric coarse thread size M3 to M48 (D181)");
                return false;
            }

            // The coarse pitch is millimetres in an inch program too (_PITA 0, SW4.92 p.1130), PITCH program units.
            decimal inProgramUnits = Inch(block) ? Math.Round(pitch / MillimetresPerInch, 6) : pitch;
            value = SiemensNumbers.Of(size < 0 ? -inProgramUnits : inProgramUnits);
            return true;
        }

        // The dwell is positive seconds, negative revolutions, which the reader converts with the speed it knows
        // (controller-mapping 5, CYCLE_DWELL).
        private Value? Dwell(Value value)
        {
            if (SiemensNumbers.NumberOf(value) is not decimal dwell || dwell >= 0)
            {
                return value;
            }

            // D380: the thousands digit of the CYCLE830 _AMODE, not the sign, says seconds or revolutions of _DTD
            // (SW4.92 p.1123); the reader takes only _AMODE 0 or 1, seconds, where a negative dwell means nothing.
            if (entry.Native == "CYCLE830")
            {
                Raw($"{entry.CycleDwell} of CYCLE830 is negative, which in seconds is no dwell (SW4.92 p.1123)");
                return null;
            }

            string? role = SiemensSpindles.MasterRole(block);
            if (role is null || !block.Facts.Rpm.TryGetValue(role, out decimal rpm) || rpm <= 0)
            {
                Raw($"{entry.CycleDwell} of {entry.Native} dwells by revolutions of a spindle whose speed the reader "
                    + "does not know");
                return null;
            }

            return SiemensNumbers.Of(Math.Round(-dwell * 60m / rpm, 3, MidpointRounding.AwayFromZero));
        }

        private string? AxisName()
        {
            return Find("AXIS") is IdentValue axis ? axis.Name : null;
        }

        private Value? Find(string key)
        {
            foreach (Word word in _words)
            {
                if (word.Key == key)
                {
                    return word.Value;
                }
            }

            return null;
        }

        // The value of the position a word maps; null for an empty or missing position.
        private Value? Argument(string word)
        {
            string? native = entry.NativeOf(word);
            int index = native is null ? -1 : IndexOf(entry.Signature, native);
            if (index < 0 || index >= arguments.Count || arguments[index].Length == 0)
            {
                return null;
            }

            Value? value = SiemensExpression.ValueOf(block, arguments[index], out string? problem);
            if (value is null)
            {
                Raw($"{native}={arguments[index]} of {entry.Native} has no NCX value: {problem}");
            }

            return value;
        }

        // SDIS and DPR are entered without a sign: SDIS moves the reference plane forward, towards the retract plane,
        // and DPR counts from it the other way, towards the depth (D334). The direction is the one the cycle works out
        // itself from RTP and RFP, so a call that needs it and does not give both as numbers stays RAW.
        private Value? Shift(Value surface, Value distance, bool towardsTheRetractPlane)
        {
            decimal? by = SiemensNumbers.NumberOf(distance);
            if (by == 0)
            {
                return surface;
            }

            if (Direction(surface) is not int sign)
            {
                Raw($"{entry.Native} gives a distance without a sign, and its direction needs the retract plane RTP "
                    + "and the reference plane RFP as two different numbers (D334)");
                return null;
            }

            bool plus = towardsTheRetractPlane == sign > 0;
            if (SiemensNumbers.NumberOf(surface) is decimal from && by is decimal number)
            {
                return SiemensNumbers.Of(plus ? from + number : from - number);
            }

            return SiemensExpression.Parse(block,
                "(" + TextOf(surface) + ") " + (plus ? "+" : "-") + " (" + TextOf(distance) + ")", out _);
        }

        // The cycle drills away from the retract plane, which lies before the reference plane (D334): 1 where RTP lies
        // above RFP, -1 where it lies below, null where the two are not two different numbers.
        private int? Direction(Value surface)
        {
            if (SiemensNumbers.NumberOf(surface) is not decimal reference
                || SiemensNumbers.NumberOf(Argument("SAFE")) is not decimal retract || retract == reference)
            {
                return null;
            }

            return retract > reference ? 1 : -1;
        }

        // A position no word of the entry carries is not written, with a WARNING (D164; wave-2 question #79); the
        // reference point of SLOT1 and SLOT2, which the call position carries (D379), and a distance the machine
        // holds with the same value (D358) are carried.
        private void WarnNotCarried()
        {
            var unmapped = new List<string>();
            for (int index = 0; index < arguments.Count && index < entry.Signature.Count; index++)
            {
                string native = entry.Signature[index];
                // D362: a 0 is the control's behaviour anyway and is dropped silently.
                if (arguments[index].Length > 0 && !_used.Contains(native) && !entry.Fixed.ContainsKey(native)
                    && Array.IndexOf(s_modes, native) < 0 && !Carried(native, arguments[index])
                    && SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[index])) != 0)
                {
                    unmapped.Add(native);
                }
            }

            if (unmapped.Count > 0)
            {
                block.Draft.Warnings.Add(new SiemensWarning(DiagnosticCodes.SiemensValueNotCarried,
                    $"{string.Join(", ", unmapped)} of {entry.Native} {(unmapped.Count == 1 ? "has" : "have")} no NCX "
                    + $"word in the catalog entry {entry.Name} and {(unmapped.Count == 1 ? "is" : "are")} not written "
                    + "(controllers siemens.md 7, machine-config 6)."));
            }
        }

        // The positions a rule carries beside the params: _CPA/_CPO and the reserved RAD and INDA of SLOT1 (D379);
        // _VRT and _DIS1 where they equal the [cycles] key the compiler writes them from, or the control's own value
        // where the key is left out, _VRT 0 or 1 mm and _DIS1 0, automatic (D358; SW4.92 p.1087).
        private bool Carried(string native, string argument)
        {
            if (native is "_CPA" or "_CPO" || (entry.Native == "SLOT1" && native is "RAD" or "INDA"))
            {
                return true;
            }

            // The keys are millimetres; the compiler writes them in program units (D357).
            decimal? value = SiemensNumbers.NumberOf(SiemensNumbers.Parse(argument));
            CyclesConfig? cycles = block.Machine.Cycles;
            decimal divisor = Inch(block) ? MillimetresPerInch : 1m;
            decimal lift = (cycles?.ChipBreakLift ?? 1m) / divisor;
            return native switch
            {
                "_VRT" => value == lift || (value == 0 && cycles?.ChipBreakLift is null or 1m),
                "_DIS1" => value == (cycles?.PeckClearance ?? 0m) / divisor,
                _ => false,
            };
        }

        private SiemensCycle? Raw(string reason)
        {
            block.Draft.KeepAsRaw(reason);
            return null;
        }

        private static string TextOf(Value value)
        {
            return value is ExprValue expression ? expression.Text : value.ToCanonical();
        }
    }
}
