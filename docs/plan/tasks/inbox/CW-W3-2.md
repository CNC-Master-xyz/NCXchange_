# CW-W3-2 Reader rules, plugins, batch

Phase: code wave 3 | Branch: `w3-2` | Depends on: waves 1 and 2 (main 8ed2ee4) | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The ten decisions of package W3-2 of the code-wave plan (`.worktrees/codewave/PLAN.md`, section 5): D231, D232, D238, D284, D285, D317, D318, D330, D331, D332, and from the wave-3 notes the CLI half of D111.

## Scope

- Readers: `ReaderBase.cs` (rule order, claimed runs), `ISourceRule.cs`, `SourceBlock.cs`; the `LeavesUndecided` overrides and the `Names` helpers of the three families go.
- Plugins: `PluginSourceRule`, `PluginDiagnostics`, `PluginRewriter`, `PluginLoader`, `PluginClasses`, `DiagnosticCodes`.
- Config: `ProjectSettingsLoader`, `ProjectSettings`, one optional parameter of `ConfigTable.Table`.
- CLI: `NcxTomlPlugins`, `PluginBuild`, `PluginCheck`, `PluginFolder`, `RunPlugins`, `BatchCommand`, `BatchReport`, `DiagnosticCodes.Plugin` (CLI560); for D111 `FormatCommand`, `Pipeline`, `PipelineRun`, `AnnotateCommand`, `AnnotatedText`, `CompileCommand`.
- Samples and template: `samples/plugins/ThroughCoolantSourceRule`, `samples/plugins/README.md`, `templates/ncx-plugin/README.md`.
- Spec: architecture 9, 10, 13; code-guidelines 11; VM 7; machine-config 5, 5a, 10; controller-mapping 4; 13 P3-01, P3-07; 17 P7-01, P7-02; plugins.md; reading-the-code.md; machine-builders.md; tests/corpus-reports/README.md; `src/Ncx.Readers/README.md`; 03-open-questions rows 13, 17, 43, 85, 86, 177-188.

## References

- `docs/decisions/decisions.md` rows D111, D231, D232, D238, D284, D285, D317, D318, D330, D331, D332, D388.
- `.worktrees/codewave/PLAN.md` sections 2, 5 (W3-2), 6, 7; `.worktrees/codewave/WAVE-BRIEF.md`.

## Done when

- Each decision is on the branch with its spec sentences, its markers turned into comments and its open-question rows settled.
- `dotnet build NCXchange.sln -warnaserror`, `dotnet test NCXchange.sln --filter "FullyQualifiedName!~TakesWellUnderASecond"` and `dotnet format NCXchange.sln --verify-no-changes` are green.

## Log

