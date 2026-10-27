# CW-W2-4 Fanuc cycles, reader and compiler

Phase: code wave 2 | Package: W2-4 (`.worktrees/codewave/PLAN.md` section 4) | Depends on: wave 1, W2-1, W2-3 (hooks) | Size: L (D278)

## Goal

The Fanuc cycle decisions of W2-4 on the branch: D114 (TAPER), D157 (reader, RDR120), D158 (reader), D159 (reader G4 and cycle P, compiler cycle P), D177, D224 (Fanuc), D248, D278 with D388 item 4, D279 (Fanuc), D355 (CMP387), D359, D360, D361/D377 (WriteBefore), D362 (Fanuc), D383 to D386, and the W2-4 lines of `gaps/W1-D.md`.

## Scope

- Reader: `src/Ncx.Readers/Fanuc/FanucCycles.cs`, `FanucCycle.cs`, `FanucState.cs`, the new `FanucBeforeLineWalk.cs` and `FanucLinePaths.cs` (the M29 walk, moved out of `FanucReader.cs`), `FanucMotion.ReadDwell` (D159), `src/Ncx.Readers/DiagnosticCodes.Fanuc.cs` (RDR120-RDR122).
- Compiler: `src/Ncx.Compilers/Fanuc/FanucCycles.cs`, the new `FanucContour.cs`, `FanucContours.cs`, `FanucContourRange.cs`; hooks in `FanucBlock.cs` (Contours) and `FanucCompiler.cs`; the new `CompilerBase.Contours.cs` and `InlinedSection.cs` with one line each in `CompilerBase.Layout.cs` (two places) and `CompilerBase.Walks.cs`; `DiagnosticCodes.Fanuc.cs` (CMP384 retired, CMP387-CMP396).
- Spec: controllers fanuc.md 4, 6, 9 rules 5 and 9, 10 rule 7; controller-mapping 5; machine-config 6 (contour); language 4.13; `docs/spec/examples/POLAR_FACE.ncx` line 39; 03-open-questions rows.

## References

- `docs/decisions/decisions.md` rows D114, D157, D158, D159, D177, D224, D248, D278, D279, D355, D359-D362, D377, D383-D386, D388.
- `.worktrees/codewave/PLAN.md` sections 2, 4 (W2-4), 6, 7; `gaps/W1-D.md` (W2-4 line).

## Done when

- Each decision is on the branch with its spec sentences, its markers turned into comments and its open-question rows settled.
- `dotnet build NCXchange.sln -warnaserror`, `dotnet test NCXchange.sln --filter "FullyQualifiedName!~TakesWellUnderASecond"` and `dotnet format NCXchange.sln --verify-no-changes` are green.

## Log

