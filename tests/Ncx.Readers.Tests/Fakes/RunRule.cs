using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Readers.Tests.Fakes;

/// <summary>
/// A reader rule that claims a run of source blocks (D232): offered a block that holds its first M code, it claims
/// that block and every following block it is shown, up to its run length, and writes one NCX word for the whole run.
/// It records the lines of the following blocks it was shown at each offer, and can claim more blocks than it was
/// shown, as a faulty rule would.
/// </summary>
internal sealed class RunRule : ISourceRule
{
    private readonly decimal _code;
    private readonly int _length;
    private readonly bool _claimsBeyond;

    /// <param name="code">The number of the M code a run starts with, 5 for M5.</param>
    /// <param name="length">How many source blocks a run takes, the offered one among them.</param>
    /// <param name="claimsBeyond">True to return the run length even when fewer blocks follow.</param>
    public RunRule(decimal code, int length, bool claimsBeyond = false)
    {
        _code = code;
        _length = length;
        _claimsBeyond = claimsBeyond;
    }

    /// <summary>
    /// The lines of the following blocks shown at each offer, in order of the offers.
    /// </summary>
    public List<List<int>> Shown { get; } = [];

    /// <summary>
    /// The active code of modal group 1 the state showed at each offer.
    /// </summary>
    public List<string?> MotionSeen { get; } = [];

    public int Read(SourceBlock block, SourceState state, NcxBuilder builder)
    {
        var lines = new List<int>();
        foreach (SourceBlock following in block.Following)
        {
            lines.Add(following.Line);
        }

        Shown.Add(lines);
        MotionSeen.Add(state.ActiveCode(1));
        if (block.Find("M")?.Number != _code || (!_claimsBeyond && block.Following.Count < _length - 1))
        {
            return 0;
        }

        builder.Begin(block.Line).Word("COOLANT", "THROUGH", new IdentValue("ON")).End();
        return _length;
    }
}
