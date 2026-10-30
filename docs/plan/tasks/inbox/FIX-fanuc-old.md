# FIX-fanuc-old Four older Fanuc bugs from the W2-4 reviews

Branch: `fix-fanuc-old` (worktree `.worktrees/fix-fanuc-old`), from `main` 4fc3175. Source: `.worktrees/codewave/gaps/W2-4.md`, "Existing (not W2-4)". Every control fact from the cnc-manuals connector; the page numbers are its pages.

## Log

2026-10-30

1. Lathe `Q2000` read as PECK=2000 and written as `Q2000.`.
   - Manual: B-64484EN p.229 (8.3): a lathe (T) takes a decimal point in `X Y Z U V W A B C I J K R F` and `H`, not in `Q`; a mill (M) takes it in `Q` as well. Bit 0 (DPI) of parameter 3401 applies only to an address that can take a point (B-64490EN-05 p.190). B-64484EN-1 p.96 writes `G83 Z-40.0 R-5.0 Q5000`.
   - No machine key for calculator input exists; none added. The readers keep the documented calculator-type assumption (fanuc.md 2) for every address that takes a point, so `X70`, `R5`, `F100` and a mill `Q2000` stay 70 mm, 5 mm, F100, 2000 mm. A lathe `Q` takes no point, so it counts least input increments whatever DPI says.
   - Fix: the reader reads a lathe `Q` without a point as Q x 0.001 mm (0.0001 inch under G20/G70), IS-B as everywhere else (D366); a lathe `Q` with a point or from an expression stays RAW. The compiler writes a lathe `Q` as the integer number of increments (`PECK=2` is `Q2000`), the mill `Q` with its point as before. Other no-point cycle addresses: cycle `P` already counts `dwell_increment` (D159); the lathe `P`/`Q` of `G74`/`G75`/`G76` and `D` of `G71`..`G73` (B-64484EN-1 p.278-279) are not mapped by the catalog and keep their blocks RAW, so nothing to change.
   - Open point closed in step 5.
2. `M29` inside a running G84 read silently as `MFUNC=29`.
   - Manual: B-64484EN-2 p.104 (G84) and p.108 (G74), B-64484EN-1 p.114: an `M29` in a tapping cycle raises PS0204.
   - Fix: a block that carries the codes of the before line while the run entered with it goes on stays RAW with a reason naming PS0204; a block with `G80`, a group-01 code or another cycle code ends the run first and is read as before.
3. `S600 M3` written inside a rigid TAP run.
   - Manual: B-64484EN-2 p.104, p.108: S between M29 and G84 is PS0203, M29 inside the run PS0204; p.104: the cancel clears the rigid-tapping S to S0.
   - Choice: end the run, not an ERROR. A block that writes an S word or a spindle code (`SPINDLE`, `RPM`, `ORIENT`, `VC`, `CSS`, `RPM_MAX`, `SPINDLE_MODE`) inside a run that the before line entered gets `G80` in a line of its own before it; the next call writes `M29 S<new>` and the full definition again. The program stays runnable with the speed NCX asked for; an ERROR would refuse a valid NCX program.
4. `CYCLE=OFF` after `G90` wrote `G0 G80`.
   - Found: the reader turns `G0 X100.` after a lathe `G90` into `RAPID X=100 CYCLE=OFF`; the compiler wrote `G0 G80 X100.`. The `G0` is asked for (RAPID) and has to stand: B-64484EN-1 p.46 (and p.257-258) ends a simple turning cycle only with a group-01 code other than G90/G92/G94, while G80 cancels the drilling cycles only (p.309). The `G80` was the surplus word.
   - Fix: no `G80` for a `CYCLE=OFF` (or a cancel before a tool change or motion) that ends `G90`/`G92`/`G94` (`G77`..`G79` in B, `G20`/`G21`/`G24` in C); the target state still ends the cycle and makes the motion unknown, so the block's or the next motion writes its group-01 code. `RAPID X=100 CYCLE=OFF` is `G0 X100.` again.
   - Not changed: after a drilling cycle the compiler still writes `G0 G80 X20.` for `RAPID X=20 CYCLE=OFF` (legal, G80 group 09 on a mill, group 10 on a lathe, B-64484EN-1 p.28). The G0 there is asked for; it could be dropped only if the target state kept group 01 known through a drilling cycle (B-64484EN-1 p.25 item 5 says the cycle does not touch group 01), which the compiler deliberately forgets. Also noted: the reader puts the lathe drilling codes and G80 into group 01 (FanucModalGroups), while the manual's list gives G80 group 10 (B-64484EN-1 p.28); left as is.

