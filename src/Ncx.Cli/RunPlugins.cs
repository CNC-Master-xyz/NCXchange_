using Ncx.Config;
using Ncx.Core.Model;
using Ncx.Plugins;

namespace Ncx.Cli;

/// <summary>
/// The plugins of a run (architecture 9, 10; D80, D106; implementation 17, P7-01): the assemblies [plugins] assemblies
/// of ncx.toml names (D238) and every DLL of plugins/ of the working directory, each with its own
/// [plugins.&lt;name&gt;] settings, loaded once the inputs of the run are read. What the plugins report goes into the
/// diagnostics of the run, named after the plugin; a plugin that fails is left out, and the run goes on without it.
/// </summary>
internal static class RunPlugins
{
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> s_noSettings = [];

    /// <summary>
    /// Loads the plugins of the working directory of a run.
    /// </summary>
    /// <param name="settings">The file and the working directory of the run.</param>
    /// <param name="project">ncx.toml of the working directory; null without one.</param>
    /// <param name="diagnostics">The diagnostics of the run, whose file is the file the plugins see.</param>
    public static PluginSet Load(RunSettings settings, ProjectSettings? project, Diagnostics diagnostics)
    {
        return Load(settings, project, new PluginDiagnostics(diagnostics));
    }

    /// <summary>
    /// Loads the plugins of the working directory of a run that hands what they report to the file it works on, the
    /// batch (D284).
    /// </summary>
    /// <param name="settings">The file and the working directory of the run.</param>
    /// <param name="project">ncx.toml of the working directory; null without one.</param>
    /// <param name="diagnostics">Where the plugins report, until the run points it at another file.</param>
    public static PluginSet Load(RunSettings settings, ProjectSettings? project, PluginDiagnostics diagnostics)
    {
        // [plugins] assemblies of ncx.toml names the listed DLLs, which load first, and each [plugins.<name>] section
        // holds the settings of one plugin (D238, D80); the DLLs of plugins/ follow (D317).
        IReadOnlyList<string> files = PluginLoader.Files(settings.WorkingDirectory, project?.Plugins ?? []);
        return PluginLoader.Load(settings.WorkingDirectory, files, project?.PluginSettings ?? s_noSettings,
            diagnostics);
    }
}
