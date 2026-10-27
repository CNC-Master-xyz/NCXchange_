namespace Ncx.Readers;

/// <summary>
/// The look-ahead of every Read step of a reading plan, worked out once before the reading (D232): the blocks a reader
/// rule sees after the block it is offered run up to the next step that writes the file structure or the next block
/// of the control flow, and a claim of n blocks ends at the step of the n-th of them. Each look-ahead is a slice of
/// one shared list, so offering every block costs constant time.
/// </summary>
internal sealed class ReadLookAhead
{
    private readonly List<SourceBlock> _reads = [];
    private readonly List<int> _stepOfRead = [];
    private readonly int[] _readOfStep;
    private readonly int[] _end;

    /// <param name="plan">The reading plan of the structure pass.</param>
    /// <param name="flow">The source blocks that end a look-ahead in front of them: the blocks of the file structure
    /// and the blocks the family reads as a jump, a return or a call (D232; gap closed 2026-10-28, delegated).</param>
    public ReadLookAhead(List<ReadStep> plan, HashSet<SourceBlock> flow)
    {
        _readOfStep = new int[plan.Count];
        for (int step = 0; step < plan.Count; step++)
        {
            _readOfStep[step] = -1;
            if (plan[step].Kind == ReadStepKind.Read)
            {
                _readOfStep[step] = _reads.Count;
                _reads.Add(plan[step].Source);
                _stepOfRead.Add(step);
            }
        }

        // One pass backwards: a Write step ends the look-ahead of the reads before it in front of the reads after it,
        // a block of the flow in front of itself.
        _end = new int[_reads.Count];
        int stop = _reads.Count;
        int nextRead = _reads.Count;
        for (int step = plan.Count - 1; step >= 0; step--)
        {
            if (plan[step].Kind == ReadStepKind.Write)
            {
                stop = nextRead;
            }
            else if (_readOfStep[step] is int read and >= 0)
            {
                _end[read] = stop;
                if (flow.Contains(plan[step].Source))
                {
                    stop = read;
                }

                nextRead = read;
            }
        }
    }

    /// <summary>
    /// The blocks a rule sees after the block of a Read step.
    /// </summary>
    /// <param name="step">The index of a Read step in the plan.</param>
    public IReadOnlyList<SourceBlock> Following(int step)
    {
        int read = _readOfStep[step];
        return new SourceBlockSlice(_reads, read + 1, _end[read] - read - 1);
    }

    /// <summary>
    /// The index of the last step a claim takes.
    /// </summary>
    /// <param name="step">The index of the Read step of the offered block.</param>
    /// <param name="claimed">The number of blocks claimed, 1 to the length of the look-ahead plus 1.</param>
    public int LastStep(int step, int claimed)
    {
        return _stepOfRead[_readOfStep[step] + claimed - 1];
    }
}
