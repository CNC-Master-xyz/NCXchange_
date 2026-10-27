using Ncx.Core.Model;
using Ncx.Core.Writing;
using Ncx.Readers;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// A faulty reader rule: it returns a fixed count for every block, and writes one block or none (D232).
/// </summary>
internal sealed class CountingSourceRule(int count, bool writes) : ISourceRule
{
    public int Read(SourceBlock block, SourceState state, NcxBuilder builder)
    {
        if (writes)
        {
            builder.Begin(block.Line).Word("COOLANT", null, new IdentValue("ON")).End();
        }

        return count;
    }
}
