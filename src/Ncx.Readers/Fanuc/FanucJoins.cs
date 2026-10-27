using Ncx.Core.Machine;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The chain of transforms where control flow joins (virtual machine 3.6; D225): a label a jump enters and the head
/// of a loop are reached with the chain of every edge into them, so the reader keeps the chain only where every known
/// edge agrees with the chain it reads on, and forgets it otherwise; a G69, G50.1 or G52 after that stays RAW
/// (FanucFrames.UnknownChain).
/// </summary>
internal static class FanucJoins
{
    // The codes that change the chain of transforms or the G52 shift (controllers fanuc.md 4; D244, D245).
    private static readonly string[] s_frameCodes =
    [
        "G52", "G54", "G55", "G56", "G57", "G58", "G59", "G54.1", "G92", "G68", "G68.1", "G68.2", "G68.3", "G68.4",
        "G69", "G51.1", "G50.1",
    ];

    /// <summary>
    /// The labels a block jumps to: the label of GOTO, the block of an in-section M99 P (D221), and on a machine of
    /// the builder nakamura the I{n} of a G411 or of a branch command (controller-mapping 6, 8).
    /// </summary>
    /// <param name="fanuc">The facts of the file.</param>
    /// <param name="block">A source block.</param>
    /// <param name="machine">The machine.</param>
    public static List<string> JumpsOf(FanucState fanuc, SourceBlock block, MachineConfig machine)
    {
        var labels = new List<string>();
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "GOTO" && word.Number is not null)
            {
                labels.Add(FanucState.LabelOf(word));
            }

            string? code = word.Address == "G" ? NativeCode.Of(word) : null;
            if (code is not null && FanucMacro.IsBuilderNakamura(machine)
                && (code == "G411" || FanucMacro.IsBranchCommand(machine, code))
                && block.Find("I") is SourceWord target && FanucMacro.WholeNumber(target) is long number)
            {
                labels.Add(number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        if (fanuc.ReturnsToTheBlock.TryGetValue(block.Line, out string? returns))
        {
            labels.Add(returns);
        }

        return labels;
    }

    /// <summary>
    /// The chain at a label a jump enters: kept where every jump from before it left the chain the reader reads on and
    /// no jump from after it closes a range that changes the frames; forgotten otherwise (virtual machine 3.6; D225).
    /// </summary>
    /// <param name="fanuc">The facts of the file.</param>
    /// <param name="block">The block of the label.</param>
    /// <param name="label">The label.</param>
    /// <param name="chainAtJumps">The chain, as FanucChain.ToKey, after each jump block the reader has read, by
    /// line.</param>
    /// <param name="machine">The machine.</param>
    public static void AtLabel(FanucState fanuc, SourceBlock block, string label,
        IReadOnlyDictionary<int, string> chainAtJumps, MachineConfig machine)
    {
        string here = fanuc.Chain.ToKey();
        List<SourceBlock> section = [.. fanuc.SectionOf(block)];
        int at = section.FindIndex(candidate => candidate.Line == block.Line);
        for (int index = 0; index < section.Count; index++)
        {
            SourceBlock source = section[index];
            if (!JumpsOf(fanuc, source, machine).Contains(label))
            {
                continue;
            }

            bool agrees = index < at
                ? chainAtJumps.TryGetValue(source.Line, out string? there) && there == here
                : !ChangesFrames(section, at, index, machine);
            if (!agrees)
            {
                fanuc.Chain.Forget();
                return;
            }
        }
    }

    /// <summary>
    /// The chain at the head of a loop, DOm: kept only where no block of the loop, to its ENDm, changes the frames;
    /// forgotten otherwise, since the jump back carries the chain of the end of the loop (virtual machine 3.6; D225).
    /// </summary>
    /// <param name="fanuc">The facts of the file.</param>
    /// <param name="block">The block of DOm.</param>
    /// <param name="number">The loop number m.</param>
    /// <param name="machine">The machine.</param>
    public static void AtLoopHead(FanucState fanuc, SourceBlock block, int number, MachineConfig machine)
    {
        List<SourceBlock> section = [.. fanuc.SectionOf(block)];
        int head = section.FindIndex(candidate => candidate.Line == block.Line);
        for (int index = head + 1; head >= 0 && index < section.Count; index++)
        {
            if (section[index].Find("END") is SourceWord end && end.Number == number)
            {
                if (ChangesFrames(section, head, index, machine))
                {
                    fanuc.Chain.Forget();
                }

                return;
            }
        }

        fanuc.Chain.Forget();
    }

    /// <summary>
    /// The chain at the start of a program that M99 loops back to (controller-mapping 6): the second pass starts with
    /// the chain the first leaves, so the empty chain of the start holds only where the program ends with every
    /// rotation, tilted plane, mirror, 3D conversion and G52 shift cancelled again and calls nothing; forgotten
    /// otherwise (virtual machine 3.6; D225).
    /// </summary>
    /// <param name="fanuc">The facts of the file.</param>
    /// <param name="begin">The first block of the program.</param>
    /// <param name="machine">The machine.</param>
    public static void AtProgramLoop(FanucState fanuc, SourceBlock begin, MachineConfig machine)
    {
        var on = new HashSet<string>(StringComparer.Ordinal);
        var mirrored = new HashSet<string>(StringComparer.Ordinal);
        foreach (SourceBlock block in fanuc.SectionOf(begin))
        {
            if (FanucMacro.CallsOf(block, machine).Count > 0)
            {
                fanuc.Chain.Forget();
                return;
            }

            List<string> axes = AxisLetters(block);
            foreach (SourceWord word in block.Words)
            {
                switch (word.Address == "G" ? NativeCode.Of(word) : null)
                {
                    case "G68" or "G68.1" or "G68.2" or "G68.3" or "G68.4":
                        on.Add("rotation");
                        break;
                    case "G69":
                        on.Remove("rotation");
                        break;
                    case "G51.1":
                        mirrored.UnionWith(axes);
                        break;
                    case "G50.1":
                        mirrored.ExceptWith(axes);
                        break;
                    case "G52" when AllZero(block):
                        on.Remove("shift");
                        break;
                    case "G52" or "G92":
                        on.Add("shift");
                        break;
                }
            }
        }

        if (on.Count > 0 || mirrored.Count > 0)
        {
            fanuc.Chain.Forget();
        }
    }

    // The letters of the axis words of a block, X Y Z U V W A B C.
    private static List<string> AxisLetters(SourceBlock block)
    {
        var letters = new List<string>();
        foreach (SourceWord word in block.Words)
        {
            if (word.Address.Length == 1 && "XYZUVWABC".Contains(word.Address[0]))
            {
                letters.Add(word.Address);
            }
        }

        return letters;
    }

    // Whether every axis word of a block is 0, G52 X0 Y0 that cancels the local system (D244).
    private static bool AllZero(SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address.Length == 1 && "XYZUVWABC".Contains(word.Address[0]) && word.Number != 0m)
            {
                return false;
            }
        }

        return true;
    }

    // Whether a block of the range, both ends included, changes the chain or calls a program that may.
    private static bool ChangesFrames(List<SourceBlock> section, int from, int to, MachineConfig machine)
    {
        for (int index = Math.Max(0, from); index <= to && index < section.Count; index++)
        {
            SourceBlock candidate = section[index];
            if (FanucMacro.CallsOf(candidate, machine).Count > 0)
            {
                return true;
            }

            foreach (SourceWord word in candidate.Words)
            {
                if (word.Address == "G" && NativeCode.Of(word) is string code && s_frameCodes.Contains(code))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
