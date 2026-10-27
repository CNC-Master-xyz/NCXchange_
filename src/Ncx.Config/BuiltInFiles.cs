using System.Text;

namespace Ncx.Config;

/// <summary>
/// The machine files of machines/ and the cycle catalogs of cycles/ that the repository ships, built into the binary:
/// the fourth and last place of the lookup of a machine by name and of the catalog a machine names, after the three
/// folders, so that a shop's own file of that name still wins and a single-file ncx finds the shipped ones (D206,
/// D207, D371). The loaders take their text as they take a file's (MachineConfigLoader.LoadText,
/// CycleCatalogLoader.LoadText).
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

    // A built-in file is named in a diagnostic as built-in machines/<name>.toml, never by a path (D371, D98).
    private const string BuiltIn = "built-in ";

    // The resources are named <folder>/<file>, as Ncx.Config.csproj embeds them.
    private const char Separator = '/';

    /// <summary>
    /// The text of a shipped machine file.
    /// </summary>
    /// <param name="fileName">The file name with its extension: "fanuc-mill-30i.toml".</param>
    /// <returns>The text; null when the repository ships no machine file of that name.</returns>
    public static string? Machine(string fileName)
    {
        return Text(MachinesFolder, fileName);
    }

    /// <summary>
    /// The text of a shipped cycle catalog.
    /// </summary>
    /// <param name="fileName">The file name with its extension: "fanuc.toml".</param>
    /// <returns>The text; null when the repository ships no catalog of that name.</returns>
    public static string? Catalog(string fileName)
    {
        return Text(CyclesFolder, fileName);
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
    /// The names of the built-in files of a folder, in ordinal order.
    /// </summary>
    /// <param name="folder">MachinesFolder or CyclesFolder.</param>
    public static IReadOnlyList<string> Names(string folder)
    {
        string prefix = folder + Separator;
        var names = new List<string>();
        foreach (string resource in typeof(BuiltInFiles).Assembly.GetManifestResourceNames())
        {
            if (resource.StartsWith(prefix, StringComparison.Ordinal))
            {
                names.Add(resource.Substring(prefix.Length));
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    // The built-in files are the files of the two folders themselves: a name with a folder in it names none of them.
    private static string? Text(string folder, string fileName)
    {
        if (fileName.Contains(Separator, StringComparison.Ordinal) || fileName.Contains('\\', StringComparison.Ordinal))
        {
            return null;
        }

        using Stream? stream = typeof(BuiltInFiles).Assembly.GetManifestResourceStream(folder + Separator + fileName);
        if (stream is null)
        {
            return null;
        }

        // UTF-8, a byte order mark dropped, as File.ReadAllText reads the file of the repository.
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
