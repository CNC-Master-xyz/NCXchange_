using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// One label the Heidenhain compiler numbers (D271): its key, the NCX value it carries (null for the labels the
/// compiler adds, before the M30 of JUMP=END and before the LBL 0 of a RETURN, D272), the block it is written for, and
/// the program whose own numbering it takes; null for the labels of the subprograms, numbered once for the compile.
/// </summary>
/// <param name="Key">The key of the label in HeidenhainLabelNumbers.</param>
/// <param name="Value">The value of the LABEL or SUB NAME word; null for a label the compiler adds.</param>
/// <param name="Block">The block it is written for.</param>
/// <param name="Program">The program whose numbering it takes; null for a subprogram's.</param>
internal sealed record HeidenhainLabelKey(string Key, Value? Value, Block Block, Section? Program);
