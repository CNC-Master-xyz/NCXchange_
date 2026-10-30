using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Cycles;

// Machine-config 6 and 5a: the [[cycle]] entries of a catalog file or of a machine file, each as the file writes it.
public static partial class CycleCatalogLoader
{
    // The keys of a catalog entry (machine-config 6) and the four keys of its expansion rule (machine-config 5a).
    private static readonly string[] s_entryKeys =
    [
        "name", "native", "for_axis", "params", "absolute_from_surface", "modal", "contour", "signature",
        "fixed", "before", "defaults", "title", "dwell_native", "channel", "available", "first_block",
        "pre", "post", "requires", "restore",
    ];

    // The keys only a [[cycle]] table of a machine file takes (D361, D377).
    // D377, D410: channel describes one machine; available is a key of both files, since a catalog file may withdraw
    // an entry it inherits.
    private static readonly string[] s_machineOnlyKeys = ["channel"];

    // The word that the words of absolute_from_surface are relative to (machine-config 6, language 4.7).
    private const string SurfaceWord = "SURFACE";

    /// <summary>
    /// Reads the [[cycle]] entries of a catalog file or of a machine file in file order, each as the file writes it,
    /// with the keys it wrote (machine-config 6). A name written twice is an ERROR on the second entry.
    /// </summary>
    /// <param name="tables">The [[cycle]] tables of the file.</param>
    /// <param name="controller">The controller family the entries belong to; null when the machine file names
    /// none.</param>
    /// <param name="catalogFile">True for the entries of a catalog file, which take no channel key (D377); available
    /// withdraws an entry there too (D410).</param>
    internal static IReadOnlyList<CycleEntry> ReadEntries(
        IReadOnlyList<ConfigTable> tables, Controller? controller, bool catalogFile = false)
    {
        var entries = new List<CycleEntry>();
        var names = new List<string>();
        CycleCatalog family = controller is Controller known ? DrillingFamily.Catalog(known) : new CycleCatalog();
        foreach (ConfigTable table in tables)
        {
            CycleEntry? entry = ReadEntry(table, controller, catalogFile);
            if (entry is null)
            {
                continue;
            }

            // The catalog finds an entry by its name together with its for_axis, so one file names each cycle once
            // per drilling axis (machine-config 6, D160).
            string key = entry.Name + " " + entry.ForAxis + " " + entry.Channel;
            if (names.Contains(key))
            {
                string axis = entry.ForAxis is null ? "" : $" for the axis {entry.ForAxis}";
                table.Diagnostics.Error(table.Line, DiagnosticCodes.CycleNameTwice,
                    $"{table.Name} names the cycle {entry.Name}{axis} a second time in this file; the catalog finds "
                    + "an entry by its name and its for_axis (machine-config 6, D160).");
                continue;
            }

            names.Add(key);
            CheckAbsoluteFromSurface(table, entry, family.Find(entry.Name));
            entries.Add(entry);
        }

        return entries;
    }

    // One entry: name and native are required, every other key is optional, and pre, post, requires and restore make
    // its expansion rule (machine-config 6, 5a). The entry keeps the keys the file wrote, by which it overrides the
    // entry of the same name before it.
    private static CycleEntry? ReadEntry(ConfigTable table, Controller? controller, bool catalogFile)
    {
        table.WarnUnknownKeys(s_entryKeys);
        string? name = table.RequiredText("name");
        string? native = ReadNative(table);
        if (name is not null)
        {
            CheckName(table, name);
        }

        string? forAxis = ReadForAxis(table);
        IReadOnlyDictionary<string, string> parameters = ReadParams(table);
        IReadOnlyList<string> absoluteFromSurface = table.TextList("absolute_from_surface");
        bool modal = table.Flag("modal");
        IReadOnlyList<string> contour = ReadContour(table);
        IReadOnlyList<string> firstBlock = ReadFirstBlock(table, parameters);
        IReadOnlyList<string> signature = table.TextList("signature");
        IReadOnlyDictionary<string, decimal> fixedValues =
            table.Table("fixed")?.Numbers() ?? new Dictionary<string, decimal>();
        IReadOnlyDictionary<string, string> defaults = ReadDefaults(table);
        ExpansionRule? rule = MachineConfigLoader.ReadRule(table);
        CheckFamilyKeys(table, controller, modal, signature);
        int? channel = catalogFile ? null : table.Integer("channel");
        bool available = !table.Has("available") || table.Flag("available");
        if (catalogFile)
        {
            WarnMachineKeys(table);
        }

        if (name is null || native is null)
        {
            return null;
        }

        return new CycleEntry
        {
            Name = name,
            Native = native,
            ForAxis = forAxis,
            Params = parameters,
            AbsoluteFromSurface = absoluteFromSurface,
            Modal = modal,
            Contour = contour,
            FirstBlock = firstBlock,
            Signature = signature,
            Fixed = fixedValues,
            Defaults = defaults,
            Title = table.Text("title"),
            DwellNative = table.Text("dwell_native"),
            Channel = channel,
            Available = available,
            Before = table.Has("before") ? table.Template("before") ?? "" : null,
            Rule = rule,
            Family = controller,
            WrittenKeys = WrittenKeys(table, catalogFile),
        };
    }

