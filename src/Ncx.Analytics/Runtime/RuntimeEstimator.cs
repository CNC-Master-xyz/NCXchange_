using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Analytics.Runtime;

/// <summary>
/// The runtime estimate of virtual machine 8 and D64 as a stream: it reads the events of a run one by one and hands
/// every time it charges, of a motion, a dwell, a cycle dwell or a spindle start, stop or speed change, to the analytic
/// that owns it. It keeps nothing but the one motion whose exit speed waits for the next motion, so that it runs over a
/// point list of millions of blocks (implementation 14, risks).
/// </summary>
// D286: the path rests, while the programmed PATH_MODE state is CONTINUOUS, at DWELL, a spindle start or stop,
// TOOL_BEGIN/END, STOP, program and file events, a motion not timed, a direction reversal, CYCLE_DWELL, a cycle call
// not timed, every SYNC, and every block without path motion that the control executes (COOLANT, FUNC, MFUNC,
// PATH_MODE, TOLERANCE and the other machine-function and state blocks); comments, variable computations, LABEL, JUMP
// and CALL are transparent corners. The end of a RAPID is a rest point on a Fanuc-family machine and a corner
// otherwise, [dynamics] rapid_exact_stop overriding that. Under EXACT_STOP every block rests (D350). A block without
// path motion is seen through the events it raises: one that raises none, a state word repeating its value or a SYNC
// of a run of one channel, stays invisible and is taken as a corner (gap closed 2026-10-27, delegated).
internal sealed class RuntimeEstimator
{
    private const double SecondsPerMinute = 60d;
    private const string SpindlePrefix = "SPINDLE:";
    private const string RpmPrefix = "RPM:";
    private const string PathModeVariable = "PATH_MODE";
    private const string CycleDwellKey = "CYCLE_DWELL";
    private const string PeckKey = "PECK";
    private const string ChipBreakCycle = "CHIP_BREAK";
    private const string PeckClearanceKey = "peck_clearance";
    private const string ChipBreakLiftKey = "chip_break_lift";

    // The verbs of a block with path motion (language 4.3, 4.7); every other block that raises an event, but the
    // transparent ones, is a rest point under CONTINUOUS (D286).
    private static readonly HashSet<string> s_pathMotionVerbs =
        new(StringComparer.Ordinal) { "RAPID", "LINE", "ARC", "RETRACT", "HOME", "CYCLE_CALL" };

    private readonly MachineConfig _machine;
    private readonly RangeFilter _range;
    private readonly Action<TimedStep> _timed;

    private PendingMotion? _pending;
    private CycleCallEvent? _openCycle;
    private bool _openCycleMoved;
    private string? _section;

    // The block and the spindle of the last spindle change charged: a block raises one STATE_CHANGE per changed
    // variable, SPINDLE: and RPM: of one spindle among them, and its time is charged once (D323).
    private Block? _spindleBlock;
    private string? _spindleCharged;

    /// <summary>
    /// An estimate over one run.
    /// </summary>
    /// <param name="machine">The machine of the run.</param>
    /// <param name="range">The block range of the analytic: the estimate runs over the whole run, so that the motions
    /// at the ends of the range enter and leave with their true speeds, and counts what it cannot time inside the range
    /// only.</param>
    /// <param name="timed">Receives every time charged, in the order the estimate charges it.</param>
    public RuntimeEstimator(MachineConfig machine, RangeFilter range, Action<TimedStep> timed)
    {
        _machine = machine;
        _range = range;
        _timed = timed;
        Dynamics = new MachineDynamics(machine);
    }

    /// <summary>
    /// The values of the machine file the estimate uses.
    /// </summary>
    public MachineDynamics Dynamics { get; }

    /// <summary>
    /// The motions not timed because their length is unknown: an axis not known at both ends in one frame.
    /// </summary>
    public int UnknownLength { get; private set; }