- Claude (agent), 2026-10-27, branch `w2-4`:
  - D383-D386 (tests first; 8 failed on main): the D383 and D384 markers are comments; the D384 reason names B-64303EN-1 p.927 and NTX1000SZM p.281-282; D385 ends the walk unseen at M3/M4/M5/M19 between the line and the G84, with a reason that names the code (a spindle code before the line is unaffected); D386 keeps a drilling block with G0..G3 RAW with the WARNING RDR121, sets motion to the group-01 code and the mill cycle group to G80, writes CYCLE=OFF after the RAW block where an NCX cycle was written, and gives the line before it no TAP mark. Manual-audit rows 3 and 4 settled.
  - D359, D360 (tests first; the RAW expectations of main failed): a G84 the line surely does not reach reads as TAP_COMPENSATING (`CycleCatalog.FindNativeByBeforeLine`); the M29 walk moved into `FanucBeforeLineWalk`, which decides a jump target, a walked jump target or a called section head from the flow (every path into the block, the jump back of the run neutral): TAP where every path has the line, TAP_COMPENSATING where none has, RAW with the reason "one path reaches ..." where they differ. Rows 193, 195, 209, 210 settled.
  - D359, D361, D377 compiler: TAP_COMPENSATING is G84 without the line; G80 separates a TAP run from a TAP_COMPENSATING run on one G84; the catalog of the compiled channel (`CycleCatalog.ForChannel(After.ChannelId)`) gives the entry and its before line. `available = false` and CMP004 are W2-1's. Row 211 settled.
  - D177 (gaps/W1-D): the reader reads mill G89 P as REAM with CYCLE_DWELL (the dwell_native) and drops a P on G73/G81/G83/G85/G86 with the new WARNING RDR122; the compiler writes DRILL with a dwell as G82 (DRILL_DWELL), REAM with one as G89, and drops it on PECK, CHIP_BREAK, BORE (and DRILL without a DRILL_DWELL entry) with the WARNING CMP388.
  - D159: reader G4 P, and X/U without a decimal point, and a cycle P are units of `dwell_increment`; the compiler writes a cycle P as round(seconds / dwell_increment). The G4 X writer is W2-1's. D158: SURFACE = R - surface_clearance, SURFACE=0 without the key. D157: RDR120 outside G17 on a mill. D224: CYCLE=OFF after a G70..G76 CYCLE_CALL; the compiler writes nothing for it. Rows 14, 56, 74 settled.
  - D248: lathe R = signed radius distance from the initial level (reader CLEARANCE = level + R, + 2R on a diameter X, RAW where the level is unknown; compiler R = CLEARANCE - level, the level of the run kept in the target state). POLAR_FACE line 39 comment says R-3. from X60. Rows 55, 66 settled.
  - D114: TAPER of G90/G92/G94 read from R (modal, a new R a new CYCLE) and written as R.
  - D279, D355, D362: CMP393 (PECK without a Q: G76/G87/G88/G89, G74/G84 without `peck_rigid_tapping`), CMP394 (G73/G83 without PECK), CMP395 (native parameter not one letter), Q on G84 with `peck_rigid_tapping`; INFO CMP387 once at the defining block where SURFACE and CLEARANCE differ; PECK=0 dropped silently. The BOHREN round trip in `Acceptance/Examples/FanucCompilerTests` now accepts INFOs (CMP387 fires there, since fanuc-mill-30i.toml sets no surface_clearance). Row 58 settled.
  - D278, D388 (4): the contour of G70..G73 is written inline after the first cycle of the program (`G71 Pp Qq`, `Np` first block, `Nq` last), numbered after the greatest label (D277), a later cycle repeats P and Q, a second program gets its own copy; `CompilerBase.InlineSection` writes the section's walk from the caller's target state without motion and F, and the layout and the write pass skip an inlined section. CMP389-CMP392; CMP384 retired. Row 52 settled.
  - Gap closed 2026-10-27 (delegated): RDR122 is the reader WARNING for a dropped mill P (D177); CMP396 is the ERROR for a lathe R whose initial level is not known (D248, no code was reserved); CMP393 = PECK without Q, CMP394 = G73/G83 without PECK, CMP395 = native key not one letter (D279); CMP389 type II undeclared, CMP390 PS0325, CMP391 forbidden block, CMP392 first block no G00/G01 (D278); a CONTOUR naming no SUB is CMP385.
  - Left: rows 205 (D355) and 212 (D362) also wait for W2-6/W2-7; row 227 (D377) for W2-1's CMP004.
  - Review fixes after merging main (86ac434): B1, a before line further back than the S or axis block that stopped the walk (`M29 S500` / `G0 X20.` / `G84`) is alarm PS0203 and stays RAW (`FanucState.BeforeLineAlarm`), on the direct walk and on the D360 paths. B2, the inlined contour starts with motion, F, feed mode, plane and G90/G91 unknown and the cycle (G80), compensation and units pinned to the contour's values; VM 3.9 states the exception. B3, P names the first motion line (a bare label line in front of it gives its number), the contour's labels take free numbers of the O program through the new `FanucLabels.Allocate`/`Renumbered` (D277), the walk's own numbers are not counted twice for CMP318, CMP392 checks the written line. B4, a contour section that a CALL also names is written as an O program as well (`CompilerBase.IsCalled`). D158: SURFACE on a lathe X axis too (R minus twice the key on a diameter X, 0 without the key). D155: `FanucState.FunctionHolding` (empty at a program start, null after `ForgetWritten`) and `FanucFunctions.AllOf` through `FunctionChoice`.
  - Second review (B5, B6, D158): a later G70 writes the range's modes in front of it and forgets the range's codes, F and modes afterwards (`FanucContourRange.Modes`, `Forgotten`); the plane, the feed mode and G90/G91 are pinned to the contour walk and written in front of the G7x line where the program differs, so the range holds G00-G03, G04, axis words and F only. Gap closed 2026-10-27 (delegated): D278's "from an unknown state" cannot hold for those groups inside the range (B-64484EN-1 p.90; PS0325 p.91). Gap closed 2026-10-27 (delegated): D158's 0 is the Z face; a lathe X-axis cycle writes no SURFACE without the key, CLEARANCE less twice the key with it.
