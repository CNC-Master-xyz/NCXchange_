using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The frames of a Fanuc block (controllers fanuc.md 4, 9 rules 3 and 6; controller-mapping 1; language 4.2): G54 to
/// G59 and G54.1 P as ORIGIN, G53 as FRAME=MACHINE, G28 and G30 as HOME, G92 (G50 in system A) as SETPOS and RPM_MAX,
/// G52 as SHIFT with SHIFT=RESET, G68/G69 as ROTATE, G51.1/G50.1 as MIRROR, G43.4/G43.5 as TCPM, G12.1/G13.1 as POLAR,
/// G7.1 as CYLINDER, G5.1 as TOLERANCE, and the builder's codes of [transform]. The tilted plane of G68.2 is FanucTilt,
/// the chain of transforms FanucChain.
/// </summary>
internal static partial class FanucFrames
{
    // G54 to G59 are the datums 1 to 6 (language 4.2, ORIGIN).
    private static readonly string[] s_origins = ["G54", "G55", "G56", "G57", "G58", "G59"];

    private static readonly IdentValue s_on = new("ON");
    private static readonly IdentValue s_off = new("OFF");

    /// <summary>
    /// Reads the frame words of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        block.ChainBefore = block.Fanuc.Chain.Copy();
        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadOrigin(block);

        // G53 in the block: machine coordinates for that block, also with macro variables (controllers fanuc.md 4, 9
        // rule 3); the motion writes FRAME=MACHINE.
        block.MachineFrame = block.TakeCode("G53");
        ReadHome(block);
        ReadSetpos(block);
        ReadShift(block);
        ReadRotation(block);
        ReadMirror(block);
        FanucTilt.Read(block);
        ReadTcpm(block);
        ReadTransformations(block);
        ReadTolerance(block);
    }

    /// <summary>
    /// Writes the blocks of a change of the chain (FanucChain.Append, FanucChain.TryChange): before the main block,
    /// each in a block of its own, since a RESET and the entries after it depend on their order (language 4.2) while
    /// the state words of one block do not (virtual machine 3, step 3); after it when the main block selects the datum,
    /// which empties the chain first (language 4.2, ORIGIN).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="blocks">The NCX blocks of the change, in order.</param>
    public static void Write(FanucBlock block, List<DraftBlock> blocks)
    {
        List<DraftBlock> place = block.Draft.Main.Has("ORIGIN", null) ? block.Draft.After : block.Draft.Before;
        place.AddRange(blocks);
    }

    /// <summary>
    /// Why a block stays RAW whose code acts on entries of a chain the reader does not know (FanucChain.Known).
    /// </summary>
    /// <param name="code">The code, "G69".</param>
    public static string UnknownChain(string code)
    {
        return $"{code} acts on transforms the reader does not know here: those of the caller of its subprogram, of a "
            + "called subprogram, or of a jump or a loop that enters this place (virtual machine 3.6, 3.9; D225)";
    }

    // The chain becomes the one the control holds after the code (FanucChain.TryChange); where no cut of the chain
    // means the same under both readings of SHIFT=RESET, the block stays RAW (wave-1 question #95; D5).
    private static void Change(FanucBlock block, List<FanucChainEntry> desired, string code)
    {
        var blocks = new List<DraftBlock>();
        if (!block.Fanuc.Chain.TryChange(desired, blocks))
        {
            block.Draft.KeepAsRaw(
                $"{code} would unwind the chain of transforms with a SHIFT=RESET that the documents read in two ways, "
                + "the shifts or the last one (language 4.2, virtual machine 2.1)");
            return;
        }

        Write(block, blocks);
        block.Fanuc.ForgetPositions();
    }

    // ORIGIN empties the chain of NCX, and a new frame makes the positions of the reader unknown (language 4.2,
    // virtual machine 3.4).
    private static void ClearChain(FanucBlock block)
    {
        block.Fanuc.Chain.Clear();
        block.Fanuc.ForgetPositions();
    }

    // The words of the active G52 shift, the axes that are not 0 in the order the G52 named them (D244).
    private static List<Word> LocalWords(FanucChain chain)
    {
        var words = new List<Word>();
        foreach (KeyValuePair<string, decimal> axis in chain.Local)
        {
            if (axis.Value != 0m)
            {
                words.Add(new Word { Key = axis.Key, Value = FanucNumbers.Of(axis.Value) });
            }
        }

        return words;
    }

    private static IntegerValue Integer(int number)
    {
        return new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture));
    }
}
