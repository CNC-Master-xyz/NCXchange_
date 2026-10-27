using Ncx.Core.Expander;
using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// A rewriter that replaces the block with this canonical text by another NCX text (architecture 9, Replace).
/// </summary>
internal sealed class ReplacingRewriter(string blockText, string replacement) : IProgramRewriter
{
    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        return NcxWriter.WriteBlock(block) == blockText
            ? RewriteResult.Replace(replacement, "the shop's limit")
            : RewriteResult.Unchanged;
    }
}
