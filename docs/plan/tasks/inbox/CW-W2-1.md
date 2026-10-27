# CW-W2-1 Compiler framework and shared output

Phase: code wave 2 | Branch: `w2-1` | Depends on: all of wave 1 | Size: L

## Goal

The compiler side of D150, D151, D154, D159, D172, D185, D263, D264, D266, D267, D282, D299, D349, D351,
D361/D377 and D367 (`.worktrees/codewave/PLAN.md`, W2-1): the framework (`CompilerBase*`, `OutputBuffer`,
`CommentCharset`, `LookAhead`, `DiagnosticCodes`), the motion writers and the template call sites of the three
families. W2-1 merges first in wave 2.

## Log

Claude (agent), 2026-10-27. The session restarted once after the D154 commit; the work went on from there.

Commits:
- ac1cc96 D263: the compilers read `[format]` through `OutputFormat.WithFamilyDefaults`.
- 7de2fbf D264, D266: every SUB written at least once (language 4.13 with the D278 exception, C4), CMP003 retired,
  program file names compared case-insensitively with CMP005.
- 408fd34 D267, D150: `{next}` at the last change is the program's first tool, CMP006; preload marker.
- 2380480 D172: `change_named` and `preload_named`, CMP008.
- 1b169e2 D154: an ORIENT template without `{angle}` only for an angle of 0, CMP007; Siemens spindle-sync pair.
- 05b5497 D159: Fanuc `DWELL` as `G4 X<seconds>` with a decimal point.
- 3ecc38f D299, D282: `MotionCodeRule` reads `[format] motion_code` in the Fanuc and Siemens writers.
- b1faacb D151, D367: `ToleranceTemplate` (ON_ROTARY in every family), CMP015 in `CompilerBase.CheckTolerance`,
  `held_comment`.
- d15115a D349: `PathModeTemplate`, CMP361, CMP533, CMP120.
- b6a5218, 3f5220d D185: a vector ARC is CMP017 on Heidenhain and Siemens; on Fanuc it takes the radius form.
- a80385c D361, D377: `CycleCatalog.ForChannel` for the compiled channel, CMP004; `CheckHeldDistances` checks the
  channel tables.
- 14ae89b D351: `CompilerBase.StockOf`, `StockForm`, `StockNumber`, the three family stock writers, CMP012-CMP014;
  `TemplateText.Has` shared by ORIENT, tolerance and stock.

Tests were written with the change. Run against the old code: `Dwell_InSeconds_IsG4XInSecondsWithADecimalPoint`,
`Dwell_AsAnExpression_IsG4XWithTheExpression` and five `JobBindingTests` (G4 P1000 -> G4 X1.) failed;
`MotionCode_Change_*` (Fanuc, Siemens) and `MotionCode_EveryBlock_WritesG1Again` failed; the tests of the new codes
(`ToleranceReportTests`, `PathModeWriterTests`, `VectorArcTests`, `CycleAvailabilityTests`, `StockWriterTests`) do not
compile against the old code.

Decided while doing it:
- Gap closed 2026-10-27 (delegated): CMP015 reports `{mode}` only for `TOLERANCE_MODE=ROUGH`; FINISH is the start value
  the control takes without the placeholder. `held_value` (a path tolerance in mm, an inch value converted) makes only
  the `{tol}` report an INFO; `{rotary}` and `{mode}` are always WARNINGs.
- Gap closed 2026-10-27 (delegated): the `held_comment` text is `TOL <value> <held_in>`, written beside the switch as
  `(...)` on Fanuc, `; ...` on Siemens and as a comment line after the cycle 32 on Heidenhain; none when the template
  carries `{tol}`.
- Gap closed 2026-10-27 (delegated): ON_ROTARY is chosen in the Fanuc writer too (no shipped Fanuc file has one).
- Gap closed 2026-10-27 (delegated): `PATH_MODE` is written as a line of its own after the tolerance words; the
  Heidenhain code is `HeidenhainPathModeNotWritten` (CMP120); CMP361 and CMP533 keep their names, their texts gain
  PATH_MODE.
- Gap closed 2026-10-27 (delegated): CMP017 `VectorArcNotWritable` is checked before the run like CMP001
  (`WritesVectorArcs` false for Heidenhain and Siemens). On Fanuc an ARC with a vector is written in its radius form
  `G3 X Y R I J K`; one by its centre is CMP017, as I J K of G43.5 are the vector.
- Gap closed 2026-10-27 (delegated): a compile without a job takes the `[[cycle]]` tables of the machine's first
  channel (1 without `channels`); CMP004 `CycleNotAvailable` stands at the block that defines the cycle.
- Gap closed 2026-10-27 (delegated): STOCK `{dist}` is the coordinate of the face nearest the spindle (the top along the
  tool axis, Heidenhain DIST, TNC 640 Klartext 340590-05 p.103); `{radius}`/`{diameter}` from the first plane axis;
  corners given by expressions are CMP013; a CYLINDER template without `{axis}` writes about Z only and one without a
  plane-axis coordinate only a cylinder centred on the datum, otherwise CMP013. Heidenhain writes the corners and DIST
  signed, R and L unsigned.
- Gap closed 2026-10-27 (delegated): a RAW blank of the target (CMP014) is a RAW of the family or builder (or without
  address) whose first word is the first word of a `[stock]` template, `BLK` or `WORKPIECE`.
- Gap closed 2026-10-27 (delegated): the shared rule of `motion_code` is `MotionCodeRule`; Heidenhain reads no key,
  Klartext writes L on every block (family value `every_block`).

Spec: controller-mapping 1 (DWELL), 2 (tool vectors); controllers fanuc.md 4 and 10 rule 1, siemens.md 12 rule 2;
machine-config 5 (`[stock]` and `[tolerance]` rules); with the earlier commits language 4.13, machine-config 2, 3, 5,
VM 3.5, 3.9, heidenhain 8 rule 6, architecture 13. Open questions settled: 73, 74, 75, 146, 217, 211, 227 and the
P3-03 rows 2, 4, 5, 7, 9, 10, 11, 14, 15, the P3-06 row 80 and the P5-02 row 148.

Left over / for other owners:
- D159: the P of the Fanuc canned cycles (`round(seconds / dwell_increment)`) and the reader are W2-4's;
  `FanucExpressions.WordValue` keeps the factor 1000 for `CYCLE_DWELL` until then.
- D299 (N8): siemens-840dsl-mill.toml keeps the family value `every_block`; the corpus is outside the repository.
  `SiemensEquivalence` needed no change.
- D185: the Heidenhain RAPID marker (`HeidenhainMotion.cs:55`, open question 36) is W2-6's.
- D154: the reader rows 73, 114, 121, 123 are the readers'.
- Klartext places BLK FORM directly after BEGIN PGM in the manual's examples; D351 puts STOCK after the first ORIGIN,
  so the compiled BLK FORM follows the cycle 247. The iTNC 530 accepts a later blank (SPEC FCT, 340490 p.114).
