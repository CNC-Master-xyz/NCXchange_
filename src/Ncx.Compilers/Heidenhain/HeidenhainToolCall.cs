using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// TOOL CALL and TOOL DEF (controllers heidenhain.md 4; 8 rule 3; controller-mapping 1 WORKPLANE, 3; machine-config 3;
/// virtual machine 3.5): TOOL CALL n axis S assembled from TOOL, WORKPLANE and the RPM of the same or the following
/// block, S1592 or SQ2 of a Q parameter (controllers heidenhain.md 6), TOOL DEF n for PRELOAD, the offsets implicit in
/// the call (offsets_with_change), and a speed without a tool through the RPM template of the spindle.
/// </summary>
internal static class HeidenhainToolCall
{
    /// <summary>
    /// What the control has active under this key of the target state: the tool axis of the last TOOL CALL, which only
    /// a TOOL CALL writes; a jump that reaches a label with another one is checked where it stands (CheckArrival).
    /// </summary>
    public const string PlaneKey = "PLANE";

    // What the control has active: the speed of each spindle (TargetState).
    private const string SpeedKey = "RPM:";

    /// <summary>
    /// Writes the tool words of the block: the change, the preload, a speed without a tool, and the working plane,
    /// which only a TOOL CALL writes.
    /// </summary>
    public static void Write(HeidenhainBlock writing)
    {
        TakeOffsets(writing);
        WriteChange(writing);
        WritePointChange(writing);
        WritePreload(writing);
        WriteSpeeds(writing);
        CheckWorkplane(writing);
    }

    // The called tool with the index of its named point: TOOL CALL 5.1 for point 2, the plain number for point 1 and
    // for a tool whose program named no point (D373 improvement 1, D392; iTNC 530 Klartext 2010 p. 165-166).
    private static void SetIndexedTool(HeidenhainBlock writing, TemplateValues values)
    {
        if (writing.PointRegisters?.Index is int index and > 0
            && writing.After.LastHolder is string holder
            && writing.After.Holders.TryGetValue(holder, out HolderSnapshot? state)
            && state.SpindleTool.Number is int number)
        {
            values.Set("tool", string.Create(CultureInfo.InvariantCulture, $"{number}.{index}"));
        }
    }

    // A point change of the tool in the spindle is an indexed TOOL CALL with the tool axis and the speed the control
    // has (D373 improvement 1). The call carries a tool number, so the TNC 640 runs its change macro (Klartext
    // 892903-18 p. 123), and on the iTNC 530 that depends on MP7480.0 and the builder's macro (Technisches Handbuch
    // p. 1614): the WARNING CMP123, unless point_change = "call" says the builder accepts it. TIP_OFFSET and
    // POINT_RADIUS are never written, no DL, DR or DR2 (D375).
    private static void WritePointChange(HeidenhainBlock writing)
    {
        writing.TakeAll("TIP_OFFSET");
        writing.TakeAll("POINT_RADIUS");
        if (writing.TakeAll("TOOL_POINT").Count == 0 || writing.Block.Has("TOOL")
            || writing.PointRegisters?.Index is not int
            || writing.After.LastHolder is not string holder
            || !writing.After.Holders.TryGetValue(holder, out HolderSnapshot? state)
            || state.SpindleTool == new ToolRef(0))
        {
            return;
        }

        MachineConfig machine = writing.Machine;
        string? spindle = SpindleOf(machine, holder);
        if (SpeedOf(writing, writing.Step, spindle) is not string speed)
        {
            return;
        }

        var values = new TemplateValues();
        values.Set("tool", state.SpindleTool.ToString());
        SetIndexedTool(writing, values);
        values.Set("rpm", speed);
        values.Set("axis", HeidenhainAxes.LetterOf(writing, HeidenhainAxes.ToolAxis(writing.After.Frame.Workplane)));
        writing.Template(machine.ToolChange?.Change, "[tool_change] change (machine-config 3, D373)", values);
        if (machine.ToolPoints is { PointChangeByCall: false })
        {
            writing.Warning(DiagnosticCodes.HeidenhainToolPoint,
                $"Point {state.ToolPoint.ToString(CultureInfo.InvariantCulture)} of tool {state.SpindleTool} is "
                + "selected by an indexed TOOL CALL of the tool in the spindle, which runs the change macro on the "
                + "TNC 640 and on the iTNC 530 depending on MP7480.0 and the builder's macro; point_change = \"call\" "
                + "says the builder accepts it (machine-config 3, D373).");
        }
    }

