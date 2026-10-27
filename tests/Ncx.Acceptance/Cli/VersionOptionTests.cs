using System.Reflection;
using Ncx.Cli;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// ncx --version and ncx --help, the two options System.CommandLine gives the root command: each prints, runs nothing
/// and exits 0 without a diagnostic (architecture 10; D347). The built-in --version prints the version of the entry
/// assembly, so its tests run ncx as a process of its own, not inside the test host.
/// </summary>
public sealed class VersionOptionTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose()
    {
        _cli.Dispose();
    }

    // D347: --version prints the informational version of ncx, the Version of Directory.Build.props (stamped from the
    // tag by the release job) with the commit the SDK appends after "+", and exits 0 with nothing on standard error.
    [Fact]
    public void Ncx_Version_PrintsTheInformationalVersionAndExitsZero()
    {
        string? version = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        NcxProcess run = NcxProcess.Run("--version");

        Assert.Equal(0, run.ExitCode);
        Assert.NotNull(version);
        Assert.Equal(version + "\n", run.Output.ReplaceLineEndings("\n"));
        Assert.Equal("", run.Error);
    }

    // D347: the part before "+" is the version of the release, which the CAM's CI asserts; the assembly of ncx carries
    // the same number, both from the one Version of Directory.Build.props.
    [Fact]
    public void Ncx_Version_PartBeforeThePlusIsTheVersionOfTheAssembly()
    {
        NcxProcess run = NcxProcess.Run("--version");

        string release = run.Output.Trim().Split('+')[0];
        Version? assembly = typeof(Program).Assembly.GetName().Version;

        Assert.NotNull(assembly);
        Assert.StartsWith(assembly.ToString(3), release, StringComparison.Ordinal);
    }

    // Architecture 10, F28: the root help lists --version and --help beside the commands and exits 0.
    [Fact]
    public void Ncx_Help_ListsVersionAndHelpAndExitsZero()
    {
        int exitCode = _cli.Run("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--version", _cli.Output, StringComparison.Ordinal);
        Assert.Contains("--help", _cli.Output, StringComparison.Ordinal);
        Assert.Equal("", _cli.Error);
    }
}
