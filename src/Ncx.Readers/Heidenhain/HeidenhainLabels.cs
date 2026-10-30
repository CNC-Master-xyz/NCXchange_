namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The labels of a Klartext file in two passes (controllers heidenhain.md 1, 7 rule 3; controller-mapping 6): the first
/// collects the calls, the repeats and the jumps, the second decides every LBL. An LBL n closed by LBL 0 and called
/// without REP is a subprogram, a SUB section of the file; an LBL used by REP or as the target of FN 9 to FN 12 is a
/// LABEL; an FN jump to an LBL that the end of the program follows is JUMP=END.
/// </summary>
internal sealed class HeidenhainLabels
{
    // The words of cycle 14 that name its contour subprograms (controllers heidenhain.md 5).
    private const string ContourLabel = "KONTURLABEL";

    /// <summary>
    /// The name of every subprogram by the line of its LBL block.
    /// </summary>
    public Dictionary<int, string> SubBegins { get; } = [];

    /// <summary>
    /// The lines of the LBL 0 blocks that close a subprogram.
    /// </summary>
    public HashSet<int> SubEnds { get; } = [];

    /// <summary>
    /// The names of the subprograms.
    /// </summary>
    public HashSet<string> Subs { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The places among the blocks with words of the first and the last block of every subprogram, by name.
    /// </summary>
    public Dictionary<string, int> SubFirst { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The place of the LBL 0 of every subprogram, by name.
    /// </summary>
    public Dictionary<string, int> SubLast { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The lines of the LBL blocks of the subprograms that the main flow of their program runs through as well: a
    /// section before the M2 or M30, or in a program without one (D254). The structure pass moves such a section into
    /// a SUB behind the program and writes a CALL where it stood.
    /// </summary>
    public HashSet<int> Lifted { get; } = [];

    /// <summary>
    /// The lines of the LBL 0 blocks that close such a section (D254).
    /// </summary>
    public HashSet<int> LiftedEnds { get; } = [];

    /// <summary>
    /// The labels a REP or an FN jump uses, each a LABEL.
    /// </summary>
    public HashSet<string> Targets { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The lines of the FN jumps whose label the end of the program follows: JUMP=END (controller-mapping 1).
    /// </summary>
    public HashSet<int> JumpsToTheEnd { get; } = [];

    /// <summary>
    /// The labels that the end of the program follows and that only JUMP=END jumps reach: their LBL writes nothing, the
    /// jumps continue at PROGRAM=END (controller-mapping 1, JUMP=END).
    /// </summary>
    public HashSet<string> EndTargets { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The lines of the FN jumps that leave a part a CALL LBL n REP k repeats, from inside it to a target outside it,
    /// with the label of that part (D213).
    /// </summary>
    public Dictionary<int, string> JumpsLeavingRepeats { get; } = [];

    /// <summary>
    /// The lines of the contour subprograms of cycle 14, LBL n .. LBL 0 for a CYCL DEF 14.1 KONTURLABEL n: the reader
    /// keeps them RAW with the cycle, so that the contour after the M30 does not become motion blocks behind
    /// JUMP=END (D394, gap closed 2026-10-30, delegated).
    /// </summary>
    public HashSet<int> ContourLines { get; } = [];

    /// <summary>
    /// The label a block names after its keyword LBL: "1" of LBL 1 and of CALL LBL 1 REP 3, "NAME" of LBL "NAME"; null
    /// when it names none.
    /// </summary>
    /// <param name="block">A block with the word LBL.</param>
    public static string? LabelOf(SourceBlock block)
    {
        for (int index = 0; index < block.Words.Count; index++)
        {
            SourceWord word = block.Words[index];
            if (word.Address != "LBL")
            {
                continue;
            }

            string text = word.Text.Length > 0 || index + 1 >= block.Words.Count
                ? word.Text
                : block.Words[index + 1].Text;
            text = text.Trim('"').Trim();
            if (text.Length == 0)
            {
                return null;
            }

            // A number label without its leading zeros, LBL 01 is LBL 1; a name as written.
            bool digits = true;
            foreach (char character in text)
            {
                digits &= char.IsAsciiDigit(character);
            }

            if (!digits)
            {
                return text;
            }

            string number = text.TrimStart('0');
            return number.Length == 0 ? "0" : number;
        }

        return null;
    }

    /// <summary>
    /// Tells whether a block is a label, LBL n or LBL 0, and not a call of one.
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool IsLabel(SourceBlock block)
    {
        return block.Words.Count > 0 && block.Words[0].Address == "LBL";
    }

    /// <summary>
    /// Tells whether a block is CALL LBL, with or without REP.
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool IsLabelCall(SourceBlock block)
    {
        return block.Words.Count > 1 && block.Words[0].Address == "CALL" && block.Words[1].Address == "LBL";
    }

    /// <summary>
    /// Tells whether a block repeats a program part, CALL LBL n REP k.
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool IsRepeat(SourceBlock block)
    {
        return IsLabelCall(block) && block.Find("REP") is not null;
    }

    /// <summary>
    /// Tells whether a block is a conditional jump, FN 9 to FN 12 ... GOTO LBL n (controllers heidenhain.md 6).
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool IsJump(SourceBlock block)
    {
        return block.Words.Count > 0 && block.Words[0].Address == "FN" && block.Words[0].Number is >= 9 and <= 12
            && block.Find("GOTO") is not null;
    }

    /// <summary>
    /// Tells whether a block ends the program, M30 or M2 (controllers heidenhain.md 1).
    /// </summary>
    /// <param name="block">A block with words.</param>
    public static bool Ends(SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "M" && NativeCode.Of(word) is "M30" or "M2")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Classifies the labels of the blocks with words of a file.
    /// </summary>
    /// <param name="blocks">The blocks with words, in file order.</param>
    public static HeidenhainLabels Classify(IReadOnlyList<SourceBlock> blocks)
    {
        // First pass: the end of the program, the calls, the repeats, the jumps and where every label stands.
        var labels = new HeidenhainLabels();
        var called = new HashSet<string>(StringComparer.Ordinal);
        var jumps = new List<int>();
        var repeats = new List<int>();
        int mainEnd = -1;
        for (int index = 0; index < blocks.Count; index++)
        {
            SourceBlock block = blocks[index];
            string? label = LabelOf(block);
            if (mainEnd < 0 && Ends(block) && !block.BlockSkip)
            {
                mainEnd = index;
            }

            if (IsRepeat(block) && label is not null)
            {
                labels.Targets.Add(label);
                repeats.Add(index);
            }
            else if (IsLabelCall(block) && label is not null)
            {
                called.Add(label);
            }
            else if (IsJump(block) && label is not null)
            {
                jumps.Add(index);
            }
        }

        labels.FindContours(blocks);
        labels.DecideJumps(blocks, jumps);
        labels.FindJumpsLeavingRepeats(blocks, jumps, repeats);
        labels.DecideSubs(blocks, called, mainEnd);
        return labels;
    }

    // D213: a JUMP that leaves the LABEL..REPEAT part ends the repeat in NCX, and a later arrival starts it fresh; the
    // TNC keeps a REP count as block state that only GOTO 0 or a new program selection resets, so the reader warns at
    // every FN jump inside the part between LBL n and CALL LBL n REP k whose target, JUMP=END included, lies outside it
    // (decisions.md; controllers heidenhain.md 1).
    private void FindJumpsLeavingRepeats(IReadOnlyList<SourceBlock> blocks, List<int> jumps, List<int> repeats)
    {
        foreach (int repeat in repeats)
        {
            string label = LabelOf(blocks[repeat])!;
            int start = FindLabel(blocks, label);
            if (start < 0 || start > repeat)
            {
                continue;
            }

            foreach (int jump in jumps)
            {
                int target = JumpsToTheEnd.Contains(blocks[jump].Line) ? blocks.Count
                    : FindLabel(blocks, LabelOf(blocks[jump])!);
                // A jump to a label the file does not hold leaves nothing: its block stays RAW.
                if (target >= 0 && jump > start && jump < repeat && (target < start || target > repeat))
                {
                    JumpsLeavingRepeats.TryAdd(blocks[jump].Line, label);
                }
            }
        }
    }

    // An FN jump to an LBL that the end of the program follows directly is JUMP=END, which needs no LABEL; every other
    // FN jump uses its label (controller-mapping 1, JUMP=END; language 4.9).
    private void DecideJumps(IReadOnlyList<SourceBlock> blocks, List<int> jumps)
    {
        foreach (int jump in jumps)
        {
            string label = LabelOf(blocks[jump])!;
            int target = FindLabel(blocks, label);
            bool endFollows = target >= 0 && target + 1 < blocks.Count && Ends(blocks[target + 1])
                && !blocks[target + 1].BlockSkip;
            if (endFollows)
            {
                JumpsToTheEnd.Add(blocks[jump].Line);
                EndTargets.Add(label);
            }
            else
            {
                Targets.Add(label);
            }
        }
    }

    // Subprograms are LBL n ... LBL 0 sections of the program (controllers heidenhain.md 1); an LBL n is one when a
    // CALL LBL n without REP calls it, no REP or FN jump uses it as a label, and an LBL 0 closes it (heidenhain 7
    // rule 3). A section inside another subprogram is none. D254: a section that stands before the M2 or M30, or in a
    // program without one, is run by the main flow as ordinary blocks as well, over and above its calls, so it is a
    // SUB with a CALL where it stood, which runs the same blocks in the same order (decisions.md; language 4.13,
    // D89). An LBL that is called and also used by REP or an FN jump stays a LABEL, as it was.
    private void DecideSubs(IReadOnlyList<SourceBlock> blocks, HashSet<string> called, int mainEnd)
    {
        int insideUntil = -1;
        for (int index = 0; index < blocks.Count; index++)
        {
            SourceBlock block = blocks[index];
            string? label = IsLabel(block) ? LabelOf(block) : null;
            if (label is null || label == "0" || index <= insideUntil || !called.Contains(label)
                || Targets.Contains(label) || Subs.Contains(label))
            {
                continue;
            }

            int closing = index + 1;
            while (closing < blocks.Count && !(IsLabel(blocks[closing]) && LabelOf(blocks[closing]) == "0"))
            {
                closing++;
            }

            if (closing >= blocks.Count)
            {
                continue;
            }

            if (mainEnd < 0 || index < mainEnd)
            {
                Lifted.Add(block.Line);
                LiftedEnds.Add(blocks[closing].Line);
            }

            SubBegins[block.Line] = label;
            SubEnds.Add(blocks[closing].Line);
            Subs.Add(label);
            SubFirst[label] = index;
            SubLast[label] = closing;
            insideUntil = closing;
        }
    }

    // D394, gap closed 2026-10-30 (delegated): cycle 27 stays RAW with its contour. Every LBL n that a CYCL DEF 14.1
    // KONTURLABEL n names, KONTURLABEL 1/2/3 naming several, is a contour of cycle 14 (iTNC 530
    // Benutzerhandbuch-Klartext 340490, connector p. 430-431), and its blocks up to the LBL 0 after it stay RAW.
    private void FindContours(IReadOnlyList<SourceBlock> blocks)
    {
        foreach (SourceBlock block in blocks)
        {
            int at = block.Text.IndexOf(ContourLabel, StringComparison.Ordinal);
            if (at < 0 || !block.Text.Contains("CYCL DEF 14.1", StringComparison.Ordinal))
            {
                continue;
            }

            string list = block.Text.Substring(at + ContourLabel.Length).Trim();
            const StringSplitOptions Options = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;
            foreach (string label in list.Split('/', Options))
            {
                int start = FindLabel(blocks, label);
                for (int index = start; start >= 0 && index < blocks.Count; index++)
                {
                    ContourLines.Add(blocks[index].Line);
                    if (index > start && IsLabel(blocks[index]) && LabelOf(blocks[index]) == "0")
                    {
                        break;
                    }
                }
            }
        }
    }

    private static int FindLabel(IReadOnlyList<SourceBlock> blocks, string label)
    {
        for (int index = 0; index < blocks.Count; index++)
        {
            if (IsLabel(blocks[index]) && LabelOf(blocks[index]) == label)
            {
                return index;
            }
        }

        return -1;
    }
}
