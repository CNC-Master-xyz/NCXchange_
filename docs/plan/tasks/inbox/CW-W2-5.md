# CW-W2-5 Heidenhain compiler, non-cycle

Phase: code wave 2 | Branch: `w2-5` | Depends on: wave 1, W2-1 | Size: L

## Goal

The Heidenhain compiler side of D146, D149, D169, D191, D213, D266, D269, D271, D272, D274, D319, D320 and D338,
the reader default of D269 in `HeidenhainPlane.cs`, and the spec sections the plan gives W2-5 (heidenhain 1, 2, 6, 7
rule 6, 8; controller-mapping 1; machine-config 2).

## Scope

- Compiler: `Cm/Heidenhain/*` except `HeidenhainCycles.cs`, `HeidenhainCycleValues.cs` (W2-6), `HeidenhainArcs.cs`
  (W3-5) and the W2-1 methods; the new `HeidenhainLabelNumbers`, `HeidenhainLabelKey`, `HeidenhainRepeats`,
  `RepeatPart`, `HeidenhainSpills`, `HeidenhainReads`; `HeidenhainLabels.cs` (compiler) removed.
- Reader: `R/Heidenhain/HeidenhainPlane.cs` (D269).
- Config: `[format] counter_registers` (`OutputFormat.CounterRegisters`, `Core/Machine/CounterRange`, the loader).
- Codes: CMP116 retired; CMP125-CMP130.

## Log

Claude (agent), 2026-10-27.

Commits: a6fb1e4 (D191, D269), 46ad358 (D271, D272, D213, D274 spec), 28355ce (D146, D274, D319, D320 markers),
219a53d (D266), 29b8c2d (D338), 098c4c0 (D149), eeae8c0 (D169), and the log commit.

Tests written first. Against the old code the new tests did not compile (the new `DiagnosticCodes` and
`OutputFormat.CounterRegisters`); behaviourally they failed there: no `TABLE ROT` was written, a PLANE SPATIAL
without ROT read as TABLE, labels were CMP116 instead of renumbered, RETURN in a subprogram wrote a second LBL 0 and
under IF was CMP101, a formula in a coordinate was CMP102, names were not checked. Changed tests: the CMP116 tests of
`HeidenhainFlowTests` became the D271 renumbering tests, the RETURN tests follow D272, `JumpToTheEnd` expects the
lowest free number, `Rule2_FormulaInACoordinate` the spill, `PlaneAxial_IsTiltAxis...` reads ROT=COORD, and the
2.5D_FRAESEN acceptance compile expects CMP127 for the name "2.5D FRAESEN".

Decided while doing it:
- Gap closed 2026-10-27 (delegated): D271 numbering details: of two equal numeric labels the first in block order keeps
  its number; "next free" is the lowest number of the range no label has. The subprograms (their names, labels and
  D272 return labels) are numbered once for the compile, since a SUB's text is written once and copied into every
  caller's file (D99); the labels of each program and its JUMP=END label take numbers the subprograms do not have.
  The JUMP=END label is numbered with the labels, no longer "highest plus one".
- Gap closed 2026-10-27 (delegated): codes CMP125 `HeidenhainArgumentReadAfterCall` (ERROR), CMP126
  `HeidenhainStringComparison` (ERROR), CMP127 `HeidenhainProgramNameLimits` (WARNING), CMP128
  `HeidenhainRotWithoutEffect` (INFO), CMP129 `HeidenhainNoFreeLabel` (ERROR), CMP130 `HeidenhainRepeatWithoutCounter`
  (ERROR, D213; taken from the W2-5 spare range).
- Gap closed 2026-10-27 (delegated): `[format] counter_registers = ["QL90", "QL99"]`, the first and the last name of
  one kind of Q parameter (Q, QL or QR), as `[variables] repeat_counters` lists names; anything else is CFG005. Not
  shipped in `heidenhain-itnc530.toml`.
- Gap closed 2026-10-27 (delegated): D213 counter loop form: `QLc = 0` before `LBL n`; at the REPEAT
  `QLc = QLc + 1`, `FN 12: IF +QLc LT +k+1 GOTO LBL n`, `QLc = 0`. A RETURN in the part leaves it like a JUMP. Each
  counter loop of the compile takes its own register, in block order.
- Gap closed 2026-10-27 (delegated): D269 "a tilt that leaves no free rotary axis" is decided without kinematics only
  for a machine with no rotary axis at all (as VM422); a ROT counts as stated when the block has a ROT word or the
  entry is COORD. CMP108 is reworded: a stated ROT on a machine without the map, or whose TILT_ON lacks `{rot}`.
- Gap closed 2026-10-27 (delegated): D338 spills reuse QL499 downward per block (the spills are read by the block
  itself); the named QL numbers are those of any word, address or RAW text of the run, and the counter range is kept
  out. D169: two QS parameters compare with `STRCOMP ( SRC_QSa SEA_QSb )` (iTNC 530 DIN/ISO 606420-01 p. 291) into a
  spilled QL, tested with FN 9 / FN 10 against 0.
- Gap closed 2026-10-27 (delegated): D149 is an ERROR (the control would compute with another value), raised on the
  CALL when a later block of the caller's section reads an ARG name before assigning it; for CALL LBL as for CALL PGM.
- Gap closed 2026-10-27 (delegated): D266 length limit applies when `[machine] dialect` is "iTNC530"; the character
  check on every Heidenhain target. The final name repeats the framework's rule (stem, case-insensitive `_2`) inside
  the Heidenhain compiler, so it needs no framework API.

Left over / for other owners:
- D146: CYLINDER is CMP101 with a TODO(question) and the new 03 row 234 (cycle 27 needs a contour subprogram and Q1,
  Q3, Q6, Q10-Q12, Q17 that no NCX word carries).
- D338: SETPOS formulas are not spilled yet (a later SETPOS rewrites every standing shift, so the register would have
  to stay reserved while the shift stands); they stay CMP102 with a plain TODO. F and S formulas keep the
  `HeidenhainParameters` marker.
- D266: the framework's file name (`CompilerBase.Layout.FileNameOf`, W2-1) still compares names ordinally; W2-1 makes
  it case-insensitive (CMP005), after which file name and BEGIN PGM agree.
- W1-F gap note (BLK FORM signs, `X+0 Y+0 Z-40`): the `[stock]` hook is W2-1's; not touched here.

Review fixes (2026-10-27, one commit):
- B1 (D213): a JUMP or REPEAT outside a counter loop's part whose label lies inside it writes `QLc = 0` before its
  jump line (`HeidenhainRepeats.ResetsOnEntry`).
- B2 (D213): counter registers that a word of the file names are skipped (`HeidenhainSpills.NamedParameters`, which
  now also reads Q and QR names from RAW); none free is CMP130.
- B3 (D271): when a subprogram's walk holds a JUMP=END, every program's end label takes one number of the
  subprograms' numbering, so the shared SUB text does not depend on its caller.
- `counter_registers = ["QL90", "QL99"]` shipped in `machines/heidenhain-itnc530.toml` (the only copy: the file is
  not among docs/spec/examples/machines).
- N3: a JUMP or RETURN whose label got no number (CMP129) takes its IF, so no CMP101 besides. N4: a QL argument of
  CALL PGM is not CMP125, QL being program-local.
- controller-mapping 6 (REPEAT, CALL + ARG), heidenhain 7 rule 3 (the D272 jump reads back as JUMP plus LABEL), the
  Heidenhain README, 03 row 234 moved after 233, the STRCOMP case-sensitivity note.
