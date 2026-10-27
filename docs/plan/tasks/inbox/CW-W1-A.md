# CW-W1-A Build, diagnostic model, CLI lookup

Phase: code wave 1 | Package: W1-A (`.worktrees/codewave/PLAN.md` section 3) | Depends on: nothing | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The thirteen decisions of W1-A on main, merged first in wave 1: CA1305 checks every project (D108), CI holds the 120-character line (D109), `Diagnostic.Line` is nullable for command-line diagnostics (D110), and the CLI lookup, outputs and release follow D200, D205, D206, D207, D209, D215 (CLI half), D217, D303, D347 and D371.

## Scope

- Build: `Directory.Build.props`, `.editorconfig`, `.github/workflows/ci.yml`, `src/Ncx.Cli/Ncx.Cli.csproj`, `src/Ncx.Config/Ncx.Config.csproj`.
- Core: `src/Ncx.Core/Model/Diagnostic.cs`, `Diagnostics.cs`.
- CLI: `Program.cs`, `ProjectFolders.cs`, `RunMachine.cs`, `RunSettings.cs`, `Pipeline.cs`, `JobPipeline.cs`, `DiagnosticCodes.Project.cs`, `FoundFile.cs`, the markers of the commands; new `src/Ncx.Config/BuiltInFiles.cs`.
- Spec and plan: code-guidelines 3.3, 3.4, 6, 9; 00-method 3; P0-01; architecture 4, 10, 11, 12; machine-config 6, 8, 10; VM 2.9, 3.6, 6; language 4.9, 4.15; controller-mapping 6; P2-04; phase 3 (status, P3-02, P3-03, P3-05, P3-06, P3-07, exit checklist); P7-03 and its task; 20-schedule; phases.md; sources/README.md; the 2.5D_FRAESEN notes.

## References

- `docs/decisions/decisions.md` rows D108, D109, D110, D200, D205, D206, D207, D209, D215, D217, D303, D347, D371, D388.
- `.worktrees/codewave/PLAN.md` sections 2, 3 (W1-A), 6, 7.

## Done when

- Each decision is on the branch with its spec sentences, its markers turned into comments and its open-question rows settled.
- `dotnet build NCXchange.sln -warnaserror`, `dotnet test NCXchange.sln` and `dotnet format NCXchange.sln --verify-no-changes` are green.

## Log