    // TOOL CALL n axis S: the tool, the tool axis of the working plane and the speed in one call (heidenhain 8 rule 3,
    // virtual machine 3.5). The template of [tool_change] fills {tool} with the number of the tool in the spindle,
    // which is how a bare TOOL gets its number (D91); the compiler gives {axis} and {rpm}. An F of a TOOL block is the
    // modal feed of the control and is folded into the call behind the template (D251, heidenhain 8 rule 3).
    private static void WriteChange(HeidenhainBlock writing)
    {
        if (writing.Take("TOOL") is null)
        {
            return;
        }

        if (writing.Block.Skip)
        {
            ReportSkip(writing);
            return;
        }

        MachineConfig machine = writing.Machine;
        string? holder = writing.After.LastHolder;
        string? spindle = SpindleOf(machine, holder);
        BlockStep? following = FollowingBlock(writing);

        // The RPM and the WORKPLANE of the same block, or else of the following one (heidenhain 8 rule 3).
        BlockStep speedStep = !HasSpeed(machine, writing.Block, spindle)
            && following is not null && HasSpeed(machine, following.Block, spindle)
            ? following
            : writing.Step;
        BlockStep planeStep = !writing.Block.Has("WORKPLANE") && following?.Block.Has("WORKPLANE") == true
            ? following
            : writing.Step;
        string axis = HeidenhainAxes.LetterOf(writing, HeidenhainAxes.ToolAxis(planeStep.After.Frame.Workplane));
        if (SpeedOf(writing, speedStep, spindle) is not string speed)
        {
            return;
        }

        var values = new TemplateValues();
        values.Set("rpm", speed);
        values.Set("axis", axis);
        SetIndexedTool(writing, values);
        writing.WriteToolChange(values, FoldedFeed(writing));
        TakeSpeeds(writing, spindle);
        writing.Take("WORKPLANE");

        // The call puts the tool axis and the speed on the control; TOOL CALL 0 empties the spindle (heidenhain 4).
        bool emptied = holder is not null && writing.After.Holders.TryGetValue(holder, out HolderSnapshot? state)
            && state.SpindleTool == new ToolRef(0);
        if (!emptied && spindle is not null)
        {
            writing.Target.Set(PlaneKey, axis);
            writing.Target.Set(SpeedKey + spindle, ActiveSpeed(writing, speedStep, spindle, speed));
        }
    }

    // The feed of a TOOL block, written behind the TOOL CALL where it changes, as the control takes it there (D251,
    // heidenhain 8 rule 3; controllers heidenhain.md 4). A block that also moves writes its feed with its motion.
    private static string? FoldedFeed(HeidenhainBlock writing)
    {
        return writing.Block.Verb is null && writing.Block.Has("F") ? HeidenhainMotion.Feed(writing) : null;
    }

    // TOOL DEF n prepares the next tool, PRELOAD=n (controllers heidenhain.md 4; heidenhain 8 rule 3).
    private static void WritePreload(HeidenhainBlock writing)
    {
        if (writing.TakeAll("PRELOAD").Count == 0)
        {
            return;
        }

        if (writing.Block.Skip)
        {
            ReportSkip(writing);
            return;
        }

        writing.WritePreload();
    }