    // native: the G code of a Fanuc cycle, the number of a Heidenhain CYCL DEF, the name of a Siemens cycle; a number
    // is kept as its text, the text a reader compares (machine-config 6). Every entry names it, the entries of
    // machine-config 5a and 6 included.
    private static string? ReadNative(ConfigTable table)
    {
        if (!table.Has("native"))
        {
            table.MissingKey("native");
            return null;
        }

        return table.PlaceholderValue("native");
    }

    // for_axis: the drilling axis the entry maps its cycle under, the X of the Fanuc lathe G87 to G89; the entry
    // without it maps the default axis (machine-config 6, D160). It is an axis name as a program writes AXIS
    // (language 4.7).
    private static string? ReadForAxis(ConfigTable table)
    {
        string? axis = table.Text("for_axis");
        if (axis is not null && !IsNcxWord(axis))
        {
            table.Diagnostics.Error(table.LineOf("for_axis"), DiagnosticCodes.CycleWordMalformed,
                $"The for_axis {axis} in {table.Name} is not an axis name, a capital followed by capitals, digits "
                + "and underscores; no cycle block can name it with AXIS (language 4.7, machine-config 6, D160).");
            return null;
        }

        return axis;
    }

    // A program names the cycle as the value of CYCLE, so the name is an NCX identifier (language 3, 4.7.1).
    private static void CheckName(ConfigTable table, string name)
    {
        if (!IsNcxWord(name))
        {
            table.Diagnostics.Error(table.LineOf("name"), DiagnosticCodes.CycleWordMalformed,
                $"The cycle name {name} in {table.Name} is not an NCX identifier, a capital followed by capitals, "
                + "digits and underscores; no program can name it (language 3, 4.7.1).");
        }
    }

    // params: every key the NCX word of a parameter, every value the native name that carries it; a program writes
    // the words on the cycle block, so each is an NCX key (machine-config 6, language 3).
    private static IReadOnlyDictionary<string, string> ReadParams(ConfigTable table)
    {
        ConfigTable? parameters = table.Table("params");
        if (parameters is null)
        {
            return new Dictionary<string, string>();
        }

        foreach (string word in parameters.Keys)
        {
            if (!IsNcxWord(word))
            {
                table.Diagnostics.Error(parameters.LineOf(word), DiagnosticCodes.CycleWordMalformed,
                    $"The parameter word {word} in {table.Name} is not an NCX key, a capital followed by capitals, "
                    + "digits and underscores; no cycle block can carry it (language 3, machine-config 6).");
            }
            else if (WordCatalog.Lookup(word) is null && WordCatalog.TryMachineAxis(word, out _))
            {
                // D114: a key of the machine-axis form stays a machine axis word of D93 in every block, so a catalog
                // parameter of that form would read differently with and without a machine file.
                table.Diagnostics.Error(parameters.LineOf(word), DiagnosticCodes.CycleParameterAxisForm,
                    $"The parameter word {word} in {table.Name} has the form of a machine axis name, which a program "
                    + "reads as a machine axis word (D93); a catalog parameter cannot take that form (D114).");
            }
        }

        return parameters.Strings(null);
    }

    // defaults: a native name and the value the compiler writes for it, a number or the control's literal such as
    // MAX, kept as its text (D164); a literal stands only on the native side (D115).
    private static Dictionary<string, string> ReadDefaults(ConfigTable table)
    {
        var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
        if (table.Table("defaults") is not ConfigTable values)
        {
            return defaults;
        }

        foreach (string native in values.Keys)
        {
            switch (values.NumberOrString(native))
            {
                case StringValue text:
                    defaults[native] = text.Content;
                    break;
                case Value number:
                    defaults[native] = number.ToCanonical();
                    break;
            }
        }

        return defaults;
    }

    // contour: two words are the first and the last block of the contour range, one names the contour subprogram
    // (machine-config 6, D65); more cannot be read.
    private static IReadOnlyList<string> ReadContour(ConfigTable table)
    {
        IReadOnlyList<string> contour = table.TextList("contour");
        if (contour.Count > 2)
        {
            table.Diagnostics.Error(table.LineOf("contour"), DiagnosticCodes.CycleContourWordCount,
                $"contour in {table.Name} lists {contour.Count} words; two are the first and the last block of the "
                + "contour, one names the contour subprogram (machine-config 6).");
        }

        return contour;
    }

