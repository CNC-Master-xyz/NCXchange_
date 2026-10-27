namespace Ncx.Core.Model;

/// <summary>
/// A comment-only or blank line. It is not a block: it is kept in place and written back as read, and it may stand
/// on any line, also before FILE=BEGIN and after FILE=END (language 3, Block and Comment; D92). It records the index
/// of the block it stands before, which places it; its line stays as data (D142).
/// </summary>
/// <param name="Line">The 1-based line number in the file; for a program a reader built, the line of the block begun
/// last, 0 before the first.</param>
/// <param name="Text">The line as read, without its line ending; empty or whitespace for a blank line.</param>
/// <param name="BlockIndex">The index in NcxProgram.Blocks of the block the line stands before; the number of blocks
/// for a line after the last block, after FILE=END (D142).</param>
public sealed record Trivia(int Line, string Text, int BlockIndex);