    // A speed without a tool change is written with the RPM template of the spindle, on change (heidenhain 4; VM 3.8
    // rule 2a); machines/heidenhain-itnc530.toml writes it TOOL CALL S{rpm}, whose acceptance by the iTNC 530 is D180.
    private static void WriteSpeeds(HeidenhainBlock writing)
    {
        if (writing.Block.Has("TOOL"))
        {
            return;
        }

        MachineConfig machine = writing.Machine;
        foreach (Word word in writing.TakeAll("RPM"))
        {
            string? spindle = word.Addr is string role ? machine.ResolveRole(role)?.Id : SpindleOf(machine, null);
            if (spindle is null || SpeedOf(writing, writing.Step, spindle) is not string speed
                || HasSpeedActive(writing, spindle, speed))
            {
                continue;
            }

            writing.Target.Set(SpeedKey + spindle, ActiveSpeed(writing, writing.Step, spindle, speed));

            string? tableRole = word.Addr ?? HeidenhainFunctions.RoleOf(machine, spindle);
            string? template = null;
            if (tableRole is not null && machine.SpindleTables.TryGetValue(tableRole, out FunctionTable? table))
            {
                table.States.TryGetValue("RPM", out template);
            }

            var values = new TemplateValues();
            values.Set("rpm", speed);
            writing.Template(template, $"[spindle.{tableRole}] RPM (machine-config 5)", values);
        }
    }

    // Klartext gives the working plane with the tool axis of TOOL CALL (controller-mapping 1, WORKPLANE; heidenhain 8
    // rule 3), so a WORKPLANE writes nothing of its own; it is folded into the TOOL CALL of its block or of the block
    // before it.
    // D321: Klartext fixes the working plane at the tool call with the spindle axis, and no other block sets it, so a
    // WORKPLANE that changes the tool axis of the last TOOL CALL without a TOOL in its block, and not in the following
    // block of a TOOL block (D275), is an ERROR (CMP110, decisions.md); where no TOOL CALL has been written the
    // control's plane is not known and nothing is written.
    private static void CheckWorkplane(HeidenhainBlock writing)
    {
        if (writing.Take("WORKPLANE") is not Word plane || writing.Block.Has("TOOL"))
        {
            return;
        }

        string axis = HeidenhainAxes.LetterOf(writing, HeidenhainAxes.ToolAxis(writing.After.Frame.Workplane));
        if (writing.Target.ActiveOf(PlaneKey) is string active && active != axis)
        {
            writing.Error(DiagnosticCodes.HeidenhainWorkplaneWithoutToolCall,
                $"{plane.ToCanonical()} changes the tool axis {active} of the last TOOL CALL to {axis} without a TOOL "
                + "in its block; Klartext gives the working plane with the tool axis of TOOL CALL (controllers "
                + "heidenhain.md 8 rule 3; controller-mapping 1, WORKPLANE).");
        }
    }

    /// <summary>
    /// A JUMP or a REPEAT brings the control to its label with the tool axis of the last TOOL CALL before the jump, and
    /// a WORKPLANE after the label writes nothing up to the next TOOL CALL, since Klartext gives the working plane with
    /// the tool axis of TOOL CALL (controller-mapping 1, WORKPLANE; heidenhain 8 rule 3; language 4.9): one that names
    /// another tool axis is reported at the jump (CMP110), as CheckWorkplane reports it on the way the text runs.
    /// </summary>
    /// <param name="writing">The block being written, with the JUMP or REPEAT.</param>
    /// <param name="jump">The JUMP or REPEAT word.</param>
    /// <param name="label">The step of the label in the walk order of the look-ahead.</param>
    public static void CheckArrival(HeidenhainBlock writing, Word jump, int label)
    {
        if (writing.Target.ActiveOf(PlaneKey) is not string active)
        {
            return;
        }

        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        for (int index = label; index < steps.Count && !HeidenhainArrivals.EndsTheRun(steps[index]); index++)
        {
            Block block = steps[index].Block;
            if (block.Has("TOOL"))
            {
                return;
            }

            if (block.Find("WORKPLANE") is not Word plane)
            {
                continue;
            }

            string axis = HeidenhainAxes.LetterOf(writing, HeidenhainAxes.ToolAxis(steps[index].After.Frame.Workplane));
            if (axis != active)
            {
                writing.Error(DiagnosticCodes.HeidenhainWorkplaneWithoutToolCall,
                    $"{jump.ToCanonical()} reaches LBL {HeidenhainFlow.Label(jump.Value)} with the tool axis {active} "
                    + $"of the last TOOL CALL before it, and the {plane.ToCanonical()} on line {block.Line} after the "
                    + $"label names the tool axis {axis} without a TOOL in its block; Klartext gives the working plane "
                    + "with the tool axis of TOOL CALL (controllers heidenhain.md 8 rule 3; controller-mapping 1, "
                    + "WORKPLANE; language 4.9).");
                return;
            }
        }
    }

