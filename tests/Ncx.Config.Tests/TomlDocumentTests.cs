using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// The tables of a TOML file as TOML 1.0 defines them: every [[name]] appends to its array in the order of the file,
/// whatever tables stand between the entries.
/// </summary>
public sealed class TomlDocumentTests
{
    // TOML 1.0, array of tables: an array below a table takes its entries from both runs, [[t.a]] before and after [u].
    [Fact]
    public void Tables_ANestedArrayComingAgainAfterAnotherTable_KeepsBothEntries()
    {
        ConfigTable root = Root("""
            [t]
            [[t.a]]
            n = 1

            [u]
            v = 0

            [[t.a]]
            n = 2
            """);

        IReadOnlyList<ConfigTable> entries = Assert.IsType<ConfigTable>(root.Table("t")).Tables("a");
        Assert.Equal([1, 2], entries.Select(entry => entry.Integer("n")));
    }

    // TOML 1.0, array of tables: [[a.b]] after another table belongs to the last [[a]] entry, and a later [[a]] adds
    // an entry; nothing is lost.
    [Fact]
    public void Tables_AnArrayInsideTheLastEntryComingAgain_JoinsThatEntry()
    {
        ConfigTable root = Root("""
            [[a]]
            n = 1
            [[a.b]]
            m = 1

            [x]
            v = 0

            [[a.b]]
            m = 2

            [[a]]
            n = 2
            """);

        IReadOnlyList<ConfigTable> entries = root.Tables("a");
        Assert.Equal([1, 2], entries.Select(entry => entry.Integer("n")));
        Assert.Equal([1, 2], entries[0].Tables("b").Select(entry => entry.Integer("m")));
        Assert.Empty(entries[1].Tables("b"));
    }

    // P2-01: the keys of an entry that comes after other tables keep their own lines.
    [Fact]
    public void LineOf_AKeyOfALateEntry_IsItsOwnLine()
    {
        ConfigTable root = Root("""
            [[a]]
            n = 1

            [x]
            v = 0

            [[a]]
            n = 2
            """);

        IReadOnlyList<ConfigTable> entries = root.Tables("a");
        Assert.Equal([1, 7], entries.Select(entry => entry.Line));
        Assert.Equal(8, entries[1].LineOf("n"));
    }

    private static ConfigTable Root(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        TomlDocument? document = TomlDocument.Parse(toml, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        return Assert.IsType<TomlDocument>(document).Root("test");
    }
}
