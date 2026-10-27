using Ncx.Cli.Commands;

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

    private void Write(params string[] names)
    {
        foreach (string name in names)
        {
            File.WriteAllText(Path.Combine(_folder.FullName, name), "");
        }
    }
}