    // offsets_with_change: the length and the radius come from the tool table with the TOOL CALL, and the OFFSET
    // words are not written (machine-config 3; controllers heidenhain.md 4; language 2 rule 4).
    private static void TakeOffsets(HeidenhainBlock writing)
    {
        if (writing.Machine.ToolChange?.OffsetsWithChange == true)
        {
            writing.TakeAll("OFFSET");
        }
    }

    // D275: the following block of heidenhain 8 rule 3 is the first block after the TOOL block, in the same section,
    // that is not a block of PRELOAD words alone (decisions.md). A TOOL DEF has no speed field, so the PRELOAD=2 that
    // Expected/BOHREN.ncx carries between TOOL=1 and its RPM, which TOOL DEF writes right after the call
    // (preload_position = "after_change"), is never the block the speed comes from.
    private static BlockStep? FollowingBlock(HeidenhainBlock writing)
    {
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        for (int index = writing.Step.Index + 1; index < steps.Count; index++)
        {
            BlockStep next = steps[index];
            if (next.Section != writing.Step.Section || next.Block.Has("SUB") || next.Block.Has("PROGRAM"))
            {
                return null;
            }

            if (!IsPreloadOnly(next.Block))
            {
                return next;
            }
        }

        return null;
    }

    private static bool IsPreloadOnly(Block block)
    {
        foreach (Word word in block.Words)
        {
            if (word.Key != "PRELOAD")
            {
                return false;
            }
        }

        return block.Words.Count > 0;
    }

    // Whether a block sets the speed of the spindle (SpeedWord).
    private static bool HasSpeed(MachineConfig machine, Block block, string? spindle)
    {
        return SpeedWord(machine, block, spindle) is not null;
    }

    // The word of a block that sets the speed of the spindle: RPM without an address for the default spindle, RPM:role
    // for the spindle of the role (language 4.5, virtual machine 3.8 rules 1 and 2); null where it has none.
    private static Word? SpeedWord(MachineConfig machine, Block block, string? spindle)
    {
        foreach (Word word in block.Words)
        {
            if (word.Key != "RPM")
            {
                continue;
            }

            string? target = word.Addr is string role ? machine.ResolveRole(role)?.Id : SpindleOf(machine, null);
            if (target == spindle)
            {
                return word;
            }
        }

        return null;
    }

    private static void TakeSpeeds(HeidenhainBlock writing, string? spindle)
    {
        foreach (Word word in writing.Block.Words)
        {
            string? target = word.Addr is string role
                ? writing.Machine.ResolveRole(role)?.Id
                : SpindleOf(writing.Machine, null);
            if (word.Key == "RPM" && target == spindle)
            {
                writing.MarkWritten(word);
            }
        }
    }

    // The speed of the spindle after a step as S writes it, 1592, or the Q parameter that set it, Q2; null when it has
    // no Klartext form, which is reported.
    private static string? SpeedOf(HeidenhainBlock writing, BlockStep step, string? spindle)
    {
        if (spindle is null || !step.After.Spindles.TryGetValue(spindle, out SpindleSnapshot? state))
        {
            return writing.Numbers.FormatSpeed(0m, writing.Block);
        }

        if (step.After.Unknown.Contains(SpeedKey + spindle))
        {
            return ParameterSpeed(writing, step, spindle);
        }

        return writing.Numbers.FormatSpeed(state.Rpm, writing.Block);
    }

