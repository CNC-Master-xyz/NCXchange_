using Ncx.Core.Writing;
using Ncx.Readers;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// A reader rule that looks at every block it is offered and at the blocks it is shown after it, and claims none: the
/// cost of the rules to the reader alone (D231, D232).
/// </summary>
internal sealed class DecliningRule : ISourceRule
{
    /// <summary>
    /// The number of following blocks the rule was shown, summed over every offer.
    /// </summary>
    public long Shown { get; private set; }

    public int Read(SourceBlock block, SourceState state, NcxBuilder builder)
    {
        Shown += block.Following.Count;
        return block.Following.Count > 0 && block.Following[0].Line < 0 ? 1 : 0;
    }
}
