using Ncx.Core.Model;

namespace Ncx.Config;

/// <summary>
/// Loads ncx.toml, the settings of the working directory (machine-config 10, architecture 10). The file is walked key
/// by key like the machine file, so that an unknown key is a WARNING on its line that names the nearest known key and
/// a wrong type an ERROR on its line (P2-01).
/// </summary>
public static class ProjectSettingsLoader
{
    // Machine-config 10 describes the project layout and ncx.toml.
    private const string Section = "machine-config 10";

    // The keys of ncx.toml: the machine file --machine defaults to, the machine and cycle folders, the output folder
    // and the plugin assemblies (architecture 10, machine-config 10).
    private static readonly string[] s_keys = ["machine", "machines", "cycles", "out", "plugins"];

    // The key of [plugins] that lists the plugin assemblies, which no plugin may take as its name (D238).
    private const string AssembliesKey = "assemblies";

    /// <summary>
    /// Loads the ncx.toml at a path. A file that cannot be read is an I/O error, thrown for the composition root, which
    /// reports it with the file name (code-guidelines 6).
    /// </summary>
    /// <param name="path">The ncx.toml of the working directory.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The settings, or null when the file has an ERROR.</returns>
    public static ProjectSettings? Load(string path, Diagnostics diagnostics)
    {
        return LoadText(File.ReadAllText(path), diagnostics);
    }

    /// <summary>
    /// Loads ncx.toml from its text: an unknown key is a WARNING, a wrong type an ERROR, each on its line (P2-01).
    /// </summary>
    /// <param name="text">The TOML text of ncx.toml.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The settings, or null when the file has an ERROR.</returns>
    public static ProjectSettings? LoadText(string text, Diagnostics diagnostics)
    {
        int errorsBefore = TomlDocument.ErrorCount(diagnostics);
        TomlDocument? document = TomlDocument.Parse(text, diagnostics);
        if (document is null)
        {
            return null;
        }

        // ncx.toml names the machine file that --machine defaults to, the machine and cycle folders, the output folder
        // and the plugin assemblies (architecture 10); any other key is a WARNING that names the nearest one, as in the
        // machine file (P2-01). A folder left out is the one of the project layout (machine-config 10).
        ConfigTable root = document.Root(Section, ProjectSettings.FileName);
        root.WarnUnknownKeys(s_keys);
        ConfigTable? plugins = PluginsTable(root);
        var settings = new ProjectSettings
        {
            Machine = root.Text("machine"),
            MachineLine = root.LineOf("machine"),
            Machines = root.Text("machines") ?? ProjectSettings.MachinesFolder,
            Cycles = root.Text("cycles") ?? ProjectSettings.CyclesFolder,
            Out = root.Text("out") ?? ProjectSettings.OutFolder,
            Plugins = plugins?.TextList(AssembliesKey) ?? [],
            PluginSettings = ReadPluginSettings(plugins),
        };

        return TomlDocument.ErrorCount(diagnostics) > errorsBefore ? null : settings;
    }

    // [plugins]: the plugin assemblies, assemblies = ["MyShop.NcxPlugins.dll"], and the section of each plugin below it
    // (D238). A top-level list plugins = [...] is the wrong type; null without the table.
    private static ConfigTable? PluginsTable(ConfigTable root)
    {
        return root.Table("plugins", "[plugins]", null,
            "the table [plugins] with assemblies = [\"MyShop.NcxPlugins.dll\"] (D238)");
    }

    // [plugins.<name>]: the settings of one plugin, a string dictionary that the plugin reads through its context
    // (D80); assemblies is the list of [plugins], never a plugin (D238).
    private static Dictionary<string, IReadOnlyDictionary<string, string>> ReadPluginSettings(ConfigTable? plugins)
    {
        var settings = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        if (plugins is null)
        {
            return settings;
        }

        foreach (string name in plugins.Keys)
        {
            if (name != AssembliesKey && plugins.Table(name, "[plugins." + name + "]") is ConfigTable plugin)
            {
                settings[name] = plugin.Strings(null);
            }
        }

        return settings;
    }
}
