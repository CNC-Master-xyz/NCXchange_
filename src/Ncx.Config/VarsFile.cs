using System.Globalization;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Config;

/// <summary>
/// The start values of the variables of a program, &lt;name&gt;.vars.toml: numbers and strings by variable name, the
/// seed of the variable store that analyze starts from (machine-config 8, virtual machine 2.7, 3.6).
/// </summary>
public sealed record VarsFile
{
    /// <summary>
    /// The start value of each variable in file order: Q1 = 10 as an integer, Q2 = 5.5 as a decimal, QS1 = "TEXT" as
    /// a string.
    /// </summary>
    public IReadOnlyDictionary<string, Value> Variables { get; init; } = new Dictionary<string, Value>();

    /// <summary>
    /// Loads the vars file at a path. A file that cannot be read is an I/O error, thrown for the composition root
    /// (code-guidelines 6).
    /// </summary>
    /// <param name="path">The vars file.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The start values, or null when the file has an ERROR.</returns>
    public static VarsFile? Load(string path, Diagnostics diagnostics)
    {
        return LoadText(File.ReadAllText(path), diagnostics);
    }

    /// <summary>
    /// Loads a vars file from its text: every key is a variable and its value a number or a string; anything else is
    /// a wrong type, an ERROR on its line (machine-config 8, P2-01).
    /// </summary>
    /// <param name="text">The TOML text of the vars file.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The start values, or null when the file has an ERROR.</returns>
    public static VarsFile? LoadText(string text, Diagnostics diagnostics)
    {
        int errorsBefore = TomlDocument.ErrorCount(diagnostics);
        TomlDocument? document = TomlDocument.Parse(text, diagnostics);
        if (document is null)
        {
            return null;
        }

        // Every key of the file names a variable; its start value is a number or a string, kept as written
        // (machine-config 8). The key is the name as the program writes it, without $: a bare key, or a quoted key
        // with the index of a SYS_ register as plain whole numbers, "SYS_WEAR_Z[99]", comma-separated for a
        // multi-index, "SYS_X[3,1]" (D120, D145).
        ConfigTable root = document.Root("machine-config 8");
        var variables = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (string name in root.Keys)
        {
            if (KeyOf(name) is not string key)
            {
                diagnostics.Error(root.LineOf(name), DiagnosticCodes.ValueNotAllowed,
                    $"\"{name}\" names no variable as a program writes it: a name such as Q1, or a SYS_ name with the "
                    + "index of its register as plain whole numbers, \"SYS_WEAR_Z[99]\" or \"SYS_X[3,1]\" "
                    + "(machine-config 8, D120).");
                continue;
            }

            if (root.NumberOrString(name) is Value value)
            {
                variables[key] = value;
            }
        }

        if (TomlDocument.ErrorCount(diagnostics) > errorsBefore)
        {
            return null;
        }

        return new VarsFile { Variables = variables };
    }

    // The key under which the variable store looks the start value up: the name, and for a SYS_ register the index
    // as whole numbers without leading zeros; null for a key of another form (D120). The name is an ADDR of
    // language 3, an index stands on a SYS_ name only (D145).
    private static string? KeyOf(string key)
    {
        int bracket = key.IndexOf('[', StringComparison.Ordinal);
        string name = bracket < 0 ? key : key.Substring(0, bracket);
        if (!IsName(name))
        {
            return null;
        }

        if (bracket < 0)
        {
            return name;
        }

        if (!SystemNames.IsSystem(name) || !key.EndsWith(']'))
        {
            return null;
        }

        var indexes = new List<string>();
        foreach (string index in key.Substring(bracket + 1, key.Length - bracket - 2).Split(','))
        {
            if (index.Length == 0 || !index.All(char.IsAsciiDigit)
                || !int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out int register))
            {
                return null;
            }

            indexes.Add(register.ToString(CultureInfo.InvariantCulture));
        }

        return name + "[" + string.Join(',', indexes) + "]";
    }

    // [A-Z][A-Z0-9_]*, an ADDR of language 3.
    private static bool IsName(string name)
    {
        return name.Length > 0 && char.IsAsciiLetterUpper(name[0])
            && name.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character)
                || character == '_');
    }
}
