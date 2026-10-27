# CW-W2-2 Fanuc reader

Phase: code wave 2 | Branch: w2-2 | Depends on: W1-C, W1-E, W1-F | Size: L

## Goal

The Fanuc reader side of the 2026-10 decisions D125, D154, D155, D185, D186, D218, D223, D225, D240, D244, D245, D249, D250, D349, D354 (`.worktrees/codewave/PLAN.md`, W2-2). The cycle half (D383-D386 and the rest of W2-4) is not in this package.

## Log

Claude (agent), 2026-10-27.

- D223: `ReadOptions.Channel`, `ncx convert --channel n` (a usage error with `--batch` or below 1), the `.P-n` suffix (n = 1..10, `.P-1` included) on any Fanuc machine, RDR107 for a channel the machine file does not have, on the first O line, with `CHANNEL=n` still written. `ReaderBase` gains the protected `Options` (one property in a shared file). The samples are `NAKAMURA_WY250L_O1000.nc` and `NAKAMURA_WY250L_O1000.P-2.nc`, the vars fixture `NAKAMURA_WY250L_O1000.P-2.vars.toml`; job manifest, job, CLI, round-trip, template and acceptance tests follow; the two batch reports are re-accepted (the P-2 file now has RDR107 on fanuc-mill-30i, which names one channel). Tests first: the option tests did not compile against the old code, the mill `.P-2` and `.P-10` cases returned no channel and no ERROR.
- D244: `FanucChain.Clear` keeps the G52 shift; ORIGIN writes it again; G92 (G50 in system A) writes SHIFT=RESET, SETPOS, the G52 SHIFT of the other axes; G52..G59, G92, G28, G30 under the 2D G68 or G51.1 stay RAW naming PS0412 (0i-D B-64304EN-2 p.191, checked in the connector), G52..G59 and G92 under the 3D G68 RAW, under G68.2 RAW as "kept native".
- D245: G68 turns about the tool position without X Y (plane of G17/G18/G19), ROTATE only at the plane origin; a second 2D G68 replaces under G90, stays RAW under G91; G68 with I J K stays RAW, counted, the third names PS5043; a second G68.2 replaces the first through TryChange; cross forms name PS5462. Main dropped the G69 of a G68 kept RAW silently and appended the ROTATE of a G68 with I J K to the chain before the block went RAW; both fixed: the modes a RAW block switched on are tracked in FanucChain, and their G69/G50.1 stays RAW. The compiler marker of D245 (Cm/Fanuc/FanucFrames.cs) became a comment.
- D186: G2/G3 with R and I J K is the R form with RDR109. D185: under G43.5 G0 (and an implicit rapid) with I J K is RAPID with TX TY TZ, G2/G3 with I J K and R the radius form with TX TY TZ, without R RAW.
- D349: G61 and G64 are PATH_MODE=EXACT_STOP and CONTINUOUS; G62, G63 stay RAW.
- D354: under `helix_feed = "arc"` (the Fanuc default) the F of a helix is scaled by path over plane length, rounded to 3 decimals, and the next LINE or ARC writes the control's feed again; RDR110 where geometry or F are unknown.
- D240: RDR108 where a `SYS_POS_` or `SYS_MPOS_` maps onto an axis programmed in diameters or switchable.
- D154, D155: M codes go through `TemplateSet.FindFunctionsByCode` and `FunctionChoice.Choose` with the channel of the file; the [workpiece] selection is written first; ORIENT and PHASE take the {angle} of the template (ORIENT also S or R of the block), else 0 with RDR021; [spindle_sync] ON and PHASE couple `MachineConfig.SpindleSyncRoles()`. M92 on the test lathe is now `SPINDLE_SYNC=MAIN,SUB PHASE=0`.
- D125: the lathe G52 X is written as given (a diameter under DIAMETER=ON); test with a check run.
- D250: the Nakamura acceptance allows RAW:FANUC only for G10 and the one #5024 block of path 2.
- D218, D225, D249: markers turned into comments; controller-mapping 5 (CYCLE_F) and 6 (CALL), fanuc 1.
- 03-open-questions: wave-2 rows 57, 59, 61, 69, 71, 72, 73, 75, 76, 77 settled.

Gaps closed 2026-10-27 (delegated):
- RDR107 checks that n is one of the channel numbers of the machine file, a file without `channels` having channel 1; `CHANNEL=n` is still written.
- `--channel` with `--batch`, or below 1, is a usage error (CLI001).
- RDR110 FanucHelixFeedNotScaled, a WARNING, for a helix whose geometry or F the reader does not know (D354); the scaled F keeps 3 decimals.
- A G54..G59 or G92 while the G52 shift of the callers is unknown (D225) stays RAW.
- Under the 3D G68 G52..G59 and G92 stay RAW (B-64484EN p.344-345 per the D244 answer); a 2D G68 under the 3D conversion and a 3D G68 under the 2D one stay RAW without being counted; G68.4 stays RAW.
- A G69 or G50.1 that cancels a G68, G68.2 or G51.1 kept as RAW stays RAW, so the compile gives it back to the control.

Left over:
- Heidenhain and Siemens readers do not read `ReadOptions.Channel` yet (W2-6, W2-8).
- D155 "holds already": the Fanuc reader keeps no state of the function tables (FanucState is W2-4's), so no state is dropped as holding or as the OFF of a resource not on.
- D354 compiler half (CMP324, the scaling on output) is W3-5; until then a Fanuc helix does not round-trip to the same F.
- The comment in `C/Jobs/JobRunner.Words.cs:144` (sample name) is W3-1's.

Review fixes, 2026-10-27:
- A block kept as RAW now sets its mode afterwards. `FanucFrames.KeepRawModes` runs when the block is written, and the chain first goes back to `FanucBlock.ChainBefore`. A G68, G68.2, G68.3, G68.4 or G51.1 kept as RAW, for any reason (also `G62 G68 …`), marks its mode native, and its G69 or G50.1 then stays RAW. A replacing RAW G68, G68.2 or G68.3, and every RAW cancel, writes the end of the entries the chain holds (ROTATE=RESET, MIRROR=OFF, SHIFT/TILT=RESET) beside the RAW block (`FanucDraft.BesideRaw`). The chain becomes unknown only when that cut is ambiguous. On an unknown chain the entries are dropped.
- A G68 or G68.2 on a chain of callers the reader does not know stays RAW (D225, D245 (b)). Before, it was appended.
- G68.3 and G68.4 are taken by FanucTilt and kept RAW, so a G54 under them stays RAW (D244 (c)).
- G-code groups follow B-64484EN-2 p.27-28: 12 is G66/G67, 14 is G54..G59, 15 is G61..G64. G68.2/.3/.4 belong to group 16. Changed in FanucModalGroups, the fanuc.md 3 table, and the toml comments (both copies).
- RDR107-110 are now in number order. RDR021 has its own wording for PHASE; RDR108 no longer calls an MPOS value a diameter. FanucBuilder no longer uses `!`. The I J K of G68 are read without an array. FanucFrames is split into partials: Datums, Rotations, Modes. Row 71 now reads "settled 2026-10-27 (D250)" only.