5. `[format] calculator_input` (orchestrator decision, delegated 2026-10-30).
   - Key `[format] calculator_input = true|false`, default true, parameter 3401#0 DPI (B-64490EN-05 p.190), for the addresses that take a decimal point (B-64484EN p.229). True: X, R, F, the mill Q and `G4 X`/`U` without a point read as written (mm, inch, degrees, seconds). False: they count least input increments of IS-B (0.001 mm, 0.0001 inch, 0.001 degree on a rotary axis; F by the feed mode, mill 1 mm/min and 0.01 mm/rev, lathe 1 mm/min and 0.0001 mm/rev, B-64484EN p.527-528, B-64484EN-2 p.104), `G4 X`/`U` in `dwell_increment`; an F without a point under a caller's unknown feed mode stays RAW. Cycle P, `G4 P` and the lathe Q count increments under both.
   - Reader: `FanucDecimalPoint` rewrites the words to their values before any concern reads them (only under false); `FanucMotion.ReadDwell` follows the key. Compiler: under false every address that takes a point keeps it also where `[format] decimals` writes none (`F100.`). Loader: `ConfigTable.FlagOr`, `OutputFormat.CalculatorInput`.
   - Behaviour change of the default: `G4 X2` without a point now reads 2 s (was 0.002 s under D159).
   - D159 row note: Gap closed 2026-10-30 (delegated): G4 X/U without a decimal point follow [format] calculator_input (parameter 3401#0): seconds by default, dwell_increment under false; G4 P and the cycle P always count dwell_increment.
   - Spec: machine-config 1 (dwell_increment comment) and 2 (key and paragraph), controllers fanuc.md 2 and 4.
   - G80 group: B-64484EN-1 p.28 gives G80 (and the lathe drilling codes) group 10, the reader files them under group 01 (`FanucModalGroups.SystemA`/`SystemBOrC`). Trial: moving the lathe drilling codes with G80 into the cycle group changes a reading (`LatheG87_DrillsAlongXAtEveryCPosition`: the C120. after G87 reads `RAPID C=120 CYCLE=OFF` instead of `CYCLE_CALL C=120`), and moving G80 alone would leave G83 active in group 01 after G80. Not changed, reported.

Gate 2026-10-30 on the final state: `dotnet build NCXchange.sln -warnaserror` 0 warnings, 0 errors; `dotnet test NCXchange.sln` 5814 passed, 0 failed, 7 skipped (Acceptance); `dotnet format NCXchange.sln --verify-no-changes` clean. Each commit was built and tested on its own.
Gate 2026-10-30 after step 5: build -warnaserror 0 warnings, 0 errors; test 5822 passed, 0 failed, 7 skipped; format --verify-no-changes clean.

6. Review round (orchestrator, 2026-10-30). Main (fix-wy-siemens) merged in first.
   - Units of the lathe Q: one helper `FanucNumbers.IsInch` (G20/G21, else units_default as the header assumes) used by `LeastInputIncrement` and `FanucDecimalPoint`; an inch-default lathe without G20 read the Q 10x off.
   - `[machine] input_increment` = "IS-A" | "IS-B" | "IS-C" (parameter 1013, B-64490EN-05 p.57-58), default IS-B: 0.01 / 0.001 / 0.0001 mm, a tenth in inch, the same number in degrees on a rotary axis; used by the lathe Q (reader and compiler) and the calculator_input = false reading.
   - An RPM on a call inside a rigid run: the re-arm line carries the S (`M29 S600`), the G84 block none (an S there is PS0203, B-64484EN-2 p.103-104).
   - A definition that the before line enters or re-arms writes F every time (B-64484EN-2 p.105; G80 clears the drilling data, p.84).
   - The G80 that ends a rigid run clears its S to S0 (B-64484EN-2 p.104): the target state holds S0 for the tap's spindle, and the next cut or cycle call with the spindle running writes the programmed S again before it (also before a compensating G84 that follows a rigid run).
   - Reader: an M29 inside any tapping run, compensating included, is RAW with PS0204 (B-64484EN-2 p.104; B-64484EN-1 p.119).
   - Nested ternary in `FanucDecimalPoint` replaced, stale comment in `FanucBeforeLineWalk` removed.

## Decision lines for decisions.md

- calculator_input: Gap closed 2026-10-30 (delegated): `[format] calculator_input = true|false`, default true, is parameter 3401#0 DPI (B-64490EN-05 p.190) for the addresses that take a decimal point (B-64484EN p.229). True reads X, R, F, the mill Q and G4 X/U without a point as written (mm, inch, degrees, seconds); false counts least input increments of `input_increment`, F by the feed mode (B-64484EN p.527-528), G4 X/U in dwell_increment. Cycle P, G4 P and the lathe Q count increments under both. The compiler writes the point on every such address, under false also where `decimals` writes none.
- input_increment: Gap closed 2026-10-30 (delegated): `[machine] input_increment = "IS-A" | "IS-B" | "IS-C"`, default "IS-B", parameter 1013 (B-64490EN-05 p.57-58): 0.01 / 0.001 / 0.0001 mm, 0.001 / 0.0001 / 0.00001 inch. Every count of the least input increment uses it: the lathe Q, the calculator_input = false reading.
- End the run, then re-arm: Gap closed 2026-10-30 (delegated): an S word or a spindle code inside a run that the before line entered ends the run with G80 before the block, and the next call re-arms it with the line and the full definition, F included; an RPM on the call stands in the line only; after the G80 the control's S is S0 and the next cut or cycle call writes the programmed S again (B-64484EN-2 p.103-105, p.108).
- No G80 for G90/G92/G94: Gap closed 2026-10-30 (delegated): a CYCLE=OFF or a cancel that ends a simple turning cycle (G77-G79 in B, G20/G21/G24 in C) writes no G80; the group-01 code of the block or of the next motion ends it (B-64484EN-1 p.46, p.309).
- D159 (corrected): Gap closed 2026-10-30 (delegated): G4 X/U without a decimal point follow `[format] calculator_input` (parameter 3401#0): seconds by default, `dwell_increment` under false; G4 P and the cycle P always count `dwell_increment`.

Gate 2026-10-30 after step 6 (with main merged): build -warnaserror 0 warnings, 0 errors; test 5955 passed, 0 failed, 7 skipped; format --verify-no-changes clean.
