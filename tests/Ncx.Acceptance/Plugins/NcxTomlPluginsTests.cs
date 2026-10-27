using Ncx.Cli.Commands;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// The line ncx plugin build adds to ncx.toml: the plugin assemblies of [plugins], assemblies =
/// ["MyShop.NcxPlugins.dll"], beside the [plugins.&lt;name&gt;] sections of D80 (D238; machine-config 10;
/// implementation 17, P7-02).
/// </summary>
public sealed class NcxTomlPluginsTests
{
    // D238: without ncx.toml the file is the table [plugins] with the assemblies.
    [Fact]
    public void Add_NoNcxToml_IsThePluginsTableWithTheAssemblies()
    {
        Assert.Equal("[plugins]\nassemblies = [\"CoolantClutch.dll\"]\n",
            NcxTomlPlugins.Add(null, "CoolantClutch.dll"));
    }

    // D238: an ncx.toml without [plugins] gets the table at its end, after a blank line.
    [Fact]
    public void Add_NcxTomlWithoutPlugins_AddsTheTableAtItsEnd()
    {
        string text = "machine = \"dmu50\"\nout = \"nc\"\n";

        Assert.Equal("machine = \"dmu50\"\nout = \"nc\"\n\n[plugins]\nassemblies = [\"CoolantClutch.dll\"]\n",
            NcxTomlPlugins.Add(text, "CoolantClutch.dll"));
    }

    // A file whose last line has no line ending gets one before the new table.
    [Fact]
    public void Add_LastLineWithoutLineEnding_EndsItFirst()
    {
        Assert.Equal("machine = \"dmu50\"\n\n[plugins]\nassemblies = [\"CoolantClutch.dll\"]\n",
            NcxTomlPlugins.Add("machine = \"dmu50\"", "CoolantClutch.dll"));
    }

    // D238: a list on one line gets the assembly at its end, in the form the list has.
    [Fact]
    public void Add_ListOnOneLine_AddsTheAssemblyAtItsEnd()
    {
        string text = "[plugins]\nassemblies = [\"MyShop.NcxPlugins.dll\"]   # the shop\n";

        Assert.Equal("[plugins]\nassemblies = [\"MyShop.NcxPlugins.dll\", \"CoolantClutch.dll\"]   # the shop\n",
            NcxTomlPlugins.Add(text, "CoolantClutch.dll"));
    }

    // An empty list gets the assembly as its only entry.
    [Fact]
    public void Add_EmptyList_GetsTheAssembly()
    {
        Assert.Equal("[plugins]\nassemblies = [\"CoolantClutch.dll\"]\n",
            NcxTomlPlugins.Add("[plugins]\nassemblies = []\n", "CoolantClutch.dll"));
    }

    // D238: a [plugins] table without the key gets it as its first line.
    [Fact]
    public void Add_PluginsTableWithoutAssemblies_AddsTheKeyBelowTheHeader()
    {
        string text = "[plugins]\n# the shop\n\n[plugins.CoolantClutch]\nreason = \"clutch\"\n";

        Assert.Equal(
            "[plugins]\nassemblies = [\"CoolantClutch.dll\"]\n# the shop\n\n[plugins.CoolantClutch]\n"
            + "reason = \"clutch\"\n",
            NcxTomlPlugins.Add(text, "CoolantClutch.dll"));
    }

    // D238: the list stands beside the sections of D80; a file with sections and no [plugins] gets the table in front
    // of its first section.
    [Fact]
    public void Add_SectionOfD80_AddsTheTableInFrontOfIt()
    {
        string text = "machine = \"dmu50\"\n\n[plugins.CoolantClutch]\nreason = \"clutch\"\n";

        Assert.Equal(
            "machine = \"dmu50\"\n\n[plugins]\nassemblies = [\"CoolantClutch.dll\"]\n\n[plugins.CoolantClutch]\n"
            + "reason = \"clutch\"\n",
            NcxTomlPlugins.Add(text, "CoolantClutch.dll"));
    }

    // D330: a plugin that brings assemblies is listed by the path of its build output.
    [Fact]
    public void Add_PathOfABuildOutput_IsListedAsWritten()
    {
        Assert.Equal("[plugins]\nassemblies = [\"MyShopRules/bin/ncx/MyShopRules.dll\"]\n",
            NcxTomlPlugins.Add(null, "MyShopRules/bin/ncx/MyShopRules.dll"));
    }

    // A list over several lines is left as its author wrote it.
    [Fact]
    public void Add_ListOverSeveralLines_LeavesNcxTomlAsItIs()
    {
        string text = "[plugins]\nassemblies = [\n  \"MyShop.NcxPlugins.dll\",\n]\n";

        Assert.Null(NcxTomlPlugins.Add(text, "CoolantClutch.dll"));
    }

    // The new lines end with the line ending of the file.
    [Fact]
    public void Add_FileWithCrLf_KeepsCrLf()
    {
        Assert.Equal("machine = \"dmu50\"\r\n\r\n[plugins]\r\nassemblies = [\"CoolantClutch.dll\"]\r\n",
            NcxTomlPlugins.Add("machine = \"dmu50\"\r\n", "CoolantClutch.dll"));
    }

    // D238: an assembly the list names, by its name or by its path in plugins/, is listed.
    [Theory]
    [InlineData("[plugins]\nassemblies = [\"CoolantClutch.dll\"]\n")]
    [InlineData("[plugins]\nassemblies = [\"plugins/CoolantClutch.dll\"]\n")]
    public void Lists_AssemblyTheListNames_IsListed(string text)
    {
        Assert.True(NcxTomlPlugins.Lists(text, "CoolantClutch.dll"));
    }

    // An assembly the list does not name is not listed, nor in a file without a list.
    [Theory]
    [InlineData("[plugins]\nassemblies = [\"MyShop.NcxPlugins.dll\"]\n")]
    [InlineData("machine = \"dmu50\"\n")]
    public void Lists_AssemblyTheListDoesNotName_IsNotListed(string text)
    {
        Assert.False(NcxTomlPlugins.Lists(text, "CoolantClutch.dll"));
    }

    // D331: build names the line of ncx.toml with its table.
    [Fact]
    public void KeyLine_AssembliesOfThePluginsTable_IsNamedWithTheTable()
    {
        Assert.Equal("[plugins] assemblies = [\"CoolantClutch.dll\"]",
            NcxTomlPlugins.KeyLine("machine = \"dmu50\"\n\n[plugins]\nassemblies = [\"CoolantClutch.dll\"]\n"));
    }
}
