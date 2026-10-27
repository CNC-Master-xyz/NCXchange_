# CW-W2-6 Heidenhain reader and Heidenhain cycles

Phase: code wave 2 | Package: W2-6 (`.worktrees/codewave/PLAN.md` section 4) | Depends on: W1-D, W1-G, W2-1 | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The sixteen decisions of W2-6 on the branch: the reader halves of D115, D154, D155, D185, D213, D215, D225, D239 and D253, and the Heidenhain cycle rules of D157, D164, D188, D270, D355, D358 and D362, with D388 (1) settling Q204 = Q200 for CLEARANCE.

## Scope

- Reader: `src/Ncx.Readers/Heidenhain/*` (not `HeidenhainPlane.cs`), new `HeidenhainCycleMatch.cs`, `src/Ncx.Readers/DiagnosticCodes.Heidenhain.cs`.
- Compiler: `src/Ncx.Compilers/Heidenhain/HeidenhainCycles.cs`, `HeidenhainCycleValues.cs`, the D185 marker of `HeidenhainMotion.cs`, `src/Ncx.Compilers/DiagnosticCodes.Heidenhain.cs`.
- Spec: heidenhain 1, 2, 3, 4, 5, 7 rules 1, 2, 3, 6, 7, 8 rule 7; controller-mapping 1 (ORIGIN), 5, 6; machine-config 3 (D239); 03-open-questions rows.

## References

- `docs/decisions/decisions.md` rows D115, D154, D155, D157, D164, D185, D188, D213, D215, D225, D239, D253, D270, D355, D358, D362, D388.
- `.worktrees/codewave/PLAN.md` sections 2, 4 (W2-6), 6, 7.

## Done when

- Each decision is on the branch with its spec sentences, its markers turned into comments and its open-question rows settled.
- `dotnet build NCXchange.sln -warnaserror`, `dotnet test NCXchange.sln` and `dotnet format NCXchange.sln --verify-no-changes` are green.

## Log

- Claude (agent), 2026-10-27, branch `w2-6`:
  - Tests first, run against the old code with the new code files stashed (the diagnostic-code files kept so that the tests compile): 24 new or changed tests failed there, among them every compiler test of D157, D164, D188 (the message names D188 now; the ERROR was already CMP104), D270, D358 and D362, the reader tests of D164, D154, D155, D185, D213, D215 and D253, and the acceptance BOHREN compile (two CMP105 against one).
  - D185 (reader): `LN ... FMAX` under M128 reads as RAPID with TX TY TZ (NX NY NZ). The compiler marker `HeidenhainMotion.cs:55` is a comment.
  - D213 (reader): `HeidenhainLabels` finds every FN jump between `LBL n` and `CALL LBL n REP k` whose target, JUMP=END included, lies outside that part; the jump carries the WARNING RDR303. The compiler counter loop is W2-5's.
  - D215 (reader): CALL PGM drops a path (`\`, `/`, `:`) and a `.h`/`.H`; any other extension keeps the block RAW with RDR001.
  - D154, D155 (reader): `HeidenhainFunctions` asks `FindFunctionsByCode` through `FunctionChoice.Choose` with an empty context (one channel, no addressed spindle) and writes every state chosen, RDR020 included; an ORIENT state reads as ORIENT with the template's angle, or ORIENT at 0 with RDR021; a [workpiece] state gives WORKPIECE. The corner test that used M19 as a RAW table state now uses an RPM template `M19`.
  - D225, D239: markers turned into comments; D239's heidenhain 4 correction (the iTNC 530 606420 calls by name) and the machine-config 3 sentence (the tool-table row maps the name to the number).
  - D253: (a) the chain marker becomes a comment, behaviour as coded; (b) heidenhain 3 records the reset-in-reverse-order rule; (d) cycle 247 reads as ORIGIN only over SHIFT entries and an optional tilt, the tilt written again after ORIGIN with MOVE=STAY; MIRROR, ROTATE or an unknown chain keep it RAW. The (c) markers of the compiler stay TODO(question).
  - D164 (reader): new `HeidenhainCycleMatch` compares every Q parameter no word of the entry carries with what the compiler writes (block rules, Q256 from the key, `defaults`); not compared: Q213 of PECK/CHIP_BREAK, Q256 of PECK, Q205 while Q212=0. Any difference gives `CYCLE:HEIDENHAIN=n`. RDR301 is retired (`[Obsolete]`), so BOHREN.h reads without its four RDR301 (corpus report re-accepted); the reading of BOHREN.h is otherwise unchanged.
  - D164, D358 (compiler): Q210, Q212 and Q208=MAX come from the catalog's `defaults`; Q202/Q205/Q208-of-201 stay compiler rules; Q256 is `[cycles] chip_break_lift`, clamped to 0.1..99999.9999 (iTNC 530 Zyklen 670388-12 p.89-90) with the WARNING CMP121 where the cycle lifts at all.
  - D157, D188, D226/D388: CMP140 WARNING for CYCLE_RETRACT=SAFE with SAFE at or below CLEARANCE; CYCLE_RETRACT=SAFE without SAFE is CMP104 with a D188 reason; Q204 = Q200 for CLEARANCE stays (D388 corrects D157's Q204=0).
  - D362: CMP105 only where the value is derivable (`CycleEntry.DerivedFrom`), CYCLE_DWELL=0 and PECK=0 dropped silently, CMP122 ERROR otherwise, checked once at the CYCLE block before the values.
  - D270: `CYCL DEF n` takes the entry's `title`, a native cycle the title of the catalog entry of its number; the hard-coded names are gone. The tilde and dot conventions already hold.
  - D355: comment only; Q202 counts from the surface Q203 (iTNC 530 Zyklen 670388-12 p.87, 89-90), so PECK is written unchanged.
  - D115: marker already a comment on main; nothing left.
  - Gaps closed 2026-10-27 (delegated):
    - D213: RDR303 HeidenhainJumpLeavesRepeat (WARNING).
    - D362: CMP122 HeidenhainCycleWordLost; D358: CMP121 HeidenhainChipBreakLiftOutOfRange; D157: CMP140 HeidenhainSafeNotAboveClearance.
    - D358: without `[cycles] chip_break_lift` (no machine file) the compiler takes the VM's absent-key lift 0 (D357) and writes Q256=0.1 with CMP121; CMP121 is not reported for PECK, whose Q213=0 never lifts.
    - D164: RDR301 retired; the reader compares Q256 with the value the compiler writes: the key clamped to the range, divided by 25.4 under an INCH program (BEGIN PGM units), rounded to the [format] decimals of Q256 or else its four input decimals (`DrillingFamily.HeidenhainChipBreakLift`, shared by both sides).
    - D253 (d): cycle 247 with an unknown chain (after a RAW transform or a subprogram call) stays RAW too.
  - Review fixes: the shared Q256 helper with an INCH reader and compiler test; the cycle 247 markers of `HeidenhainChain.cs` and `HeidenhainChainArrivals.cs` become D253 (d) comments and open-questions row 18 (P3-04) is settled (the (c) markers stay); CMP104 and CMP122 summaries; a jump to a missing label no longer counts as leaving a repeat; a test for AXIS on BACK_BORE (CMP122).
  - Gate: build -warnaserror, test (filter `FullyQualifiedName!~TakesWellUnderASecond`) and format green.
