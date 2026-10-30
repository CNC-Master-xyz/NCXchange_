using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.Machine;

/// <summary>
/// The cycle catalog of one controller family as a machine uses it: the built-in drilling family, the entries of the
/// catalog file of [cycles] and the [[cycle]] entries of the machine file, each overriding the entry of the same name
/// before it (machine-config 6, language 4.7.1). Readers look an entry up by its native cycle, compilers and the
/// expander by its NCX name (architecture 6).
/// </summary>
public sealed record CycleCatalog
{
    /// <summary>
    /// The controller family whose native cycles the entries map; null for the empty catalog of the default machine of
    /// D103, which serves no reader and no compiler.
    /// </summary>
    public Controller? Controller { get; init; }

    /// <summary>
    /// The entries in catalog order: the built-in drilling family first, then the cycles that the catalog file and the
    /// machine file add.
    /// </summary>
    public IReadOnlyList<CycleEntry> Entries { get; init; } = [];

    /// <summary>
    /// The [[cycle]] tables of the machine file that name a channel, in file order, kept apart from Entries until
    /// ForChannel applies those of the channel compiled (machine-config 6, D361).
    /// </summary>
    public IReadOnlyList<CycleEntry> ChannelEntries { get; init; } = [];

    /// <summary>
    /// The entry of an NCX cycle name, CYCLE=RECT_POCKET (language 4.7.1); null when the catalog has none, which the
    /// caller reports.
    /// </summary>
    /// <param name="ncxName">The cycle name as a program writes it.</param>
    public CycleEntry? Find(string ncxName)
    {
        // A program may use any catalog name; the entry of that name maps it (language 4.7.1).
        foreach (CycleEntry entry in Entries)
        {
            if (entry.Name == ncxName)
            {
                return IsHidden(entry) ? null : entry;
            }
        }

        return null;
    }

    /// <summary>
    /// The entry of an NCX cycle name under a drilling axis: the entry whose for_axis is that axis where there is
    /// one, otherwise the entry without a for_axis, which maps the default axis (machine-config 6, D160).
    /// </summary>
    /// <param name="ncxName">The cycle name as a program writes it.</param>
    /// <param name="axis">The drilling axis of the cycle, the NCX name of AXIS.</param>
    public CycleEntry? Find(string ncxName, string axis)
    {
        CycleEntry? defaultAxis = null;
        foreach (CycleEntry entry in Entries)
        {
            if (entry.Name != ncxName)
            {
                continue;
            }

            if (entry.ForAxis == axis)
            {
                return IsHidden(entry) ? null : entry;
            }

            defaultAxis ??= entry.ForAxis is null ? entry : null;
        }

        return defaultAxis is not null && IsHidden(defaultAxis) ? null : defaultAxis;
    }

    /// <summary>
    /// The first entry of a native cycle in catalog order, "G81", "200", "CYCLE81"; null when the catalog has none.
    /// </summary>
    /// <param name="native">The native cycle as the catalog writes it.</param>
    public CycleEntry? FindNative(string native)
    {
        // A reader turns a native cycle into the entry that maps it; of two entries of one cycle, DRILL and
        // DRILL_DWELL on cycle 200, the first in catalog order (controller-mapping 5). A native no entry writes as its
        // own is the dwell_native of an entry, the Fanuc mill G89 of REAM (D177).
        // D410: an entry withdrawn with available = false maps no native cycle; the compiler reports its name (CMP004).
        foreach (CycleEntry entry in Entries)
        {
            if (entry.Native == native && entry.Available)
            {
                return entry;
            }
        }

        foreach (CycleEntry entry in Entries)
        {
            if (entry.DwellNative == native && entry.Available)
            {
                return entry;
            }
        }

        return null;
    }

    // D181, D164: a Siemens position without a value takes the value zero, which the entry records in defaults (VARI
    // of CYCLE83 = 0, chip breaking); a block that leaves out a fixed value is compared through that default, and
    // fits no entry that fixes it where the entry has none.

