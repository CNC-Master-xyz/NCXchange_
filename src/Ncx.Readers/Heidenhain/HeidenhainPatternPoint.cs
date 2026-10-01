namespace Ncx.Readers.Heidenhain;

/// <summary>
/// A machining position of a PATTERN DEF: its point in the working plane and the workpiece surface in the tool axis
/// the position starts at, which acts in addition to the Q203 of the machining cycle (D254; controllers heidenhain.md
/// 5).
/// </summary>
/// <param name="Point">The point in the working plane.</param>
/// <param name="Surface">The tool-axis coordinate of the position; 0 where the point names none.</param>
internal sealed record HeidenhainPatternPoint(HeidenhainPoint Point, decimal Surface);
