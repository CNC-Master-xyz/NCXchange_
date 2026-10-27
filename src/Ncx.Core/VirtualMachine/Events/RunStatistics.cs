namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// The figures the events keep under a tool (TOOL_END) and under a program (the run statistics of PROGRAM_END),
/// counted block by block from the MOTION lengths (virtual machine 7, D235): the blocks executed, the feed and the
/// rapid distance, and the motions whose length is unknown, so that a report can call the distances a lower bound.
/// Mutable; only the events of the virtual machine count.
/// </summary>
internal sealed class RunStatistics
{
    /// <summary>
    /// The sum of the known lengths of the LINE and ARC motions, the plunges of an expanded cycle among them.
    /// </summary>
    public decimal FeedDistance { get; private set; }

    /// <summary>
    /// The sum of the known lengths of the RAPID, HOME and RETRACT motions.
    /// </summary>
    public decimal RapidDistance { get; private set; }

    /// <summary>
    /// The MOTIONs whose length is unknown.
    /// </summary>
    public int UnknownLengthMotions { get; private set; }

    /// <summary>
    /// The blocks counted.
    /// </summary>
    public int Blocks { get; private set; }

    /// <summary>
    /// Counts one executed block with the motions it raised, those of an expanded CYCLE_CALL and of generated blocks
    /// included (D235); a motion of unknown length adds nothing to a distance and is counted apart.
    /// </summary>
    public void Count(IReadOnlyList<MotionEvent> motions)
    {
        Blocks++;
        foreach (MotionEvent motion in motions)
        {
            if (motion.Length is not decimal length)
            {
                UnknownLengthMotions++;
            }
            else if (motion.Verb is not (State.Verb.Line or State.Verb.Arc))
            {
                // RAPID, HOME and RETRACT are rapid motions on the controls, as the tool list counts them (D235).
                RapidDistance += length;
            }
            else
            {
                FeedDistance += length;
            }
        }
    }

    /// <summary>
    /// Starts counting anew, at TOOL_BEGIN and PROGRAM_BEGIN.
    /// </summary>
    public void Reset()
    {
        FeedDistance = 0m;
        RapidDistance = 0m;
        UnknownLengthMotions = 0;
        Blocks = 0;
    }
}