- Claude (agent), 2026-10-27, branch `w1-a`:
  - D110 (first commit, as the plan asks): `Diagnostic.Line` is `int?`; null renders `ncx: ERROR CLI001: message`. `Diagnostics.CommandLineError` reports the diagnostics whose origin is the tool: the usage errors (CLI001), CLI250, CLI400, CLI550, CLI551, CLI553, CLI554 and dotnet not started. `Program.CommandLine` is gone. The plugin template that is missing and an unwritable plugin file keep line 1 of their file. Tests first: 32 acceptance tests that expected `ncx(1): ` failed on main; the two new `DiagnosticsTests` did not compile there.
  - D108: the `InvariantGlobalization` property is replaced by the `RuntimeHostConfigurationOption` item. A full rebuild finds no CA1305 hit; a probe `decimal.Parse(text)` in Ncx.Cli is `error CA1305`, and the runtimeconfig.json of ncx and of the test projects still carries `System.Globalization.Invariant: true`.
  - D109: a bash step on the Linux job of ci.yml greps `src/` and `tests/` for `*.cs` lines over 120 characters, with `LC_ALL=C.UTF-8` so that grep counts characters. No line of main is over 120 characters (REPO-01 wrapped them); `ArcResolver.cs:88` was 120 characters and 122 bytes and is rewrapped, so a byte count passes too. `LineLengthTests` stays, the same limit in every local test run.
  - D200: `Diagnostics.Error`, `Warning` and `Info` on a block prefix the message of a generated block with `<source> (<reason>): `. CMP712 no longer restates the job compiler and takes the deadlock text from "Deadlock:" on. The updated `D98_DiagnosticOnAGeneratedBlock_NamesTheOriginatingLine` and the new `DiagnosticsTests` case failed on main. No golden file changed.
  - D371, D206, D207, D303: `Ncx.Config` embeds `machines/*.toml` and `cycles/*.toml` (logical names `machines/<file>`, `cycles/<file>`), and `BuiltInFiles` reads them. `ProjectFolders` takes the built-in copy after the three folders and names it `built-in machines/<name>.toml`. CLI200 and CLI202 end with "and no built-in machine file of that name" and "and no built-in catalog of that name". The D206, D207 and D303 markers become comments, since main already resolves as decided. The new tests (`BuiltInFilesTests`, two in `ProjectFoldersTests`, two in `RunMachineTests`) did not compile on main.
  - D205, D209: the command markers become comments. The new test `Trace_ErrorOfTheVirtualMachine_WritesTheRowsOfTheBlocksBeforeItAndExitsOne` passes on main (D205 keeps the code). The markers of `AnalyzeCommand` and `AnalyzeJob` (wave-2 question #42 for analyze, row 94) stay: D205 does not name analyze.
  - D215 (CLI half): `Pipeline.LoadExternalProgram` opens `<name>.ncx` only, and VM752 names that file. Both new `PipelineTests` failed on main. The Heidenhain reader half is W2-6's and the `FanucFlow` marker W2-3's.
  - D347: `<Version>1.0.0</Version>` in `Directory.Build.props`. `Ncx.Cli.csproj` gets `PackageId` NCXchange.Cli, a `Description`, `PackageLicenseExpression` MIT, `RepositoryUrl` and `RepositoryType`; `Authors` is left out until D165. ci.yml gains a `publish` matrix on `v*` tags (linux-x64 on Ubuntu, win-x64 on Windows, osx-arm64 and osx-x64 on macOS, single-file self-contained, `-p:Version=${tag#v}`), which asserts on the native runners that `--version` reports the tag before `+`. A `release` job with `contents: write` packs the tool and creates the GitHub release. Checked locally: `dotnet pack -p:Version=1.0.0-alpha.1` gives `NCXchange.Cli.1.0.0-alpha.1.nupkg`, and `ncx --version` prints `1.0.0-alpha.1+<commit>`. The built-in `--version` prints the version of the entry assembly, so `VersionOptionTests` runs ncx as a process (`NcxProcess`). These tests pass on main, whose default version is also 1.0.0.
  - D217: the criterion is amended in phases.md, architecture 11 and 12, phase 3, sources/README.md and the done tasks P3-02, P3-05 and P3-06. The example's notes are complete: note 2 now covers the RPM placement, and new notes 5 (SECTION against trivia), 6 (R0 against G40), 7 (NUMBER) and 8 (BLK FORM) follow. The header stays two lines, so no line number moves. The annotate golden is re-accepted (comment text only). The test markers become comments. No reader changes.
  - Gaps closed 2026-10-27 (delegated), each in the form of its neighbours:
    - D110: the method is named `Diagnostics.CommandLineError`; every diagnostic whose file is the tool counts as a command-line diagnostic.
    - D200: the prefix is `<source> (<reason>): `, the form `GeneratedOrigin` gives trace and annotate. It applies to every diagnostic reported on a generated block through `Diagnostics`, the compilers' included.
    - D371: the reader is `Ncx.Config.BuiltInFiles`. A value with a folder in it never names a built-in file, and a name is matched without regard to case, as the folders match on macOS and Windows. A tool table of `machines/` (`*.tools.toml`, `BuiltInFiles.ToolTableSuffix`) is built in too but is never a machine: `Machine` returns null for it, the machines listing leaves it out, and `ProjectFolders.FindMachine` finds no machine by such a name. A built-in machine finds its tool table as the built-in file its `tool_table` names, named `built-in machines/<name>.tools.toml`, so a lone binary compiling with a built-in machine does not fall back to the D10 warning block. This replaces the earlier note that a built-in machine has no tool table next to it.
    - D347: the release jobs are `publish` and `release`, and the Version is 1.0.0.
    - D217: the new notes are numbered 5 to 8.
  - Found while doing it:
    - The rationale of D109 counts 53 lines over 120; main has none (REPO-01 wrapped them).
    - Architecture 12 has no release row, so D371 adds nothing there.
    - `FanucCompilerTests.Chain_3DFraesen_TakesWellUnderASecond` is a timing test that fails on main too while other builds load the machine.
- Claude (agent), 2026-10-27, review fixes of W1-A (orchestrator review):
  - D215: `LoadExternalProgram` returns null for an empty name, a name with `/` or `\` in it, or a rooted name, so the result is VM752; main loaded `sub/O9010`, `../outside/OUT` and absolute paths. The new test `LoadExternalProgram_NameWithAFolderOrRooted_IsNoExternalProgram` (with `sub/O9010.ncx` present) failed before the fix.
  - D371: the built-in tool tables, as in the gap note above. `BuiltInFilesTests` compares `*.tools.toml` of `machines/` as tool tables and reads a machine and its tool table embedded in the test assembly (`Fixtures/BuiltIn/`, logical names `machines/sample.toml` and `machines/sample.tools.toml`), because no tool table is shipped yet (W1-H adds `fanuc-mill-30i.tools.toml`). `BuiltInFiles.Names` is internal.
  - D200: the parse diagnostics of generated text start with `<source> (<reason>): "<text>": `.
  - ci.yml: `chmod +x` on the Linux and macOS binaries before they are zipped; `--prerelease` for a version with a `-`; the comment of the line-length step says that `.gitattributes` keeps every checkout LF. The binary name (`Ncx.Cli` against `ncx`) is left to P7-03.
