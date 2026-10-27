using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The canned cycles of a Fanuc block (controllers fanuc.md 6, 9 rule 5; controller-mapping 5; language 4.7, 4.7.1;
/// machine-config 6): G73 to G89 define the cycle and call it at the position of their block, every following block
/// with a position calls it again, G80 cancels; R and Z become the absolute CLEARANCE and DEPTH, G98/G99 CYCLE_RETRACT,
/// K repeats expand into CYCLE_CALL blocks; on a lathe G83 to G85 drill along Z and G87 to G89 along X (AXIS), the
/// simple turning cycles of system A come from the catalog, and G70 to G76 are the catalog cycles with the contour
/// that follows them as a SUB section, CONTOUR=name (D65).
/// </summary>
internal static class FanucCycles
{
    // The most repeats K the reader expands, four digits.
    private const int MaxRepeats = 9999;

    // The canned cycles of a mill, group 09 (controllers fanuc.md 3, 6).
    private static readonly string[] s_millCycles =
        ["G73", "G74", "G76", "G81", "G82", "G83", "G84", "G85", "G86", "G87", "G88", "G89"];

    // The drilling cycles of a lathe, along Z on the face and along X on the circumference (controllers fanuc.md 6).
    private static readonly string[] s_latheDrilling = ["G83", "G84", "G85", "G87", "G88", "G89"];

    // The simple turning cycles: G90, G92, G94 in system A, G77, G78, G79 in system B and G20, G21, G24 in system C
    // (controllers fanuc.md 3; D174).
    private static readonly string[] s_turningOfSystemA = ["G90", "G92", "G94"];
    private static readonly string[] s_turningOfSystemB = ["G77", "G78", "G79"];
    private static readonly string[] s_turningOfSystemC = ["G20", "G21", "G24"];

    // The multiple repetitive cycles of a lathe, one-shot with the contour range P Q: G70 to G76 in the systems A and
    // B, G72 to G78 in system C (controllers fanuc.md 6; D174).
    private static readonly string[] s_repetitive = ["G70", "G71", "G72", "G73", "G74", "G75", "G76"];
    private static readonly string[] s_repetitiveOfSystemC = ["G72", "G73", "G74", "G75", "G76", "G77", "G78"];

    // The one-shot cycles the manual states a return to the cycle start point for (D187).
    private static readonly string[] s_returnToTheStartPoint = ["G70", "G73", "G76"];

    private static readonly string[] s_motion = ["G0", "G1", "G2", "G3"];

    // D177: the mill drilling codes whose format has no dwell P (B-64484EN-2 p.56, 61-84).
    private static readonly string[] s_millWithoutDwell = ["G73", "G81", "G83", "G85", "G86"];

    // The word of the thread lead F of a simple turning cycle (D175).
    private const string PitchWord = "PITCH";

    // The catalog word of the taper R of a simple turning cycle (D114, D175).
    private const string TaperWord = "TAPER";
    private static readonly IntegerValue s_zero = new(0, "0");

    /// <summary>
    /// Reads the "before" line of a catalog entry, the native line that belongs to the cycle itself: the Fanuc M29 S
    /// of rigid tapping is part of CYCLE=TAP, and its S becomes RPM only where it differs from the speed the spindle
    /// runs at (machine-config 6; controllers fanuc.md 6, 9 rule 9; D256). D256 names two ways the line reaches the
    /// cycle, "M29 S before the tapping block" and "M29 S in the same block as the tapping command", so the line is
    /// read in the cycle block itself and in front of it, in the nearest block with an S word or an axis word, since
    /// the control forbids only those between M29 and G84 (B-64484EN-2 p.103-104, PS0203; FanucReader.MarkBeforeLines).
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void ReadBeforeLine(FanucBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        if (!BeforeLineMatches(block, block.Fanuc.CycleBefore.GetValueOrDefault(block.Line),
                out TemplateValues values)
            && !BeforeLineMatches(block, EntryOfTheCycleOf(block), out values))
        {
            return;
        }

        // The line matched, so the cycle of this block, or of the block after it, is the machining the entry names; a
        // cycle block whose entry carries a "before" line that reaches it in neither way, and that re-issues the code
        // of no running cycle of the entry, is another machining and stays RAW (Start, D256).
        block.Fanuc.CycleBeforeRead.Add(block.Line);
        if (!values.TryGetNumber("rpm", out decimal rpm))
        {
            return;
        }

        string? role = block.State.LastSpindle;
        if (block.Fanuc.Rpm.GetValueOrDefault(role ?? "") == rpm)
        {
            return;
        }

        block.Draft.AddState("RPM", FanucFunctions.SpindleAddress(block, role), FanucNumbers.Of(rpm));
        block.Fanuc.Rpm[role ?? ""] = rpm;
    }

    // Whether the words of the block are the "before" line of the named catalog entry; false for no name and for an
    // entry without one (machine-config 6).
    private static bool BeforeLineMatches(FanucBlock block, string? name, out TemplateValues values)
    {
        values = new TemplateValues();
        return name is not null && block.Machine.CycleCatalog.Find(name) is CycleEntry entry
            && FanucTemplates.TryMatch(block, entry.Before, out values);
    }

    // The entry whose cycle code this very block carries and whose "before" line may therefore stand in the block
    // itself: the second of the three ways into the mode that D256 names, "M29 S in the same block as the tapping
    // command" (machine-config 6).
    private static string? EntryOfTheCycleOf(FanucBlock block)
    {
        foreach (CycleEntry entry in block.Machine.CycleCatalog.Entries)
        {
            if (!string.IsNullOrEmpty(entry.Before) && block.HasCode(entry.Native))
            {
                return entry.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the cycle words of a block: the end of a cycle, a new cycle, its modal values and its calls.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        if (block.Draft.IsRaw || ReadRepetitiveCycle(block))
        {
            return;
        }

        bool returnLevel = ReadReturnLevel(block);
        SourceWord? code = CycleCode(block);

        // The active cycle of the caller of a subprogram, or the one a called subprogram left, may be unknown to the
        // reader (virtual machine 3.9; FanucCallerState): a cycle code, G80 or a code that ends every cycle decides it,
        // and a block with a position before that, which calls the cycle where one is active, stays RAW.
        bool unknownEnds = false;
        if (block.Fanuc.Unknowns.Cycle)
        {
            if (code is null && !block.HasCode("G80") && !EndsEveryCycle(block))
            {
                if (FanucAxes.Unread(block).Count > 0)
                {
                    block.Draft.KeepAsRaw(FanucUnknowns.Reason(
                        "the active cycle, which every following block with a position calls "
                        + "(controllers fanuc.md 6)"));
                }

                return;
            }

            block.Fanuc.Unknowns.Cycle = false;
            unknownEnds = code is null;
        }

        // D386: a group-01 code in a drilling cycle block cancels the cycle; the block is no cycle.
        if (code is not null && GroupOneCodeOf(block) is string motion && !IsTurningCode(block, code))
        {
            CancelledByGroupOne(block, code, motion);
            return;
        }

        bool cancels = block.TakeCode("G80");
        if (cancels || unknownEnds || Ends(block, code))
        {
            End(block);
        }

        if (code is not null)
        {
            Start(block, code);
        }

        FanucCycle? cycle = block.Fanuc.Cycle;
        if (cycle is null)
        {
            return;
        }

        if (returnLevel)
        {
            cycle.ReturnsToInitialLevel = block.State.ActiveCode(FanucModalGroups.ReturnLevel) == "G98";
        }

        if (cycle.Entry is null)
        {
            if (code is not null || Positions(block, cycle).Count > 0)
            {
                block.Draft.KeepAsRaw(cycle.Problem ?? $"{cycle.Code} has no cycle in the catalog of the machine "
                    + "(machine-config 6); its blocks stay RAW");
            }

            return;
        }

        if (cycle.IsTurning)
        {
            ReadTurning(block, cycle, code is not null);
        }
        else
        {
            ReadDrilling(block, cycle, code is not null);
        }
    }

    /// <summary>
    /// The contour a G70 to G76 block of a lathe names with the two contour words of its catalog entry, P and Q, the
    /// labels of the first and the last block of the contour (machine-config 6, contour; language 4.7.1; D65), when
    /// the reader reads the block whole: its code, the two labels, and the words its entry maps as numbers; null
    /// otherwise, with the reason.
    /// </summary>
    /// <param name="block">A source block.</param>
    /// <param name="machine">The machine with its G-code system and its cycle catalog.</param>
    /// <param name="problem">Why a G70 to G76 block names no contour the reader reads; null for any other
    /// block.</param>
    public static SourceContour? ContourOf(SourceBlock block, MachineConfig machine, out string? problem)
    {
        problem = null;
        GcodeSystem? system = machine.Machine.GcodeSystem;
        SourceWord? code = null;
        foreach (SourceWord word in block.Words)
        {
            // D174: the numbers of the multiple repetitive cycles are G70 to G76 in the systems A and B and G72 to
            // G78 in system C (controllers fanuc.md 3).
            if (word.Address == "G" && NativeCode.Of(word) is string native
                && RepetitiveOf(system).Contains(native))
            {
                code = word;
                break;
            }
        }

        if (code is null)
        {
            return null;
        }

        string cycle = SystemANative(NativeCode.Of(code)!, system);
        if (ContourEntry(machine.CycleCatalog, cycle) is not CycleEntry entry)
        {
            problem = $"{cycle} has no cycle with a contour in the catalog of the machine (machine-config 6)";
            return null;
        }

        SourceWord? first = block.Find(entry.Contour[0]);
        SourceWord? last = block.Find(entry.Contour[1]);
        if (first is null || last is null || FanucMacro.WholeNumber(first) is not long from
            || FanucMacro.WholeNumber(last) is not long to)
        {
            problem = $"{cycle} names its contour with {entry.Contour[0]} and {entry.Contour[1]}, the labels of its "
                + "first and its last block";
            return null;
        }

        // TODO(question): the other words of G70 to G76, depth of cut, allowances, retract and thread data, have no
        // NCX word (wave-1 question #18); a block with a word its catalog entry does not map stays RAW with its
        // contour.
        foreach (SourceWord word in block.Words)
        {
            bool mapped = entry.Params.Values.Contains(word.Address) && word.Number is not null
                && word.Expression is null;
            bool own = ReferenceEquals(word, code) || ReferenceEquals(word, first) || ReferenceEquals(word, last);
            if (word.Address != "N" && !own && !mapped)
            {
                problem = $"{word.Address}{word.Text} of {cycle} has no NCX word in the catalog entry {entry.Name}";
                return null;
            }
        }

        return new SourceContour(from.ToString(CultureInfo.InvariantCulture),
            to.ToString(CultureInfo.InvariantCulture));
    }

    // G70 to G76 of a lathe are one-shot blocks whose P and Q name the contour that follows them (controllers fanuc.md
    // 6; language 4.7.1): the structure pass turns the contour into a SUB section of the file, and the cycle block is
    // the catalog cycle with CONTOUR=name (language 4.7, CONTOUR; machine-config 6, modal and contour; D65). A cycle
    // whose contour the structure pass made no section of stays RAW with the blocks of its contour, as the control
    // reads them (FanucReader.MarkContour; D5).
    // TODO(question): the catalog gives G75 (GROOVE) and G76 (THREAD, D161) a contour as the documents say, which
    // wave-1 questions #19 and #29 doubt; the reader follows the entry of the catalog.
    private static bool ReadRepetitiveCycle(FanucBlock block)
    {
        if (block.System is null)
        {
            return false;
        }

        // D174: system C numbers the multiple repetitive cycles G72 to G78; the catalog keeps the system A numbers
        // and the reader translates by gcode_system (controllers fanuc.md 3).
        foreach (string code in RepetitiveOf(block.System))
        {
            if (block.FindCode(code) is not SourceWord word)
            {
                continue;
            }

            block.MarkRead(word);
            if (block.Fanuc.Contours.TryGetValue(block.Line, out string? contour)
                && ContourEntry(block.Machine.CycleCatalog, SystemANative(code, block.System)) is CycleEntry entry)
            {
                ReadContourCycle(block, entry, contour);
            }
            else
            {
                ContourOf(block.Source, block.Machine, out string? problem);
                block.Draft.KeepAsRaw((problem ?? $"the contour of {code} is no range of plain blocks that follows it "
                    + "(language 4.7.1)") + "; the cycle stays RAW with its contour");
            }

            return true;
        }

        return false;
    }

    // The one-shot entry of a native cycle whose contour two words name, the first and the last block of the contour
    // (machine-config 6, modal and contour).
    private static CycleEntry? ContourEntry(CycleCatalog catalog, string native)
    {
        foreach (CycleEntry entry in catalog.Entries)
        {
            if (entry.Native == native && !entry.Modal && entry.Contour.Count == 2)
            {
                return entry;
            }
        }

        return null;
    }

    // CYCLE=name CONTOUR=section with the words the entry maps, and a CYCLE_CALL at the current position: the one-shot
    // block runs once where it stands (machine-config 6, modal; controller-mapping 5, CYCLE_CALL, the block that calls
    // once at the current position; language 4.7). A modal cycle still active is written again before its next call,
    // since the one-shot CYCLE replaced it in NCX (language 4.7); where the tool stands afterwards the reader does not
    // follow.
    // D224: a one-shot entry runs once where it stands (machine-config 6), while the virtual machine keeps a CYCLE
    // until CYCLE=OFF, so the reader closes it with CYCLE=OFF after its call.
    private static void ReadContourCycle(FanucBlock block, CycleEntry entry, string contour)
    {
        block.Take(entry.Contour[0]);
        block.Take(entry.Contour[1]);
        var definition = new DraftBlock()
            .Add("CYCLE", new IdentValue(entry.Name))
            .Add("CONTOUR", new IdentValue(contour));
        foreach (KeyValuePair<string, string> parameter in entry.Params)
        {
            if (block.Take(parameter.Value) is SourceWord word && FanucMacro.ValueOf(block, word) is Value value)
            {
                definition.Add(parameter.Key, value);
            }
        }

        foreach (Word word in definition.Words)
        {
            block.Draft.AddState(word.Key, word.Addr, word.Value);
        }

        block.Draft.After.Add(new DraftBlock().WithVerb("CYCLE_CALL"));

        // D224: the reader writes CYCLE=OFF directly after the CYCLE_CALL of a one-shot entry, and a compiler writes
        // nothing for it.
        block.Draft.After.Add(new DraftBlock().Add("CYCLE", new IdentValue("OFF")));
        block.Fanuc.Record("CYCLE", "OFF");
        if (block.Fanuc.Cycle is FanucCycle active)
        {
            active.Written = null;
        }

        // D187: G70 returns in rapid to the cycle start point, note 3 of G73 says the same and the tool path of G76
        // is a closed loop back to it, so the tool stands where it stood; where G71, G72, G74 and G75 leave it no
        // page of the manual says, and the reader forgets the positions there (decisions.md; fanuc 6).
        if (Array.IndexOf(s_returnToTheStartPoint, entry.Native) < 0)
        {
            block.Fanuc.ForgetPositions();
        }
    }

    // G98 returns to the initial level and G99 to the R level (mills, systems B and C; controllers fanuc.md 3, group
    // 10; controller-mapping 5, CYCLE_RETRACT); in system A the two codes are the feed mode.
    private static bool ReadReturnLevel(FanucBlock block)
    {
        if (block.System == GcodeSystem.A)
        {
            return false;
        }

        bool initial = block.TakeCode("G98");
        bool clearance = block.TakeCode("G99");
        return initial || clearance;
    }

    // The code of a cycle in the block, of the cycles of the machine's G-code system.
    private static SourceWord? CycleCode(FanucBlock block)
    {
        string[] codes = CodesOf(block);
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "G" && NativeCode.Of(word) is string code && codes.Contains(code))
            {
                return word;
            }
        }

        return null;
    }

    private static string[] CodesOf(FanucBlock block)
    {
        return CodesOf(block.System);
    }

    private static string[] CodesOf(GcodeSystem? system)
    {
        return system switch
        {
            null => s_millCycles,
            GcodeSystem.A => [.. s_latheDrilling, .. s_turningOfSystemA],
            GcodeSystem.C => [.. s_latheDrilling, .. s_turningOfSystemC],
            _ => [.. s_latheDrilling, .. s_turningOfSystemB],
        };
    }

    /// <summary>
    /// Tells whether a code starts a cycle in the G-code system: a drilling or turning cycle of the system, and on a
    /// lathe the multiple repetitive cycles, G70 to G76 in the systems A and B and G72 to G78 in system C
    /// (controllers fanuc.md 3, 6; D174).
    /// </summary>
    /// <param name="code">The code without leading zeros, "G81".</param>
    /// <param name="system">The G-code system of the machine; null for a mill.</param>
    public static bool StartsACycle(string code, GcodeSystem? system)
    {
        return CodesOf(system).Contains(code) || RepetitiveOf(system).Contains(code);
    }

    // The multiple repetitive cycles of the G-code system; none on a mill, where the same numbers are drilling cycles
    // and absolute programming (controllers fanuc.md 3, 6; D174).
    private static string[] RepetitiveOf(GcodeSystem? system)
    {
        return system switch
        {
            GcodeSystem.A or GcodeSystem.B => s_repetitive,
            GcodeSystem.C => s_repetitiveOfSystemC,
            _ => [],
        };
    }

    // The simple turning cycles of the G-code system; none on a mill (controllers fanuc.md 3; D174).
    private static string[] TurningOf(GcodeSystem? system)
    {
        return system switch
        {
            GcodeSystem.A => s_turningOfSystemA,
            GcodeSystem.B => s_turningOfSystemB,
            GcodeSystem.C => s_turningOfSystemC,
            _ => [],
        };
    }

    // The native of the catalog, which is written in the numbers of system A, for a cycle code of the machine's
    // G-code system: G77 of system B and G20 of system C are the G90 of the catalog, G72 of system C its G70
    // (controllers fanuc.md 3; D174).
    private static string SystemANative(string code, GcodeSystem? system)
    {
        int turning = Array.IndexOf(TurningOf(system), code);
        if (turning >= 0)
        {
            return s_turningOfSystemA[turning];
        }

        int repetitive = Array.IndexOf(RepetitiveOf(system), code);
        return repetitive >= 0 ? s_repetitive[repetitive] : code;
    }

    // A code that ends any cycle: on a mill G0 to G3 (Ends), on a lathe, whose cycles belong to group 01, every other
    // code of that group (controllers fanuc.md 3, 6).
    private static bool EndsEveryCycle(FanucBlock block)
    {
        if (!block.IsLathe)
        {
            foreach (string motion in s_motion)
            {
                if (block.HasCode(motion))
                {
                    return true;
                }
            }

            return false;
        }

        IReadOnlyDictionary<string, int> groups = FanucModalGroups.For(block.System);
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "G" && NativeCode.Of(word) is string code
                && groups.TryGetValue(code, out int group) && group == FanucModalGroups.Motion)
            {
                return true;
            }
        }

        return false;
    }

    // A cycle ends with G80; on a lathe, where the cycles belong to group 01, with the next code of that group
    // (controllers fanuc.md 3, 6).
    // D247: any code of group 01 (G0 to G3) ends a canned cycle and leaves the same state as G80, on the mill and on
    // the lathe, so such a block moves and does not drill (fanuc 6, 9 rule 5; controller-mapping 5; decisions.md).
    private static bool Ends(FanucBlock block, SourceWord? code)
    {
        if (block.Fanuc.Cycle is null || code is not null)
        {
            return false;
        }

        if (!block.IsLathe)
        {
            foreach (string motion in s_motion)
            {
                if (block.HasCode(motion))
                {
                    return true;
                }
            }

            return false;
        }

        string? active = block.State.ActiveCode(FanucModalGroups.Motion);
        return active is null || !CodesOf(block).Contains(active);
    }

    // The group-01 code G0 to G3 of a block (controllers fanuc.md 3); null for none.
    private static string? GroupOneCodeOf(FanucBlock block)
    {
        foreach (string motion in s_motion)
        {
            if (block.HasCode(motion))
            {
                return motion;
            }
        }

        return null;
    }

    // Whether a cycle code is a simple turning cycle of the lathe, G90, G92, G94 of system A, which D386 does not name.
    private static bool IsTurningCode(FanucBlock block, SourceWord code)
    {
        return TurningOf(block.System).Contains(NativeCode.Of(code)!);
    }

    // D386: a block with a group-01 code (G0..G3) and a drilling canned-cycle code (G73, G74, G76, G81-G89, mill and
    // lathe) is not a cycle: the control executes the group-01 code and the cycle is cancelled as by G80. The block
    // stays RAW with the WARNING RDR121, since the control's handling of R, Q, P and K in a G0 block is not
    // documented, and the reader writes no RAPID guess. Afterwards the motion is the block's group-01 code, no cycle
    // is active (CYCLE=OFF after the RAW block where one was written), and R and Z are cleared, so the following X/Y
    // blocks are plain moves.
    private static void CancelledByGroupOne(FanucBlock block, SourceWord code, string motion)
    {
        string native = NativeCode.Of(code)!;
        block.Draft.KeepAsRaw($"{motion} and {native} in one block: the group-01 code cancels the cycle (D386)");
        block.Diagnostics.Warning(block.Line, DiagnosticCodes.FanucGroupOneCancelsTheCycle,
            $"a group-01 code in the {native} block cancels {native}; the control moves with the block's axis words, "
            + "the hole-bottom Z included (B-64484EN-2 p.105, D386). The block is kept as RAW and no cycle is read.");
        block.Fanuc.Cycle = null;
        if (block.Fanuc.TakeChange("CYCLE", "OFF"))
        {
            block.Fanuc.CycleOffAfterRaw.Add(block.Line);
        }

        var groups = new Dictionary<int, string>(block.State.ModalGroups)
        {
            [FanucModalGroups.Motion] = motion,
        };
        if (!block.IsLathe)
        {
            groups[FanucModalGroups.Cycle] = "G80";
        }

        block.State.SetModalGroups(groups);
    }

    // CYCLE=OFF cancels (controller-mapping 5), written where an NCX cycle is active.
    private static void End(FanucBlock block)
    {
        block.Fanuc.Cycle = null;
        if (block.Fanuc.TakeChange("CYCLE", "OFF"))
        {
            block.Draft.AddState("CYCLE", null, new IdentValue("OFF"));
        }
    }

    // A cycle code starts the cycle, or replaces the code of the active one; the control keeps the depth, R, Q, P and F
    // of the active cycle and the initial level, where the drilling axis stood when the cycle mode began (controllers
    // fanuc.md 6).
    private static void Start(FanucBlock block, SourceWord word)
    {
        block.MarkRead(word);
        string code = NativeCode.Of(word)!;
        FanucCycle? previous = block.Fanuc.Cycle;
        CycleCatalog catalog = block.Machine.CycleCatalog;
        if (TurningOf(block.System).Contains(code))
        {
            // D174: the catalog is written in system A numbers and the reader translates by gcode_system, so a
            // G77 of system B and a G20 of system C find the entry of G90 (controllers fanuc.md 3; decisions.md).
            block.Fanuc.Cycle = new FanucCycle
            {
                Code = code,
                Entry = ModalEntry(catalog, SystemANative(code, block.System)),
                IsTurning = true,
                Feed = block.Fanuc.ControlFeed,
            };
            return;
        }

        // A new drilling cycle takes the return level of G98 or G99 and, on a mill, its drilling axis from the plane;
        // where that is the unknown state of a caller (FanucCallerState), the cycle is not known and the block stays
        // RAW.
        FanucUnknowns unknown = block.Fanuc.Unknowns;
        if (unknown.Groups.Contains(FanucModalGroups.ReturnLevel)
            || (!block.IsLathe && unknown.Groups.Contains(FanucModalGroups.Plane)))
        {
            block.Fanuc.Cycle = null;
            unknown.Cycle = true;
            block.Draft.KeepAsRaw(FanucUnknowns.Reason(
                "G98 or G99, or the plane of G17 to G19, which a new cycle takes"));
            return;
        }

        string axis = block.IsLathe ? (code is "G87" or "G88" or "G89" ? "X" : "Z") : ToolAxis(block);
        if (!block.IsLathe && axis != "Z")
        {
            // D157: outside G17 the drilling axis of the control depends on parameter 5101#0 FXY.
            block.Draft.Warnings.Add((DiagnosticCodes.FanucDrillingAxisByParameter,
                $"{code} outside G17 drills along {axis}, the axis the reader derives from the plane; the control "
                + "drills along Z where parameter 5101#0 FXY is 0 and by the plane where it is 1 (controller-mapping "
                + "5, AXIS; D157)."));
        }

        CycleEntry? matched = block.IsLathe
            ? LatheEntry(block, catalog, code, axis, previous)
            : ModalEntry(catalog, code) ?? DwellNativeEntry(catalog, code);
        bool before = matched is not null && !string.IsNullOrEmpty(matched.Before)
            && WithItsBeforeLine(block, matched, code, previous);
        string? problem = matched is null || string.IsNullOrEmpty(matched.Before) || before
            ? null
            : WithoutItsBeforeLine(block, matched);
        if (problem is not null && LineSurelyAbsent(block)
            && catalog.FindNativeByBeforeLine(code, lineReaches: false) is CycleEntry withoutLine
            && catalog.Find(withoutLine.Name, axis) is not null)
        {
            // D359: a G84 the line reaches in none of D256's three ways is the floating tap, the entry of its native
            // with no before key, TAP_COMPENSATING (an amendment of D160, machine-config 6).
            matched = withoutLine;
            problem = null;
        }
        else if (problem is not null)
        {
            matched = null;
        }

        decimal? initial = previous?.InitialLevel;
        if (initial is null && block.State.Positions.TryGetValue(axis, out decimal level))
        {
            initial = level;
        }

        // The cycle feeds with the modal F of the control, which a new CYCLE of NCX does not inherit and the reader
        // writes as CYCLE_F (language 2 rule 4, 4.7; D29).
        var cycle = new FanucCycle
        {
            Code = code,
            Entry = matched,
            Problem = problem,
            EnteredWithBefore = before,
            Axis = axis,
            WritesAxis = block.IsLathe,
            InitialLevel = initial,
            ReturnsToInitialLevel = block.State.ActiveCode(FanucModalGroups.ReturnLevel) == "G98",
            Feed = block.Fanuc.ControlFeed,
        };
        if (previous is not null && !previous.IsTurning && previous.Axis == axis)
        {
            cycle.Depth = previous.Depth;
            cycle.Clearance = previous.Clearance;
            cycle.Peck = previous.Peck;
            cycle.Dwell = previous.Dwell;
        }

        block.Fanuc.Cycle = cycle;
    }

    // Whether the mode the entry's "before" line switches on is in force for this cycle, so that the cycle is the
    // machining the entry names (machine-config 6; controllers fanuc.md 6, 9 rule 9; D256). D256 names three ways
    // into it: the line in front of the cycle block with neither an S command nor an axis movement between them
    // (B-64484EN-2 p.103-104, PS0203; FanucReader.MarkBeforeLines), the line in the cycle block itself, and the
    // parameter that needs none, which is the documented before = "" entry and reaches this test as an entry with no
    // line. The mode then holds for the whole run of the cycle - D256's own evidence has an M29 inside a running
    // tapping cycle alarm PS0204, so a legal source cannot repeat the line in front of a second cycle block - and a
    // run ends with G80 or with a code of group 01 that cancels the cycle (fanuc 6; D247), which leaves no cycle
    // active here. A block that only re-issues the native code of a cycle entered with the line is therefore the
    // same machining as its entry.
    private static bool WithItsBeforeLine(FanucBlock block, CycleEntry entry, string code, FanucCycle? previous)
    {
        if (block.Fanuc.CycleBeforeRead.Contains(block.Line))
        {
            return true;
        }

        // Where the reader does not see what runs in front of the cycle block back to the place of the line, a jump
        // into it among them, it cannot tell whether the mode is in force (FanucReader.MarkBeforeLines).
        if (block.Fanuc.BeforeLineUnseen.Contains(block.Line))
        {
            return false;
        }

        if (block.Fanuc.BeforeLinesOf.TryGetValue(block.Line, out List<int>? before)
            && before.Exists(block.Fanuc.CycleBeforeRead.Contains))
        {
            return true;
        }

        return previous is not null && previous.EnteredWithBefore && previous.Code == code
            && previous.Entry?.Name == entry.Name;
    }

    // Why a cycle block whose code the catalog maps is not read as the entry's cycle, although the entry carries a
    // "before" line, the native line of the machining itself (machine-config 6; controllers fanuc.md 6; D256).
    // Where the reader sees the block that runs before the cycle block and the line stands in none of the places
    // D256 names, the source programmed the other machining, which Fanuc states apart as a fact of the control: a
    // G84 with M29 S in front of it taps rigidly, and "G84(G88) cycle that don't use the M29 code will be activated
    // with normal tapping", tapping with a compensating chuck. D359 names it TAP_COMPENSATING, the entry of the same
    // native with no before key, which Start then reads (LineSurelyAbsent). Where the reader cannot tell - a block
    // it does not see past, a line block kept RAW, a cancel or a spindle code at the line, or a flow that reaches the
    // block with the line on one path and without it on another (D360) - the blocks stay RAW with the reason (D5).
    private static string? WithoutItsBeforeLine(FanucBlock block, CycleEntry entry)
    {
        if (block.Fanuc.BeforeLineWithACancel.Contains(block.Line))
        {
            // D384: the reason names the facts the manuals give.
            return $"the line \"{entry.Before}\" of the cycle {entry.Name} stands in front of {entry.Native} in a "
                + "block with a code that cancels the rigid tapping mode, G80, a cycle code or a group-01 code, and "
                + "the manual does not say whether the line holds there: the manuals state the group-01 cancel only "
                + "after tapping (B-64303EN-1 p.927), and the one builder example of a move in the M29 block has no G "
                + "code (Mori NTX1000SZM p.281-282) (machine-config 6, D256, D384); its blocks stay RAW";
        }

        if (block.Fanuc.BeforeLineSpindle.TryGetValue(block.Line, out string? spindle))
        {
            // D385: the spindle code between the line and the cycle block.
            return $"{spindle} stands between the line \"{entry.Before}\" of the cycle {entry.Name} and "
                + $"{entry.Native}, and a spindle command between M29 and G84 is handled by the machine's PMC and may "
                + "raise an alarm or drop the rigid mode (machine-config 6, D256, D385); its blocks stay RAW";
        }

        if (block.Fanuc.BeforeLinesOf.TryGetValue(block.Line, out List<int>? lines)
            && lines.Find(block.Fanuc.BeforeLineRaw.Contains) is int raw and > 0)
        {
            return $"the line \"{entry.Before}\" of the cycle {entry.Name} in front of {entry.Native} stands in a "
                + $"block the reader keeps as RAW, line {raw}, which carries it to the control as it stands, so it is "
                + "not read into the cycle (machine-config 6, D256, D5); its blocks stay RAW";
        }

        if (block.Fanuc.BeforeLineAlarm.TryGetValue(block.Line, out int alarm))
        {
            // D256, D359: an S command or an axis movement between M29 and G84 is alarm PS0203.
            return $"the line \"{entry.Before}\" of the cycle {entry.Name} on line {alarm} is followed by an S "
                + $"command or an axis movement before {entry.Native}, which the control rejects with alarm PS0203 "
                + "(B-64484EN-2 p.104, p.108; machine-config 6, D256); its blocks stay RAW";
        }

        if (block.Fanuc.BeforeLineNotOnEveryPath.Contains(block.Line))
        {
            // D360: one path reaches the cycle block without the line.
            return $"one path reaches {entry.Native} with the line \"{entry.Before}\" of the cycle {entry.Name} and "
                + "one path reaches it without the line, so the mode is not in force on every path (machine-config 6, "
                + "D256, D360); its blocks stay RAW";
        }

        if (!SeesWhatRunsBefore(block))
        {
            return $"the reader does not see which block runs before this one, or what a block between it and the "
                + "nearest block with an S word or an axis word does, so it cannot tell whether the line "
                + $"\"{entry.Before}\" of the cycle {entry.Name} stands in front of {entry.Native} here "
                + "(machine-config 6, D256); its blocks stay RAW";
        }

        return $"{entry.Native} without the line \"{entry.Before}\" in front of it is not the cycle {entry.Name} of "
            + "the catalog but another machining, which NCX cannot name (machine-config 6, D256); its blocks stay RAW";
    }

    // Whether the reader sees that the "before" line reaches the cycle block in none of D256's ways, so that the block
    // is the machining without the line (D359): it sees what runs before it, and no RAW line block, cancel or
    // spindle code stands at the place of the line.
    private static bool LineSurelyAbsent(FanucBlock block)
    {
        FanucState fanuc = block.Fanuc;
        return SeesWhatRunsBefore(block) && !fanuc.BeforeLineWithACancel.Contains(block.Line)
            && !fanuc.BeforeLineSpindle.ContainsKey(block.Line) && !fanuc.BeforeLineAlarm.ContainsKey(block.Line)
            && !(fanuc.BeforeLinesOf.TryGetValue(block.Line, out List<int>? lines)
                && lines.Exists(fanuc.BeforeLineRaw.Contains));
    }

    // Whether the reader sees the blocks that run before a cycle block back to the place of its "before" line: the
    // blocks in front of it in the file run before it, unless a jump enters the cycle block, or the walk back of
    // FanucReader.MarkBeforeLines ended at a block it does not see past - the head of a section a block of the file
    // calls, a block a jump enters, a call (virtual machine 3.9; controller-mapping 6; D256). Nothing runs before the
    // first block of the file, which the reader sees as well.
    private static bool SeesWhatRunsBefore(FanucBlock block)
    {
        // D360: a jump into the cycle block is decided from the flow (FanucBeforeLineWalk), which leaves the block in
        // BeforeLineUnseen where it cannot decide.
        return !block.Fanuc.BeforeLineUnseen.Contains(block.Line);
    }

    // The entry of a native cycle that stays active after its block, as the drilling cycles and the simple turning
    // cycles do (machine-config 6, modal); a one-shot entry of the same code, the lathe's G76 on a mill, is none of
    // them.
    private static CycleEntry? ModalEntry(CycleCatalog catalog, string native)
    {
        foreach (CycleEntry entry in catalog.Entries)
        {
            if (entry.Native == native && entry.Modal)
            {
                return entry;
            }
        }

        return null;
    }

    // D177: a mill code no entry writes as its own may be the dwell_native of one, G89 of REAM (G85 with a dwell P).
    private static CycleEntry? DwellNativeEntry(CycleCatalog catalog, string native)
    {
        foreach (CycleEntry entry in catalog.Entries)
        {
            if (entry.DwellNative == native && entry.Native != native && entry.Modal)
            {
                return entry;
            }
        }

        return null;
    }

    // On a mill the drilling axis is the tool axis of G17 to G19 (controller-mapping 5, AXIS).
    private static string ToolAxis(FanucBlock block)
    {
        return block.State.ActiveCode(FanucModalGroups.Plane) switch
        {
            "G18" => "Y",
            "G19" => "X",
            _ => "Z",
        };
    }

    // D160: a Fanuc lathe drills with G83/G84/G85 along Z and with G87/G88/G89 along X, and the lathe catalog carries
    // the two triples as entries of one name that differ by for_axis, the entry without the key mapping the default
    // axis Z. Of two entries on one native the reader takes the first in catalog order whose params cover the words
    // of the block, so a G83 with a peck depth Q is PECK and one without it DRILL; Q is modal in the cycle, so a peck
    // depth of the active cycle counts as well (machine-config 6; controllers fanuc.md 6; decisions.md).
    private static CycleEntry? LatheEntry(FanucBlock block, CycleCatalog catalog, string code, string axis,
        FanucCycle? previous)
    {
        string? forAxis = axis == "X" ? "X" : null;
        List<string> words = CycleWordsOf(block, catalog, code, previous);
        foreach (CycleEntry entry in catalog.Entries)
        {
            if (entry.Native == code && entry.Modal && entry.ForAxis == forAxis && entry.CoversNativeWords(words))
            {
                return entry;
            }
        }

        return null;
    }

    // The native words of the block that an entry of this native cycle may map, Q and P of a lathe drilling block; a
    // peck depth of the active cycle counts, because Q stays with the cycle (controllers fanuc.md 6; D160).
    private static List<string> CycleWordsOf(FanucBlock block, CycleCatalog catalog, string code,
        FanucCycle? previous)
    {
        var mappable = new List<string>();
        foreach (CycleEntry entry in catalog.Entries)
        {
            if (entry.Native != code)
            {
                continue;
            }

            foreach (string native in entry.Params.Values)
            {
                if (!mappable.Contains(native))
                {
                    mappable.Add(native);
                }
            }
        }

        var words = new List<string>();
        foreach (string native in mappable)
        {
            if (block.Find(native) is not null)
            {
                words.Add(native);
            }
        }

        if (previous?.Peck is not null && mappable.Contains("Q") && !words.Contains("Q"))
        {
            words.Add("Q");
        }

        return words;
    }

    // The axis words that position the cycle: every axis but the drilling axis, every axis of a turning cycle.
    private static List<FanucAxisWord> Positions(FanucBlock block, FanucCycle cycle)
    {
        var positions = new List<FanucAxisWord>();
        foreach (FanucAxisWord axis in FanucAxes.Unread(block))
        {
            if (cycle.IsTurning || axis.Axis != cycle.Axis)
            {
                positions.Add(axis);
            }
        }

        return positions;
    }

    // R before the depth: under G91 R is the distance from the initial level and Z the distance from the R level; Q is
    // the peck, P the dwell in milliseconds, F the feed of the cycle and also the modal F of the control, K the number
    // of repeats (controllers fanuc.md 6; controller-mapping 5; D29).
    // D248: on a lathe G83..G89 R is the signed incremental distance from the initial level, always a radius, so
    // CLEARANCE is the initial level + R, + 2R on an X axis programmed in diameters; where the initial level is not
    // known the block stays RAW. The depth word follows its address in system A (Z/X absolute, W/U incremental from
    // R) and G90/G91 in systems B and C. On a mill under G91 R counts from the initial level and Z from R.
    private static void ReadDrilling(FanucBlock block, FanucCycle cycle, bool defines)
    {
        // Under G90 or G91 of a caller the reader does not know (FanucCallerState), R, the depth and the positions of
        // absolute addresses are neither levels nor points it knows.
        if (block.Fanuc.Unknowns.Groups.Contains(FanucModalGroups.Distance)
            && (block.Find("R") is not null || FanucAxes.Unread(block).Exists(axis => !axis.Incremental)))
        {
            block.Draft.KeepAsRaw(FanucUnknowns.Reason("G90 or G91"));
            return;
        }

        SourceWord? clearance = block.Take("R");
        if (clearance is not null && block.IsLathe)
        {
            cycle.Clearance = LatheLevel(block, clearance, cycle);
        }
        else if (clearance is not null)
        {
            cycle.Clearance = Level(block, clearance, cycle.InitialLevel, block.Incremental);
        }

        FanucAxisWord? depth = FanucAxes.Find(block, cycle.Axis);
        if (depth is not null)
        {
            block.MarkRead(depth.Word);
            cycle.Depth = Level(block, depth.Word, FanucNumbers.NumberOf(cycle.Clearance), depth.Incremental);
        }

        SourceWord? peck = block.Take("Q");
        if (peck is not null)
        {
            cycle.Peck = FanucMacro.ValueOf(block, peck);
        }

        // D159: P of a cycle is the dwell in units of [machine] dwell_increment, seconds per unit, 0.001 by default.
        SourceWord? dwell = block.Take("P");
        if (dwell is not null && !block.IsLathe && s_millWithoutDwell.Contains(cycle.Code))
        {
            // D177: the code takes no dwell, so the control ignores P, and the reader drops it with a WARNING.
            block.Draft.Warnings.Add((DiagnosticCodes.FanucCycleDwellIgnored,
                $"P{dwell.Text} of {cycle.Code}: {cycle.Code} takes no dwell P on a machining centre (B-64484EN-2 "
                + "p.56, 61-84), so the dwell is dropped (controllers fanuc.md 6; D177)."));
        }
        else if (dwell?.Number is decimal units && dwell.Expression is null)
        {
            cycle.Dwell = FanucNumbers.Of(units * block.Machine.Machine.DwellIncrement);
        }
        else if (dwell is not null)
        {
            block.Draft.KeepAsRaw("the dwell P of the cycle is no number");
        }

        SourceWord? feed = block.Take("F");
        if (feed is not null && FanucMacro.ValueOf(block, feed) is Value value)
        {
            cycle.Feed = value;
            block.Fanuc.ControlFeed = value;
        }

        SourceWord? repeats = block.Take("K");
        List<FanucAxisWord> positions = Positions(block, cycle);
        if (block.Draft.IsRaw || (!defines && positions.Count == 0))
        {
            return;
        }

        int count = 1;
        if (repeats is not null)
        {
            if (repeats.Number is not decimal times || repeats.Expression is not null || times < 0
                || times > MaxRepeats || times != decimal.Truncate(times))
            {
                block.Draft.KeepAsRaw("K of the cycle is no number of repeats");
                return;
            }

            count = decimal.ToInt32(times);
        }

        foreach (FanucAxisWord position in positions)
        {
            block.MarkRead(position.Word);
        }

        // Zero repeats drill no hole.
        if (count > 0 && WriteDefinition(block, cycle))
        {
            Call(block, cycle, positions, count);
        }
    }

    // D248: the R level of a lathe drilling cycle, the initial level plus R, twice R on a diameter X axis.
    private static Value? LatheLevel(FanucBlock block, SourceWord word, FanucCycle cycle)
    {
        if (cycle.InitialLevel is not decimal initial || word.Number is not decimal distance
            || word.Expression is not null)
        {
            block.Draft.KeepAsRaw($"R{word.Text} of {cycle.Code} counts from the initial level, where the drilling "
                + "axis stood before the cycle, which the reader does not know here (D248)");
            return null;
        }

        bool diameter = cycle.Axis == "X" && block.Machine.ResolveAxis("X")?.Programming == Programming.Diameter;
        return FanucNumbers.Of(initial + (diameter ? 2m * distance : distance));
    }

    // An absolute level as written, or under G91 the distance from a known level (controllers fanuc.md 6).
    private static Value? Level(FanucBlock block, SourceWord word, decimal? from, bool incremental)
    {
        if (!incremental)
        {
            return FanucMacro.ValueOf(block, word);
        }

        if (from is decimal origin && word.Number is decimal distance && word.Expression is null)
        {
            return FanucNumbers.Of(origin + distance);
        }

        block.Draft.KeepAsRaw(
            $"{word.Address}{word.Text} under G91 needs the level it starts from, which the reader does not know");
        return null;
    }

    // The CYCLE block: the name of the catalog entry, AXIS on a lathe, SURFACE, the absolute CLEARANCE and DEPTH,
    // CYCLE_RETRACT with SAFE under G98, PECK, CYCLE_F, CYCLE_DWELL and the PITCH of a tapping cycle
    // (controller-mapping 5; language 4.7), written before a call whenever it differs from the one written last, since
    // a new CYCLE replaces all parameters (language 4.7).
    private static bool WriteDefinition(FanucBlock block, FanucCycle cycle)
    {
        CycleEntry entry = cycle.Entry!;
        if (cycle.Depth is null || cycle.Clearance is null)
        {
            block.Draft.KeepAsRaw(
                $"{cycle.Code} needs its depth and its R level, DEPTH and CLEARANCE (virtual machine 3.3)");
            return false;
        }

        var definition = new DraftBlock().Add("CYCLE", new IdentValue(entry.Name));
        if (cycle.WritesAxis)
        {
            definition.Add("AXIS", new IdentValue(cycle.Axis));
        }

        // D158: SURFACE is R minus [cycles] surface_clearance, SURFACE=0 where the machine file leaves the key out (as
        // language 6 writes the drilling of G81); none on a cycle along X, whose 0 is the axis of the part, and none
        // where R is no number the reader knows.
        decimal? gap = block.Machine.Cycles?.SurfaceClearance;
        if (cycle.Axis != "X" && gap is null)
        {
            definition.Add("SURFACE", s_zero);
        }
        else if (cycle.Axis != "X" && FanucNumbers.NumberOf(cycle.Clearance) is decimal level)
        {
            definition.Add("SURFACE", FanucNumbers.Of(level - gap!.Value));
        }

        definition.Add("CLEARANCE", cycle.Clearance).Add("DEPTH", cycle.Depth);
        bool safeUnknown = false;
        if (cycle.ReturnsToInitialLevel)
        {
            definition.Add("CYCLE_RETRACT", new IdentValue("SAFE"));
            if (cycle.InitialLevel is decimal initial)
            {
                definition.Add("SAFE", FanucNumbers.Of(initial));
            }
            else
            {
                safeUnknown = true;
            }
        }
        else
        {
            definition.Add("CYCLE_RETRACT", new IdentValue("CLEARANCE"));
        }

        AddParameters(block, cycle, entry, definition);
        if (Write(block, cycle, definition) && safeUnknown)
        {
            block.Draft.Warnings.Add((DiagnosticCodes.FanucInitialLevelUnknown,
                $"{cycle.Code} under G98 returns to the initial level, and where the drilling axis stood is not known; "
                + "CYCLE_RETRACT=SAFE is written without SAFE (controller-mapping 5)."));
        }

        return true;
    }

    // PECK from Q, CYCLE_F from F, CYCLE_DWELL from P where the entry maps them, and the pitch of a tapping cycle, F /
    // S in the per-minute mode and F in the per-revolution mode (controller-mapping 5, TAP; language 4.7, PITCH).
    private static void AddParameters(FanucBlock block, FanucCycle cycle, CycleEntry entry, DraftBlock definition)
    {
        if (entry.NativeOf("PECK") is not null && cycle.Peck is Value peck)
        {
            definition.Add("PECK", peck);
        }

        if (entry.CycleF is not null && cycle.Feed is Value feed)
        {
            definition.Add("CYCLE_F", feed);
        }

        if (entry.CycleDwell is not null && cycle.Dwell is Value dwell)
        {
            definition.Add("CYCLE_DWELL", dwell);
        }

        if (!entry.RuleWords.Contains("PITCH") || FanucNumbers.NumberOf(cycle.Feed) is not decimal perMinute)
        {
            return;
        }

        // The feed mode and the speed are the caller's in a subprogram and those its subprogram left in a caller
        // (virtual machine 3.9); where the reader does not know them, the pitch is not known (FanucCallerState).
        FanucUnknowns unknown = block.Fanuc.Unknowns;
        string? mode = block.State.ActiveCode(FanucModalGroups.FeedMode);
        if (unknown.Groups.Contains(FanucModalGroups.FeedMode))
        {
            block.Draft.KeepAsRaw(FanucUnknowns.Reason(
                "the feed mode, which makes F the pitch or the feed per minute"));
        }
        else if (mode == (block.System == GcodeSystem.A ? "G99" : "G95"))
        {
            definition.Add("PITCH", cycle.Feed!);
        }
        else if (unknown.Spindle || (unknown.Speeds && !block.Fanuc.Rpm.ContainsKey(block.State.LastSpindle ?? "")))
        {
            block.Draft.KeepAsRaw(FanucUnknowns.Reason("the speed of the spindle, which gives the pitch F / S"));
        }
        else if (block.Fanuc.Rpm.TryGetValue(block.State.LastSpindle ?? "", out decimal speed) && speed != 0)
        {
            // TODO(question): no document says how many decimals a quotient the reader computes keeps (wave-1
            // question #46); the pitch keeps six.
            definition.Add("PITCH", FanucNumbers.Of(Math.Round(perMinute / speed, 6, MidpointRounding.AwayFromZero)));
        }
    }

    // The definition goes into the main block when it differs from the one written last; true when it was written.
    private static bool Write(FanucBlock block, FanucCycle cycle, DraftBlock definition)
    {
        var words = new List<string>();
        foreach (Word word in definition.Words)
        {
            words.Add(word.ToCanonical());
        }

        string written = string.Join(" ", words);
        if (written == cycle.Written)
        {
            return false;
        }

        cycle.Written = written;
        foreach (Word word in definition.Words)
        {
            block.Draft.AddState(word.Key, word.Addr, word.Value);
        }

        block.Fanuc.Record("CYCLE", definition.Words[0].Value.ToCanonical());
        return true;
    }

    // D220: the reader writes the axis words the source block carries and no others, so a defining block without a
    // position, G81 G99 Z-21.732 R5. F565, becomes a bare CYCLE_CALL, which drills where the tool stands
    // (language 4.7; decisions.md); the Fanuc cycle positions in the plane only where the block carries the words for
    // it. A CYCLE_CALL at the position of the block, K times; under G91 each repeat moves by
    // the incremental words (controllers fanuc.md 6; controller-mapping 5, CYCLE_CALL and repeats; language 4.7). The
    // plane axes stand at the call point afterwards and the drilling axis at the level the cycle returns to (virtual
    // machine 3.3).
    private static void Call(FanucBlock block, FanucCycle cycle, List<FanucAxisWord> positions, int count)
    {
        var values = new List<Value>();
        foreach (FanucAxisWord position in positions)
        {
            if (FanucMacro.ValueOf(block, position.Word) is not Value value)
            {
                return;
            }

            values.Add(value);
        }

        bool mainIsFree = block.Draft.Main.Verb is null && !block.Draft.Main.Has("CYCLE", null);
        for (int repeat = 0; repeat < count; repeat++)
        {
            DraftBlock call = repeat == 0 && mainIsFree ? block.Draft.Main : new DraftBlock();
            if (!ReferenceEquals(call, block.Draft.Main))
            {
                block.Draft.After.Add(call);
            }

            call.WithVerb("CYCLE_CALL");
            for (int index = 0; index < positions.Count; index++)
            {
                call.Add(positions[index].Key, values[index]);
                FanucMotion.Move(block, positions[index], values[index]);
            }

            if (!cycle.IsTurning)
            {
                decimal? level = cycle.ReturnsToInitialLevel
                    ? cycle.InitialLevel
                    : FanucNumbers.NumberOf(cycle.Clearance);
                if (level is decimal known)
                {
                    block.State.SetPosition(cycle.Axis, known);
                }
                else
                {
                    block.State.ForgetPosition(cycle.Axis);
                }
            }
        }
    }

    // The simple turning cycles of system A, G90 (OD), G92 (thread) and G94 (face), are modal like the drilling cycles:
    // CYCLE= and one CYCLE_CALL per block with X and Z or U and W (language 4.7.1; controller-mapping 5, turning
    // cycles; cycles/fanuc.toml). D175: F is the cutting feed CYCLE_F on G90 and G94 and the thread lead PITCH on
    // G92, and R is the taper of the pass, the catalog word TAPER, a radius value (D60). X(U), Z(W) and R are modal
    // data the cycles share, so a repeat block with a new value is a new CYCLE block, because a new CYCLE replaces
    // all parameters (language 4.7). A word the entry does not map keeps the block RAW (D5).
    private static void ReadTurning(FanucBlock block, FanucCycle cycle, bool defines)
    {
        CycleEntry entry = cycle.Entry!;
        List<FanucAxisWord> positions = Positions(block, cycle);
        if (block.Draft.IsRaw || (!defines && positions.Count == 0))
        {
            return;
        }

        // D114, D175: the taper R of G90, G92 and G94 is the catalog word TAPER, a parameter name of the entry, which
        // the parser takes in a block whose CYCLE is a catalog name (language 4.7.1, the third provisional form).
        string? taperAddress = entry.Params.GetValueOrDefault(TaperWord);
        if (taperAddress is not null && block.Take(taperAddress) is SourceWord taper)
        {
            if (FanucMacro.ValueOf(block, taper) is not Value value)
            {
                return;
            }

            cycle.Taper = value;
        }

        SourceWord? feed = block.Take("F");
        if (feed is not null)
        {
            bool lead = entry.Params.GetValueOrDefault(PitchWord) == "F";
            if (!lead && entry.CycleF is null)
            {
                block.Draft.KeepAsRaw($"F of {cycle.Code} has no NCX word");
                return;
            }

            if (FanucMacro.ValueOf(block, feed) is Value value)
            {
                if (lead)
                {
                    cycle.Pitch = value;
                }
                else
                {
                    cycle.Feed = value;
                    block.Fanuc.ControlFeed = value;
                }
            }
        }

        foreach (FanucAxisWord position in positions)
        {
            block.MarkRead(position.Word);
        }

        var definition = new DraftBlock().Add("CYCLE", new IdentValue(entry.Name));
        if (entry.CycleF is not null && cycle.Feed is Value cycleFeed)
        {
            definition.Add("CYCLE_F", cycleFeed);
        }

        if (entry.Params.ContainsKey(PitchWord) && cycle.Pitch is Value cyclePitch)
        {
            definition.Add(PitchWord, cyclePitch);
        }

        if (taperAddress is not null && cycle.Taper is Value cycleTaper)
        {
            definition.Add(TaperWord, cycleTaper);
        }

        Write(block, cycle, definition);
        CallTurning(block, cycle, positions);
    }

    // D187: the control brings the tool back to the start point A after a simple turning cycle, so no axis has moved
    // after the call and none becomes unknown; the words of a call are the end point of one pass, not where the tool
    // stands. X(U) and Z(W) are modal data the three cycles share, so an omitted word repeats the previous value and
    // an incremental U or W counts from the start point. The reader therefore writes both end-point words on every
    // CYCLE_CALL, absolutely, and leaves the source-side position where it is (decisions.md; fanuc 6; language 4.7,
    // CYCLE_CALL).
    private static void CallTurning(FanucBlock block, FanucCycle cycle, List<FanucAxisWord> positions)
    {
        foreach (FanucAxisWord position in positions)
        {
            if (FanucMacro.ValueOf(block, position.Word) is not Value value)
            {
                return;
            }

            if (FanucNumbers.NumberOf(value) is not decimal number)
            {
                block.Draft.KeepAsRaw($"the end point of the pass of {cycle.Code} is no number the reader knows");
                return;
            }

            if (position.Incremental)
            {
                if (!block.State.Positions.TryGetValue(position.Axis, out decimal start))
                {
                    block.Draft.KeepAsRaw(
                        $"{position.Word.Address} of {cycle.Code} counts from a start point the reader does not know");
                    return;
                }

                number += start;
            }

            if (!cycle.TurningEnd.ContainsKey(position.Axis))
            {
                cycle.TurningAxes.Add(position.Axis);
            }

            cycle.TurningEnd[position.Axis] = FanucNumbers.Of(number);
        }

        DraftBlock call = block.Draft.Main;
        if (call.Verb is not null || call.Has("CYCLE", null))
        {
            call = new DraftBlock();
            block.Draft.After.Add(call);
        }

        call.WithVerb("CYCLE_CALL");
        foreach (string axis in cycle.TurningAxes)
        {
            call.Add(axis, cycle.TurningEnd[axis]);
        }
    }
}
