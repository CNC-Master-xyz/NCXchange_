namespace Ncx.Compilers.Siemens;

/// <summary>
/// The parts of a RAW PROC line that the compiler compares with its own PROC line: the name of the subprogram and the
/// text between the parentheses of its parameters, null without them (840D sl NC-Programmierung SW4.92, p.510-512).
/// </summary>
internal sealed record ProcLineParts(string Name, string? Parameters);