    /// <summary>
    /// The motions not timed because they have no known feed, a feed per revolution while the spindle stands, or a
    /// RAPID, HOME or RETRACT without a rapid rate in the machine file.
    /// </summary>
    public int WithoutSpeed { get; private set; }

    /// <summary>
    /// The cycle calls not timed because the virtual machine does not know their motions: a catalog cycle or a
    /// CYCLE:controller=n (virtual machine 3.3, D94).
    /// </summary>
    public int UnknownCycles { get; private set; }

    /// <summary>
    /// The dwells not timed because their seconds come from an expression STATIC mode does not evaluate.
    /// </summary>
    public int UnknownDwells { get; private set; }

    /// <summary>
    /// The motions that turned a rotary axis, whose travel adds no time (virtual machine 9).
    /// </summary>
    public int RotaryMoves { get; private set; }

    /// <summary>
    /// The motions of the polar plane through its centre, L = 0, timed with the X limits alone (D290).
    /// </summary>
    public int ThroughCentre { get; private set; }

    /// <summary>
    /// The corners between two timed motions under PATH_MODE=CONTINUOUS (D350).
    /// </summary>
    public int ContinuousCorners { get; private set; }

    /// <summary>
    /// The corners between two timed motions under PATH_MODE=EXACT_STOP (D350).
    /// </summary>
    public int ExactStopCorners { get; private set; }

    /// <summary>
    /// The corners timed in a mode other than the start value for which the machine has no [transform] template, so
    /// that it cannot express it there (D350), by mode.
    /// </summary>
    public Dictionary<PathMode, int> UnexpressedCorners { get; } = [];

    /// <summary>
    /// True when the run changed PATH_MODE (D350).
    /// </summary>
    public bool PathModeChanged { get; private set; }

    /// <summary>
    /// The [cycles] keys the expanded cycles of the range needed and the machine file leaves out, where the family's
    /// behaviour is 0: chip_break_lift, peck_clearance (D357).
    /// </summary>
    public SortedSet<string> DefaultedCycleKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Reads one event of the run.
    /// </summary>
    public void On(VmEvent vmEvent)
    {
        CloseCycleCall(vmEvent);
        switch (vmEvent)
        {
            case MotionEvent motion:
                AddMotion(motion);
                break;
            case CycleCallEvent cycleCall:
                _openCycle = cycleCall;
                _openCycleMoved = false;
                break;
            case DwellEvent dwell:
                AddDwell(dwell);
                break;
            case StateChangeEvent change:
                PathModeChanged |= change.Variable == PathModeVariable;
                AddSpindleChange(change);
                RestAtBlock(change);
                break;
            case SectionEvent section:
                _section = section.Text;
                break;
            case ProgramEvent program:
                Stop();
                _section = program.Phase == EventPhase.Begin ? null : _section;
                break;
            case ToolEvent or StopEvent or FileEvent or SyncEvent:
                Stop();
                break;
            case FunctionEvent or PreloadEvent:
                RestAtBlock(vmEvent);
                break;
        }
    }

    /// <summary>
    /// Ends the estimate after the last event: the motion that waits comes to rest.
    /// </summary>
    public void Finish()
    {
        CloseCycleCall(null);
        Stop();
    }

    // Per motion a trapezoidal velocity profile from the machine configuration (virtual machine 8, D64); without
    // [dynamics] distance over feed. A motion whose length is unknown is not timed, and the path comes to rest around
    // it.
    private void AddMotion(MotionEvent motion)
    {
        if (motion.Length is not decimal length)
        {
            UnknownLength += InRange(motion);
            Stop();
            return;
        }

        double millimetres = (double)length * CommandedFeed.MillimetresPerUnit(motion);
        MovingAxes axes = MovingAxes.Of(motion, Dynamics);
        RotaryMoves += axes.TurnsRotary ? InRange(motion) : 0;
        ThroughCentre += axes.ThroughCentre ? InRange(motion) : 0;
        double? speed = SpeedOf(motion, axes);
        if (millimetres > 0 && speed is null)
        {
            WithoutSpeed += InRange(motion);
            Stop();
            return;
        }

        double velocity = speed ?? 0;

        // Without dynamics in the configuration the estimate falls back to distance over feed (virtual machine 8).
        if (!Dynamics.HasProfile)
        {
            Charge(motion, millimetres > 0 ? millimetres / velocity : 0, _section);
            return;
        }

        double entry = 0;
        if (_pending is not null)
        {
            CountCorner(_pending.Motion);
            entry = CornerSpeed(_pending, motion, velocity);
            Complete(_pending, entry);
        }

        _pending = new PendingMotion
        {
            Motion = motion,
            Length = millimetres,
            Speed = velocity,
            Entry = entry,
            Acceleration = axes.Acceleration,
            Section = _section,
        };
    }

