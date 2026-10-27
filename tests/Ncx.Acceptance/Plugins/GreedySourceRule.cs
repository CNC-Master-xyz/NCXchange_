using Ncx.Core.Model;
using Ncx.Core.Writing;
using Ncx.Readers;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// A faulty reader rule: it writes a block and claims two source blocks, whatever it was shown (D232).
/// </summary>
internal sealed class GreedySourceRule : ISourceRule
{
    public int Read(SourceBlock block, SourceState state, NcxBuilder builder)
    {
        builder.Begin(block.Line).Word("COOLANT", null, new IdentValue("ON")).End();
        return 2;
    }
}
