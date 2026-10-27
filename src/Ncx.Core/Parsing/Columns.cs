namespace Ncx.Core.Parsing;

/// <summary>
/// The columns of a line: a column is one Unicode code point of the line, first is column 1 (D166; language 5
/// rule 7). The lexer reports its columns in code points, and the canonical writer counts them so.
/// </summary>
internal static class Columns
{
    /// <summary>
    /// The number of code points of a text.
    /// </summary>
    /// <param name="text">The text, a line or a part of one.</param>
    public static int CodePoints(string text)
    {
        return CodePoints(text, text.Length);
    }

    /// <summary>
    /// The number of code points before a string index of a text.
    /// </summary>
    /// <param name="text">The text, a line or a part of one.</param>
    /// <param name="index">The string index; a surrogate pair before it counts once.</param>
    public static int CodePoints(string text, int index)
    {
        // The low half of a surrogate pair belongs to the code point its high half starts.
        int count = 0;
        for (int position = 0; position < index; position++)
        {
            bool closesAPair = position > 0 && char.IsLowSurrogate(text[position])
                && char.IsHighSurrogate(text[position - 1]);
            if (!closesAPair)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// The 1-based column of a string index of a line.
    /// </summary>
    /// <param name="text">The line.</param>
    /// <param name="index">The string index.</param>
    public static int Of(string text, int index)
    {
        return CodePoints(text, index) + 1;
    }
}
