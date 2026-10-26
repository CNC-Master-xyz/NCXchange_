namespace Ncx.Compilers.Siemens;

/// <summary>
/// The parts of a RAW PROC line that the compiler compares with its own PROC line: the name of the subprogram, the
/// text between the parentheses of its parameters, null without them, and the attributes after them, SAVE, SBLOF,
/// DISPLOF (840D sl NC-Programmierung SW4.92, p.510-518). Readable is false where the parentheses do not close, and
/// then no parameters or attributes are known.
/// </summary>
internal sealed record ProcLineParts(string Name, string? Parameters, IReadOnlyList<string> Attributes, bool Readable);
