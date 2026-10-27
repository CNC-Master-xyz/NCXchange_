# CW-W2-8 Siemens reader, non-cycle

Phase: code wave 2 | Package: W2-8 (`.worktrees/codewave/PLAN.md` section 4) | Depends on: W1-C, W1-E, W1-F | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

The fifteen reader decisions of W2-8 on main: D125 (r), D132 (r), D149 (marker), D154 (r), D155 (r), D240 (r), D260, D291, D292 (r), D294 (r), D295, D297, D333 (r), D349 (r) and D382.

## Scope

- Reader: `src/Ncx.Readers/Siemens/*` except `SiemensCycles.cs`; `src/Ncx.Readers/DiagnosticCodes.Siemens.cs` (RDR507, RDR509).
- Machine data: the `G75` comment of `machines/millturn1.toml` and its copy in `docs/spec/examples/machines/`.
- Spec: controllers/siemens.md 2, 4, 5, 8, 11; controller-mapping 1, 4, 5, 6, 9; machine-config 5 (D294); language 4.2 (the D333 sentence of the SHIFT row).
- Tests: `tests/Ncx.Readers.Tests/Siemens/*` except the cycle tests; the `G75` line of `tests/Ncx.Acceptance/Examples/SiemensReaderTests.cs`.

## References

- `docs/decisions/decisions.md` rows D125, D132, D149, D154, D155, D240, D260, D291, D292, D294, D295, D297, D333, D349, D382, D388.
- `.worktrees/codewave/PLAN.md` sections 2, 4 (W2-8), 6, 7.

## Done when

- Each decision is on the branch with its spec sentences, its markers turned into comments and its open-question rows settled.
- `dotnet build NCXchange.sln -warnaserror`, `dotnet test NCXchange.sln` and `dotnet format NCXchange.sln --verify-no-changes` are green.

## Log

