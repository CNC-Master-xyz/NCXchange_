using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.Writing;
using Ncx.Readers;

namespace Ncx.Plugins;

/// <summary>
/// Stands in a reader for a reader rule of a plugin (architecture 7, 9; D40, D66, D231, D232): it offers the rule the
/// source block, and hands the rule's blocks to the program only when the rule claimed a run without failing; a plugin
/// that throws, claims fewer than none or more blocks than it was shown, or claims blocks and writes nothing, is
/// reported and left out, and the reader reads the block as it would without the plugin (implementation 17, P7-01).
/// </summary>
internal sealed class PluginSourceRule : ISourceRule
{
    private readonly Plugin _plugin;
    private readonly PluginDiagnostics _diagnostics;

    public PluginSourceRule(Plugin plugin, ISourceRule rule, PluginDiagnostics diagnostics)
    {
        _plugin = plugin;
        _diagnostics = diagnostics;
        Rule = rule;
    }

    /// <summary>
    /// The reader rule of the plugin.
    /// </summary>
    public ISourceRule Rule { get; }

    public int Read(SourceBlock block, SourceState state, NcxBuilder builder)
    {
        // A plugin that failed is asked nothing more in the run (implementation 17, P7-01).
        if (_plugin.IsDropped)
        {
            return 0;
        }

        // The rule writes into a builder of its own. Only a claim made without failing reaches the program, so that a
        // rule that fails halfway, a block begun and not ended among it, leaves nothing behind (D5: the reader then
        // reads the block, and nothing is lost).
        var ownBuilder = new NcxBuilder(new Diagnostics(_diagnostics.File));
        NcxProgram claimed;
        int count;
        try
        {
            count = Rule.Read(block, state, ownBuilder);
            if (count == 0)
            {
                return 0;
            }

            claimed = ownBuilder.Build();
        }
        catch (Exception exception) when (PluginFaults.IsPluginFault(exception))
        {
            _diagnostics.Failed(_plugin.Name, $"{nameof(Read)} threw {PluginFaults.Describe(exception)}", block.Line,
                null);
            _plugin.Drop();
            return 0;
        }

        // A claim runs from the offered block over at most the blocks the rule was shown (D232); any other count is a
        // fault of the plugin, reported like a throw (gap closed 2026-10-27, delegated).
        int most = block.Following.Count + 1;
        if (count < 0 || count > most)
        {
            _diagnostics.Failed(_plugin.Name,
                string.Create(CultureInfo.InvariantCulture,
                    $"{nameof(Read)} claimed {count} source blocks where it was shown {most}"),
                block.Line, null);
            _plugin.Drop();
            return 0;
        }

        // A claim stands for its source blocks: a rule that claims blocks and writes nothing for them would drop them
        // without a trace (D5, D232), a fault reported like a throw (gap closed 2026-10-28, delegated).
        if (claimed.Blocks.Count == 0)
        {
            _diagnostics.Failed(_plugin.Name,
                string.Create(CultureInfo.InvariantCulture,
                    $"{nameof(Read)} claimed {count} source blocks and wrote no NCX block for them"),
                block.Line, null);
            _plugin.Drop();
            return 0;
        }

        ClaimedBlocks.CopyInto(claimed, builder);
        return count;
    }
}
