using Ncx.Cli.Commands;
using Ncx.Core.Model;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// What ncx plugin build takes from a plugin's build output (D330): a plugin whose output holds only its own assembly
/// is copied into plugins/; one that brings assemblies is listed in [plugins] assemblies by the path of its output.
/// </summary>
public sealed class PluginBuildOutputTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("ncx-plugin-build-");

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    // D330: the DLL of the plugin, its symbols and the Ncx assemblies, which a run shares with the plugin (D106), are
    // no assemblies the plugin brings.
    [Fact]
    public void BroughtAssemblies_OwnAssemblyAndNcxAssemblies_AreNone()
    {
        Write("MyShopRules.dll", "MyShopRules.pdb", "MyShopRules.deps.json", "Ncx.Core.dll", "Ncx.Plugins.dll");

        Assert.Empty(PluginBuild.BroughtAssemblies(_folder.FullName, "MyShopRules"));
    }

    // D330: a package the plugin references lies next to it in the build output, and the plugin brings it.
    [Fact]
    public void BroughtAssemblies_PackageNextToThePlugin_IsBrought()
    {
        Write("MyShopRules.dll", "MyShopRules.pdb", "CsvHelper.dll", "Ncx.Core.dll");

        Assert.Equal(["CsvHelper.dll"], PluginBuild.BroughtAssemblies(_folder.FullName, "MyShopRules"));
    }

    // D330, D238: the path of a build output that cannot join ncx.toml, a list over several lines, leaves the file as
    // it is with the WARNING CLI560, since the plugin does not load without it.
    [Fact]
    public void AddLine_PathThatCannotJoin_WarnsWithCli560()
    {
        string path = Path.Combine(_folder.FullName, "ncx.toml");
        const string text = "[plugins]\nassemblies = [\n  \"Other.dll\",\n]\n";
        var diagnostics = new Diagnostics("ncx");
        using var output = new StringWriter();

        PluginBuild.AddLine(text, "MyShopRules/bin/ncx/MyShopRules.dll", path, inPluginsFolder: false, output,
            diagnostics);

        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal("CLI560", warning.Code);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.False(File.Exists(path));
        Assert.Equal("", output.ToString());
    }

    // D330: a plugin that now brings assemblies loads from its build output, and the copy an earlier build put into
    // plugins/ goes with its symbols, named by the INFO CLI561 (gap closed 2026-10-28, delegated).
    [Fact]
    public void RemoveStaleCopy_CopyInPlugins_IsRemovedWithAnInfo()
    {
        Directory.CreateDirectory(Path.Combine(_folder.FullName, "plugins"));
        Write(Path.Combine("plugins", "MyShopRules.dll"), Path.Combine("plugins", "MyShopRules.pdb"));
        var diagnostics = new Diagnostics("ncx");

        PluginBuild.RemoveStaleCopy(_folder.FullName, "MyShopRules", diagnostics);

        Assert.Empty(Directory.GetFiles(Path.Combine(_folder.FullName, "plugins")));
        Diagnostic info = Assert.Single(diagnostics.Items);
        Assert.Equal("CLI561", info.Code);
        Assert.Equal(Severity.Info, info.Severity);
    }

    private void Write(params string[] names)
    {
        foreach (string name in names)
        {
            File.WriteAllText(Path.Combine(_folder.FullName, name), "");
        }
    }
}
