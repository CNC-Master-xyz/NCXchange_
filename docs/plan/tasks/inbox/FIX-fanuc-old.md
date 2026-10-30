# FIX-fanuc-old Four older Fanuc bugs from the W2-4 reviews

Branch: `fix-fanuc-old` (worktree `.worktrees/fix-fanuc-old`), from `main` 4fc3175. Source: `.worktrees/codewave/gaps/W2-4.md`, "Existing (not W2-4)". Every control fact from the cnc-manuals connector; the page numbers are its pages.

## Log

2026-10-30

1. Lathe `Q2000` read as PECK=2000 and written as `Q2000.`.
   - Manual: B-64484EN p.229 (8.3): a lathe (T) takes a decimal point in `X Y Z U V W A B C I J K R F` and `H`, not in `Q`; a mill (M) takes it in `Q` as well. Bit 0 (DPI) of parameter 3401 applies only to an address that can take a point (B-64490EN-05 p.190). B-64484EN-1 p.96 writes `G83 Z-40.0 R-5.0 Q5000`.
   - No machine key for calculator input exists; none added. The readers keep the documented calculator-type assumption (fanuc.md 2) for every address that takes a point, so `X70`, `R5`, `F100` and a mill `Q2000` stay 70 mm, 5 mm, F100, 2000 mm. A lathe `Q` takes no point, so it counts least input increments whatever DPI says.
   - Fix: the reader reads a lathe `Q` without a point as Q x 0.001 mm (0.0001 inch under G20/G70), IS-B as everywhere else (D366); a lathe `Q` with a point or from an expression stays RAW. The compiler writes a lathe `Q` as the integer number of increments (`PECK=2` is `Q2000`), the mill `Q` with its point as before. Other no-point cycle addresses: cycle `P` already counts `dwell_increment` (D159); the lathe `P`/`Q` of `G74`/`G75`/`G76` and `D` of `G71`..`G73` (B-64484EN-1 p.278-279) are not mapped by the catalog and keep their blocks RAW, so nothing to change.
   - Open, not changed: `G4 X`/`U` without a point counts `dwell_increment` (D159), which is the DPI = 0 reading and contradicts the calculator-type assumption of fanuc.md 2. Needs a decision: either a `[format]` key for DPI with both readings, or D159 follows fanuc.md 2. Noted in fanuc.md 2.
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

Gate 2026-10-30 on the final state: `dotnet build NCXchange.sln -warnaserror` 0 warnings, 0 errors; `dotnet test NCXchange.sln` 5814 passed, 0 failed, 7 skipped (Acceptance); `dotnet format NCXchange.sln --verify-no-changes` clean. Each commit was built and tested on its own.
