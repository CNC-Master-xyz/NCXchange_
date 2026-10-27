namespace Ncx.Tests.Fixtures;

/// <summary>
/// The text of a machine file with tables a test adds: a table the file already has takes the added keys, an added key
/// replacing the file's key of the same name, because TOML defines a table once; any other table, and every entry of an
/// array of tables, is appended. A test
/// that gives a mill of machines/ its own [transform] keeps the path mode templates of the file (D349).
/// </summary>
internal static class MachineText
{
    /// <summary>
    /// The machine file text with the added tables merged into it.
    /// </summary>
    /// <param name="toml">The text of the machine file.</param>
    /// <param name="tables">TOML tables, each a header line followed by its keys.</param>
    public static string With(string toml, string tables)
    {
        var lines = new List<string>(toml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'));
        var appended = new List<string>();
        foreach (List<string> table in Tables(tables))
        {
            // An array of tables, [[axis]], adds one more entry and is appended.
            int header = table[0].StartsWith("[[", StringComparison.Ordinal) ? -1 : HeaderLine(lines, Header(table[0]));
            if (header < 0)
            {
                appended.AddRange(table);
                continue;
            }

            int end = header + 1;
            while (end < lines.Count && !lines[end].StartsWith('['))
            {
                end++;
            }

            // The added keys replace the keys of the same name; the rest of the table stays.
            var added = table.GetRange(1, table.Count - 1);
            for (int index = end - 1; index > header; index--)
            {
                if (Key(lines[index]) is string key && added.Exists(line => Key(line) == key))
                {
                    lines.RemoveAt(index);
                    end--;
                }
            }

            while (end > header + 1 && lines[end - 1].Trim().Length == 0)
            {
                end--;
            }

            lines.InsertRange(end, added.FindAll(line => line.Trim().Length > 0));
        }

        return string.Join('\n', lines) + "\n" + string.Join('\n', appended) + "\n";
    }

    // The tables of the added text, each from its header line to the next.
    private static List<List<string>> Tables(string tables)
    {
        var result = new List<List<string>>();
        foreach (string line in tables.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.StartsWith('['))
            {
                result.Add([line]);
            }
            else if (result.Count > 0)
            {
                result[^1].Add(line);
            }
        }

        return result;
    }

    // The line of a table header in the file, the comment after it ignored; -1 when the file has no such table.
    private static int HeaderLine(List<string> lines, string header)
    {
        return lines.FindIndex(line => line.StartsWith('[') && Header(line) == header);
    }

    // A header without its comment: "[transform]".
    private static string Header(string line)
    {
        int end = line.IndexOf(']', StringComparison.Ordinal);
        return end < 0 ? line.Trim() : line.Substring(0, end + 1);
    }

    // The key of a line, "TILT_ON" of TILT_ON = "...", null for a comment or a blank line.
    private static string? Key(string line)
    {
        string trimmed = line.Trim();
        int equals = trimmed.IndexOf('=', StringComparison.Ordinal);
        bool noKey = trimmed.Length == 0 || trimmed.StartsWith('#') || equals < 0;
        return noKey ? null : trimmed.Substring(0, equals).Trim();
    }
}
