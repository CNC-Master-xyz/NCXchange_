using Ncx.Config;
using Ncx.Core.Model;
using Ncx.Plugins;

namespace Ncx.Cli.Commands;

/// <summary>
/// The line of ncx.toml that names the plugin assemblies, assemblies = ["MyShop.NcxPlugins.dll"] in the table [plugins]
/// beside the [plugins.&lt;name&gt;] sections of D80 (D238; machine-config 10), to which ncx plugin build adds a plugin
/// (code-guidelines 11, step 5; implementation 17, P7-02). The text changes at that one line, or gains the table or the
/// key, so that every comment and every other line of the file stays as the user wrote it, and a change is kept only
/// when the file loads with it and lists the plugin.
/// </summary>
internal static class NcxTomlPlugins
{
    // The table of the plugins and its key of the plugin assemblies (D238).
    private const string Table = "[plugins]";
    private const string Key = "assemblies";

    // No character: outside the strings of a line of TOML.
    private const char NoQuote = '\0';

    /// <summary>
    /// Tells whether ncx.toml lists a plugin assembly among its plugin assemblies: a DLL of plugins/ by its name or by
    /// its path in plugins/, any other by its path from the working directory (D238, D317; implementation 17, P7-01).
    /// </summary>
    /// <param name="text">The text of ncx.toml without its byte order mark.</param>
    /// <param name="entry">The entry: CoolantClutch.dll, or MyShopRules/bin/ncx/MyShopRules.dll.</param>
    public static bool Lists(string text, string entry)
    {
        if (Load(text) is not ProjectSettings settings)
        {
            return false;
        }

        string inPluginsFolder = PluginLoader.PluginsFolder + "/" + entry;
        foreach (string listed in settings.Plugins)
        {
            string written = listed.Replace('\\', '/');
            if (written == entry || written == inPluginsFolder)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The text of ncx.toml with a plugin assembly added to the assemblies of [plugins]: the entry at the end of a list
    /// on one line, the key below a [plugins] without it, or the table in front of the first [plugins.&lt;name&gt;]
    /// section, else at the end of the file (D238).
    /// </summary>
    /// <param name="text">The text of ncx.toml without its byte order mark; null when there is no ncx.toml.</param>
    /// <param name="entry">The entry: CoolantClutch.dll, or the path of a build output (D330).</param>
    /// <returns>The new text; null when ncx.toml holds its plugins in a form the entry cannot join.</returns>
    public static string? Add(string? text, string entry)
    {
        string quoted = "\"" + entry + "\"";
        string keyLine = Key + " = [" + quoted + "]";
        if (text is null)
        {
            return Table + "\n" + keyLine + "\n";
        }

        string lineEnding = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        List<string> lines = LinesOf(text);
        int header = HeaderIndex(lines);
        if (header >= 0)
        {
            int key = KeyLineIndex(lines, header);
            if (key < 0)
            {
                lines.Insert(header + 1, keyLine);
            }
            else if (WithEntry(lines[key], quoted) is string withEntry)
            {
                lines[key] = withEntry;
            }
            else
            {
                return null;
            }
        }
        else
        {
            // A table stands after the keys of the file: in front of the first section of a plugin, which it holds,
            // or at the end of the file, after a blank line.
            int firstSection = FirstPluginSectionIndex(lines);
            if (firstSection >= 0)
            {
                lines.InsertRange(firstSection, [Table, keyLine, ""]);
            }
            else
            {
                if (lines[lines.Count - 1].Length == 0)
                {
                    lines.RemoveAt(lines.Count - 1);
                }

                if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0)
                {
                    lines.Add("");
                }

                lines.AddRange([Table, keyLine, ""]);
            }
        }

        string changed = string.Join(lineEnding, lines);
        return Lists(changed, entry) ? changed : null;
    }

    /// <summary>
    /// The line of ncx.toml that names the plugin assemblies, as written, with its table: [plugins] assemblies =
    /// ["CoolantClutch.dll"] (D331).
    /// </summary>
    /// <param name="text">The text of ncx.toml without its byte order mark.</param>
    /// <returns>The line; null for a file without one.</returns>
    public static string? KeyLine(string text)
    {
        List<string> lines = LinesOf(text);
        int header = HeaderIndex(lines);
        int key = header < 0 ? -1 : KeyLineIndex(lines, header);
        return key < 0 ? null : Table + " " + lines[key].Trim();
    }

    // ncx.toml as every command loads it; null when it has an ERROR (P2-01).
    private static ProjectSettings? Load(string text)
    {
        return ProjectSettingsLoader.LoadText(text, new Diagnostics(ProjectSettings.FileName));
    }

    // The lines of a text without their line endings; a text that ends with a line ending ends with an empty line.
    private static List<string> LinesOf(string text)
    {
        var lines = new List<string>();
        foreach (string line in text.Split('\n'))
        {
            lines.Add(line.TrimEnd('\r'));
        }

        return lines;
    }

    // The line of the header [plugins], a comment after it allowed; -1 when there is none.
    private static int HeaderIndex(List<string> lines)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index].Trim();
            if (line.StartsWith(Table, StringComparison.Ordinal)
                && (line.Length == Table.Length || line.Substring(Table.Length).TrimStart().StartsWith('#')))
            {
                return index;
            }
        }

        return -1;
    }

    // The line of the key of the plugin assemblies among the keys of the table [plugins]: assemblies = [...]; -1 when
    // there is none.
    private static int KeyLineIndex(List<string> lines, int header)
    {
        for (int index = header + 1; index < lines.Count; index++)
        {
            string line = lines[index].TrimStart();
            if (line.StartsWith('['))
            {
                break;
            }

            if (line.StartsWith(Key, StringComparison.Ordinal)
                && line.Substring(Key.Length).TrimStart().StartsWith('='))
            {
                return index;
            }
        }

        return -1;
    }

    // The first line that begins the section of a plugin, [plugins.MyShopRules]; -1 when there is none.
    private static int FirstPluginSectionIndex(List<string> lines)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            if (lines[index].TrimStart().StartsWith("[plugins.", StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    // The line of the key with the entry at the end of its list; null when the value is no list that ends on the line.
    private static string? WithEntry(string line, string entry)
    {
        int equals = line.IndexOf('=', StringComparison.Ordinal);
        int open = line.IndexOf('[', equals + 1);
        if (open < 0 || line.Substring(equals + 1, open - equals - 1).Trim().Length > 0)
        {
            return null;
        }

        int close = ClosingBracket(line, open);
        if (close < 0)
        {
            return null;
        }

        string inside = line.Substring(open + 1, close - open - 1).TrimEnd();
        string added = inside.Trim().Length == 0 ? entry : (inside.EndsWith(',') ? " " : ", ") + entry;
        return line.Insert(open + 1 + inside.Length, added);
    }

    // The bracket that closes the list opened at a position, outside the strings of TOML; -1 when the line ends or a
    // comment begins before it.
    private static int ClosingBracket(string line, int open)
    {
        char quote = NoQuote;
        for (int index = open + 1; index < line.Length; index++)
        {
            char character = line[index];
            if (quote != NoQuote)
            {
                if (character == '\\' && quote == '"')
                {
                    index++;
                }
                else if (character == quote)
                {
                    quote = NoQuote;
                }
            }
            else if (character == '"' || character == '\'')
            {
                quote = character;
            }
            else if (character == ']')
            {
                return index;
            }
            else if (character == '#')
            {
                return -1;
            }
        }

        return -1;
    }
}
