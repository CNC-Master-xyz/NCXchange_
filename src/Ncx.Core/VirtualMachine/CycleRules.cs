using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// CYCLE_CALL, the execution of the active cycle (language 4.7, virtual machine 3.3): the sequence of the built-in
/// drilling family along the drilling axis, from the clearance plane to the depth and back to the retract plane, the
/// position afterwards, and the individual motions of the sequence under the option ExpandCycles (D37); a
/// CYCLE:&lt;controller&gt;=n cycle positions the plane axes only (D94).
/// </summary>
internal static class CycleRules
{
    private const string ClearanceKey = "CLEARANCE";
    private const string DepthKey = "DEPTH";
    private const string SafeKey = "SAFE";
    private const string CycleRetractKey = "CYCLE_RETRACT";
    private const string PeckKey = "PECK";
    private const string CycleFeedKey = "CYCLE_F";
    private const string SurfaceKey = "SURFACE";

    // The built-in cycles whose names say how they move (language 4.7): CHIP_BREAK breaks the chip without leaving the
    // hole, TAP backs out along its thread behind the spindle reversal.
    private const string ChipBreakCycle = "CHIP_BREAK";
    private const string TapCycle = "TAP";

    // D190 change 1: REAM feeds out at CYCLE_F like TAP.
    private const string ReamCycle = "REAM";

    // D187: the Fanuc turning cycles that bring the tool back to their start point, so that after their call no axis
    // has moved and none becomes unknown. Operation 4 of G90, G92 and G94 (G77, G78, G79 in the G-code systems B and
    // C) is the return to the start point A, G70 returns in rapid to the cycle start point, note 3 of G73 says the
    // same, and the tool path of G76 is a closed loop back to A (fanuc 6).
    private static readonly string[] s_cyclesReturningToTheStartPoint =
        ["G90", "G92", "G94", "G77", "G78", "G79", "G70", "G73", "G76"];

    /// <summary>
    /// Executes a CYCLE_CALL block.
    /// </summary>
    /// <param name="context">The CYCLE_CALL block with its axis words resolved (step 2).</param>
    /// <param name="expandCycles">The VM option ExpandCycles (D37).</param>
    /// <returns>The individual motions of the sequence under ExpandCycles; empty without it, and for a cycle whose
    /// sequence the VM does not know.</returns>
    public static IReadOnlyList<CycleMotion> Call(BlockContext context, bool expandCycles)
    {
        Block block = context.Block;
        CycleState cycle = context.State.Cycle;

        // CYCLE_CALL requires cycle.name not OFF (virtual machine 3.3, 5), a cycle rule, suppressed inside a subprogram
        // that no program of the file calls (3.9, D99). The axis words of the block position their axes all the same.
        if (cycle.Name is not string name)
        {
            context.CallerRuleDiagnostics.Error(block, DiagnosticCodes.CycleCallWithoutCycle,
                "CYCLE_CALL executes the active cycle, and no CYCLE is defined (virtual machine 3.3).");
            MotionRules.MoveAxes(context);
            return [];
        }

        // D187: a cycle of the cycle catalog (CYCLE=RECT_POCKET, language 4.7.1) is called like the native form of
        // D94, because the VM knows the sequence of the built-in family alone: the call positions the plane axes at
        // the axis words of the call block, the drilling axis is unknown afterwards, and ExpandCycles raises no
        // MOTION events for it (virtual machine 3.3; decisions.md).
        if (cycle.Controller is not null || !DrillingFamily.Names.Contains(name))
        {
            // The one exception: a Fanuc turning cycle brings the tool back to its start point and has no drilling
            // axis, so no axis moves and none becomes unknown; the words of its call are the end point of one pass.
            // TODO(question): no manual page says where G71 type I, G72 type I, G74 and G75 leave the tool, so those
            // four keep the D94 rule and the drilling axis becomes unknown after them.
            if (!ReturnsToTheStartPoint(context, cycle, name))
            {
                PositionTheHoleOnly(context, cycle);
            }

            return [];
        }

        // A built-in drilling cycle requires DEPTH and CLEARANCE written (virtual machine 3.3, 5), a cycle rule,
        // suppressed inside a subprogram that no program of the file calls (3.9, D99). D188: "known" means "written";
        // a DEPTH or CLEARANCE from an expression, unknown in STATIC mode (virtual machine 1), is no ERROR here, it
        // counts toward the WARNING "unresolved expression" like every expression of an executed block, and the
        // positions that follow from it are unknown.
        if (ParameterOf(cycle, DepthKey) is null || ParameterOf(cycle, ClearanceKey) is null)
        {
            context.CallerRuleDiagnostics.Error(block, DiagnosticCodes.CycleCallWithoutDepthOrClearance,
                $"CYCLE_CALL of {name} needs DEPTH and CLEARANCE on its CYCLE block (virtual machine 3.3).");
            PositionTheHoleOnly(context, cycle);
            return [];
        }

        // The drilling axis is AXIS of the cycle, by default the tool axis of the active WORKPLANE (virtual machine
        // 3.3); AXIS naming an axis that is not a linear axis of the machine is an ERROR (virtual machine 5, D59).
        string? drillingAxis = context.Resources.ResolveAxis(cycle.Axis, block, context.State, context.Diagnostics);
        if (drillingAxis is not null && context.Resources.IsRotary(drillingAxis))
        {
            context.Diagnostics.Error(block, DiagnosticCodes.CycleAxisNotLinear,
                $"AXIS={cycle.Axis} names a rotary axis; the drilling axis of a cycle is a linear axis of the machine "
                + "(language 4.7, virtual machine 3.3, D59).");
            drillingAxis = null;
        }

        if (drillingAxis is null)
        {
            MotionRules.MoveAxes(context);
            return [];
        }

        return Drill(context, name, drillingAxis, expandCycles);
    }

