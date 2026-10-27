using Ncx.Tests.Fixtures;

namespace Ncx.Config.Tests;

/// <summary>
/// Every machine file and cycle catalog the repository ships is built into the binary, the fourth and last place of
/// the lookup of D206 and D207 (D371).
/// </summary>
public sealed class BuiltInFilesTests
{
    // D371: the built-in copy of each shipped machine file equals the file of the repository, and there is one for
    // every file of machines/ and no other.
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
    public void Machine_NameThatIsNoShippedFile_HasNoBuiltInCopy(string fileName)
    {
        Assert.Null(BuiltInFiles.Machine(fileName));
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
            shipped.Add(fileName);
            Assert.Equal(File.ReadAllText(file), builtIn(fileName));
        }

        shipped.Sort(StringComparer.Ordinal);
        Assert.NotEmpty(shipped);
        Assert.Equal(shipped, BuiltInFiles.Names(folder));
    }
}