    // The speed of a motion in mm/s: RAPID and HOME use the rapid rate, the smallest of the moving axes (virtual
    // machine 8; HOME is a MOTION, virtual machine 7; D287); every other motion the commanded feed, which the profile
    // limits by the max_feed of every axis that moves. The fallback is distance over the feed as commanded.
    // D287: a RETRACT takes no feed of its own (virtual machine 3.1a: "A RETRACT with a feed ... ERROR"), and M140 MB
    // without a feed traverses at rapid, so RETRACT is timed at the rapid rate of its moving axes with the same
    // profile, as the tool list counts its distance as rapid (virtual machine 8; language 4.3; decisions.md).
    private double? SpeedOf(MotionEvent motion, MovingAxes axes)
    {
        if (motion.Verb is Verb.Rapid or Verb.Home or Verb.Retract)
        {
            return axes.Rapid / SecondsPerMinute;
        }

        if (CommandedFeed.MillimetresPerMinute(motion, _machine) is not double perMinute || perMinute <= 0)
        {
            return null;
        }

        if (Dynamics.HasProfile && axes.MaxFeed is double maxFeed)
        {
            perMinute = Math.Min(perMinute, maxFeed);
        }

        return perMinute / SecondsPerMinute;
    }

    // D350: the estimate reads the PATH_MODE state of the motion before the corner: EXACT_STOP brakes to zero in every
    // block, CONTINUOUS carries the speed through corners up to the configured corner_speed (virtual machine 8), and
    // never faster than either motion travels, nor faster than the motion before the corner can reach by its end. D286:
    // the end of a RAPID on a machine whose rapids stop, and a direction reversal, are rest points.
    private double CornerSpeed(PendingMotion before, MotionEvent next, double nextSpeed)
    {
        if (before.Motion.After.Frame.PathMode != PathMode.Continuous
            || (before.Motion.Verb == Verb.Rapid && Dynamics.RapidRests)
            || PathDirection.Reverses(before.Motion, next, Dynamics))
        {
            return 0;
        }

        double reachable = before.Acceleration is double acceleration
            ? Math.Sqrt((before.Entry * before.Entry) + (2 * acceleration * before.Length))
            : double.PositiveInfinity;
        return Math.Min(Math.Min(before.Speed, nextSpeed), Math.Min(Dynamics.CornerSpeed, reachable));
    }

    // D350: the report counts the corners timed in each mode, and those in a mode the program asked for that the
    // machine has no template for.
    private void CountCorner(MotionEvent before)
    {
        if (!_range.Contains(before))
        {
            return;
        }

        PathMode mode = before.After.Frame.PathMode;
        if (mode == PathMode.Continuous)
        {
            ContinuousCorners++;
        }
        else
        {
            ExactStopCorners++;
        }

        if (mode != Dynamics.StartPathMode && !Dynamics.CanExpress(mode))
        {
            UnexpressedCorners[mode] = UnexpressedCorners.GetValueOrDefault(mode) + 1;
        }
    }

    // D286: a block without path motion that the control executes is a rest point; the events of a block with a
    // motion verb are not, and comments, variable computations, LABEL, JUMP and CALL raise none of the events that call
    // this.
    private void RestAtBlock(VmEvent vmEvent)
    {
        if (vmEvent.Block.Verb?.Key is string verb && s_pathMotionVerbs.Contains(verb))
        {
            return;
        }

        Stop();
    }