    // The sequence of the built-in family (virtual machine 3.3, D190): rapid in the plane to the axis words at the
    // current drilling-axis position (if any), rapid to CLEARANCE, feed to DEPTH with CYCLE_F (pecking with PECK,
    // dwell with CYCLE_DWELL, spindle reversal for TAP), retract to CLEARANCE or SAFE per CYCLE_RETRACT. Position
    // afterwards: plane axes at the call point, drilling axis at the retract plane. The planes are absolute
    // coordinates along the drilling axis in the active frame (language 4.7), and under DIAMETER=ON the X values of an
    // AXIS=X cycle are halved like every other X (virtual machine 3.3, D59, D60).
    private static List<CycleMotion> Drill(BlockContext context, string name, string drillingAxis, bool expandCycles)
    {
        ChannelState state = context.State;
        CycleState cycle = state.Cycle;
        PositionFrame frame = MotionRules.FrameOf(state, cycle.Axis);
        AxisPosition clearance = PlaneAt(state, ClearanceKey, drillingAxis, frame);
        AxisPosition depth = PlaneAt(state, DepthKey, drillingAxis, frame);

        // D188: "known" in virtual machine 3.3 means "written"; a plane without a value, from an expression in STATIC
        // mode or SAFE missing under CYCLE_RETRACT=SAFE, makes the motions to it and the drilling axis after the call
        // unknown. The ERROR for it falls to the compiler that must write the plane (Heidenhain Q204, Siemens RTP).
        bool toSafe = ParameterOf(cycle, CycleRetractKey) is Word retract && BlockContext.IdentOf(retract) == SafeKey;
        AxisPosition retractPlane = toSafe ? PlaneAt(state, SafeKey, drillingAxis, frame) : clearance;

        // D189: a word on the drilling axis in the call block of the built-in family is an ERROR; the cycle's own
        // planes move that axis, and a pre-positioning is a RAPID of its own (virtual machine 3.3, 5; decisions.md).
        ReportDrillingAxisWord(context, drillingAxis);
        Dictionary<string, AxisPosition> hole = PositionTheHole(context);
        state.Motion.Position[drillingAxis] = retractPlane;
        if (!expandCycles)
        {
            return [];
        }

        // With the VM option ExpandCycles the call is raised as the individual MOTION events (virtual machine 3.3,
        // D37), as D190 states them; CYCLE_DWELL is time at DEPTH and moves nothing (the runtime estimate adds it,
        // virtual machine 8).
        decimal? feed = ParameterOf(cycle, CycleFeedKey) is Word feedWord ? MotionRules.NumberOf(feedWord) : null;
        var motions = new List<CycleMotion>();
        if (hole.Count > 0)
        {
            motions.Add(new CycleMotion { Verb = Verb.Rapid, To = hole });
        }

        motions.Add(Rapid(drillingAxis, clearance));
        AxisPosition surface = PlaneAt(state, SurfaceKey, drillingAxis, frame);
        AxisPosition start = ParameterOf(cycle, SurfaceKey) is null ? clearance : surface;
        CycleDistances distances = CycleDistances.Of(context, clearance, surface, start, depth);
        foreach (AxisPosition peck in Pecks(cycle, start, depth))
        {
            motions.Add(Feed(drillingAxis, peck, feed));
            if (name == ChipBreakCycle)
            {
                // D190, D357: CHIP_BREAK lifts by chip_break_lift and feeds on without leaving the hole.
                if (distances.ChipBreakLift > 0)
                {
                    motions.Add(Rapid(drillingAxis, Toward(peck, clearance, distances.ChipBreakLift)));
                }
            }
            else
            {
                // D190, D357: between the steps the tool returns at rapid to CLEARANCE and comes back at rapid to
                // peck_clearance above the depth reached.
                motions.Add(Rapid(drillingAxis, clearance));
                motions.Add(Rapid(drillingAxis, Toward(peck, clearance, distances.PeckClearance)));
            }
        }

        motions.Add(Feed(drillingAxis, depth, feed));
        Retract(motions, name, drillingAxis, feed, clearance, retractPlane, toSafe && distances.ReamFeedsOutToSafe);
        return motions;
    }

