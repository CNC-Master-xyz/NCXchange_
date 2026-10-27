using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// F above an axis max_feed, VM441 (virtual machine 5, machine-config 4, D64, D192): the per-minute path feed of every
/// LINE and ARC against the max_feed of each linear axis the block moves, rotary axes only in a rotary-only block under
/// ROTARY_FEED=DEG_MIN, and CYCLE_F of a built-in drilling cycle against the drilling axis. One WARNING per F or
/// CYCLE_F word, on its first exceeding block. One per run, because it remembers which word it warned for.
/// </summary>
internal sealed class MaxFeedCheck
{
    // Millimetres per inch: max_feed is a feed per minute in mm/min (machine-config 4), and F under UNITS=INCH is one
    // in inches per minute (language 4.3, D192).
    private const decimal MillimetresPerInch = 25.4m;

    private const string CycleFeedKey = "CYCLE_F";

    private Word? _feedWord;
    private bool _feedWarned;
    private Word? _cycleFeedWord;
    private bool _cycleFeedWarned;
    private Dictionary<string, AxisPosition>? _positionsBefore;

    /// <summary>
    /// Before the state words of a block: a new F or CYCLE_F word may warn again, and a motion block's positions are
    /// kept to tell which axes it moves.
    /// </summary>
    public void BeforeBlock(BlockContext context)
    {
        if (context.Block.Find("F") is Word feed)
        {
            _feedWord = feed;
            _feedWarned = false;
        }

        if (context.Block.Find(CycleFeedKey) is Word cycleFeed)
        {
            _cycleFeedWord = cycleFeed;
            _cycleFeedWarned = false;
        }

        _positionsBefore = MotionRules.IsMotion(context.Block)
            ? new Dictionary<string, AxisPosition>(context.State.Motion.Position)
            : null;
    }

    /// <summary>
    /// After the verb of a block: a LINE or an ARC compares its path feed, a CYCLE_CALL of a built-in drilling cycle
    /// its CYCLE_F (D192).
    /// </summary>
    public void AfterBlock(BlockContext context)
    {
        string? verb = context.Block.Verb?.Key;
        if (_positionsBefore is null)
        {
            return;
        }

        if (verb is "LINE" or "ARC" && !_feedWarned && _feedWord is Word feedWord)
        {
            _feedWarned = CheckPathFeed(context, feedWord, _positionsBefore);
        }
        else if (verb == "CYCLE_CALL" && !_cycleFeedWarned && _cycleFeedWord is Word cycleFeedWord)
        {
            _cycleFeedWarned = CheckCycleFeed(context, cycleFeedWord);
        }
    }

    // The per-minute path feed against the max_feed of each linear axis the block moves; in a rotary-only block under
    // ROTARY_FEED=DEG_MIN against the rotary axes it moves. No axis is named that does not move (D192).
    private static bool CheckPathFeed(BlockContext context, Word feedWord,
        IReadOnlyDictionary<string, AxisPosition> before)
    {
        ChannelState state = context.State;
        if (FeedPerMinute(context) is not decimal perMinute)
        {
            return false;
        }

        var linear = new List<AxisDef>();
        var rotary = new List<AxisDef>();
        foreach (string axis in MovedAxes(context, before))
        {
            if (context.Machine.ResolveAxis(axis) is not AxisDef definition)
            {
                continue;
            }

            (definition.Kind == AxisKind.Linear ? linear : rotary).Add(definition);
        }

        // INCH is multiplied by 25.4 for a length; degrees per minute are degrees in either unit (D192).
        if (linear.Count > 0)
        {
            decimal lengthPerMinute = state.Frame.Units == Units.Inch ? perMinute * MillimetresPerInch : perMinute;
            return Warn(context, feedWord, lengthPerMinute, linear, "mm/min");
        }

        return state.Frame.RotaryFeed == RotaryFeed.DegMin && Warn(context, feedWord, perMinute, rotary, "deg/min");
    }

