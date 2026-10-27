using System.Reflection;
using System.Text;

namespace Ncx.Config;

/// <summary>
/// The machine files of machines/ and the cycle catalogs of cycles/ that the repository ships, built into the binary:
/// the fourth and last place of the lookup of a machine by name and of the catalog a machine names, after the three
/// folders, so that a shop's own file of that name still wins and a single-file ncx finds the shipped ones (D206,
/// D207, D371). The tool tables of machines/ are built in as well, for a built-in machine that names one (D10), but
/// are never a machine. The loaders take their text as they take a file's (MachineConfigLoader.LoadText,
/// CycleCatalogLoader.LoadText, ToolTable.LoadText).
/// </summary>
public static class BuiltInFiles
{
    /// <summary>
    /// The folder of the shipped machine files, and the name of their built-in copies.
    /// </summary>
    public const string MachinesFolder = "machines";

    /// <summary>
    /// The folder of the shipped cycle catalogs, and the name of their built-in copies.
    /// </summary>
    public const string CyclesFolder = "cycles";

    /// <summary>
    /// The ending of a tool table of machines/, "fanuc-mill-30i.tools.toml", which is no machine file (D10).
    /// </summary>
    public const string ToolTableSuffix = ".tools.toml";

    // A built-in file is named in a diagnostic as built-in machines/<name>.toml, never by a path (D371, D98).
    private const string BuiltIn = "built-in ";

    // The resources are named <folder>/<file>, as Ncx.Config.csproj embeds them.
    private const char Separator = '/';

    // The assembly the files are built into.
    private static readonly Assembly s_resources = typeof(BuiltInFiles).Assembly;

    /// <summary>
    /// The text of a shipped machine file.
    /// </summary>
    /// <param name="fileName">The file name with its extension: "fanuc-mill-30i.toml".</param>
    /// <returns>The text; null when the repository ships no machine file of that name; a tool table is none.</returns>
    public static string? Machine(string fileName)
    {
        return Machine(s_resources, fileName);
    }

    /// <summary>
    /// The text of a shipped cycle catalog.
    /// </summary>
    /// <param name="fileName">The file name with its extension: "fanuc.toml".</param>
    /// <returns>The text; null when the repository ships no catalog of that name.</returns>
    public static string? Catalog(string fileName)
    {
        return Text(s_resources, CyclesFolder, fileName);
    }

    /// <summary>
    /// The text of a shipped tool table of machines/, which a built-in machine names with [machine] tool_table (D10).
    /// </summary>
    /// <param name="fileName">The file name: "fanuc-mill-30i.tools.toml".</param>
    /// <returns>The text; null when the repository ships no tool table of that name.</returns>
    public static string? ToolTable(string fileName)
    {
        return ToolTable(s_resources, fileName);
    }

    /// <summary>
    /// A built-in file as a diagnostic names it: "built-in machines/fanuc-mill-30i.toml" (D371).
    /// </summary>
    /// <param name="folder">MachinesFolder or CyclesFolder.</param>
    /// <param name="fileName">The file name with its extension.</param>
    public static string NameOf(string folder, string fileName)
    {
        return BuiltIn + folder + Separator + fileName;
    }

    /// <summary>
    /// True for the name of a tool table, which is no machine file (D10).
    /// </summary>
    public static bool IsToolTable(string fileName)
    {
        return fileName.EndsWith(ToolTableSuffix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The names of the built-in files of a folder, in ordinal order; for machines/ the machine files alone.
    /// </summary>
    /// <param name="folder">MachinesFolder or CyclesFolder.</param>
    internal static IReadOnlyList<string> Names(string folder)
    {
        string prefix = folder + Separator;
        var names = new List<string>();
        foreach (string resource in s_resources.GetManifestResourceNames())
        {
            if (resource.StartsWith(prefix, StringComparison.Ordinal))
            {
                string name = resource.Substring(prefix.Length);
                if (folder != MachinesFolder || !IsToolTable(name))
                {
                    names.Add(name);
                }
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// A machine file built into the assembly given, never a tool table.
    /// </summary>
    internal static string? Machine(Assembly assembly, string fileName)
    {
        return IsToolTable(fileName) ? null : Text(assembly, MachinesFolder, fileName);
    }

    /// <summary>
    /// A tool table of machines/ built into the assembly given.
    /// </summary>
    internal static string? ToolTable(Assembly assembly, string fileName)
    {
        return IsToolTable(fileName) ? Text(assembly, MachinesFolder, fileName) : null;
    }

    // The built-in files are the files of the two folders themselves: a name with a folder in it names none of them.
    // The name is compared without regard to case, as the folders are on macOS and Windows.
    private static string? Text(Assembly assembly, string folder, string fileName)
    {
        if (fileName.Contains(Separator, StringComparison.Ordinal) || fileName.Contains('\\', StringComparison.Ordinal))
        {
            return null;
        }

        string wanted = folder + Separator + fileName;
        foreach (string resource in assembly.GetManifestResourceNames())
        {
            if (string.Equals(resource, wanted, StringComparison.OrdinalIgnoreCase)
                && assembly.GetManifestResourceStream(resource) is Stream stream)
            {
                // UTF-8, a byte order mark dropped, as File.ReadAllText reads the file of the repository.
                using (stream)
                using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        return null;
    }
}
