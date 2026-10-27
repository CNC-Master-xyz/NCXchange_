using System.Reflection;
using Ncx.Tests.Fixtures;

namespace Ncx.Config.Tests;

/// <summary>
/// Every machine file and cycle catalog the repository ships is built into the binary, the fourth and last place of
/// the lookup of D206 and D207 (D371).
/// </summary>
public sealed class BuiltInFilesTests
{
    // D371: the built-in copy of each shipped machine file equals the file of the repository, and there is one for
    // every machine file of machines/ and no other; a tool table of machines/ is built in as a tool table, equal too.
    [Fact]
    public void Machines_EveryBuiltInCopy_EqualsTheFileOfTheRepository()
    {
        AssertEqualsRepository(BuiltInFiles.MachinesFolder, BuiltInFiles.Machine);
    }

    // D371: the built-in copy of each shipped cycle catalog equals the file of the repository, and there is one for
    // every file of cycles/ and no other.
    [Fact]
    public void Catalogs_EveryBuiltInCopy_EqualsTheFileOfTheRepository()
    {
        AssertEqualsRepository(BuiltInFiles.CyclesFolder, BuiltInFiles.Catalog);
    }

    // D371: a name that the repository does not ship has no built-in copy, nor has a name with a folder in it, since
    // the built-in files are named as the files of machines/ and cycles/ alone.
    [Theory]
    [InlineData("nowhere.toml")]
    [InlineData("fanuc-mill-30i")]
    [InlineData("shop/fanuc-mill-30i.toml")]
    [InlineData("../machines/fanuc-mill-30i.toml")]
    [InlineData("README.md")]
    [InlineData("fanuc-mill-30i.tools.toml")]
    public void Machine_NameThatIsNoShippedFile_HasNoBuiltInCopy(string fileName)
    {
        Assert.Null(BuiltInFiles.Machine(fileName));
    }

    // D10, D371: a tool table of machines/ is built in, but it is never a machine, and the machines/ listing leaves it
    // out; a built-in machine finds it by the name its tool_table gives.
    [Fact]
    public void ToolTable_BuiltInBesideItsMachine_IsFoundAsToolTableAndNeverAsMachine()
    {
        Assembly assembly = typeof(BuiltInFilesTests).Assembly;

        Assert.NotNull(BuiltInFiles.Machine(assembly, "sample.toml"));
        Assert.Null(BuiltInFiles.Machine(assembly, "sample.tools.toml"));
        Assert.Null(BuiltInFiles.ToolTable(assembly, "sample.toml"));
        Assert.StartsWith("# The tool table of the sample machine",
            BuiltInFiles.ToolTable(assembly, "sample.tools.toml"), StringComparison.Ordinal);
    }

    // D371: a built-in file is found whatever the case of its name, as the folders find a file on macOS and Windows.
    [Fact]
    public void BuiltInFile_NameInAnotherCase_IsFound()
    {
        Assert.Equal(BuiltInFiles.Machine("fanuc-mill-30i.toml"), BuiltInFiles.Machine("FANUC-Mill-30i.toml"));
        Assert.NotNull(BuiltInFiles.ToolTable(typeof(BuiltInFilesTests).Assembly, "Sample.Tools.toml"));
    }

    // D371: a built-in file is named in a diagnostic as built-in machines/<name>.toml, never by a path.
    [Fact]
    public void NameOf_BuiltInMachine_IsBuiltInAndItsFolderAndFile()
    {
        Assert.Equal("built-in machines/fanuc-mill-30i.toml",
            BuiltInFiles.NameOf(BuiltInFiles.MachinesFolder, "fanuc-mill-30i.toml"));
    }

    // The files of a folder of the repository, read as the loaders read them, each against its built-in copy.
    private static void AssertEqualsRepository(string folder, Func<string, string?> builtIn)
    {
        string path = Path.Combine(Fixture.RepositoryRoot(), folder);
        var shipped = new List<string>();
        foreach (string file in Directory.GetFiles(path, "*.toml"))
        {
            string fileName = Path.GetFileName(file);
            if (folder == BuiltInFiles.MachinesFolder && BuiltInFiles.IsToolTable(fileName))
            {
                Assert.Equal(File.ReadAllText(file), BuiltInFiles.ToolTable(fileName));
                continue;
            }

            shipped.Add(fileName);
            Assert.Equal(File.ReadAllText(file), builtIn(fileName));
        }

        shipped.Sort(StringComparer.Ordinal);
        Assert.NotEmpty(shipped);
        Assert.Equal(shipped, BuiltInFiles.Names(folder));
    }
}