    // CYCLE_F of the built-in drilling cycles against the drilling axis (virtual machine 3.3, language 4.7, D192).
    private static bool CheckCycleFeed(BlockContext context, Word cycleFeedWord)
    {
        CycleState cycle = context.State.Cycle;
        if (cycle.Controller is not null
            || cycle.Name is not string name
            || !DrillingFamily.Names.Contains(name)
            || MotionRules.NumberOf(cycleFeedWord) is not decimal cycleFeed
            || context.Resources.KeyOfAxis(cycle.Axis, context.State) is not string axis
            || context.Machine.ResolveAxis(axis) is not AxisDef drillingAxis
            || drillingAxis.Kind != AxisKind.Linear)
        {
            return false;
        }

        decimal perMinute = context.State.Frame.Units == Units.Inch ? cycleFeed * MillimetresPerInch : cycleFeed;
        return Warn(context, cycleFeedWord, perMinute, [drillingAxis], "mm/min");
    }

    // The feed per minute the block runs at: F under PER_MIN, F times the known rpm under PER_REV, skipped while the
    // rpm is unknown; a feed from an expression is not compared (virtual machine 1, D192, D288).
    private static decimal? FeedPerMinute(BlockContext context)
    {
        MotionState motion = context.State.Motion;
        if (motion.Feed is not decimal feed || context.State.Unknown.Contains("F"))
        {
            return null;
        }

        if (motion.FeedMode == FeedMode.PerMin)
        {
            return feed;
        }

        return motion.FeedMode == FeedMode.PerRev && RevolutionsPerMinute(context) is decimal rpm ? feed * rpm : null;
    }

    // The rpm a feed per revolution follows (D288): the spindle of the current tool holder, the default spindle for a
    // holder without one, while it turns; otherwise the workpiece holder's spindle while it turns in SPINDLE mode.
    // Null while that rpm is unknown: from an expression or under CSS, which changes it with the radius.
    private static decimal? RevolutionsPerMinute(BlockContext context)
    {
        ChannelState state = context.State;
        MachineConfig machine = context.Machine;
        string? holderSpindle = state.LastHolder is string holder ? machine.FindResource(holder)?.Spindle : null;
        holderSpindle ??= machine.ResolveDefaultSpindle()?.Id;
        string? spindle = Turns(state, holderSpindle) ? holderSpindle : null;
        if (spindle is null
            && state.Frame.WorkpieceHolder is string workpiece
            && Turns(state, workpiece)
            && state.Spindles[workpiece].Mode == SpindleMode.Spindle)
        {
            spindle = workpiece;
        }

        if (spindle is null
            || state.Spindles[spindle].Css
            || state.Unknown.Contains(BlockContext.StateKey("RPM", spindle)))
        {
            return null;
        }

        return state.Spindles[spindle].Rpm;
    }

    private static bool Turns(ChannelState state, string? spindle)
    {
        return spindle is not null
            && state.Spindles.TryGetValue(spindle, out SpindleState? speed)
            && speed.Direction != SpindleDirection.Off;
    }

    // The axes the block moves: every axis whose stored position it changed, and every axis it names.
    private static List<string> MovedAxes(BlockContext context, IReadOnlyDictionary<string, AxisPosition> before)
    {
        var moved = new List<string>();
        foreach (KeyValuePair<string, AxisPosition> position in context.State.Motion.Position)
        {
            bool changed = !before.TryGetValue(position.Key, out AxisPosition was) || was != position.Value;
            if (changed || context.AxisOf.ContainsValue(position.Key))
            {
                moved.Add(position.Key);
            }
        }

        return moved;
    }

    // The WARNING names the axes whose max_feed the feed is above; the control "may clamp", because on Fanuc the clamp
    // is a per-axis share and can be smaller or absent on a diagonal move (parameter 1430, D192).
    private static bool Warn(BlockContext context, Word feedWord, decimal perMinute, List<AxisDef> axes, string unit)
    {
        var exceeded = new List<string>();
        foreach (AxisDef axis in axes)
        {
            if (axis.MaxFeed is decimal maxFeed && perMinute > maxFeed)
            {
                exceeded.Add($"{axis.NcxName} ({maxFeed.ToString(CultureInfo.InvariantCulture)} {unit})");
            }
        }

        if (exceeded.Count == 0)
        {
            return false;
        }

        context.Diagnostics.Warning(context.Block, DiagnosticCodes.FeedAboveMaxFeed,
            $"{feedWord.ToCanonical()} is above the max_feed of {string.Join(", ", exceeded)}, the feed the axis can "
            + "follow; the control may clamp the feed (virtual machine 5, D64, D192).");
        return true;
    }
}
