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
- Gap closed 2026-10-27 (delegated): the UNKNOWN marks of the shared rows (`RPM:S1` and the like) stay per channel;
  a channel that set an RPM from an expression keeps it UNKNOWN until its own next RPM word.
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
  `.ncxjob.toml`, plus `_LIB`; a second file of the job with uncalled SUBs gets `_LIB_2`. Siemens writes the SPF
  units into `<job>_LIB.mpf` (the family extension); Heidenhain writes one `<job>_LIB_<sub>.h` per SUB, BEGIN PGM and
  END PGM in the machine's `units_default`, no M30 (iTNC 530 Klartext 340490, connector p.523).
- Gap closed 2026-10-27 (delegated): `JobChannel.Build` places each comment line before the block that now takes the
  place of the block it stood before (before the blocks the job compiler inserted there); the lines of sections the
  channel leaves out are dropped.

Left: none of the package's decisions. `SharedResources.cs` keeps its marker (row 160, answered by D20, not a
decision of this package).

Gate: `dotnet build NCXchange.sln -warnaserror`, `dotnet test NCXchange.sln --filter
"FullyQualifiedName!~TakesWellUnderASecond"` and `dotnet format NCXchange.sln --verify-no-changes` green.
