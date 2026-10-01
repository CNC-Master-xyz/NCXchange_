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
                return entry;
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
                return entry;
            }

            defaultAxis ??= entry.ForAxis is null ? entry : null;
        }

        return defaultAxis;
    }

    /// <summary>
    /// The first entry of a native cycle in catalog order, "G81", "200", "CYCLE81"; null when the catalog has none.
    /// </summary>
    /// <param name="native">The native cycle as the catalog writes it.</param>
    public CycleEntry? FindNative(string native)
    {
        // A reader turns a native cycle into the entry that maps it; of two entries of one cycle, DRILL and
        // DRILL_DWELL on cycle 200, the first in catalog order (controller-mapping 5).
        foreach (CycleEntry entry in Entries)
        {
            if (entry.Native == native)
            {
                return entry;
            }
        }

        return null;
    }

    // TODO(question): a Siemens position without a value takes the default of the cycle (siemens 7), and that default
    // is not in the documents (VARI of CYCLE83); a block that leaves out a fixed value fits no entry that fixes it
    // (D181).

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
            if (entry.Native == native
                && (forAxis is null || entry.ForAxis == forAxis)
                && CarriesFixedValues(nativeValues, entry)
                && entry.CoversNativeWords(nativeValues.Keys))
            {
                return entry;
            }
        }

        return null;
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
        foreach (CycleEntry entry in entries)
        {
            int index = IndexOfName(overridden, entry.Name, entry.ForAxis);
            if (index < 0)
            {
                // D160: the entries of one name differ only by the drilling axis, so an entry added under a for_axis
                // carries the same words of a reader or compiler rule as the entry of that name already in the
                // catalog, which only the built-in family can name (D157).
                // TODO(question): neither D160 nor D157 says where the rule words of a for_axis entry come from, and a
                // catalog file cannot name them; the entry of the same name without a for_axis lends them, so that
                // SURFACE, SAFE, CYCLE_RETRACT and AXIS stand on the X entry of a lathe as on its Z entry.
                overridden.Add(entry.RuleWords.Count > 0 || Find(entry.Name) is not CycleEntry sibling
                    ? entry
                    : entry with { RuleWords = sibling.RuleWords });
            }
            else
            {
                overridden[index] = overridden[index].OverriddenBy(entry);
            }
        }

        return new CycleCatalog { Controller = Controller, Entries = overridden };
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

    // An entry fits a source block when the block carries every fixed value of the entry (machine-config 6).
    private static bool CarriesFixedValues(IReadOnlyDictionary<string, decimal> nativeValues, CycleEntry entry)
    {
        foreach (KeyValuePair<string, decimal> fixedValue in entry.Fixed)
        {
            if (!nativeValues.TryGetValue(fixedValue.Key, out decimal value) || value != fixedValue.Value)
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
