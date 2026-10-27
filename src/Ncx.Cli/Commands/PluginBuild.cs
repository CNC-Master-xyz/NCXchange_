using System.Globalization;
using System.Text;
using Ncx.Config;
using Ncx.Core.Model;
using Ncx.Plugins;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx plugin build [&lt;folder&gt;]: runs dotnet build on a plugin against the Ncx assemblies of the ncx that runs,
/// copies its DLL into plugins/ of the working directory and adds it to [plugins] assemblies of ncx.toml, so that the
/// user copies nothing (code-guidelines 11, step 5; implementation 17, P7-02; D238). A plugin that brings assemblies
/// is not copied: ncx.toml lists the DLL in its build output, where the load context finds what it brings (D330). The
/// DLL and the line of ncx.toml are named on the standard output (D331). Exit code 0; 1 when the build fails or its
/// result cannot be written; 2 when there is no plugin to build or ncx.toml cannot be read (D97).
/// </summary>
internal static class PluginBuild
{
    // A DLL is named after its project: MyShopRules.dll.
    private const string DllExtension = ".dll";

    // The assemblies of ncx, which a run shares with every plugin and which are no assemblies a plugin brings (D106).
    private const string NcxAssemblyPrefix = "Ncx.";

    // The symbols of a DLL, which the plugin loader reads with it when they lie next to it (implementation 17, P7-01).
    private const string SymbolsExtension = ".pdb";

    // A diagnostic about a whole file stands on its first line.
    private const int FileLine = 1;

    // ncx plugin build builds into a folder of the plugin's own below bin/, which a project leaves out of its files.
    private static readonly string s_buildFolder = Path.Combine("bin", "ncx");

    // ncx.toml stays UTF-8 text, with its byte order mark only when it had one (language 3, Encoding).
    private static readonly UTF8Encoding s_utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Builds a plugin into plugins/ and registers it in ncx.toml.
    /// </summary>
    /// <param name="folder">The folder of the plugin; null for the one PluginFolder finds.</param>
    /// <param name="settings">The working directory, the folder of ncx, dotnet and --strict.</param>
    /// <param name="output">The standard output, for the DLL and the line of ncx.toml.</param>
    /// <param name="error">The standard error, for what a failed build wrote and the diagnostics (D98).</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(string? folder, PluginCommandSettings settings, TextWriter output, TextWriter error)
    {
        var diagnostics = new Diagnostics(Program.ToolName);
        PluginProject? project = PluginFolder.Find(folder, settings.WorkingDirectory, "build", diagnostics);
        if (project is null)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.NotStarted;
        }

        // ncx.toml is an input like the others and is read before the build: one that cannot be read decides exit
        // code 2, one that loads with an ERROR stops the run (D97; architecture 10), so that the line can join it
        // afterwards.
        string ncxTomlPath = Path.Combine(settings.WorkingDirectory, ProjectSettings.FileName);
        string? ncxToml = null;
        if (File.Exists(ncxTomlPath))
        {
            ncxToml = InputFile.Read(ncxTomlPath, ProjectSettings.FileName, DiagnosticCodes.ProjectFileUnreadable,
                "ncx.toml of the working directory", "architecture 10, D97", diagnostics);
            if (ncxToml is null)
            {
                error.Write(diagnostics.ToText());
                return ExitCodes.NotStarted;
            }

            if (!Loads(WithoutByteOrderMark(ncxToml), diagnostics))
            {
                error.Write(diagnostics.ToText());
                return ExitCodes.Error;
            }
        }