    // D190, D378: the retract is a rapid, for BORE after the spindle stop; TAP and REAM feed out at CYCLE_F. TAP feeds
    // only to CLEARANCE on every family, and under CYCLE_RETRACT=SAFE the rest of the way to SAFE is a rapid; REAM
    // feeds under SAFE up to the plane of ream_feed_out (reamToSafe) and rapids the rest.
    private static void Retract(List<CycleMotion> motions, string name, string drillingAxis, decimal? feed,
        AxisPosition clearance, AxisPosition retractPlane, bool reamToSafe)
    {
        if (name != TapCycle && name != ReamCycle)
        {
            motions.Add(Rapid(drillingAxis, retractPlane));
            return;
        }

        if (name == ReamCycle && reamToSafe)
        {
            motions.Add(Feed(drillingAxis, retractPlane, feed));
            return;
        }

        motions.Add(Feed(drillingAxis, clearance, feed));
        if (retractPlane != clearance)
        {
            motions.Add(Rapid(drillingAxis, retractPlane));
        }
    }

    // A position a distance from a plane toward another along the drilling axis: the lift of CHIP_BREAK above the
    // depth reached, the end of the re-entry of PECK. It never passes the plane it goes toward, CLEARANCE, so a lift
    // larger than the gap left ends there. Unknown when either plane is.
    private static AxisPosition Toward(AxisPosition plane, AxisPosition toward, decimal distance)
    {
        if (!plane.Known || !toward.Known)
        {
            return AxisPosition.Unknown;
        }

        decimal gap = Math.Abs(toward.Value - plane.Value);
        decimal direction = toward.Value < plane.Value ? -1m : 1m;
        return plane with { Value = plane.Value + (direction * Math.Min(distance, gap)) };
    }

    // D355: the depths a PECK greater than 0 reaches before DEPTH, in steps of PECK from SURFACE, or from CLEARANCE
    // where the block gives no SURFACE: the first ends one PECK from that plane in the drilling direction, each further
    // one a PECK deeper, and the last stops at DEPTH. PECK is a radius value (language 4.7, D60); none without a known
    // PECK, start plane and DEPTH. The store holds the planes through the same setpos shift, so the steps are taken on
    // the stored values.
    private static List<AxisPosition> Pecks(CycleState cycle, AxisPosition start, AxisPosition depth)
    {
        var pecks = new List<AxisPosition>();
        if (!start.Known
            || !depth.Known
            || ParameterOf(cycle, PeckKey) is not Word peckWord
            || MotionRules.NumberOf(peckWord) is not decimal peck
            || peck <= 0)
        {
            return pecks;
        }

        decimal direction = depth.Value < start.Value ? -1m : 1m;
        for (decimal reached = start.Value + (direction * peck);
            direction * (depth.Value - reached) > 0;
            reached += direction * peck)
        {
            pecks.Add(depth with { Value = reached });
        }

        return pecks;
    }

