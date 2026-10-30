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
- D394 (Q-1): HeidenhainCylinder + HeidenhainCylinderReader + HeidenhainCylinders. A CYLINDER section of the cycle's
  shape is CYCL DEF 14.0/14.1, CYCL DEF 27 (Q1, Q3=0, Q6, Q10, Q11, Q12, Q16, Q17=1) and CYCL CALL in place of the
  CYLINDER=r block; the covered blocks write nothing; the contour LBL n .. LBL 0 stands right after the M30 (before the
  SUB sections), label from the D271 numbering (key "CYLINDER <line>"). Letters: TNC 640 X/Y, iTNC 530 rotary letter and
  the parallel axis letter (D146). Gap choices: (1) DR flips where the circumference letter comes first in the Klartext
  plane (always on the TNC 640), since NCX turns from the cylinder axis to C (D102); (2) the retract must return to the
  radius the plunge started from, where the cycle ends; (3) a tool table without a radius writes the cycle with the new
  WARNING CMP131 instead of CMP101; (4) CYLINDER inside a SUB, incremental words, a block skip, comments or other
  words inside the section stay CMP101; (5) the tool axis must be the radial axis. The reader is unchanged: a source
  CYCL DEF 27 is not in the catalog and stays RAW (not expanded); the native CYCLE:HEIDENHAIN=27 form of D394 would need
  a catalog entry and is left open. Row CAM-234 settled.

## Review fixes (after merging main)

- B1/B2: the contour is circumference letter first on both dialects (TNC 640 Zyklen 892905-18 p. 346-347, iTNC 530
  Zyklen 670388-12 p. 239-240), the mirror of the NCX plane, so DR and RL/RR both flip: COMP=LEFT is RR, NCX CW is
  DR+. Gap choice (1) of D394 is dropped.
- D394 reader half (delegated): a source cycle 14 + cycle 27 and the LBL n .. LBL 0 that KONTURLABEL n names stay RAW
  as one unit (HeidenhainLabels.ContourLines); row CAM-234 and its code location updated.
- D397 (delegated): `FN 20: WAIT FOR SYNC` before an FN 18 read of ID270 and ID240 (iTNC 530 Klartext 340490-05
  p. 313-314).
- Cycle 27 under FEED_MODE=PER_REV is CMP101 (delegated).
- Tuples replaced by the record CylinderPoint; README updated; code locations of rows 19, 29, 40, 139, 234 fixed; the
  D401 RAW reason no longer gets the D58 suffix.