    /// <summary>
    /// The entry of a native cycle whose fixed values the source block carries: CYCLE83 with VARI=1 is PECK, with
    /// VARI=0 CHIP_BREAK (machine-config 6, controller-mapping 5); an entry without fixed values fits every block of
    /// its cycle. Null when no entry fits.
    /// </summary>
    /// <param name="native">The native cycle as the catalog writes it.</param>
    /// <param name="nativeValues">The native parameters the source block gives, by native name.</param>
    /// <param name="forAxis">The drilling axis the source block cuts along, which picks the entry of that for_axis
    /// (D160); null where the family has no such entries.</param>
    public CycleEntry? FindNative(string native, IReadOnlyDictionary<string, decimal> nativeValues,
        string? forAxis = null)
    {
        // The reader takes the first entry in catalog order whose fixed values the source block carries and whose
        // params cover its words, so that a Fanuc lathe G83 with a peck depth Q is PECK and one without it DRILL
        // (machine-config 6, D160).
        foreach (CycleEntry entry in Entries)
        {
            if (entry.Native == native && entry.Available && Fits(entry, nativeValues, forAxis))
            {
                return entry;
            }
        }

        // D177: the dwell_native of an entry, the Fanuc mill G89 of REAM, where no entry writes it as its own.
        foreach (CycleEntry entry in Entries)
        {
            if (entry.DwellNative == native && entry.Available && Fits(entry, nativeValues, forAxis))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// The entry of a native cycle that has two entries told apart by their before line, the Fanuc G84 of TAP and
    /// TAP_COMPENSATING (D359, an amendment of D160): the entry whose before line reaches the block where it does, the
    /// entry with no before key where it does not. Where an entry of the native sets before = "", the cycle is always
    /// that entry and an entry without the key counts as not in the catalog (D359). Null when no entry fits.
    /// </summary>
    /// <param name="native">The native cycle, "G84".</param>
    /// <param name="lineReaches">Whether the before line of an entry reaches the block in one of the ways of
    /// D256.</param>
    /// <param name="forAxis">The drilling axis of the block, which picks the entries of that for_axis (D160); null
    /// where the family has no such entries.</param>
    public CycleEntry? FindNativeByBeforeLine(string native, bool lineReaches, string? forAxis = null)
    {
        CycleEntry? withLine = null;
        CycleEntry? withoutKey = null;
        foreach (CycleEntry entry in Entries)
        {
            if (entry.Native != native || !entry.Available || (forAxis is not null && entry.ForAxis != forAxis))
            {
                continue;
            }

            if (entry.Before == "")
            {
                return entry;
            }

            if (entry.Before is null)
            {
                withoutKey ??= entry;
            }
            else
            {
                withLine ??= entry;
            }
        }

        return lineReaches ? withLine : withoutKey;
    }

    /// <summary>
    /// This catalog with the entries of a later file over it: an entry of a name the catalog has overrides that entry
    /// by the keys it wrote, an entry of a new name is added at the end (machine-config 6).
    /// </summary>
    /// <param name="entries">The entries of the catalog file or of the machine file, as the file writes them.</param>
    public CycleCatalog Override(IReadOnlyList<CycleEntry> entries)
    {
        // The catalog file of the family overrides the built-in drilling family, the [[cycle]] entries of a machine
        // file override the catalog per machine (machine-config 6).
        var overridden = new List<CycleEntry>(Entries);
        var channelEntries = new List<CycleEntry>(ChannelEntries);
        foreach (CycleEntry entry in entries)
        {
            if (entry.Channel is not null)
            {
                // D361: a table of one channel waits for ForChannel; the channel-less table is the fallback.
                channelEntries.Add(entry with { Family = entry.Family ?? Controller });
                continue;
            }

            int index = IndexOfName(overridden, entry.Name, entry.ForAxis);
            if (index < 0)
            {
                // D157: the rule words of an added entry, the X entry of a lathe (D160) among them, follow from its
                // own params like those of every other entry; nothing is lent from an entry of the same name.
                overridden.Add(entry with { Family = entry.Family ?? Controller });
            }
            else
            {
                overridden[index] = overridden[index].OverriddenBy(entry);
            }
        }

        return new CycleCatalog { Controller = Controller, Entries = overridden, ChannelEntries = channelEntries };
    }

    /// <summary>
    /// The catalog a channel compiles with: the [[cycle]] tables of the machine file that name this channel over the
    /// entries, the table of the compiled channel first, then the channel-less one (machine-config 6, D361, D377).
    /// </summary>
    /// <param name="channel">The channel compiled, 1 for a single-channel machine.</param>
    public CycleCatalog ForChannel(int channel)
    {
        var ofChannel = new List<CycleEntry>();
        foreach (CycleEntry entry in ChannelEntries)
        {
            if (entry.Channel == channel)
            {
                ofChannel.Add(entry with { Channel = null });
            }
        }

        return (this with { ChannelEntries = [] }).Override(ofChannel);
    }

    /// <summary>
    /// The native cycle a CYCLE:controller=n word names, as the family's catalog writes its <c>native</c>: an integer
    /// for Heidenhain (251), an identifier for Siemens and Fanuc (CYCLE952, G71), and the content of a string where
    /// the native is neither (CYCLE:FANUC="G83.5"). FindNative compares this text with the native of an entry (D144).
    /// </summary>
    /// <param name="value">The value of the CYCLE word with its controller address.</param>
    public static string NativeOf(Value value)
    {
        return value is StringValue text ? text.Content : value.ToCanonical();
    }


    /// <summary>
    /// Whether a native cycle written with this controller address, CYCLE:HEIDENHAIN=251, passes through this catalog:
    /// only when the address names the controller family of the catalog (language 4.7.1, D94).
    /// </summary>
    /// <param name="controllerAddress">The address of the CYCLE word, HEIDENHAIN.</param>
    public bool PassesThrough(string controllerAddress)
    {
        // A cycle that exists on one controller only may be written natively and compiles only to that family: its
        // compiler writes native cycle n, the cycle words through the entry of n when the catalog has one
        // (FindNative), and every key the word catalog does not know as a native parameter in source order after the
        // cycle words; a compiler of any other family reports the block as an ERROR like RAW (language 4.7.1, D45,
        // D94). NCX writes the address in capitals (language 3).
        return Controller is { } family && controllerAddress == Enum.GetName(family)?.ToUpperInvariant();
    }

    // An entry fits a source block when it maps the drilling axis asked for, the block carries its fixed values and
    // its params cover the words of the block (machine-config 6, D160).
    private static bool Fits(CycleEntry entry, IReadOnlyDictionary<string, decimal> nativeValues, string? forAxis)
    {
        return (forAxis is null || entry.ForAxis == forAxis)
            && CarriesFixedValues(nativeValues, entry)
            && entry.CoversNativeWords(nativeValues.Keys);
    }

    // D359: an entry without a before key does not count where an entry of its native and its for_axis sets
    // before = "", the machine's G84 being always rigid.
    private bool IsHidden(CycleEntry entry)
    {
        if (entry.Before is not null)
        {
            return false;
        }

        foreach (CycleEntry other in Entries)
        {
            if (other.Native == entry.Native && other.ForAxis == entry.ForAxis && other.Before == "")
            {
                return true;
            }
        }

        return false;
    }

    // An entry fits a source block when the block carries every fixed value of the entry (machine-config 6).
    private static bool CarriesFixedValues(IReadOnlyDictionary<string, decimal> nativeValues, CycleEntry entry)
    {
        foreach (KeyValuePair<string, decimal> fixedValue in entry.Fixed)
        {
            decimal? value = null;
            if (nativeValues.TryGetValue(fixedValue.Key, out decimal given))
            {
                value = given;
            }
            else if (entry.Defaults.TryGetValue(fixedValue.Key, out string? assumed)
                && decimal.TryParse(assumed, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
            {
                value = number;
            }

            if (value != fixedValue.Value)
            {
                return false;
            }
        }

        return true;
    }

    // The place of the entry of a name, or -1 when there is none.
    private static int IndexOfName(List<CycleEntry> entries, string name, string? forAxis)
    {
        for (int index = 0; index < entries.Count; index++)
        {
            // An entry is found by its name together with its for_axis, so that the lathe G83 along Z and the G87
            // along X are two entries of one name and a later file overrides each on its own (machine-config 6, D160).
            if (entries[index].Name == name && entries[index].ForAxis == forAxis)
            {
                return index;
            }
        }

        return -1;
    }
}
