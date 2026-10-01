using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The run option skip_blocks: which SKIP blocks the virtual machine skips. SKIP blocks are executed unless the option
/// says otherwise: it skips all of them, or those of the block skip switches that are on, a bare SKIP being switch 0
/// (language 4.1, virtual machine 2.2, 3.6, D53, D133).
/// </summary>
public sealed record SkipBlocks
{
    /// <summary>
    /// The switch of a bare SKIP, the first block skip switch of the control (language 4.1, D133).
    /// </summary>
    public const int BareSwitch = 0;

    /// <summary>
    /// skip_blocks none, the default: every SKIP block is executed (D53).
    /// </summary>
    public static SkipBlocks None { get; } = new();

    /// <summary>
    /// skip_blocks all: every SKIP block is skipped.
    /// </summary>
    public static SkipBlocks Every { get; } = new() { AllSwitches = true };

    /// <summary>
    /// True when every SKIP block is skipped, whatever its switch.
    /// </summary>
    public bool AllSwitches { get; init; }

    /// <summary>
    /// The block skip switches that are on, skip_blocks = [0, 3]: a SKIP=n block with n among them is skipped, and a
    /// bare SKIP block when 0 is among them (D133).
    /// </summary>
    public IReadOnlyList<int> Switches { get; init; } = [];

    /// <summary>
    /// The block skip switches that are on, skip_blocks = [0, 3] (virtual machine 3.6).
    /// </summary>
    /// <param name="switches">The switch numbers: 0 for the bare SKIP, 1 to 9 for SKIP=n (language 4.1,
    /// D133).</param>
    public static SkipBlocks OnSwitches(IReadOnlyList<int> switches)
    {
        return new SkipBlocks { Switches = switches };
    }

    /// <summary>
    /// Tells whether the virtual machine skips a block under this option (D53).
    /// </summary>
    /// <param name="block">The block, with or without SKIP.</param>
    public bool Skips(Block block)
    {
        // A block without SKIP always runs; a SKIP block runs unless the option skips it (D53).
        if (!block.Skip)
        {
            return false;
        }

        if (AllSwitches)
        {
            return true;
        }

        // SKIP=n is skipped when switch n is on, and a bare SKIP is the first switch and has the number 0 in the list
        // (virtual machine 3.6, skip_blocks = [0, 3]; D133: Siemens / and /0 are the first skip level, and the Fanuc
        // reader writes /1 as a bare SKIP because / and /1 are one switch there).
        return Switches.Contains(block.SkipNumber ?? BareSwitch);
    }
}