    // D409: first_block names the params of a two-block Fanuc cycle that stand in its first block, so each must be a
    // params word of the entry.
    private static IReadOnlyList<string> ReadFirstBlock(ConfigTable table,
        IReadOnlyDictionary<string, string> parameters)
    {
        IReadOnlyList<string> firstBlock = table.TextList("first_block");
        foreach (string word in firstBlock)
        {
            if (!parameters.ContainsKey(word))
            {
                table.Diagnostics.Error(table.LineOf("first_block"), DiagnosticCodes.CycleFirstBlockNotAParameter,
                    $"first_block in {table.Name} names {word}, which is no params word of the entry; the first block "
                    + "of a two-block cycle carries params of the entry only (machine-config 6, D409).");
            }
        }

        return firstBlock;
    }

    // Heidenhain and Siemens entries leave modal out, their calls are words of their own (CYCL CALL, M99, MCALL);
    // Fanuc entries leave the signature out, their parameters are address words of the cycle block (machine-config 6).
    private static void CheckFamilyKeys(
        ConfigTable table, Controller? controller, bool modal, IReadOnlyList<string> signature)
    {
        if (modal && controller is Controller.Heidenhain or Controller.Siemens)
        {
            table.Diagnostics.Warning(table.LineOf("modal"), DiagnosticCodes.CycleKeyOutsideItsFamily,
                $"modal = true in {table.Name}: {controller} entries leave modal out, their calls are words of their "
                + "own (machine-config 6).");
        }

        if (signature.Count > 0 && controller == Controller.Fanuc)
        {
            table.Diagnostics.Warning(table.LineOf("signature"), DiagnosticCodes.CycleKeyOutsideItsFamily,
                $"signature in {table.Name}: Fanuc entries leave the signature out, their parameters are address "
                + "words of the cycle block (machine-config 6).");
        }
    }

    // D377: channel describes one machine, so a catalog file that writes it gets CFG157 and the loader ignores it;
    // available = false withdraws an entry in a catalog file as in a machine file (D410).
    private static void WarnMachineKeys(ConfigTable table)
    {
        foreach (string key in s_machineOnlyKeys)
        {
            if (table.Has(key))
            {
                table.Diagnostics.Warning(table.LineOf(key), DiagnosticCodes.CycleMachineKeyInCatalog,
                    $"{key} in {table.Name} is a key of the [[cycle]] tables of a machine file; a catalog file "
                    + "serves every machine of its family, so it is ignored (machine-config 6, D377).");
            }
        }
    }

    // A word of absolute_from_surface is converted with the surface, NCX = SURFACE + native, so the entry maps the word
    // and SURFACE (machine-config 6). Checked on what the entry holds over the built-in entry of its name; an entry
    // that writes no params and overrides a catalog entry the file cannot see is left to that catalog.
    private static void CheckAbsoluteFromSurface(ConfigTable table, CycleEntry entry, CycleEntry? builtIn)
    {
        bool writesParams = entry.WrittenKeys.Contains("params");
        bool writesAbsolute = entry.WrittenKeys.Contains("absolute_from_surface");
        if ((!writesParams && !writesAbsolute) || (!writesParams && builtIn is null))
        {
            return;
        }

        CycleEntry held = builtIn is null ? entry : builtIn.OverriddenBy(entry);
        int line = table.LineOf(writesAbsolute ? "absolute_from_surface" : "params");
        foreach (string word in held.AbsoluteFromSurface)
        {
            if (held.NativeOf(word) is null)
            {
                table.Diagnostics.Warning(line, DiagnosticCodes.CycleAbsoluteWordUnmapped,
                    $"absolute_from_surface in {table.Name} names {word}, which params does not map; no native value "
                    + "converts to it (machine-config 6).");
            }
        }

        if (held.AbsoluteFromSurface.Count > 0 && held.NativeOf(SurfaceWord) is null)
        {
            table.Diagnostics.Warning(line, DiagnosticCodes.CycleAbsoluteWithoutSurface,
                $"{table.Name} converts from the surface but params maps no SURFACE; NCX = SURFACE + native "
                + "(machine-config 6).");
        }
    }

    // The keys of machine-config 6 and 5a that the file wrote, in file order.
    private static List<string> WrittenKeys(ConfigTable table, bool catalogFile)
    {
        var keys = new List<string>();
        foreach (string key in table.Keys)
        {
            if (s_entryKeys.Contains(key) && !(catalogFile && s_machineOnlyKeys.Contains(key)))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    // A key and an identifier are a capital followed by capitals, digits and underscores (language 3).
    private static bool IsNcxWord(string text)
    {
        if (text.Length == 0 || !char.IsAsciiLetterUpper(text[0]))
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}
