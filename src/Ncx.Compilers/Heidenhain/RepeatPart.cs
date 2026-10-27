using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The part a REPEAT repeats: the block of its LABEL and the REPEAT block, of one section (language 4.9; D213).
/// </summary>
/// <param name="Label">The LABEL block where the part begins.</param>
/// <param name="Repeat">The REPEAT block where it ends.</param>
internal sealed record RepeatPart(Block Label, Block Repeat);