    // A speed that the virtual machine keeps UNKNOWN comes from an expression (virtual machine 1), and a Q parameter
    // may stand wherever a number stands (controllers heidenhain.md 6), so S takes the parameter, SQ2: the one of the
    // RPM of the step, or of the earlier block that set the speed, where it still has the value NCX read at that word
    // (HeidenhainParameters). S of TOOL CALL and of the RPM template stands before the VAR lines of its block.
    private static string? ParameterSpeed(HeidenhainBlock writing, BlockStep step, string spindle)
    {
        MachineConfig machine = writing.Machine;
        if (SpeedSource(writing, step, spindle) is not int source)
        {
            writing.Error(DiagnosticCodes.HeidenhainValueWithoutKlartext,
                "The speed of the spindle comes from an expression that no RPM word before this block states, so "
                + "Klartext has no S for it (virtual machine 1, 3.10; controllers heidenhain.md 6).");
            return null;
        }

        Word word = SpeedWord(machine, writing.LookAhead.Steps[source].Block, spindle)
            ?? throw new InvalidOperationException("The source of the speed has no RPM.");
        if (HeidenhainParameters.Of(word.Value) is not string parameter)
        {
            HeidenhainParameters.ReportValue(writing, word);
            return null;
        }

        return HeidenhainParameters.Keeps(writing, word, parameter, source, afterVariables: false) ? parameter : null;
    }

    // The step of the RPM word that set a speed the virtual machine keeps UNKNOWN (HeidenhainParameters.SourceOf).
    private static int? SpeedSource(HeidenhainBlock writing, BlockStep step, string spindle)
    {
        MachineConfig machine = writing.Machine;
        return HeidenhainParameters.SourceOf(writing, step.Index, SpeedKey + spindle,
            block => SpeedWord(machine, block, spindle));
    }

    // What the control has active once S wrote the speed of a step: the number, or the Q parameter with the step of the
    // RPM word it was read for (HeidenhainParameters.Active).
    private static string ActiveSpeed(HeidenhainBlock writing, BlockStep step, string spindle, string speed)
    {
        return step.After.Unknown.Contains(SpeedKey + spindle) && SpeedSource(writing, step, spindle) is int source
            ? HeidenhainParameters.Active(speed, source)
            : speed;
    }

    // Whether the control has the speed of the block active already, and the RPM template writes nothing: the same
    // number, or the Q parameter read for the same RPM word, or for an earlier one while the parameter kept its value
    // (HeidenhainParameters.Holds).
    private static bool HasSpeedActive(HeidenhainBlock writing, string spindle, string speed)
    {
        string? active = writing.Target.ActiveOf(SpeedKey + spindle);
        if (!writing.After.Unknown.Contains(SpeedKey + spindle))
        {
            return active == speed;
        }

        return SpeedSource(writing, writing.Step, spindle) is int source
            && HeidenhainParameters.Holds(writing, active, speed, source);
    }

    // The spindle of a holder, or the default spindle for a holder without one (virtual machine 3.8 rule 2).
    private static string? SpindleOf(MachineConfig machine, string? holder)
    {
        string? spindle = holder is null ? null : machine.FindResource(holder)?.Spindle;
        return spindle ?? machine.ResolveDefaultSpindle()?.Id;
    }

    private static void ReportSkip(HeidenhainBlock writing)
    {
        // TODO: the framework writes the tool change and the preload with CompilerBase.Line, which knows no block
        // skip; a SKIP block with TOOL or PRELOAD is reported until the framework writes the / of the block.
        writing.Error(DiagnosticCodes.HeidenhainSkipOnToolChange,
            "A block with SKIP that changes or preloads a tool is not written: the / of the optional skip "
            + "(controllers heidenhain.md 1) would not stand in front of its TOOL CALL or TOOL DEF.");
    }
}