- Claude (agent), 2026-10-27, branch `w3-2`:
  - D231: `ReaderBase` offers every source block to the rules first, in load order; a block no rule claims is read by the family's `ReadBlock` with the tables. `LeavesUndecided` (abstract, and its Fanuc, Heidenhain and Siemens overrides) and the `Names` helpers of `FanucBuilder`, `HeidenhainFunctions` and `SiemensFunctions` had no other caller and are gone. D40's order stays inside the families. D66's row is untouched. Tests first: the inverted `FakeReaderWithThreeRules_ThreeLines_ReadsTheExpectedNcx`, `Rules_CodeTheTableNames_AreAskedBeforeTheTables` and `Rules_RuleDeclines_TheTablesReadIt` failed on main.
  - D232: `ISourceRule.Read` returns `int`; `SourceBlock.Following` holds the following blocks of the section up to the next structure block; the reader skips the claimed Read steps, applies them to `SourceState` and keeps their comments as comment-only lines after the rule's blocks. `PluginSourceRule` passes the count through. `ThroughCoolantSourceRule.Read` returns `Fold(block, block.Following, builder)`, and a new sample test folds `M5`, `M51`, `M3 S1500` through the Fanuc reader of a machine whose tables name all three. The new tests (`Rules_Offered_SeeTheFollowingBlocks...`, `..._LookAheadStopsAtALabel`, `..._ClaimOfThreeBlocks...`, `..._TooFewBlocksFollow...`, `..._CountBeyondTheBlocksShown...`, `Read_ReaderRuleClaimingMoreBlocksThanItWasShown...`, the two sample tests) did not compile on main. No golden file changed: without a plugin nothing changes.
  - D238: `[plugins] assemblies` is the list, `[plugins.<name>]` the settings; `assemblies` is never a plugin; a top-level `plugins = [...]` is CFG003 with "the table [plugins] with assemblies = [...] (D238)". `NcxTomlPlugins.Add` joins the list, adds the key below `[plugins]`, or adds the table in front of the first section, else at the end. Tests first: 4 `ProjectSettingsLoaderTests` and 12 of 15 `NcxTomlPluginsTests` failed on main.
  - D330: `PluginBuild.BroughtAssemblies` (the DLLs of the build output but the plugin's own and `Ncx.*`); with any, build copies nothing and lists the build-output path. `PluginBuildOutputTests` did not compile on main.
  - D284: `BatchCommand` skips hidden files and folders, loads the plugins once (`RunPlugins.Load` with a `PluginDiagnostics`), and `PluginDiagnostics.ReportInto` hands their diagnostics to each file; a dropped plugin stays dropped. Both new `BatchCommandTests` failed on main. The example-sources reports did not change.
  - D285, D317, D331, D332: main kept; markers turned into comments, spec sentences added (architecture 9, 10; machine-config 10 with `plugins/` in the layout).
  - D318: markers turned into comments; code-guidelines 11 step 6, P7-01, P7-02, the template README and the summaries print "inserted 3 blocks". New test `ReplacingRewriter_AsAPlugin_PrintsNoInfo` (passes on main too). `TemplateTests` compiles with `Program.Compilers()`; P7-02 is released to `done/`.
  - D111 (CLI half, wave-3 note): format, annotate, check, trace and compile hand the NCX text to the parser with its byte order mark; `PipelineRun.ByteOrderMark` is gone and `AnnotatedText` writes the mark from `NcxProgram.HasByteOrderMark`. TOML texts and controller programs keep their stripping. The existing BOM tests of format, annotate and convert pass before and after.
  - Gaps closed 2026-10-27 (delegated), each in the form of its neighbours:
    - D232: the look-ahead is `SourceBlock.Following`; it leaves out trivia lines and stops in front of a block whose `StructureOf` role is not None (O, M30, M99, the file frame) or in front of a written structure step (a jump target). A structure block itself is still offered (D231: every block), with its own look-ahead.
    - D232: `ReaderBase` caps a count at the offered block plus the blocks shown; a plugin rule whose count is below 0 or beyond that is `PLG003` ("Read claimed n source blocks where it was shown m"), dropped, and nothing it wrote reaches the program.
    - D238/D331: `ncx plugin build` prints `ncx.toml: [plugins] assemblies = [...]`.
    - D330: the entry is the build-output path from the working directory with `/`; a file it cannot join is the new WARNING `CLI560` (`PluginPathNotAdded`), since such a plugin does not load without it; a DLL in `plugins/` keeps the INFO `CLI557`.
    - D284: a path with any part starting with `.` is hidden; what the plugins report while they load (PLG002, PLG005) stays with the batch, before the first file.
  - Found while doing it:
    - The job files (`JobPipeline.cs`, `CompileJob.cs`, W3-1's) still strip the byte order mark before the parser; harmless, left to W3-1.
    - `ConfigTable.Table` (not in the W3-2 file list) gained an optional `expected` text for the CFG003 message of D238.
    - The reader families' files (`FanucReader`, `FanucBuilder`, `HeidenhainReader`, `HeidenhainFunctions`, `SiemensReader`, `SiemensFunctions`) lose only the dead `LeavesUndecided`/`Names` code.
- Claude (agent), 2026-10-28, review fixes of W3-2 (orchestrator review), after merging main (W3-3, W3-4; conflicts in architecture 13 and 03-open-questions rows 85-87 resolved by taking both sides):
  - Blocking: the look-ahead was quadratic with any rule loaded (40k blocks 52 s). `ReadLookAhead` works out the Read steps and the end of each look-ahead once, in one backward pass; `SourceBlock.Following` is a constant-time `SourceBlockSlice` over one shared list, and a claim maps through the index. New `Convert_LongProgramWithADecliningRule_TakesWellUnderASecond` (20k blocks: 7.3 s against 0.33 s on the old code, failed there) and `Convert_3DFraesenWithADecliningRule_TakesWellUnderASecond`.
  - A plugin rule that claims blocks and writes no NCX block is PLG003 and dropped; the PLG003 summary names the out-of-range count and this case. Tests for a negative count and for a claim without writing.
  - The look-ahead ends in front of a block of the control flow too: a jump (`LabelsUsed`), a call (`Calls`), a repeat, a contour, a lifted subprogram, a return to a block (`ReaderBase.IsFlow`). Test `Rules_Offered_LookAheadStopsAtACall`; plugins.md updated.
  - `ncx plugin build` removes the stale `plugins/<name>.dll` and `.pdb` when a plugin moves to its build output (INFO CLI561); `PluginLoader` loads a plugin name once and warns about a second DLL of that name (WARNING PLG007). Tests for CLI560, CLI561 and PLG007.
  - Stale text: `ISourceRule` summary, `PluginBuild` BOM comment (D111), code-guidelines 6 "inserted 3 blocks".
  - Gaps closed 2026-10-28 (delegated):
    - D232: a block the family reads as control flow (a jump, a return, a call, a repeat, a contour, a lifted subprogram) ends the look-ahead in front of it, as a structure block does. Such a block is still offered itself (D231: every block).
    - D232, D5: a claim with no NCX block written is PLG003, and the plugin is dropped.
    - D330: build removes the stale copy in `plugins/` with the INFO CLI561 (`PluginStaleCopyRemoved`); one that cannot be removed is CLI004 (ERROR).
    - D317, D330: a plugin name loads once, from its first DLL in load order; another DLL of the name is the WARNING PLG007 (`DuplicatePluginName`).
