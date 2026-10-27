using System.Diagnostics;
using Ncx.Cli;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// One run of ncx as a process of its own, Ncx.Cli.dll of the test output started by dotnet, for what only the entry
/// assembly shows: the version that --version prints (D347).
/// </summary>
internal sealed record NcxProcess
{
    // How long a run of ncx may take before the test fails.
    private static readonly TimeSpan s_timeout = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The exit code of the run.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// What the run wrote to the standard output.
    /// </summary>
    public required string Output { get; init; }

    /// <summary>
    /// What the run wrote to the standard error.
    /// </summary>
    public required string Error { get; init; }

    /// <summary>
    /// Runs ncx with the arguments given; dotnet test names the dotnet it runs under in DOTNET_HOST_PATH.
    /// </summary>
    public static NcxProcess Run(params string[] args)
    {
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host
            ? host
            : "dotnet";
        var start = new ProcessStartInfo(dotnet)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (string arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("dotnet could not be started for ncx.");
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(s_timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("ncx did not end within a minute.");
        }

        return new NcxProcess { ExitCode = process.ExitCode, Output = output, Error = error.Result };
    }
}
