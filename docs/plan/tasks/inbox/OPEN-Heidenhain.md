# OPEN-Heidenhain: decisions D394-D403 (Heidenhain package), 2026-10-30

Branch open-hh. Brief: `.worktrees/openitems/BRIEF.md`.

## Log

- D395 (Q-2): comment only; STOP stays after the motion. HeidenhainFunctions and its test cite D395; D252 row has a note.
- D396 (Q-3): F and S with a formula or a signed parameter are spilled into the highest free QL (`QL499 = Q1 * 2`, then
  `FQL499` / `SQL499`). HeidenhainParameters keeps the formula as the key of the target state (`=Q1 * 2@12`) and checks
  every variable it reads (CMP117). The written form is `FQL499` (no space), as `FQ1` already is.
- D397 (Q-4): `$SYS_` names are read by `FN 18: SYSREAD QLn = ID.. NR.. IDX..` from the D338 pool, one per distinct name
  in a block (HeidenhainBlock.SystemRead, HeidenhainFormula). Machine file comment updated. A SYS_ value is held only
  within its own step for F/S (a motion may change it). The D320 FN 18 route for SETPOS after HOME is unblocked but not
  built; CMP113 stays (note on the D320 row).
- D398 (Q-5): comments only at the three sites and the test refs; D319 row points at D398.
- D399 (Q-6): comments in HeidenhainFlow and SiemensFlow; the D212 record test is replaced by a D399 record test; a
  Siemens test pins `REPEAT ROW` without P.
- D400 (Q-7): the RAW reason of C/CP/LP after a one-axis CC names the CC block and D400 (HeidenhainState.OneAxisPole).
- D401 (Q-8): RAW reason "CHF only between two straight lines (D401)".
- D402 (Q-9): RAW reason of the corner names the CT block and D402.
- D403 (Q-10, Heidenhain half): shared helper `src/Ncx.Readers/ComputedNumbers.cs` (6 decimals, half away from zero,
  shortest form). The Heidenhain reader's decimals plumbing (LeastDecimals, DecimalsOf, PoleDecimals, Element.Decimals)
  is removed. The Fanuc package switches FanucNumbers/FanucCycles to the helper. The Siemens reader still has its own
  `SiemensNumbers.LeastDecimals` (not in the D403 markers).
- S-5: `ProgramLayouts.Of` gives the family default (file_per_program for Heidenhain); row W3-3 settled.
- S-13: HeidenhainCycles comment cites D178, D114, D164.
- Dangling pointers fixed: HeidenhainChainArrivals (D124), HeidenhainFrameTests:146, HeidenhainSetposTests:167/191,
  HeidenhainToolCallTests:29/168.
- Rows without their own 03-open-questions row (Q-3, Q-4, Q-8, Q-9, Q-10) are cited by their D number in the code.
