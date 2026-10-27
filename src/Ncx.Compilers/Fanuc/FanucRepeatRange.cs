using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The blocks a REPEAT runs again, from its LABEL to the REPEAT block (language 4.9, REPEAT), and the nesting depth
/// whose counter of [variables] repeat_counters counts its passes (D280).
/// </summary>
/// <param name="Section">The program or subprogram of the range.</param>
/// <param name="Label">The label the REPEAT names.</param>
/// <param name="First">The line of the LABEL block.</param>
/// <param name="Last">The line of the REPEAT block.</param>
/// <param name="Depth">0 for the outermost repeat, counting the ranges around a CALL of the subprogram too.</param>
internal sealed record FanucRepeatRange(Section Section, string Label, int First, int Last, int Depth)
{
    /// <summary>
    /// True for a line of the section inside the range.
    /// </summary>
    public bool Holds(int line)
    {
        return line >= First && line <= Last;
    }
}
