# OPEN-fanuc: the Fanuc decisions D403-D411 of the fifteenth round

Branch `open-fanuc`, from main 1ecacb6, 2026-10-30. Brief: `.worktrees/openitems/BRIEF.md`; decisions D403-D411 (Q-10 Fanuc half to Q-18).

## Log

- D403: `src/Ncx.Readers/ComputedNumbers.cs`, a neutral helper (6 decimals, half away from zero); the Heidenhain package (open-hh d0a7461) adds the same file, and this branch takes its content byte for byte, so the merge is clean; the Fanuc reader uses it for G16 points, chamfers, roundings, G68.2 angles and the pitch F / S. `FanucNumbers.DecimalsOf` and the `LeastDecimals` constants are gone. The helix F keeps 3 (D354). HeidenhainNumbers and the Siemens reader are not touched.
- D404: `[variables] atan_nat` (bool, default false); reader `((ATAN2(y, x) + 360) MOD 360)` and the same for ASIN, compiler `[f - 360 * FIX[f / 180]]`; plain under true.
- D405: `G28 B<position>`; a `RAPID B=v` directly before `HOME B` folds into it (FanucFrames.FoldsIntoHome); CMP321 for an unknown B; the Nakamura job test compiles for the shipped machine file.
- D406: comment only; the hard-rule test now asserts the settled row.
- D407: RDR123 FanucExternalProgramLocals on an external call after the caller assigned #1..#33.
- D408: CMP343 FanucSpeedLimitOfAnotherSpindle; no reordering.
- D409: catalog key `first_block` (CFG160); params of G71..G76 in `cycles/fanuc-lathe.toml`; the reader joins the two blocks and reads G74..G76 with their end point; the compiler writes both blocks. `RETRACT_AMOUNT` and `RETURN_AMOUNT` instead of the decision's `RETRACT` and `RETURN`, which are NCX words; F of G71..G75 as `CYCLE_F`.
- D410: `available = false` in a catalog file; native lookups skip a withdrawn entry, `Find` keeps it for CMP004; `fanuc-lathe.toml` withdraws DRILL_DWELL, CHIP_BREAK, BORE.
- D411: CycleRules and the Fanuc reader keep the positions after G71, G72, G74, G75.
- Stale S-1 (D218, FanucCycles and FanucMotion), S-2 (FanucLine and its two pointers), S-11 (reader FanucCycles), S-12 (FanucHeader, D34), S-14 (cycles/fanuc.toml, D158); the three "TODO(question) of OutputFormat" pointers in FanucMotion now cite D276.
- 03-open-questions: W1-14, W1-18, W1-19, W1-73, W1-86, W3-57, W3-60, W3-65, W3-71, W3-76, W3-175, W3-194, W3-196 updated. Q-10, Q-15, Q-17 and Q-18 have no row; their comments cite the decision.
