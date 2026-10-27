namespace Ncx.Cli;

/// <summary>
/// A file that ncx found for the run, a machine file or a cycle catalog: where it is read, and how the diagnostics name
/// it (D98); for a file built into ncx, its text (D371).
/// </summary>
internal sealed record FoundFile
{
    /// <summary>
    /// The full path the file is read from; for a built-in file, which has no path, its name (D371).
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// The file as the diagnostics name it: a path as the command line or ncx.toml gives it, a file found by name by
    /// its path from the working directory, machines/nakamura-ntjx.toml.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The text of a file built into ncx, named "built-in machines/fanuc-mill-30i.toml" (D371); null for a file of a
    /// folder, which is read from FullPath.
    /// </summary>
    public string? Text { get; init; }
}
