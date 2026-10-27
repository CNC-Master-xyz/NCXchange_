namespace Ncx.Core.Machine;

/// <summary>
/// [format] helix_feed: along which path the control measures F on an arc that moves an axis outside its plane, a
/// helix (D354). A file that leaves it out takes the value of its family: Fanuc <see cref="Arc"/> (parameter 1403#5 =
/// 0), Heidenhain and Siemens <see cref="Path"/> (<see cref="OutputFormat.WithFamilyDefaults"/>).
/// </summary>
public enum HelixFeed
{
    /// <summary>
    /// helix_feed = "path": F is the feed along the whole path of the block; the compiler writes the NCX feed.
    /// </summary>
    Path,

    /// <summary>
    /// helix_feed = "arc": F is the feed along the arc in the working plane; the compiler writes the F of a helix
    /// scaled by the plane length over the path length, and the reader scales it back.
    /// </summary>
    Arc,
}
