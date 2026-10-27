using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The contour sections of G70 to G73 the compiler has written inline, per program, with their P and Q, and the next
/// free block number of each program (D278, D277).
/// </summary>
internal sealed class FanucContours
{
    private readonly Func<Section, TargetState, InlinedSection?> _inline;
    private readonly Dictionary<Section, Dictionary<Section, FanucContourRange>> _ranges = [];
    private readonly Dictionary<Section, int> _greatest = [];

    /// <summary>
    /// The contours of one compile.
    /// </summary>
    /// <param name="inline">Writes a section inline, CompilerBase.InlineSection.</param>
    public FanucContours(Func<Section, TargetState, InlinedSection?> inline)
    {
        _inline = inline;
    }

    /// <summary>
    /// The P and Q of a contour the program has written inline already; null for the first G70 to G73 that names it.
    /// </summary>
    /// <param name="program">The program or subprogram the cycle block stands in.</param>
    /// <param name="contour">The SUB section of the contour.</param>
    public FanucContourRange? Find(Section program, Section contour)
    {
        return _ranges.TryGetValue(program, out Dictionary<Section, FanucContourRange>? ranges)
            && ranges.TryGetValue(contour, out FanucContourRange? range)
            ? range
            : null;
    }

    /// <summary>
    /// Records the P and Q of a contour written inline in a program.
    /// </summary>
    public void Add(Section program, Section contour, FanucContourRange range)
    {
        if (!_ranges.TryGetValue(program, out Dictionary<Section, FanucContourRange>? ranges))
        {
            ranges = [];
            _ranges[program] = ranges;
        }

        ranges[contour] = range;
    }

    /// <summary>
    /// The next block number of a program, after the greatest number its labels take and every number given out
    /// before (D277).
    /// </summary>
    /// <param name="program">The program or subprogram.</param>
    /// <param name="greatest">The greatest block number of its labels.</param>
    public int Next(Section program, int greatest)
    {
        int next = Math.Max(_greatest.GetValueOrDefault(program), greatest) + 1;
        _greatest[program] = next;
        return next;
    }

    /// <summary>
    /// Writes the blocks of a contour section inline (CompilerBase.InlineSection).
    /// </summary>
    /// <param name="contour">The SUB section of the contour.</param>
    /// <param name="entry">What the control has active where the lines stand, with the keys left unknown.</param>
    public InlinedSection? Inline(Section contour, TargetState entry)
    {
        return _inline(contour, entry);
    }
}
