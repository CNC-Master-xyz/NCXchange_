using Ncx.Core.Model;

namespace Ncx.Compilers;

// D278, D388 item 4: a section a controller writes inline in the block that names it, the contour of a Fanuc G70 to
// G73, which is never written as a subprogram of its own (language 4.13, the exception to "every SUB is written at
// least once").
public abstract partial class CompilerBase
{
    // The sections written inline, which the layout writes as no subprogram.
    private readonly HashSet<Section> _inlined = [];

    /// <summary>
    /// Writes the blocks of a section inline, as the walk of the section that no program calls executed them (virtual
    /// machine 3.9, D99), from the target state the caller gives with the keys it leaves unknown: the lines go to the
    /// caller, which places them, and the section is never written as a subprogram (D278, D388 item 4). Null where the
    /// run has no walk of the section.
    /// </summary>
    /// <param name="section">The section, a SUB of the file.</param>
    /// <param name="entry">What the control has active where the lines stand.</param>
    protected InlinedSection? InlineSection(Section section, TargetState entry)
    {
        var steps = new List<BlockStep>();
        bool inside = false;
        foreach (BlockStep step in _steps)
        {
            if (step.Section != section)
            {
                continue;
            }

            if (step.Block.Has("SUB", null, "BEGIN"))
            {
                inside = steps.Count == 0;
                continue;
            }

            if (step.Block.Has("SUB", null, "END") && inside)
            {
                break;
            }

            if (inside)
            {
                steps.Add(step);
            }
        }

        if (!inside)
        {
            return null;
        }

        BlockStep? savedStep = _step;
        List<string>? savedLines = _lines;
        var target = new TargetState();
        target.TakeOver(entry);
        var walk = new WriteContext
        {
            Target = target,
            Lines = [],
            Program = CurrentContext?.Program,
            Sub = section,
        };
        _contexts.Add(walk);
        var lines = new List<string>();
        foreach (BlockStep step in steps)
        {
            lines.AddRange(WriteStep(step, section));
        }

        _contexts.RemoveAt(_contexts.Count - 1);
        _step = savedStep;
        _lines = savedLines;
        _inlined.Add(section);
        var written = new List<string>();
        foreach (KeyValuePair<string, string> active in walk.Target.Active)
        {
            if (entry.ActiveOf(active.Key) != active.Value)
            {
                written.Add(active.Key);
            }
        }

        return new InlinedSection { Lines = lines, WrittenKeys = written, Steps = steps };
    }

    // Whether a section was written inline and stands as no subprogram of its own (D278, D388 item 4).
    private bool IsInlined(Section section)
    {
        return _inlined.Contains(section);
    }
}