    // The time of a motion under its profile; a block can never be shorter than the control's block_time, which is what
    // makes thousands of short segments slow (virtual machine 8).
    private void Complete(PendingMotion motion, double exit)
    {
        double seconds = MotionProfile.Seconds(motion.Length, motion.Speed, motion.Entry, exit, motion.Acceleration);
        if (Dynamics.BlockTime is double blockTime)
        {
            seconds = Math.Max(seconds, blockTime);
        }

        Charge(motion.Motion, seconds, motion.Section);
    }

    // The path comes to rest: the motion that waits ends at speed zero.
    private void Stop()
    {
        if (_pending is not null)
        {
            Complete(_pending, 0);
            _pending = null;
        }
    }

    // Dwells add their times (virtual machine 8; DWELL in seconds, language 4.1).
    private void AddDwell(DwellEvent dwell)
    {
        Stop();
        if (dwell.Seconds is not decimal seconds)
        {
            UnknownDwells += InRange(dwell);
            return;
        }

        Charge(dwell, (double)seconds, _section);
    }

    // A spindle start or stop adds accel_time of that spindle (virtual machine 8; machine-config 5), read from the
    // STATE_CHANGE of SPINDLE and RPM (architecture 9); the spindles of generated blocks count as well (virtual machine
    // 3.10, machine-config 5a). D323: where rpm_max and both speeds are known, the linear ramp from stop to rpm_max in
    // accel_time: a start adds accel_time x rpm_new / rpm_max, a stop accel_time x rpm_old / rpm_max, a speed change
    // while turning accel_time x |rpm_new - rpm_old| / rpm_max, a reversal accel_time x (rpm_old + rpm_new) / rpm_max.
    // Where rpm_max is missing or a speed unknown (CSS, no S yet), the whole accel_time per start and per stop, a
    // reversal twice, and nothing for a speed change. The path rests at a start or a stop (D286).
    private void AddSpindleChange(StateChangeEvent change)
    {
        string? spindle = SpindleOf(change.Variable);
        if (spindle is null
            || (ReferenceEquals(change.Block, _spindleBlock) && spindle == _spindleCharged)
            || !change.Before.Spindles.TryGetValue(spindle, out SpindleSnapshot? before)
            || !change.After.Spindles.TryGetValue(spindle, out SpindleSnapshot? after))
        {
            return;
        }

        _spindleBlock = change.Block;
        _spindleCharged = spindle;
        bool turned = before.Direction != SpindleDirection.Off;
        bool turns = after.Direction != SpindleDirection.Off;
        if (turned != turns || (turned && before.Direction != after.Direction))
        {
            Stop();
        }

        if (Dynamics.AccelTime(spindle) is not double accelTime)
        {
            return;
        }

        double? rpmMax = Dynamics.RpmMax(spindle);
        double? oldRpm = KnownRpm(change.Before, spindle, before, rpmMax);
        double? newRpm = KnownRpm(change.After, spindle, after, rpmMax);
        double seconds = SpindleRamp.Seconds(turned ? before.Direction : null, turns ? after.Direction : null, oldRpm,
            newRpm, accelTime, rpmMax);
        if (seconds > 0)
        {
            Charge(change, seconds, _section);
        }
    }

    // The spindle a STATE_CHANGE of SPINDLE:S1 or RPM:S1 names; null for another variable.
    private static string? SpindleOf(string variable)
    {
        if (variable.StartsWith(SpindlePrefix, StringComparison.Ordinal))
        {
            return variable.Substring(SpindlePrefix.Length);
        }

        return variable.StartsWith(RpmPrefix, StringComparison.Ordinal) ? variable.Substring(RpmPrefix.Length) : null;
    }

