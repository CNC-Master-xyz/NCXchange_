using Ncx.Cli;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// --machine by name or by path: a machine of that name in the machine folder that ncx.toml names, then in machines/ of
/// the working directory, then in machines/ of the tool's own folder; a file at the path given is that file
/// (implementation 12, P2-04; machine-config 10, architecture 10).
/// </summary>
public sealed class ProjectFoldersTests : IDisposable
{
    private readonly ProjectHarness _project = new();
    private readonly string _program;

    public ProjectFoldersTests()
    {
        _program = _project.WriteInWorkingDirectory("part.ncx", CliHarness.OneProgram("UNITS=MM"));
    }

    public void Dispose()
    {
        _project.Dispose();
    }

    // P2-04: --machine <name> is found in machines/ of the working directory.
    [Fact]
    public void MachineByName_InMachinesOfTheWorkingDirectory_IsFound()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", Marked("found_in_work"));

        int exitCode = _project.Check(_program, "mill");

        Assert.True(exitCode == 0, _project.Error);
        Assert.Contains("found_in_work", _project.Error, StringComparison.Ordinal);
    }

    // P2-04, architecture 10: the machine folder that ncx.toml names is searched first.
    [Fact]
    public void MachineByName_MachineFolderOfNcxToml_IsSearchedFirst()
    {
        _project.WriteInWorkingDirectory("ncx.toml", "machines = \"shop\"\n");
        _project.WriteInWorkingDirectory("shop/mill.toml", Marked("found_in_shop"));
        _project.WriteInWorkingDirectory("machines/mill.toml", Marked("found_in_work"));
        _project.WriteInToolFolder("machines/mill.toml", Marked("found_in_tool"));

        int exitCode = _project.Check(_program, "mill");

        Assert.True(exitCode == 0, _project.Error);
        Assert.Contains("found_in_shop", _project.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("found_in_work", _project.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("found_in_tool", _project.Error, StringComparison.Ordinal);
    }

    // P2-04: machines/ of the working directory comes before machines/ of the tool's own folder.
    [Fact]
    public void MachineByName_WorkingDirectory_IsSearchedBeforeTheToolFolder()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", Marked("found_in_work"));
        _project.WriteInToolFolder("machines/mill.toml", Marked("found_in_tool"));

        int exitCode = _project.Check(_program, "mill");

        Assert.True(exitCode == 0, _project.Error);
        Assert.Contains("found_in_work", _project.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("found_in_tool", _project.Error, StringComparison.Ordinal);
    }

    // P2-04: a machine that only the tool's own folder holds, a shipped machine file, is found there and named by its
    // full path (D98).
    [Fact]
    public void MachineByName_OnlyInTheToolFolder_IsFoundAndNamedByItsFullPath()
    {
        _project.WriteInToolFolder("machines/mill.toml", Marked("found_in_tool"));

        int exitCode = _project.Check(_program, "mill");

        Assert.True(exitCode == 0, _project.Error);
        string machineFile = Path.Combine(_project.ToolFolder, "machines", "mill.toml");
        Assert.StartsWith(machineFile + "(5): WARNING CFG002: Unknown key found_in_tool", _project.Error,
            StringComparison.Ordinal);
    }

    // P2-04: a machine found below the working directory is named by its path from there, as the user finds it (D98).
    [Fact]
    public void MachineByName_BelowTheWorkingDirectory_IsNamedFromTheWorkingDirectory()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", Marked("found_in_work"));

        _project.Check(_program, "mill");

        string machineFile = Path.Combine("machines", "mill.toml");
        Assert.StartsWith(machineFile + "(5): WARNING CFG002: Unknown key found_in_work", _project.Error,
            StringComparison.Ordinal);
    }

    // P2-04: --machine nakamura-ntjx names the file nakamura-ntjx.toml; the name with its extension names it as well.
    [Theory]
    [InlineData("mill")]
    [InlineData("mill.toml")]
    public void MachineByName_WithOrWithoutTheExtension_FindsTheMachineFile(string name)
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", Marked("found_in_work"));

        int exitCode = _project.Check(_program, name);

        Assert.True(exitCode == 0, _project.Error);
        Assert.Contains("found_in_work", _project.Error, StringComparison.Ordinal);
    }

    // D97: a name that no machine folder holds is a missing machine file, exit code 2 before the run starts; the
    // diagnostic names the value of --machine and the folders searched.
    [Fact]
    public void MachineByName_InNoFolder_ExitsTwoWithCli200()
    {
        int exitCode = _project.Check(_program, "nowhere");

        Assert.Equal(2, exitCode);
        Assert.StartsWith("nowhere(1): ERROR CLI200: --machine nowhere names no file at that path and no machine file "
            + "nowhere.toml in the machine folders machines, ", _project.Error, StringComparison.Ordinal);
        Assert.Contains(" and no built-in machine file of that name (", _project.Error, StringComparison.Ordinal);
    }

    // D371: a shipped machine that none of the machine folders holds is the copy built into ncx, the fourth and last
    // place of the lookup, and it is named as built-in machines/<name>.toml, never by a path.
    [Fact]
    public void MachineByName_ShippedMachineInNoFolder_IsTheBuiltInCopy()
    {
        var folders = new ProjectFolders(_project.WorkingDirectory, _project.ToolFolder, null);

        int exitCode = _project.Check(_program, "fanuc-mill-30i");
        FoundFile? found = folders.FindMachine("fanuc-mill-30i");

        Assert.True(exitCode == 0, _project.Error);
        Assert.NotNull(found);
        Assert.Equal("built-in machines/fanuc-mill-30i.toml", found.Name);
        Assert.Equal(Ncx.Config.BuiltInFiles.Machine("fanuc-mill-30i.toml"), found.Text);
    }

    // D10, D371: a tool table in a machine folder is no machine file, so its name finds no machine.
    [Fact]
    public void MachineByName_ToolTableOfAMachineFolder_IsNoMachine()
    {
        _project.WriteInWorkingDirectory("machines/mill.tools.toml", "[[tool]]\nnumber = 1\n");
        var folders = new ProjectFolders(_project.WorkingDirectory, _project.ToolFolder, null);

        Assert.Null(folders.FindMachine("mill.tools"));
        Assert.Null(folders.FindMachine("fanuc-mill-30i.tools.toml"));
    }

    // D206, D371: a shop's own file of a shipped name wins over the built-in copy, in the tool's own folder as well.
    [Fact]
    public void MachineByName_ShopFileOfAShippedName_WinsOverTheBuiltInCopy()
    {
        _project.WriteInToolFolder("machines/fanuc-mill-30i.toml", Marked("found_in_tool"));

        int exitCode = _project.Check(_program, "fanuc-mill-30i");

        Assert.True(exitCode == 0, _project.Error);
        Assert.Contains("found_in_tool", _project.Error, StringComparison.Ordinal);
    }

    // P1-07, P2-04: a file at the path given is that file, before a machine of that name in the machine folders.
    [Fact]
    public void MachineByPath_FileAtThePathGiven_WinsOverAMachineOfThatName()
    {
        _project.WriteInWorkingDirectory("mill.toml", Marked("found_at_path"));
        _project.WriteInWorkingDirectory("machines/mill.toml", Marked("found_in_work"));

        int exitCode = _project.Check(_program, "mill.toml");

        Assert.True(exitCode == 0, _project.Error);
        Assert.StartsWith("mill.toml(5): WARNING CFG002: Unknown key found_at_path", _project.Error,
            StringComparison.Ordinal);
    }

    // P1-07: a relative path starts at the working directory and the file is named as the command line names it.
    [Fact]
    public void MachineByPath_RelativePath_StartsAtTheWorkingDirectory()
    {
        _project.WriteInWorkingDirectory("shop/mill.toml", Marked("found_in_shop"));

        int exitCode = _project.Check(_program, "shop/mill.toml");

        Assert.True(exitCode == 0, _project.Error);
        Assert.StartsWith("shop/mill.toml(5): WARNING CFG002: Unknown key found_in_shop", _project.Error,
            StringComparison.Ordinal);
    }

    // P1-07: a path that names no file is a machine file that cannot be read, CLI100 with exit code 2, even when a
    // machine of its file name exists: a value with a folder in it is a path.
    [Fact]
    public void MachineByPath_ThatNamesNoFile_ExitsTwoWithCli100()
    {
        _project.WriteInWorkingDirectory("machines/mill.toml", Marked("found_in_work"));

        int exitCode = _project.Check(_program, "shop/mill.toml");

        Assert.Equal(2, exitCode);
        Assert.StartsWith("shop/mill.toml(1): ERROR CLI100: ", _project.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("found_in_work", _project.Error, StringComparison.Ordinal);
    }

    // The clutch mill with a key in [machine] that machine-config does not know, the WARNING CFG002 on line 5 that
    // names the key, so that the test sees which file was loaded (P2-01).
    private static string Marked(string mark)
    {
        return TestMachines.ClutchMill.Replace("channels = [1]", "channels = [1]\n" + mark + " = true",
            StringComparison.Ordinal);
    }
}