        // dotnet build of the plugin against the Ncx assemblies of this ncx, which a run shares with the plugin (D106;
        // implementation 17, P7-02, risks), into a folder of its own; no build server of dotnet stays behind.
        string buildFolder = Path.Combine(project.Folder, s_buildFolder);
        DotnetResult build = DotnetProcess.Run(settings.Dotnet,
            ["build", project.File, "--nologo", "--disable-build-servers", "--output", buildFolder,
                "-p:NcxFolder=" + settings.NcxFolder],
            settings.WorkingDirectory);
        string dll = Path.Combine(buildFolder, project.Name + DllExtension);
        if (!Built(build, project, dll, diagnostics, error))
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.Error;
        }

        // D330: a plugin that brings assemblies stays in its build output, beside its .deps.json, which the load
        // context resolves them from (D106); ncx.toml lists it by its path from the working directory. A copy in
        // plugins/ would load a second time without them.
        if (BroughtAssemblies(buildFolder, project.Name).Count > 0)
        {
            string entry = Path.GetRelativePath(settings.WorkingDirectory, dll).Replace('\\', '/');
            RemoveStaleCopy(settings.WorkingDirectory, project.Name, diagnostics);
            output.Write(entry + "\n");
            AddLine(ncxToml, entry, ncxTomlPath, inPluginsFolder: false, output, diagnostics);
            error.Write(diagnostics.ToText());
            return ExitCodes.OfRun(diagnostics, settings.Strict);
        }

        // Code-guidelines 11, step 5: the command puts the DLL into plugins/ of the working directory, where every run
        // loads it (implementation 17, P7-01), and adds it to [plugins] assemblies of ncx.toml (D238).
        string plugin = Path.Combine(PluginLoader.PluginsFolder, project.Name + DllExtension);
        if (CopyIntoPlugins(dll, Path.Combine(settings.WorkingDirectory, plugin), plugin, diagnostics))
        {
            output.Write(plugin + "\n");
            AddLine(ncxToml, project.Name + DllExtension, ncxTomlPath, inPluginsFolder: true, output, diagnostics);
        }

        error.Write(diagnostics.ToText());
        return ExitCodes.OfRun(diagnostics, settings.Strict);
    }

    // A build that dotnet ran to its end and that left the DLL; else what went wrong is reported, and what dotnet
    // wrote goes first, the errors of the compiler among it (implementation 17, P7-02).
    private static bool Built(DotnetResult build, PluginProject project, string dll, Diagnostics diagnostics,
        TextWriter error)
    {
        if (build.Failure is string failure)
        {
            DotnetProcess.ReportNotStarted(failure, diagnostics);
            return false;
        }

        if (build.ExitCode != 0)
        {
            error.Write(build.Output);
            string exitCode = build.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "";
            Report(diagnostics, Severity.Error, project.DisplayFile, DiagnosticCodes.PluginBuildFailed,
                $"dotnet build ended with exit code {exitCode}, so the plugin is not built; what it wrote stands "
                + "above (implementation 17, P7-02).");
            return false;
        }

        if (!File.Exists(dll))
        {
            Report(diagnostics, Severity.Error, project.DisplayFile, DiagnosticCodes.PluginNotBuilt,
                $"dotnet build left no {Path.GetFileName(dll)}, so there is nothing to put into plugins/; the DLL of a "
                + "plugin is named after its project (implementation 17, P7-02).");
            return false;
        }

        return true;
    }

    /// <summary>
    /// The assemblies a plugin brings: the DLLs of its build output other than its own and those of ncx, a package it
    /// references among them (D330, D106).
    /// </summary>
    /// <param name="buildFolder">The build output of the plugin.</param>
    /// <param name="name">The name of the plugin, its DLL without the extension.</param>
    /// <returns>The file names, in ordinal order; empty when the plugin brings none.</returns>
    internal static IReadOnlyList<string> BroughtAssemblies(string buildFolder, string name)
    {
        var brought = new List<string>();
        foreach (string file in Directory.GetFiles(buildFolder, "*" + DllExtension))
        {
            string fileName = Path.GetFileName(file);
            if (fileName != name + DllExtension && !fileName.StartsWith(NcxAssemblyPrefix, StringComparison.Ordinal))
            {
                brought.Add(fileName);
            }
        }

        brought.Sort(StringComparer.Ordinal);
        return brought;
    }

    // A copy of the plugin in plugins/ from a build before it brought assemblies would load without them, ahead of the
    // build output (D317), so it goes, with its symbols, and an INFO names it (D330; gap closed 2026-10-28, delegated).
    // A copy that cannot be removed is an ERROR of the run (D97).
    internal static void RemoveStaleCopy(string workingDirectory, string name, Diagnostics diagnostics)
    {
        string stale = Path.Combine(PluginLoader.PluginsFolder, name + DllExtension);
        string path = Path.Combine(workingDirectory, stale);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
            File.Delete(Path.ChangeExtension(path, SymbolsExtension));
            Report(diagnostics, Severity.Info, stale, DiagnosticCodes.PluginStaleCopyRemoved,
                $"The plugin brings assemblies and now loads from its build output, so its earlier copy {stale} is "
                + "removed (D330).");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Report(diagnostics, Severity.Error, stale, DiagnosticCodes.OutputUnwritable,
                $"The earlier copy of the plugin cannot be removed: {exception.Message.TrimEnd('.')}; it would load "
                + "without the assemblies the plugin brings (architecture 10; D330).");
        }
    }

    // The DLL, and its symbols when the build left them, into plugins/, for a plugin that brings no assemblies (D330);
    // a file that cannot be written is an ERROR of the run, which has started (D97).
    private static bool CopyIntoPlugins(string dll, string target, string plugin, Diagnostics diagnostics)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? PluginLoader.PluginsFolder);
            File.Copy(dll, target, overwrite: true);
            string symbols = Path.ChangeExtension(dll, SymbolsExtension);
            if (File.Exists(symbols))
            {
                File.Copy(symbols, Path.ChangeExtension(target, SymbolsExtension), overwrite: true);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Report(diagnostics, Severity.Error, plugin, DiagnosticCodes.OutputUnwritable,
                $"The DLL of the plugin cannot be written into plugins/: {exception.Message.TrimEnd('.')} "
                + "(architecture 10).");
            return false;
        }
    }

    // Code-guidelines 11, step 5: the command adds the plugin to [plugins] assemblies of ncx.toml (D238). A file the
    // entry cannot join stays as it is: with an INFO when the DLL lies in plugins/, which loads without the entry
    // (implementation 17, P7-01), with a WARNING when the entry is the path of a build output, which nothing else
    // loads (D330).
    internal static void AddLine(string? ncxToml, string dll, string path, bool inPluginsFolder, TextWriter output,
        Diagnostics diagnostics)
    {
        bool hasByteOrderMark = ncxToml is not null
            && ncxToml.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal);
        string byteOrderMark = hasByteOrderMark ? InputFile.ByteOrderMark : "";
        string? text = ncxToml?.Substring(byteOrderMark.Length);
        if (text is not null && NcxTomlPlugins.Lists(text, dll))
        {
            output.Write(ProjectSettings.FileName + ": " + NcxTomlPlugins.KeyLine(text) + "\n");
            return;
        }

        string? changed = NcxTomlPlugins.Add(text, dll);
        if (changed is null && inPluginsFolder)
        {
            Report(diagnostics, Severity.Info, ProjectSettings.FileName, DiagnosticCodes.PluginLineNotAdded,
                $"ncx.toml holds its plugins in a form the line of {dll} cannot join, so it stays as it is; the plugin "
                + "loads from plugins/ without the line (machine-config 10; D238).");
            return;
        }

        if (changed is null)
        {
            Report(diagnostics, Severity.Warning, ProjectSettings.FileName, DiagnosticCodes.PluginPathNotAdded,
                $"ncx.toml holds its plugins in a form the path {dll} cannot join, so it stays as it is and the plugin "
                + "does not load; add the path to [plugins] assemblies yourself (machine-config 10; D238, D330).");
            return;
        }

        try
        {
            File.WriteAllText(path, byteOrderMark + changed, s_utf8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Report(diagnostics, Severity.Error, ProjectSettings.FileName, DiagnosticCodes.OutputUnwritable,
                $"ncx.toml cannot be written: {exception.Message.TrimEnd('.')} (architecture 10).");
            return;
        }

        output.Write(ProjectSettings.FileName + ": " + NcxTomlPlugins.KeyLine(changed) + "\n");
    }

    // ncx.toml as every command reads it: its mistakes are diagnostics of ncx.toml on their lines, and an ERROR stops
    // the run (P2-01, D97).
    private static bool Loads(string text, Diagnostics diagnostics)
    {
        var projectDiagnostics = new Diagnostics(ProjectSettings.FileName);
        ProjectSettings? settings = ProjectSettingsLoader.LoadText(text, projectDiagnostics);
        foreach (Diagnostic diagnostic in projectDiagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        return settings is not null;
    }

    // A diagnostic about a whole file stands on its first line (D98).
    private static void Report(Diagnostics diagnostics, Severity severity, string file, string code, string message)
    {
        diagnostics.Add(new Diagnostic
        {
            Severity = severity,
            File = file,
            Line = FileLine,
            Code = code,
            Message = message,
        });
    }

    // A byte order mark is no part of the text the TOML loader reads; ncx.toml keeps it when it is written (D111).
    private static string WithoutByteOrderMark(string text)
    {
        return text.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal)
            ? text.Substring(InputFile.ByteOrderMark.Length)
            : text;
    }
}
