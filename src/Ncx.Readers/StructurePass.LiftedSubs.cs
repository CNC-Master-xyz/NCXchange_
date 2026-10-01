using Ncx.Core.Model;

namespace Ncx.Readers;

// A subprogram section that the main flow of its program also runs through, moved into a SUB section of the file with
// a CALL where it stood (D254; language 4.13, D89; controllers heidenhain.md 1, 7 rule 3).
internal sealed partial class StructurePass
{
    // The lifted sections of each program of the source, in the order they stand in the file.
    private readonly Dictionary<SourceSection, List<LiftedRange>> _lifted = [];

    // The control runs a labelled section that stands before the M2 or M30 of its program as ordinary blocks and
    // passes over its LBL 0, over and above the calls of it. The reader writes the section as a SUB and a CALL where
    // it stood, which runs the same blocks in the same order (D254; language 4.13, D89). The range runs from the
    // block the reader marked with LiftsSub to the block it marked as its end.
    private void FindLiftedSubs(List<SourceSection> sections)
    {
        foreach (SourceSection section in sections)
        {
            List<int> blocks = section.Blocks;
            var ranges = new List<LiftedRange>();
            for (int position = 0; position < blocks.Count; position++)
            {
                if (_structures[blocks[position]].LiftsSub is not string name
                    || ClosingPosition(blocks, position) is not int last)
                {
                    continue;
                }

                var range = new LiftedRange(name, blocks[position], blocks[last]);
                ranges.Add(range);
                Before(range.First).Add(Write(_blocks[range.First], _blocks[range.First].Line,
                    [new Word { Key = CallKey, Value = NameOrLabelValue(name) }]));
                for (int index = range.First; index <= range.Last; index++)
                {
                    _moved.Add(index);
                }

                position = last;
            }

            if (ranges.Count > 0)
            {
                section.Blocks.RemoveAll(_moved.Contains);
                _lifted[section] = ranges;
            }
        }
    }

    // The position of the block that closes the section begun at the position; null where the reader marked none.
    private int? ClosingPosition(List<int> blocks, int position)
    {
        for (int last = position + 1; last < blocks.Count; last++)
        {
            if (_structures[blocks[last]].LiftsSubEnd)
            {
                return last;
            }
        }

        return null;
    }

    // The lifted sections of a program stand behind its end and its contour and repeat sections, in front of the next
    // section or of FILE=END, each as SUB=BEGIN NAME=, its blocks with the trivia among them, and SUB=END:
    // subprograms stand in the file next to the programs, never inside one (language 4.9, 4.13).
    private void PlanLiftedSubs(SourceSection section, int? nextBegin)
    {
        if (!_lifted.TryGetValue(section, out List<LiftedRange>? ranges))
        {
            return;
        }

        List<ReadStep> boundary = Boundary(nextBegin);
        foreach (LiftedRange range in ranges)
        {
            SourceBlock first = _blocks[range.First];
            SourceBlock last = _blocks[range.Last];
            boundary.Add(Write(first, first.Line,
                [Frame(SubKey, BeginValue), new Word { Key = NameKey, Value = NameOrLabelValue(range.Name) }]));
            for (int index = range.First; index <= range.Last; index++)
            {
                SourceBlock block = _blocks[index];
                boundary.Add(new ReadStep
                {
                    Kind = block.IsTrivia ? ReadStepKind.Trivia : ReadStepKind.Read,
                    Source = block,
                });
            }

            boundary.Add(Write(last, last.Line, [Frame(SubKey, EndValue)]));
        }
    }

    // A lifted section of a program: the name of its SUB section and the indices of its first and its last block.
    private sealed record LiftedRange(string Name, int First, int Last);
}