- Claude (agent), 2026-10-27, branch `w2-8`:
  - D382: `SiemensExpression.DivisionConverts` also converts a `DIV` of two integer-valued literals of either sign whose quotient is exact (`-8 DIV 2` is `-INT(8 / 2)`, `8 DIV -2` is `INT(8 / -2)`); the minus of a negative divisor stays inside the `INT(`. `-7 DIV 2`, `-7.5 DIV 2.5` and `-8 DIV 0` stay RAW with RDR506. The marker becomes a comment (D382 keeps the general case RAW on purpose) and manual-audit row 2 is settled. The new test failed on main.
  - D132: `SiemensTools.Write` clears any preload after `TOOL=n` and marks the preload known; `T3`, `T5 M6`, `M6` gives a bare `TOOL` instead of `TOOL=3` (failed on main). D297: the T0 marker becomes a comment; a test pins `T0` followed by another `T` as `PRELOAD=0`. Rows 127 and 128 settled.
  - D149, D295: markers become comments; siemens 11 rule 6 and the controller-mapping structured-loops row name the RAW structure rule. Rows 100 and 124 settled.
  - D260: `G75 FP=2..4` is `HOME POINT=2..4`; `G75 FP=1`, `FP=0` and `G75` without `FP` stay RAW with the new WARNING RDR509 `SiemensFixedPoint1HasNoPoint`; `FP` above 4 stays RAW (MD30600 holds four points; Programmierhandbuch Grundlagen 2013 p.377-378). millturn1.toml (both copies) says "fixed points 2..4 for POINT=2 and further; FP=1 is not written". The acceptance program P501B writes `G75 FP=2` now. Row 113 settled.
  - D291: `GROUP_BEGIN` is a SECTION only with a literal `_MODE` whose bit 1 is clear; bit 1 set, a non-literal or a missing `_MODE` keeps it RAW (NC-Programmierung SW4.92 p.1170). `GROUP_END` and `GROUP_ADDEND` become comment-only lines like `DEFINE`. Tests failed on main. Rows 102 and 111 settled.
  - D294: `SiemensSpindles.RoleOf` numbers a spindle by the first extension its templates write, else the digits of its resource id, as the compiler does; `SETMS(1)` finds a main spindle named `SP1` (failed on main). Machine-config 5 gets the sentence. Row 120 settled. The test helper `SiemensRead.MillTurnWith` loads millturn1.toml with text replaced.
  - D154, D155: `SiemensFunctions.ReadTable` takes every state of `FindFunctionsByCode` through `FunctionChoice.Choose` (RDR020 where two resources would switch on) and writes each state's words: `[workpiece]` codes give `WORKPIECE`, `[spindle_sync]` ON and PHASE the pair of `SpindleSyncRoles`, ORIENT and PHASE without `{angle}` are 0 with RDR021. `M19` of the control is `ORIENT=0` with RDR021. Tests failed on main. Rows 114, 121 and 123 settled.
  - D349: group 10 leaves the RAW groups; `G60` and `G64` are `PATH_MODE=EXACT_STOP`/`CONTINUOUS` written where the mode changes; `G641`..`G645` stay RAW words and make the next `G60`/`G64` be written again. `G9` and `G601`..`G603` stay RAW. The raw-words test now uses `G641 ADIS=0.1`.
  - D292: `SiemensFacts.UnitsCode` holds the code of group 13; `SiemensMotion.FeedInNcxUnits` converts `F` and `FB` under `G70`/`G71` whose unit differs from `ResolvedBaseUnits` (25.4, six decimals; an expression is divided or multiplied), with the new WARNING RDR507 `SiemensFeedInTheBasicSystem`. `G700`/`G710` feeds stay as written. Row 115 settled.
  - D125, D240: under `DIAMON`/`DIAM90` the X of `TRANS`, `ATRANS`, `G58`, `G59` is doubled, and `$AA_IM[X]` is read as `(2 * $SYS_...)`, unless the X axis sets `frames_in_diameter` (D388). `$AA_IW[X]` maps one to one.
  - D333: `SiemensChain.TryCut` cuts only the programmable entries, where all of them stand after the last CYCLE800 entry, else RAW; a new `CYCLE800` (`_ST` ones digit 0) clears the chain (`TryClear`), and `_X0`/`_Y0`/`_Z0` and `_X1`/`_Y1`/`_Z1` become SHIFT entries of CYCLE800 before and after the TILT, which `CYCLE800()` removes with it. The D340-remainder marker in `SiemensFrames.ReadOrigin` stays (row 189). Row 101 settled. Controller-mapping 1 TILT row says "not Siemens" for the chain clause and gives the reading rule.
  - Gaps closed 2026-10-27 (delegated), each in the form of its neighbours:
    - D260: RDR509 `SiemensFixedPoint1HasNoPoint` is the WARNING of `G75 FP=1`.
    - D292: RDR507 `SiemensFeedInTheBasicSystem` is the WARNING of a converted feed; a converted literal keeps six decimals (D121); callers that differ in group 13 convert nothing.
    - D291: no RDR508; the decision keeps the existing RAW WARNING for `GROUP_BEGIN` with `_MODE` bit 1, so RDR508 stays reserved and unused.
    - D333: the reference-point shifts of CYCLE800 carry the instruction `CYCLE800` in the chain, so `TRANS` leaves them standing and `CYCLE800()` removes them; a zero or empty reference point writes no SHIFT.
  - Review fixes (orchestrator, 2026-10-27):
    - D382: a minus after `*`, `/` or `MOD` signs an operand of a longer product (`R1*-8 DIV 2` is `(R1*-8) DIV 2`), so the block stays RAW; a unary minus counts only at the start of an operand. RDR506's text names the exact-quotient case; manual-audit row 2 reads "settled 2026-10-27 by D382 (kept RAW where the quotient is not exact; the rounding itself is still undocumented)".
    - D132: `SiemensFacts.ChangedTool` holds the tool of the last change; an `M6` alone with nothing preloaded writes `TOOL=<that tool>`, or no TOOL where the reader knows none; the bare TOOL stays for an unknown preload only. The test now runs check.
    - D292: a switch of group 13 that changes UNITS converts the modal feed by 25.4 into the new unit, clears the written feed so the next LINE or ARC writes F, with RDR507; where the source may have set a feed the reader does not know (`SiemensFacts.FeedProgrammed`), it warns with RDR507. A block with its own F converts nothing. `SiemensMotion.Converted` is shared with `FeedInNcxUnits`.
    - D125, D240, D333: doubling applies only on an X axis whose `programming` is diameter or switchable (`SiemensFrames.XInRadius`); a doubled `$AA_IM[X]` carries the new WARNING RDR510 `SiemensMachinePositionDoubled`; a CYCLE800 with a non-zero `_X0` or `_X1` under DIAMON stays RAW (MD20360 bit 14).
    - D155: the FunctionContext carries the channel of a one-channel machine and, for a code with an extension `Mn=`, the role of spindle n.
    - P501B keeps `G75 FP=1` and expects it RAW with RDR509.
  - Gap closed 2026-10-27 (delegated): D240's WARNING is RDR510 `SiemensMachinePositionDoubled`, the next free code of RDR510-519.
  - Left over: the `F` that `SiemensCycles.cs` reads on a position block (W2-7's file) does not yet call `SiemensMotion.FeedInNcxUnits`.
