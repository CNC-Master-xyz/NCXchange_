using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The two-block form of the Fanuc multiple repetitive cycles (D409; controllers fanuc.md 6): G71 U(d) R(e) before
/// G71 P Q U W F, G75 R(e) before G75 X Z P Q F, G76 P Q R before G76 X Z R P Q F. The catalog entry names the params
/// of the first block in first_block (machine-config 6); the reader holds them until the second block, which writes
/// the one CYCLE block of NCX, and reads G74 to G76 with their end point on the CYCLE_CALL.
/// </summary>
internal static class FanucTwoBlockCycles
{
    // The one-shot entry of a native cycle, G70 to G76 of a lathe (machine-config 6, modal); null where the catalog has
    // none.
    public static CycleEntry? OneShotEntry(CycleCatalog catalog, string native)
    {
        foreach (CycleEntry entry in catalog.Entries)
        {
            if (entry.Native == native && !entry.Modal && entry.Available)
            {
                return entry;
            }
        }

        return null;
    }

    // D409: the addresses of the params of the second block of a two-block cycle, every params address of a cycle
    // of one block.
    public static HashSet<string> SecondBlockAddresses(CycleEntry entry)
    {
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> parameter in entry.Params)
        {
            if (!entry.FirstBlock.Contains(parameter.Key))
            {
                addresses.Add(parameter.Value);
            }
        }

        return addresses;
    }

    // D409: the first block of a two-block cycle names no contour (G71 U R without P and Q) and no end point (G75 R
    // without X and Z); the second block names one of them.
    public static bool IsFirstBlock(FanucBlock block, CycleEntry entry)
    {
        if (entry.Contour.Count == 2)
        {
            return block.Find(entry.Contour[0]) is null && block.Find(entry.Contour[1]) is null;
        }

        foreach (SourceWord word in block.Source.Words)
        {
            if (FanucAxes.Of(word, block) is not null)
            {
                return false;
            }
        }

        return true;
    }

    // D409: the first block of a two-block cycle writes no block of NCX: its params, numbers of the addresses its
    // entry names for the first block, wait for the second block, which must follow it directly with the same code,
    // and the cycle is one CYCLE block there. A first block with another word, or one that its second block does not
    // follow, stays RAW, and the control takes it as it stands (D5).
    public static void ReadFirstBlock(FanucBlock block, CycleEntry entry, string code)
    {
        var addresses = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in entry.FirstBlock)
        {
            addresses[entry.Params[name]] = name;
        }

        var values = new List<KeyValuePair<string, Value>>();
        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "N")
            {
                continue;
            }

            if (!addresses.TryGetValue(word.Address, out string? name) || word.Number is null
                || word.Expression is not null)
            {
                block.Draft.KeepAsRaw($"{word.Address}{word.Text} of the first block of {code} has no param in the "
                    + $"catalog entry {entry.Name} (D409)");
                return;
            }

            block.MarkRead(word);
            if (FanucMacro.ValueOf(block, word) is not Value value)
            {
                return;
            }

            values.Add(new KeyValuePair<string, Value>(name, value));
        }

        if (block.Fanuc.NextBlock(block.Source) is not SourceBlock next || next.BlockSkip
            || !next.Words.Any(word => word.Address == "G" && NativeCode.Of(word) == code)
            || !next.Words.Any(word => IsSecondBlockWord(entry, word.Address)))
        {
            block.Draft.KeepAsRaw($"the first block of {code} is not followed by its second block (D409)");
            return;
        }

        block.Fanuc.FirstBlock = new FanucFirstBlock(block.Source, next.Line, values);
    }

    // D409: a word that marks a second block: P of the contour on G71 to G73, an end-point word on G74 to G76.
    private static bool IsSecondBlockWord(CycleEntry entry, string address)
    {
        return entry.Contour.Count == 2 ? address == entry.Contour[0] : address is "X" or "Z" or "U" or "W";
    }

    // D409: the second block of G74, G75 or G76, the cycle with the params of both blocks and the end point X(U) Z(W)
    // as the axis words of its CYCLE_CALL, absolute, an incremental word counted from where the tool stands; the
    // one-shot cycle is closed with CYCLE=OFF (D224), and the tool stands at the start point again (D411). A word the
    // entry does not map stays RAW.
    public static void ReadEndPointCycle(FanucBlock block, CycleEntry entry, FanucFirstBlock? first)
    {
        var definition = new DraftBlock().Add("CYCLE", new IdentValue(entry.Name));
        AddParams(block, entry, first, definition);
        var call = new DraftBlock().WithVerb("CYCLE_CALL");
        foreach (FanucAxisWord position in FanucAxes.Unread(block))
        {
            block.MarkRead(position.Word);
            if (FanucMacro.ValueOf(block, position.Word) is not Value value)
            {
                return;
            }

            if (FanucNumbers.NumberOf(value) is not decimal number)
            {
                block.Draft.KeepAsRaw($"the end point of {entry.Native} is no number the reader knows");
                return;
            }

            if (position.Incremental)
            {
                if (!block.State.Positions.TryGetValue(position.Axis, out decimal start))
                {
                    block.Draft.KeepAsRaw($"{position.Word.Address} of {entry.Native} counts from a start point the "
                        + "reader does not know");
                    return;
                }

                number += start;
            }

            call.Add(position.Axis, FanucNumbers.Of(number));
        }

        foreach (SourceWord word in block.Unread())
        {
            if (word.Address != "N")
            {
                block.Draft.KeepAsRaw($"{word.Address}{word.Text} of {entry.Native} has no param in the catalog entry "
                    + $"{entry.Name} (D409)");
                return;
            }
        }

        if (block.Draft.IsRaw)
        {
            return;
        }

        foreach (Word word in definition.Words)
        {
            block.Draft.AddState(word.Key, word.Addr, word.Value);
        }

        block.Draft.After.Add(call);
        block.Draft.After.Add(new DraftBlock().Add("CYCLE", new IdentValue("OFF")));
        block.Fanuc.Record("CYCLE", "OFF");
        if (block.Fanuc.Cycle is FanucCycle active)
        {
            active.Written = null;
        }
    }

    // D409: the params of a one-shot cycle in the order of its entry, those of the first block from that block and the
    // others taken from this one, a number or an expression each.
    public static void AddParams(FanucBlock block, CycleEntry entry, FanucFirstBlock? first, DraftBlock definition)
    {
        foreach (KeyValuePair<string, Value> parameter in first?.Params ?? [])
        {
            definition.Add(parameter.Key, parameter.Value);
        }

        foreach (KeyValuePair<string, string> parameter in entry.Params)
        {
            if (!entry.FirstBlock.Contains(parameter.Key) && block.Take(parameter.Value) is SourceWord word
                && FanucMacro.ValueOf(block, word) is Value value)
            {
                definition.Add(parameter.Key, value);

                // F of G71 to G75 is also the modal F of the control, as on G90 and G94 (D218, D409).
                if (parameter.Key == "CYCLE_F")
                {
                    block.Fanuc.ControlFeed = value;
                }
            }
        }
    }
}
