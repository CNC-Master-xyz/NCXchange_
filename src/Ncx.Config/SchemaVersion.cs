using Ncx.Core.Model;

namespace Ncx.Config;

/// <summary>
/// ncx_schema: the format version of the machine-configuration schema that a machine file or a cycle catalog may
/// carry as its first key, with the meaning NCX=1 has in language 4.1; not the release of ncx and not the content of
/// the file. A file without the key is version 1; a version this build does not read is CFG009 (D348).
/// </summary>
internal static class SchemaVersion
{
    /// <summary>
    /// The root key of the version (D348).
    /// </summary>
    public const string Key = "ncx_schema";

    /// <summary>
    /// The one version this build reads. It changes only when the schema breaks, so it stays 1 through release 1.0
    /// (D348).
    /// </summary>
    public const int Supported = 1;

    /// <summary>
    /// Checks ncx_schema of the top of a file: absent or 1 is read; another whole number is the ERROR CFG009 that names
    /// the file and the version, and a value that is no whole number the CFG003 of its type (D348).
    /// </summary>
    /// <param name="root">The top of the file.</param>
    /// <param name="kind">What the file is, "machine file" or "cycle catalog", as the message names it.</param>
    public static void Check(ConfigTable root, string kind)
    {
        int? version = root.Integer(Key);
        if (version is null || version == Supported)
        {
            return;
        }

        string file = root.Diagnostics.File.Length > 0 ? " " + root.Diagnostics.File : "";
        root.Diagnostics.Error(root.LineOf(Key), DiagnosticCodes.UnsupportedSchema,
            $"The {kind}{file} declares ncx_schema = {version}, which this build does not read; it reads version "
            + $"{Supported} ({root.Section}, D348).");
    }
}