    // The rpm of a turning spindle where it is known, capped by rpm_max: not under CSS, whose rpm follows the radius
    // during the motions, and not while RPM is unknown (D323).
    private static double? KnownRpm(ChannelSnapshot state, string spindle, SpindleSnapshot speed, double? rpmMax)
    {
        if (speed.Css || state.Unknown.Contains(RpmPrefix + spindle))
        {
            return null;
        }

        double rpm = (double)speed.Rpm;
        return rpmMax is double cap ? Math.Min(rpm, cap) : rpm;
    }

    // The motions of an expanded CYCLE_CALL follow it in its block (virtual machine 3.3, D37, D234) and
    // are timed as motions; cycle plunges and dwells add their times (virtual machine 8). The dwell of CYCLE_DWELL is
    // charged once per call, at the depth, and moves nothing (virtual machine 3.3, D190). A call without
    // motions is a cycle whose sequence the virtual machine does not know (virtual machine 3.3, D94): not timed.
    private void CloseCycleCall(VmEvent? next)
    {
        if (_openCycle is null)
        {
            return;
        }

        if (next is MotionEvent && ReferenceEquals(next.After, _openCycle.After))
        {
            _openCycleMoved = true;
            return;
        }

        // D286: a cycle call not timed and a CYCLE_DWELL are rest points.
        if (!_openCycleMoved)
        {
            UnknownCycles += InRange(_openCycle);
            Stop();
        }
        else
        {
            ChargeCycleDwell(_openCycle);
            NoteDefaultedKeys(_openCycle);
        }

        _openCycle = null;
    }

    // D357: on Fanuc, and on Heidenhain for chip_break_lift and in a run without a machine family, a [cycles] key left
    // out is 0, and the report names the key it defaulted: peck_clearance for the pecks of a cycle, chip_break_lift for
    // those of CHIP_BREAK (CycleDistances of the virtual machine; gap closed 2026-10-27 by code wave 1).
    private void NoteDefaultedKeys(CycleCallEvent cycleCall)
    {
        if (!_range.Contains(cycleCall) || !Pecks(cycleCall))
        {
            return;
        }

        Controller? family = _machine.Machine.Controller;
        CyclesConfig cycles = _machine.Cycles ?? new CyclesConfig();
        if (cycleCall.Cycle == ChipBreakCycle)
        {
            if (cycles.ChipBreakLift is null && family is not Controller.Siemens)
            {
                DefaultedCycleKeys.Add(ChipBreakLiftKey);
            }
        }
        else if (cycles.PeckClearance is null && family is not (Controller.Siemens or Controller.Heidenhain))
        {
            DefaultedCycleKeys.Add(PeckClearanceKey);
        }
    }

    // A cycle call whose PECK is a number above 0 splits its feed into steps (D190, D355).
    private static bool Pecks(CycleCallEvent cycleCall)
    {
        foreach (Word word in cycleCall.Parameters)
        {
            if (word.Key == PeckKey && word.Value is DecimalValue { Number: > 0 } or IntegerValue { Number: > 0 })
            {
                return true;
            }
        }

        return false;
    }

    private void ChargeCycleDwell(CycleCallEvent cycleCall)
    {
        foreach (Word word in cycleCall.Parameters)
        {
            if (word.Key != CycleDwellKey)
            {
                continue;
            }

            Stop();
            switch (word.Value)
            {
                case DecimalValue seconds:
                    Charge(cycleCall, (double)seconds.Number, _section);
                    break;
                case IntegerValue seconds:
                    Charge(cycleCall, seconds.Number, _section);
                    break;
                default:
                    UnknownDwells += InRange(cycleCall);
                    break;
            }
        }
    }

    private void Charge(VmEvent vmEvent, double seconds, string? section)
    {
        _timed(new TimedStep { Seconds = seconds, Event = vmEvent, Section = section });
    }

    // What the estimate cannot time counts where the block stands in the range of the analytic (virtual machine 8,
    // D67): 1 inside it, 0 outside.
    private int InRange(VmEvent vmEvent)
    {
        return _range.Contains(vmEvent) ? 1 : 0;
    }
}
