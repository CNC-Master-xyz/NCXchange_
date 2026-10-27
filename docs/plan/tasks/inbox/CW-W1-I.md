# CW-W1-I New modal words PATH_MODE, STOCK, ROUND_LIMIT, core

Phase: code wave 1 | Branch: `w1-i` | Depends on: nothing | Size: L

## Goal

The core side of the new words of 2026-10-07 (D349, D350, D351, D352, D363) and the ROT rework of D191 with VM426
(D388 (6)): catalog entries and ranks, VM state, PROGRAM=END, validation, and the specification sections the plan
gives W1-I. W1-I alone amends the step-of-ten comment of `CanonicalRanks` (plan rule 6).

## Scope

- Catalog: `STOCK` (verb, 4.2), `MAX` (address X, Y, Z; ranks 371-373), `PATH_MODE` (4.1, rank 785), `ROUND_LIMIT`
  (4.1, rank 813, `ROUND_LIMIT:ROTARY` 816); the step-of-ten comment and the note of the generated table.
- Parser: `BlockRules.FrameVerbRefusal` and the frame-verb list for `STOCK`; `MAX` under any other verb is PAR012.
- VM: `FrameState`/`FrameSnapshot` gain `PathMode` (start value `[dynamics] path_mode`, `StartPathMode`) and
  `RoundLimit` (`RoundLimitState`); `FrameHandlers`, `ProgramEndRules`, `StateChanges`; `FrameValidation` and `StockValidation`:
  VM423-VM425 (`StockPlacement`), VM420 for `ROUND_LIMIT:ROTARY`, VM422 reworked, VM426.
- Out of scope (other packages): `[stock]`, `[transform]` PATH_MODE templates and data (W1-F); the writers CMP012-014,
  CMP120/361/533 (W2-1); CMP020 retired and CMP023 (W3-5); the runtime estimate (W3-3); explicit COORD/TABLE ROT in
  the Heidenhain output (W2-5); the readers of G61/G64, G60/G64 (W2-2, W2-8).

## Log

Claude (agent), 2026-10-27.

Commits: a7e74be (D351, D352, step-of-ten comment), 92711ce (D349, D350), 1386f9d (D363), d5a953a (D191, D388 (6)),
and the review fixes after it (STOCK checks moved to `StockValidation.cs`, the language 4.2 ROT row, heidenhain 1).

Tests written first; against the old code they did not compile (the new `CanonicalRanks`, `DiagnosticCodes`,
`FrameState` members), and the catalog tests (`WordCatalogTests` verb, axis-word and table lists,
`CanonicalOrderTests`) state the new words. New test files: `Parsing/StockBlockRulesTests`,
`VirtualMachine/State/PathModeTests`, `VirtualMachine/State/RoundLimitTests`; additions in `FrameValidationTests`,
`ValidationMachines.ThreeAxisMill`. `D90_Ranks_AreInStepsOfTen` became
`D90_Ranks_AreInStepsOfTenWithTheLaterWordsBetween`: the multiples of ten run without a gap, and a rank off the step is
a later word; it keeps passing when W1-H adds 435/465/467.

Decided while doing it:
- Gap closed 2026-10-27 (delegated): `STOCK` has scope block; `MAX` is a key of group Frame, section 4.2, with
  `AddrKind.Axis`, required address and the fixed set X, Y, Z (as `CENTER`), an axis word of 5 rule 2.
- Gap closed 2026-10-27 (delegated): VM codes and names: VM423 `StockWithoutItsCorners`, VM424 `StockExtentInvalid`,
  VM425 `StockMisplaced`, VM426 `RotWithTiltAxis`; VM422 keeps its constant name `RotWithoutTableKinematics` (codes and
  names are not renamed).
- Gap closed 2026-10-27 (delegated): VM425 counts per program (a `PROGRAM=BEGIN` starts again) on the blocks the run
  executes; the `ORIGIN` of the `STOCK` block counts, a motion verb is one of `MotionRules.IsMotion`. VM423/VM424
  compare literal numbers only; an expression in STATIC mode is not compared. The cylinder's sides across the tool axis
  are taken from the `WORKPLANE` the block leaves.
- Gap closed 2026-10-27 (delegated): The blank is no VM state: `STOCK` moves nothing and the compiler reads the block
  (VM 2.1 note, VM 3 step 4).
- Gap closed 2026-10-27 (delegated): `ROUND_LIMIT` reuses `AddrKind.ToleranceKind` for its address ROTARY;
  `ROUND_LIMIT=OFF` clears both values, and `PROGRAM=END` sets it OFF like the tolerance row of VM 4.
- Gap closed 2026-10-27 (delegated): The path-mode state reuses `Ncx.Core.Machine.PathMode`; `PATH_MODE=DEFAULT`
  resolves to the start value, so the state never holds "default".
- Gap closed 2026-10-27 (delegated): VM426 fires with or without a machine file ("any target"); VM422 is not raised for
  a `TILT_AXIS` block.

Left over / for other owners:
- `VirtualMachine.ResolveAxisWords` (W1-C) resolves the `X Y Z` of `STOCK` against the machine's axes, so a machine
  without a Y axis would report an unknown axis for a `STOCK`; W1-C takes the one-word skip, as `TILT` is skipped.
  `FrameValidationTests.Stock_OnAMachineWithoutAYAxis_ChecksClean` waits for it, skipped with that reason.
- The `ROT` description in `FrameWords` still says "On table kinematics" (descriptions are W1-B's); language 4.2 is
  updated.
- Machine-config 4 (`[dynamics] path_mode` as the start value, D350), machine-config 2 (rounding silent, D363) and
  VM 8 (estimate reads the state) are not in W1-I's spec list and are left to W1-F, W3-5 and W3-3.
