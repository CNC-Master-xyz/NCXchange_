# CW-W3-1 Jobs: runner and job compiler

Phase: code wave 3 | Branch: `w3-1` | Depends on: W1-G, W1-H, W2-1 | Size: L (D308)

## Goal

D304 to D311, D313 to D316, D328 and D329, with the markers of D201 and D223 in the job files
(`.worktrees/codewave/PLAN.md`, W3-1), and the review notes of `gaps/` that name W3-1: D315 data already in, the
comment that named old samples (now the D307 comment), `JobChannel.Build` and the trivia indices (D142), D316's
library file, D388 (7) and (8), and the NTJX `start_mark` 198.

## Log

Claude (agent), 2026-10-27.

Commits:
- aad8501 D308, D304, D305, D306, D307, D309: shared job state and the scheduler rules.
- 076e644 D316, D315, D310, D304, D142, D201, D311, D313, D314, D328, D329: the job compiler.

Tests were written with the change. Run against the old code: `SharedStateTests` (D308) failed in three of five
(spindle seen by the other channel, PROGRAM=END of one channel, tool seen by the other channel); the event test
fails without `TakeOverSharedChanges`. The tests of the new codes (VM573, VM855, VM856, CMP713, CMP721) do not
compile against the old code. The reserved-mark, library-file and trivia tests fail on the old code by what they
assert (the old pick took M100, no library file, both comment lines kept). `JobBindingTests.BoundWord_Diagnostic...`
was changed: under D308 the job run itself saw the sub spindle in AXIS mode, so channel 2 now turns it back after the
mark; `JobCommandTests` expect the Nakamura files `O1000` and `O1000.P-2` (D310).

State on main checked first: the D314 `start_mark` 198 and the D315 `reserved_marks` data were already in
(`nakamura-ntjx.toml`, `dmg-ctx-840d.toml`, both copies), and the Nakamura acceptance already expects M198.

Decided while doing it:
- Gap closed 2026-10-27 (delegated): D308 keeps one `ResourceState` per job (spindle rows, tool in spindle, preload)
  and `HolderOffsets` per channel (offsets and the point words of D388 (7)); lastHolder moves to VM 2.8.
- Gap closed 2026-10-27 (delegated): in a job, PROGRAM=END of a channel resets the spindles that channel owns (its
  `[[resource]] channel`, or the tool spindle of a holder of that channel) and a spindle no channel owns only once
  every channel of the job has ended.
- Gap closed 2026-10-27 (delegated): the Before of a channel's next block takes the shared rows as they stand, so a
  listener of one channel sees no TOOL_BEGIN or STATE_CHANGE for a change another channel made (VM 7).
- Gap closed 2026-10-27 (delegated): the UNKNOWN marks of the shared spindle rows (`RPM:S1` and the like) live in the
  `ResourceState` with the rows (`UnknownKeys`), so every channel sees a speed another channel set from an expression
  as UNKNOWN.
- Gap closed 2026-10-27 (delegated): D308 with D288: a channel takes a spindle for its feed per revolution (the
  default-spindle fallback, Core `MaxFeedCheck` and Analytics `SpindleSpeed`) and for VM551 only while it is the
  channel's own: its `[[resource]]` (or the holder whose tool spindle it is) names the channel, or the channel's own
  SPINDLE word turned it (`SpindleState.StartedBy`); otherwise the workpiece spindle.
- Gap closed 2026-10-27 (delegated): the new codes are VM573 `SyncParticipantsDiffer` (D304), VM855
  `ChannelNotRunByJob` (D306), VM856 `FinishedChannelNotRestarted` (D309), CMP713 `MarkParticipantsDiffer`
  (D304), CMP721 `ChannelFileNameTaken` (D310).
- Gap closed 2026-10-27 (delegated): CMP713 is the compile's form of VM573: the job compiler reports the ERROR of the
  job run under CMP713 with the PS0160 reason (FANUC 30i-A Connection Manual Function B-63943GE-1/02, connector
  p.949-950), since the STATIC run already pairs the marks; a generated SYNC cannot meet another set at its mark,
  because it stands at the same release in every channel.
- Gap closed 2026-10-27 (delegated): D388 (8) needs no CLI change: the manifest requires `[job] machine`
  (machine-config 8, F24) and a machine it does not find is CLI200, so no job runs on the built-in default
  machine. The job runner counts the job's channels as the machine's (C8), so a machine file without `channels`
  never gets VM851 for the job's own channels.
- Gap closed 2026-10-27 (delegated): CMP721 stands on line 1 of the manifest, one WARNING per renamed file.
- Gap closed 2026-10-27 (delegated): the library name is the `[job] name`, else the manifest's file name without
  `.ncxjob.toml`, plus `_LIB`; a second file of the job with uncalled SUBs gets `_LIB_2`. Siemens writes one
  `<name>.spf` per SUB, named by its unit (D316 "an SPF per SUB"); Heidenhain writes one `<job>_LIB_<sub>.h` per
  SUB, BEGIN PGM and END PGM in the machine's `units_default`, no M30 (iTNC 530 Klartext 340490, connector p.523), a
  call of another library SUB inside it as CALL PGM, and a SUB whose walk wrote no lines as an empty program. Library
  files carry `CompiledFile.Library`. Two library files that hold one program (the same O number, SPF or program
  name) are the new WARNING CMP722 `LibraryProgramTwice`. A job of one channel has no library file: its uncalled SUBs
  stand in its own file as D264 writes them, without CMP720 (language 4.13 says multi-channel).
- Gap closed 2026-10-27 (delegated): a channel file that the job renames (channel_files or the CMP721 suffix) renames
  its program, as D266: `%_N_<final>_MPF` on Siemens, BEGIN/END PGM on Heidenhain, the comment of the O line on Fanuc
  when it carries the old name.
- Gap closed 2026-10-27 (delegated): `JobChannel.Build` places each comment line before the block that now takes the
  place of the block it stood before (before the blocks the job compiler inserted there); the lines of sections the
  channel leaves out are dropped.

Review fixes (after merging main with W3-3 and W3-4): the four blocking points of the review (shared UNKNOWN marks,
Heidenhain library calls, the spindle a channel takes in a job, renamed programs) and the small ones: ChangeWait takes
the shared rows first, `ResourceState.ChannelEnded`, `ChannelState.Snapshot` adds no offsets, the lastHolder comment
cites VM 2.8, CMP301 is reported once for a SUB walked twice (also in a compile of one file).

The Nakamura WY-250L job against main (both with W3-3), `analyze --job --skip-blocks all`: path 2 tools 5 and 6 read
1785 rpm again, tool 8 1200; the VM551 at `P-2.ncx(59)` and the one at `O1000.ncx(64)` are gone (each was a spindle the
other path turned). The remaining difference is time: tools 6 and 12 of path 1 and tool 8 of path 2 lose 0.6 s to 1.4
s, totals 18.4 s to 17.2 s and 16.4 s to 14.9 s. The spindle is one row per job now, and the runtime estimate charges
the spindle ramp to the channel whose block changes the speed; a channel that commands the speed the other path has
set already sees no change and pays no ramp, where main ramped each channel's own copy. Errors and check output are
otherwise the same; the compiled files are now named O1000 and O1000.P-2 (D310).

Left: none of the package's decisions. `SharedResources.cs` keeps its marker (row 160, answered by D20, not a
decision of this package).

Gate: `dotnet build NCXchange.sln -warnaserror`, `dotnet test NCXchange.sln --filter
"FullyQualifiedName!~TakesWellUnderASecond"` and `dotnet format NCXchange.sln --verify-no-changes` green.
