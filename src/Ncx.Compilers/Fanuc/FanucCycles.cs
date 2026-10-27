using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The canned cycles of a Fanuc block (controllers fanuc.md 6; controller-mapping 5; language 4.7, 4.7.1;
/// machine-config 6): a cycle block defines and calls at its position, so the CYCLE definition of NCX is written with
/// its first CYCLE_CALL, the native code of the catalog with G98 or G99 from CYCLE_RETRACT, R from CLEARANCE, Z from
/// DEPTH, Q, P and F; every later call writes its position alone; CYCLE=OFF is G80. On a lathe G83 to G85 drill along Z
/// and G87 to G89 along X, and the simple turning cycles are modal as the drilling cycles are.
/// </summary>
internal static class FanucCycles
{
    /// <summary>
    /// The key of the definition the control has active in the target state, DefinitionOf.
    /// </summary>
    public const string Definition = "CYCLE:DEF";

    // The key of the initial level of the run of a lathe drilling cycle in the target state (D248).
    private const string InitialLevel = "CYCLE:INITIAL";

    // The words of a CYCLE block (language 4.7).
    private static readonly string[] s_cycleWords =
    [
        "CYCLE", "AXIS", "SURFACE", "CLEARANCE", "DEPTH", "SAFE", "CYCLE_RETRACT", "PECK", "CYCLE_F", "CYCLE_DWELL",
        "PITCH", "CONTOUR",
    ];

    // The drilling codes of a Fanuc lathe: G83, G84 and G85 drill along Z, G87, G88 and G89 along X, and nothing else
    // stands in that group (controllers fanuc.md 6; D160).
    private static readonly string[] s_latheDrillingCodes = ["G83", "G84", "G85", "G87", "G88", "G89"];

    // The simple turning cycles of system A and their codes in the systems B and C (controllers fanuc.md 3, 6; D174:
    // the lathe manual gives G90/G92/G94 = G77/G78/G79 = G20/G21/G24).
    private static readonly Dictionary<string, string> s_turningOfSystemB = new(StringComparer.Ordinal)
    {
        ["G90"] = "G77",
        ["G92"] = "G78",
        ["G94"] = "G79",
    };

    private static readonly Dictionary<string, string> s_turningOfSystemC = new(StringComparer.Ordinal)
    {
        ["G90"] = "G20",
        ["G92"] = "G21",
        ["G94"] = "G24",
    };

    /// <summary>
    /// The tool axis of a working plane, the drilling axis of a mill (controller-mapping 5, AXIS).
    /// </summary>
    public static string ToolAxisOf(Workplane plane)
    {
        return plane switch
        {
            Workplane.ZX => "Y",
            Workplane.YZ => "X",
            _ => "Z",
        };
    }

    /// <summary>
    /// The active cycle of NCX as the target state keeps its definition: the name, the drilling axis and the
    /// parameters (language 4.7); null where no cycle is active.
    /// </summary>
    public static string? DefinitionOf(CycleSnapshot cycle)
    {
        return cycle.Name is string name
            ? name + " " + cycle.Axis + " " + string.Join(" ", ParameterTexts(cycle))
            : null;
    }

    /// <summary>
    /// A canned cycle the control has active, or may have active, ends with G80 before a tool change (virtual machine
    /// 4, cycle row).
    /// </summary>
    public static void CancelBeforeToolChange(FanucBlock write)
    {
        Cancel(write);
    }

    /// <summary>
    /// A canned cycle the control has active, or may have active, ends with G80 before a motion that is no call, since
    /// a position block under an active G8x drills (controllers fanuc.md 6).
    /// </summary>
    public static void CancelBeforeMotion(FanucBlock write)
    {
        // D247: a code of group 01 ends the cycle by itself, and G80 before the motion says so in the text as well
        // (fanuc 6, 9 rule 5; language 4.7; decisions.md).
        Cancel(write);
    }

    /// <summary>
    /// The cycle words of the block: CYCLE=OFF as G80 where the control has a cycle active, a definition that waits for
    /// its call, and CYCLE_CALL (controllers fanuc.md 6; controller-mapping 5).
    /// </summary>
    public static void Write(FanucBlock write)
    {
        Block block = write.Block;
        if (block.Find("CYCLE") is Word cycle)
        {
            // A CYCLE block defines the cycle of NCX on every path from here on (language 4.7).
            write.StatesForLater(Definition, known: true);
            string name = cycle.Value.ToCanonical();
            // D160: the entry of a name under the drilling axis the block leaves, so that a lathe checks the words
            // against the G87 entry under AXIS=X (machine-config 6, for_axis).
            CycleEntry? defined = name == "OFF"
                ? null
                : CatalogOf(write).Find(name, write.After.Cycle.Axis);
            CheckCarried(write, defined);
            if (name == "OFF" && write.Target.ActiveOf(FanucCodes.Cycle) != "G80")
            {
                write.Main.Code(FanucCodes.RankOf(FanucCodes.Cycle), "G80");
                Ended(write);
            }
        }

        if (block.Verb?.Key == "CYCLE_CALL")
        {
            WriteCall(write);
        }
    }

    // A word of a CYCLE block stands in the cycle block through the address its catalog entry maps, or through a rule
    // of the Fanuc compiler that the entry carries: SURFACE, SAFE and CYCLE_RETRACT of a drilling entry, AXIS, and the
    // PITCH of TAP, which is F (machine-config 6; controller-mapping 5; CycleEntry.Words). Any other word of the block
    // is CMP385, never dropped (language 2 rule 8): PITCH of THREAD, whose G92 has no address for it in the catalog,
    // and DEPTH and CLEARANCE of TURN_OD, whose Z and R are no planes of G90. A cycle that the catalog does not name
    // is CMP380 at its call.
    // D157: the words a rule carries follow from the params of every entry (CycleEntry.RuleWords), an entry of a name
    // of its own included.
    // D175: the lead of G92 and G76 is the word PITCH, written through the address the entry maps. D114: the taper R
    // of G90, G92 and G94 is the catalog word TAPER, a parameter name of the entry, carried through its address.
    private static void CheckCarried(FanucBlock write, CycleEntry? entry)
    {
        write.Written("CYCLE");
        if (entry is not null)
        {
            foreach (string parameter in CatalogParameters(entry))
            {
                if (write.Block.Find(parameter) is Word catalogWord)
                {
                    write.Written(catalogWord);
                }
            }
        }

        foreach (string key in s_cycleWords)
        {
            if (key == "CYCLE" || write.Block.Find(key) is not Word word)
            {
                continue;
            }

            write.Written(word);
            // D362: a CYCLE_DWELL of 0 is the behaviour the control has anyway and is dropped silently; G81, G83, G73
            // and G86 have no P (D177).
            if (entry is not null && key == "CYCLE_DWELL" && !IsZero(word.Value) && entry.CycleDwell is null
                && DwellEntry(write, entry) is null)
            {
                // D177: PECK, CHIP_BREAK and BORE have no dwell form on a machining centre; the dwell is dropped.
                write.Warning(DiagnosticCodes.FanucCycleDwellDropped,
                    $"{word.ToCanonical()} of CYCLE={entry.Name}: {entry.Native} takes no dwell P on this machine, so "
                    + "the dwell is dropped (controllers fanuc.md 6; D177).");
                continue;
            }

            bool carried = entry is null
                || (key == "CYCLE_DWELL" && (IsZero(word.Value) || entry.CycleDwell is null))
                || (entry.Words.Contains(key)
                    && (key != "PITCH" || entry.CycleF is not null || entry.NativeOf(key) is not null));
            if (!carried)
            {
                write.Error(DiagnosticCodes.FanucCycleWordNotMapped,
                    $"{word.ToCanonical()} of CYCLE={entry!.Name} maps to no address of {entry.Native} in the catalog, "
                    + "and no rule of the Fanuc compiler carries it, so the cycle cannot be written without losing it "
                    + "(machine-config 6; controller-mapping 5; language 2 rule 8).");
            }
        }
    }

    // D177: the entry that writes a DRILL with a dwell other than 0, the DRILL_DWELL of the catalog where it maps
    // CYCLE_DWELL; null for every other entry.
    private static CycleEntry? DwellEntry(FanucBlock write, CycleEntry entry)
    {
        return entry.Name == "DRILL" && CatalogOf(write).Find("DRILL_DWELL", write.After.Cycle.Axis) is
        { CycleDwell: not null } dwell
            ? dwell
            : null;
    }

    // Whether the cycle carries a CYCLE_DWELL other than 0 (D177); an expression counts as a dwell.
    private static bool HasADwell(CycleSnapshot cycle)
    {
        return Parameter(cycle, "CYCLE_DWELL") is Value dwell && !IsZero(dwell);
    }

    // D361, D377: the catalog of the channel compiled, the [[cycle]] tables of the machine file that name it over the
    // channel-less entries (CycleCatalog.ForChannel), so that the rigid-tapping line WriteBefore writes is the line of
    // that channel; a Fanuc-family machine that names no rigid-tapping code keeps the catalog's M29 S{rpm}, the Fanuc
    // standard (parameter 5210 = 0). A cycle available = false on the channel is CMP004 of the compiler framework.
    private static CycleCatalog CatalogOf(FanucBlock write)
    {
        CycleCatalog catalog = write.Machine.CycleCatalog;
        return catalog.ChannelEntries.Count == 0 ? catalog : catalog.ForChannel(write.After.ChannelId);
    }

    // D114: the params of an entry whose key is no word of a CYCLE block of language 4.7, TAPER of the turning cycles.
    private static List<string> CatalogParameters(CycleEntry entry)
    {
        var parameters = new List<string>();
        foreach (string key in entry.Params.Keys)
        {
            if (!s_cycleWords.Contains(key))
            {
                parameters.Add(key);
            }
        }

        return parameters;
    }

    // Whether a word's value is the number 0 (D362).
    private static bool IsZero(Value value)
    {
        return value switch
        {
            IntegerValue integer => integer.Number == 0,
            DecimalValue number => number.Number == 0m,
            _ => false,
        };
    }

    // G80 in a line of its own where the control has a canned cycle active, or where the target state does not know
    // whether it has one: in a subprogram, which is written from an unknown target state (D99), and after a label that
    // a jump reaches; a caller of the subprogram may have left a cycle active, and in NCX a RAPID, LINE, ARC or HOME
    // never calls one (language 4.7, CYCLE_CALL). G80 without an active cycle changes nothing.
    private static void Cancel(FanucBlock write)
    {
        if (write.Target.ActiveOf(FanucCodes.Cycle) != "G80")
        {
            write.Write("G80");
            Ended(write);
        }
    }

    // After G80 no cycle is active; the cycles stand in group 01 (controllers fanuc.md 3), so the next motion writes
    // its code, and so does the caller's next motion after the return of a subprogram (D99). The cycle of NCX stays
    // active through a RAPID, LINE or ARC (language 4.7), so a definition that differs by path still does
    // (FanucPaths).
    private static void Ended(FanucBlock write)
    {
        write.Target.Set(FanucCodes.Cycle, "G80");
        if (!write.DependsOnThePath(Definition))
        {
            write.Target.Forget(Definition);
        }

        write.MakeUnknown(FanucCodes.Motion);
    }

    // A call writes the definition where the control has another cycle or another definition active, or where it has no
    // position, since a block with a position calls the active cycle and a block without one does not (controllers
    // fanuc.md 6); otherwise the position alone.
    private static void WriteCall(FanucBlock write)
    {
        write.Written("CYCLE_CALL");
        CycleSnapshot cycle = write.After.Cycle;
        List<FanucAxisWord> positions = FanucAxes.Of(write.Block);
        foreach (FanucAxisWord position in positions)
        {
            write.Written(position.Word);
        }

        if (cycle.Name is not string name)
        {
            return;
        }

        if (positions.Exists(position => position.Axis == cycle.Axis) && cycle.Controller is null)
        {
            // D189: a word on the drilling axis in a CYCLE_CALL block of the built-in family is an ERROR of the
            // virtual machine (VM253); this check stays as the compiler's guard, because a Fanuc cycle block would
            // take the word as its depth (decisions.md).
            write.Error(DiagnosticCodes.FanucCycleWordNotMapped,
                $"CYCLE_CALL names the drilling axis {cycle.Axis}, which a Fanuc cycle block takes as its depth "
                + "(language 4.7, CYCLE_CALL; D189).");
            return;
        }

        // The call executes the cycle of NCX of the path the control came along (language 4.7: CYCLE is modal), and
        // after a label that a jump reaches or a skipped block that cycle may differ by path, while the control holds
        // none of them, since Fanuc defines a cycle with its first call: no definition of the STATIC walk is right on
        // every path (virtual machine 1; D53).
        if (write.DependsOnThePath(Definition))
        {
            write.ReportNotHeld("CYCLE");
            return;
        }

        // D160: the compiler looks an entry up by its name together with the drilling axis, so that a lathe takes
        // the G87 entry under AXIS=X and the G83 entry under AXIS=Z (machine-config 6, for_axis).
        CycleEntry? entry = cycle.Controller is null
            ? CatalogOf(write).Find(name, cycle.Axis)
            : null;
        // D144: n of CYCLE:FANUC=n is the native cycle as the family's catalog writes it (G71, "G83.5"), so the
        // block carries it as written; every native parameter follows as its address with its value in source order.
        if (entry is not null && HasADwell(cycle) && entry.CycleDwell is null && DwellEntry(write, entry) is { } dwell)
        {
            // D177: a DRILL with a dwell other than 0 is written as G82, the DRILL_DWELL of the catalog.
            entry = dwell;
        }

        string? code = cycle.Controller is null ? CodeOf(write, entry, cycle) : name;
        if (code is null)
        {
            return;
        }

        if (entry?.DwellNative is string dwellNative && HasADwell(cycle) && code == entry.Native)
        {
            // D177: a REAM with a dwell other than 0 is its dwell_native, G89 on a machining centre.
            code = dwellNative;
        }

        string definition = DefinitionOf(cycle) ?? name;
        bool defines = write.Target.ActiveOf(FanucCodes.Cycle) != code
            || write.Target.ActiveOf(Definition) != definition || positions.Count == 0;
        // A call drills along the tool axis, so a length offset that waits for it stands in the call's block (language
        // 4.4; controllers fanuc.md 6).
        if (!defines)
        {
            if (AddPositions(write, positions))
            {
                FanucToolWords.AddOffsets(write, movesToolAxis: true);
            }

            return;
        }

        FanucMotion.WritePlane(write);
        if (write.System != GcodeSystem.A)
        {
            FanucMotion.Change(write, FanucCodes.Distance, "G90");
        }

        EndAnotherTappingRun(write, entry, code);
        if (entry is not null && !WriteBefore(write, entry, code))
        {
            return;
        }

        write.Main.Code(FanucCodes.RankOf(FanucCodes.Cycle), code);
        if (entry is null)
        {
            AddPositions(write, positions);
            AddNativeParameters(write, cycle);
            FanucToolWords.AddOffsets(write, movesToolAxis: true);
        }
        else if (!AddDefinition(write, entry, cycle, positions))
        {
            return;
        }

        write.Target.Set(FanucCodes.Cycle, code);
        write.Target.Set(Definition, definition);
        write.MakeUnknown(FanucCodes.Motion);
    }

    // The before line of the entry, the native line that belongs to the cycle itself, in a line of its own directly
    // in front of the cycle block: Fanuc M29 S{rpm} makes the tapping of G84 rigid, and a G84 without it is tapping
    // with a compensating chuck (machine-config 6; controllers fanuc.md 6, 10 rule 7; D256). An empty before line is
    // the machine whose parameter makes the cycle so without one. False when the line cannot be written.
    private static bool WriteBefore(FanucBlock write, CycleEntry entry, string code)
    {
        if (string.IsNullOrEmpty(entry.Before))
        {
            return true;
        }

        // The line stands once, in front of the block that enters the mode: the mode holds for the whole run of the
        // cycle, and D256's evidence has an M29 inside a running tapping cycle alarm PS0204, so a second definition
        // of the same cycle - another DEPTH of the same CYCLE=TAP - carries no line. The run ends with G80 or with a
        // code of group 01 (fanuc 6; D247), which Cancel and Ended write and which clears the native code of the
        // target state.
        if (write.Target.ActiveOf(FanucCodes.Cycle) == code
            && write.Target.ActiveOf(Definition) is string active
            && active.StartsWith(entry.Name + " ", StringComparison.Ordinal))
        {
            return true;
        }

        var values = new TemplateValues();
        if (write.TemplateOf(entry.Before).Placeholders.Any(placeholder => placeholder.Name == "rpm"))
        {
            string? id = TapSpindleOf(write);
            string? why = id is null ? "the machine has no spindle" : SpeedMissing(write, id);
            if (id is null || why is not null)
            {
                write.Error(DiagnosticCodes.FanucTapFeedNotKnown,
                    $"CYCLE={entry.Name} is written with the line {entry.Before} in front of it, whose speed is the "
                    + $"speed of the spindle of the current tool holder, and {why} (machine-config 6; "
                    + "controllers fanuc.md 6; D256).");
                return false;
            }

            values.Set("rpm", write.After.Spindles[id].Rpm);
        }

        if (write.Render(entry.Before, $"[[cycle]] {entry.Name} before (machine-config 6, D256)", values)
            is not string text)
        {
            return false;
        }

        write.Write(text);
        return true;
    }

    // D359: TAP and TAP_COMPENSATING share G84 and differ by the before line, so a run of the one does not continue
    // into the other: G80 ends the run first, since the rigid mode the line set holds for the run (D256) and an M29
    // inside a running tapping cycle alarms PS0204.
    private static void EndAnotherTappingRun(FanucBlock write, CycleEntry? entry, string code)
    {
        if (entry is null || write.Target.ActiveOf(FanucCodes.Cycle) != code
            || write.Target.ActiveOf(Definition) is not string active
            || active.StartsWith(entry.Name + " ", StringComparison.Ordinal))
        {
            return;
        }

        string activeName = active.Substring(0, active.IndexOf(' ', StringComparison.Ordinal));
        CycleEntry? previous = CatalogOf(write).Find(activeName, write.After.Cycle.Axis);
        if (!string.IsNullOrEmpty(entry.Before) || !string.IsNullOrEmpty(previous?.Before))
        {
            Cancel(write);
        }
    }

    // The native code of a catalog entry for the machine (machine-config 6): on a mill the drilling entries along the
    // tool axis; on a lathe the drilling codes by AXIS and the simple turning cycles in the numbers of the G-code
    // system.
    private static string? CodeOf(FanucBlock write, CycleEntry? entry, CycleSnapshot cycle)
    {
        if (entry is null)
        {
            write.Error(DiagnosticCodes.FanucCycleNotInCatalog,
                $"CYCLE={cycle.Name} is no cycle of the catalog of the machine (machine-config 6, language 4.7.1).");
            return null;
        }

        // TODO(question): G70 to G76 name their contour by the block numbers P and Q of its first and its last block,
        // and machine-config 6 has the compiler write "the block numbers of that section"; the reader writes the
        // section without labels, and no document gives the numbers, nor where a contour that G71 and G70 share
        // stands in the program; the cycle is CMP384, until that is answered (controllers fanuc.md 6; D65).
        if (!entry.Modal || entry.Contour.Count > 0)
        {
            write.Error(DiagnosticCodes.FanucContourCycleNotWritten,
                $"CYCLE={entry.Name} runs once with the contour P to Q in the program, which the Fanuc compiler does "
                + "not write yet (controllers fanuc.md 6; language 4.7.1; D65).");
            return null;
        }

        bool turning = s_turningOfSystemB.ContainsKey(entry.Native);
        if (turning)
        {
            return TurningCode(write, entry);
        }

        if (write.IsLathe)
        {
            return LatheDrillingCode(write, entry);
        }

        if (cycle.Axis != ToolAxisOf(write.After.Frame.Workplane))
        {
            write.Error(DiagnosticCodes.FanucCycleAxisNotTheToolAxis,
                $"CYCLE={entry.Name} drills along {cycle.Axis}, and a mill drills along the tool axis of its plane "
                + "(controller-mapping 5, AXIS).");
            return null;
        }

        return entry.Native;
    }

    // D174: G90, G92, G94 of system A are G77, G78, G79 in system B and G20, G21, G24 in system C, as the lathe
    // manual gives them; the catalog stays in system A numbers and the compiler translates by gcode_system
    // (controllers fanuc.md 3; language 4.7.1; decisions.md).
    private static string? TurningCode(FanucBlock write, CycleEntry entry)
    {
        string? code = write.System switch
        {
            GcodeSystem.A => entry.Native,
            GcodeSystem.B => s_turningOfSystemB[entry.Native],
            GcodeSystem.C => s_turningOfSystemC[entry.Native],
            _ => null,
        };
        if (code is null)
        {
            write.Error(DiagnosticCodes.FanucCycleNotInCatalog,
                $"CYCLE={entry.Name} is a turning cycle of a Fanuc lathe, and the machine is a mill (controllers "
                + "fanuc.md 3, 6).");
        }

        return code;
    }

    // D160: the canned-cycle group of a Fanuc lathe holds G80 and the two triples G83/G84/G85, which drill along Z,
    // and G87/G88/G89, which drill along X; the code comes from the catalog entry of the name and the axis, and a
    // cycle whose entry maps another native has no lathe code (controllers fanuc.md 6; controller-mapping 5, AXIS;
    // D59, decisions.md). The lathe catalog of the machine carries the entries (cycles/fanuc-lathe.toml).
    private static string? LatheDrillingCode(FanucBlock write, CycleEntry entry)
    {
        if (s_latheDrillingCodes.Contains(entry.Native))
        {
            return entry.Native;
        }

        write.Error(DiagnosticCodes.FanucCycleNotInCatalog,
            $"CYCLE={entry.Name} is {entry.Native} in the catalog of the machine, and the canned-cycle group of a "
            + "Fanuc lathe holds only G83, G84 and G85 along Z and G87, G88 and G89 along X (controllers fanuc.md 6; "
            + "D160).");
        return null;
    }

    // The definition after the code (controllers fanuc.md 6; controller-mapping 5): G98 or G99 from CYCLE_RETRACT, the
    // position, Z from DEPTH, R from CLEARANCE, Q from PECK, P from CYCLE_DWELL in milliseconds, F from CYCLE_F or the
    // pitch of TAP where the control's feed differs (D218), each where the entry maps it; a word it does not map is
    // CMP385 at the CYCLE block (CheckCarried). SURFACE has no Fanuc address (D158).
    private static bool AddDefinition(FanucBlock write, CycleEntry entry, CycleSnapshot cycle,
        List<FanucAxisWord> positions)
    {
        Value? depth = entry.NativeOf("DEPTH") is null ? null : Parameter(cycle, "DEPTH");
        Value? clearance = entry.NativeOf("CLEARANCE") is null ? null : Parameter(cycle, "CLEARANCE");
        if (entry.NativeOf("CLEARANCE") is not null && !AddReturnLevel(write, cycle))
        {
            return false;
        }

        if (!AddPositions(write, positions))
        {
            return false;
        }

        decimal factor = FanucAxes.WordFactor(write, cycle.Axis);
        string address = FanucMotion.DecimalsAddress(write, cycle.Axis);
        if (depth is not null && FanucExpressions.WordValue(write, address, depth, factor) is string z)
        {
            write.Main.Word((FanucAxes.LetterOf(write, cycle.Axis, incremental: false) ?? cycle.Axis) + z);
        }

        // D248: on a lathe R is the signed distance from the initial level, always a radius.
        if (clearance is not null && write.IsLathe)
        {
            if (LatheR(write, cycle, clearance) is not string latheR)
            {
                return false;
            }

            write.Main.Word("R" + latheR);
        }
        else if (clearance is not null && FanucExpressions.WordValue(write, address, clearance, factor) is string r)
        {
            write.Main.Word("R" + r);
        }

        AddMapped(write, entry, cycle, "PECK", "Q", address, 1m);
        foreach (string parameter in CatalogParameters(entry))
        {
            // D114: a parameter name of the catalog entry, TAPER of G90 as R (D175).
            AddMapped(write, entry, cycle, parameter, entry.Params[parameter], entry.Params[parameter], 1m);
        }

        // D177: P stands where the entry maps CYCLE_DWELL, G82, G84, G89 on a machining centre and every lathe entry.
        // D159: a cycle P is round(seconds / dwell_increment), an integer ([machine] dwell_increment, 0.001 s).
        AddDwell(write, entry, cycle);

        // The definition calls at its position and drills along the tool axis: H stands before F, as the sources
        // write G43 Z2. H1 (language 4.4; the TODO(question) of FanucLine).
        FanucToolWords.AddOffsets(write, movesToolAxis: true);
        AddFeed(write, entry, cycle);
        return true;
    }

    // D248: R of a lathe drilling cycle, CLEARANCE minus the initial level, halved on an X axis programmed in
    // diameters, since R is always a radius; the initial level is where the drilling axis stood when the run of the
    // cycle began, which the target state keeps for a definition later in the run. Null, with CMP396, where the level
    // or CLEARANCE is not a known number.
    private static string? LatheR(FanucBlock write, CycleSnapshot cycle, Value clearance)
    {
        decimal? initial = null;
        if (write.Target.ActiveOf(FanucCodes.Cycle) is string active && active != "G80"
            && write.Target.ActiveOf(InitialLevel) is string kept)
        {
            initial = decimal.Parse(kept, CultureInfo.InvariantCulture);
        }
        else if (write.Before.Motion.Position.TryGetValue(cycle.Axis, out AxisPosition position) && position.Known)
        {
            initial = position.Value;
        }

        if (initial is not decimal level || FanucFunctions.NumberOf(clearance) is not decimal plane)
        {
            write.Error(DiagnosticCodes.FanucLatheRWithoutInitialLevel,
                $"R of a lathe drilling cycle is the distance of CLEARANCE from the initial level, where {cycle.Axis} "
                + "stands before the cycle, and that position or CLEARANCE is not known here (controllers fanuc.md 6; "
                + "D248).");
            return null;
        }

        // The virtual machine keeps a diameter axis as a radius, while CLEARANCE is written as the program writes X.
        write.Target.Set(InitialLevel, level.ToString(CultureInfo.InvariantCulture));
        bool diameter = cycle.Axis == "X" && write.Machine.ResolveAxis("X")?.Programming == Programming.Diameter;
        return write.FormatComputed("R", diameter ? (plane / 2m) - level : plane - level);
    }

    // G98 returns to the initial level, the drilling-axis position before the cycle, G99 to R (controllers fanuc.md 3,
    // group 10; controller-mapping 5, CYCLE_RETRACT); in system A the two codes are the feed mode. The word of every
    // CYCLE block is written with the definition (language 4.7: a new CYCLE replaces all parameters).
    private static bool AddReturnLevel(FanucBlock write, CycleSnapshot cycle)
    {
        bool safe = Parameter(cycle, "CYCLE_RETRACT")?.ToCanonical() == "SAFE";
        if (write.System == GcodeSystem.A)
        {
            if (safe)
            {
                write.Error(DiagnosticCodes.FanucSafeRetractOnSystemA,
                    "CYCLE_RETRACT=SAFE has no code on a lathe of G-code system A, whose G98 and G99 are the feed mode "
                    + "(controllers fanuc.md 3).");
                return false;
            }

            return true;
        }

        string code = safe ? "G98" : "G99";
        write.Main.Code(FanucCodes.RankOf(FanucCodes.ReturnLevel), code);
        write.Target.Set(FanucCodes.ReturnLevel, code);
        if (safe && FanucFunctions.NumberOf(Parameter(cycle, "SAFE") ?? NoValue.Instance) is decimal plane
            && write.Before.Motion.Position.TryGetValue(cycle.Axis, out AxisPosition initial) && initial.Known
            && initial.Value != plane)
        {
            write.Warning(DiagnosticCodes.FanucSafeIsNotTheInitialLevel,
                $"G98 returns to the initial level {cycle.Axis}{initial.Value.ToString(CultureInfo.InvariantCulture)}, "
                + $"where the drilling axis stands before the cycle, and SAFE is "
                + $"{plane.ToString(CultureInfo.InvariantCulture)} (controller-mapping 5).");
        }

        return true;
    }

    // The positions of a call, absolute: an incremental position is the point the virtual machine reached, since the
    // planes of the cycle are absolute (controllers fanuc.md 6); in system A U and W as written.
    private static bool AddPositions(FanucBlock write, List<FanucAxisWord> positions)
    {
        foreach (FanucAxisWord position in positions)
        {
            string? word = position.Incremental && write.System != GcodeSystem.A
                ? FanucMotion.AbsoluteWord(write, position)
                : FanucMotion.Word(write, position);
            if (word is null)
            {
                return false;
            }

            write.Main.Word(word);
        }

        return true;
    }

    // A word of the definition through the native address its entry maps (machine-config 6); a word the entry does not
    // map has no Fanuc form and is CMP385 at the CYCLE block (CheckCarried).
    private static void AddMapped(FanucBlock write, CycleEntry entry, CycleSnapshot cycle, string word, string address,
        string decimalsAddress, decimal factor)
    {
        if (Parameter(cycle, word) is not Value value || entry.NativeOf(word) is null)
        {
            return;
        }

        if (FanucExpressions.WordValue(write, decimalsAddress, value, factor) is string text)
        {
            write.Main.Word(address + text);
        }
    }

    // D159: the P of a cycle block, the dwell in units of dwell_increment, an integer for a number; an expression is
    // written with its factor (controllers fanuc.md 7).
    private static void AddDwell(FanucBlock write, CycleEntry entry, CycleSnapshot cycle)
    {
        if (Parameter(cycle, "CYCLE_DWELL") is not Value value || entry.NativeOf("CYCLE_DWELL") is null)
        {
            return;
        }

        decimal increment = write.Machine.Machine.DwellIncrement;
        if (FanucFunctions.NumberOf(value) is decimal seconds)
        {
            decimal units = Math.Round(seconds / increment, 0, MidpointRounding.AwayFromZero);
            write.Main.Word("P" + units.ToString("0", CultureInfo.InvariantCulture));
            return;
        }

        if (FanucExpressions.WordValue(write, "P", value, 1m / increment) is string text)
        {
            write.Main.Word("P" + text);
        }
    }

    // F of the cycle block is CYCLE_F, and it is the control's modal feed as well (D29); a tapping cycle without
    // CYCLE_F feeds with the pitch, F = PITCH x S per minute or PITCH per revolution (controller-mapping 5, TAP). It is
    // not the F of NCX, so the next LINE or ARC writes that again where it differs (FanucBlock.WroteAnotherValue); one
    // from an expression stands every time, since its value may have changed since the last one (controllers fanuc.md
    // 7).
    // TODO(question): D218: the F of the cycle block is written only where the control's feed differs, as D218
    // recommends, until D218 is answered.
    private static void AddFeed(FanucBlock write, CycleEntry entry, CycleSnapshot cycle)
    {
        // D175: the F of a thread turning cycle is the lead itself, which the entry maps as PITCH; it is written as
        // read, while the pitch of TAP becomes a feed per revolution below.
        if (entry.NativeOf("PITCH") == "F" && Parameter(cycle, "PITCH") is Value thread)
        {
            AddFeedWord(write, FanucExpressions.WordValue(write, "F", thread, 1m), thread is not ExprValue);
            return;
        }

        if (entry.CycleF is null)
        {
            return;
        }

        string? feed = null;
        bool known = true;
        if (Parameter(cycle, "CYCLE_F") is Value cycleFeed)
        {
            feed = FanucExpressions.WordValue(write, "F", cycleFeed, 1m);
            known = cycleFeed is not ExprValue;
        }
        else if (entry.Words.Contains("PITCH") && Parameter(cycle, "PITCH") is Value pitch)
        {
            feed = PitchFeed(write, pitch);
            known = pitch is not ExprValue;
        }

        AddFeedWord(write, feed, known);
    }

    // The F of the cycle block, written where the control's feed differs from it (D218).
    private static void AddFeedWord(FanucBlock write, string? feed, bool known)
    {
        if (feed is null)
        {
            return;
        }

        if (!known || write.DependsOnThePath("F") || write.Target.ActiveOf("F") != feed)
        {
            write.Main.Word("F" + feed);
        }

        write.WroteAnotherValue("F", known ? feed : null);
    }

    // The feed of the pitch: PITCH per revolution, PITCH x S per minute (controller-mapping 5, TAP), an expression
    // with its factor as a constant (controllers fanuc.md 7). S is the speed of the spindle that turns the tap, the
    // spindle of the current tool holder, the default spindle where the holder has none (virtual machine 2.3, 5), the
    // driven tool TOOL of a turret. A spindle that is off, a positioning C axis or under constant surface speed, or
    // whose speed is not known, an RPM from an expression in STATIC mode (virtual machine 1), gives no F, and G84 would
    // tap with the feed the control has: an ERROR, never a pitch dropped silently (language 2 rule 8).
    private static string? PitchFeed(FanucBlock write, Value pitch)
    {
        decimal factor = 1m;
        if (write.After.Motion.FeedMode != FeedMode.PerRev)
        {
            string? id = TapSpindleOf(write);
            string? why = id is null ? "the machine has no spindle" : SpeedMissing(write, id);
            if (id is null || why is not null)
            {
                write.Error(DiagnosticCodes.FanucTapFeedNotKnown,
                    $"PITCH={pitch.ToCanonical()} without CYCLE_F feeds with PITCH x S per minute, S the speed of the "
                    + $"spindle that turns the tap, the spindle of the current tool holder, and {why}, so the F of the "
                    + "cycle cannot be written (controller-mapping 5, TAP; virtual machine 2.3).");
                return null;
            }

            factor = write.After.Spindles[id].Rpm;
        }

        return FanucFunctions.NumberOf(pitch) is decimal number
            ? write.FormatComputed("F", number * factor)
            : FanucExpressions.WordValue(write, "F", pitch, factor);
    }

    // The spindle of the current tool holder, the holder of the last TOOL, else the default spindle (virtual machine
    // 2.3, 5; 3.8 rule 2).
    private static string? TapSpindleOf(FanucBlock write)
    {
        string? holder = write.After.LastHolder;
        string? spindle = holder is null ? null : write.Machine.FindResource(holder)?.Spindle;
        return spindle ?? write.Machine.ResolveDefaultSpindle()?.Id;
    }

    // Why the spindle gives no speed for PITCH x S, in words of the spindle by its role (language 4.10); null where it
    // runs at a known speed.
    private static string? SpeedMissing(FanucBlock write, string id)
    {
        string name = id;
        foreach (KeyValuePair<string, string> role in write.Machine.Roles)
        {
            if (role.Value == id)
            {
                name = role.Key;
            }
        }

        if (!write.After.Spindles.TryGetValue(id, out SpindleSnapshot? spindle))
        {
            return $"{name} is no spindle of the machine";
        }

        if (spindle.Mode != SpindleMode.Spindle)
        {
            return $"{name} is a positioning axis (SPINDLE_MODE=AXIS)";
        }

        if (spindle.Direction == SpindleDirection.Off)
        {
            return $"{name} is off";
        }

        if (spindle.Css)
        {
            return $"{name} runs under CSS, whose S is the cutting speed";
        }

        return spindle.Rpm <= 0 || write.After.Unknown.Contains("RPM:" + id)
            ? $"the speed of {name} is not known here"
            : null;
    }

    private static void AddNativeParameters(FanucBlock write, CycleSnapshot cycle)
    {
        foreach (Word parameter in cycle.Parameters)
        {
            if (parameter.Definition is null
                && FanucExpressions.WordValue(write, parameter.Key, parameter.Value, 1m) is string value)
            {
                write.Main.Word(parameter.Key + value);
            }
        }
    }

    private static Value? Parameter(CycleSnapshot cycle, string key)
    {
        foreach (Word word in cycle.Parameters)
        {
            if (word.Key == key && word.Addr is null)
            {
                return word.Value;
            }
        }

        return null;
    }

    private static List<string> ParameterTexts(CycleSnapshot cycle)
    {
        var texts = new List<string>();
        foreach (Word word in cycle.Parameters)
        {
            texts.Add(word.ToCanonical());
        }

        return texts;
    }
}