    // A word on the drilling axis in the call block of the built-in family: the cycle's own planes move that axis, so
    // the word has no meaning of its own and is an ERROR (virtual machine 3.3, 5; D189).
    private static void ReportDrillingAxisWord(BlockContext context, string drillingAxis)
    {
        foreach (Word word in context.Block.Words)
        {
            if (context.AxisOf.TryGetValue(word, out string? axis) && axis == drillingAxis)
            {
                context.Diagnostics.Error(context.Block, DiagnosticCodes.CycleCallOnTheDrillingAxis,
                    $"{word.Key} stands on the drilling axis {drillingAxis} of the cycle; the cycle moves that axis "
                    + "itself, and a pre-positioning is a RAPID of its own (language 4.7, virtual machine 3.3, D189).");
                return;
            }
        }
    }

    // The axis words of the call block position the hole, each moving its axis as a RAPID does (virtual machine 3.1,
    // 3.3); a call without axis words executes at the current position (language 4.7). The positions the axes reached.
    private static Dictionary<string, AxisPosition> PositionTheHole(BlockContext context)
    {
        var hole = new Dictionary<string, AxisPosition>(StringComparer.Ordinal);
        foreach (Word word in context.Block.Words)
        {
            if (context.AxisOf.TryGetValue(word, out string? axis))
            {
                MotionRules.MoveAxis(context, word, axis);
                hole[axis] = context.State.Motion.Position[axis];
            }
        }

        return hole;
    }

    // D187: the cycles of the Fanuc catalog that bring the tool back to their start point (fanuc 6); a cycle written
    // in the native form of D94 keeps the D94 rule, and so do the turning cycles of the other families until their
    // end positions are confirmed.
    private static bool ReturnsToTheStartPoint(BlockContext context, CycleState cycle, string name)
    {
        CycleCatalog catalog = context.Machine.CycleCatalog;
        return cycle.Controller is null
            && catalog.Controller == Controller.Fanuc
            && catalog.Find(name) is CycleEntry entry
            && Array.IndexOf(s_cyclesReturningToTheStartPoint, entry.Native) >= 0;
    }

    // The plane axes go to the axis words of the call block, and the drilling axis is unknown afterwards: the VM does
    // not know the sequence (virtual machine 3.3, D94).
    private static void PositionTheHoleOnly(BlockContext context, CycleState cycle)
    {
        MotionRules.MoveAxes(context);
        Dictionary<string, AxisPosition> position = context.State.Motion.Position;
        if (context.Resources.KeyOfAxis(cycle.Axis, context.State) is string drillingAxis
            && position.ContainsKey(drillingAxis))
        {
            position[drillingAxis] = AxisPosition.Unknown;
        }
    }

    // A plane along the drilling axis, SURFACE, CLEARANCE, DEPTH or SAFE, at its coordinate in the active frame; under
    // DIAMETER=ON the X values of an AXIS=X cycle are halved (virtual machine 3.3, D60). Unknown without the word or
    // for an expression (virtual machine 1).
    private static AxisPosition PlaneAt(ChannelState state, string key, string drillingAxis, PositionFrame frame)
    {
        CycleState cycle = state.Cycle;
        if (ParameterOf(cycle, key) is not Word word || MotionRules.NumberOf(word) is not decimal value)
        {
            return AxisPosition.Unknown;
        }

        decimal coordinate = DiameterRules.ToRadius(word, value, state.Frame.Diameter, cycle.Axis);
        return MotionRules.PositionAt(state, drillingAxis, frame, coordinate);
    }

    // A parameter word of the active cycle by its key; null when the CYCLE block did not write it (virtual machine
    // 2.6).
    private static Word? ParameterOf(CycleState cycle, string key)
    {
        foreach (Word word in cycle.Parameters)
        {
            if (word.Key == key && word.Addr is null)
            {
                return word;
            }
        }

        return null;
    }

    private static CycleMotion Rapid(string axis, AxisPosition to)
    {
        return new CycleMotion { Verb = Verb.Rapid, To = new Dictionary<string, AxisPosition> { [axis] = to } };
    }

    private static CycleMotion Feed(string axis, AxisPosition to, decimal? feed)
    {
        return new CycleMotion
        {
            Verb = Verb.Line,
            To = new Dictionary<string, AxisPosition> { [axis] = to },
            Feed = feed,
        };
    }
}
