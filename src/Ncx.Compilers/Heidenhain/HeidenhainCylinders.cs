using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The CYLINDER sections of a compile that cycle 27 writes (D394): the blocks they cover, which write nothing of their
/// own, and the contour subprograms of each program, which stand after its M30 (HeidenhainCylinder).
/// </summary>
internal sealed class HeidenhainCylinders
{
    /// <summary>
    /// The blocks after a CYLINDER=r block up to its CYLINDER=OFF.
    /// </summary>
    public HashSet<Block> Covered { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The lines of the contour subprograms by program, LBL n .. LBL 0.
    /// </summary>
    public Dictionary<Section, List<string>> Contours { get; } = [];

    /// <summary>
    /// Adds a contour subprogram to a program.
    /// </summary>
    public void Add(Section program, List<string> lines)
    {
        if (Contours.TryGetValue(program, out List<string>? held))
        {
            held.AddRange(lines);
        }
        else
        {
            Contours.Add(program, lines);
        }
    }
}
