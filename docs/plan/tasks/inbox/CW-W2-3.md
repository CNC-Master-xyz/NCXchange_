# CW-W2-3 Fanuc compiler, non-cycle

Phase: code wave 2 | Branch: w2-3 | Depends on: W1-E, W1-G, W2-1 | Size: L

## Goal

The non-cycle decisions of the Fanuc compiler: D143 (CMP317), D146 (G7.1 letter), D169 (CMP316), D213 (counter clearing), D257 with D388 (3), D258, D276 (markers), D277, D280, D283, D339 with the Mori note of gaps/W1-E.md (`.worktrees/codewave/PLAN.md`, W2-3).

## Log

Claude (agent), 2026-10-27. The session restarted once; the work was resumed from the uncommitted worktree.

- D257, D388 (3): `FanucProgramNumbers` gives each program of the file its O number: NUMBER, else a NAME of up to eight digits (D249), else the lowest number from 1 that the file does not use (NUMBER, digit names and the numbers of CALLs), skipping 8000-9999, with the WARNING CMP310 that names it. CMP300 is retired (`[Obsolete]`, number not reused). Language 4.1 NUMBER, controller-mapping 1, fanuc 1.
- D283: `FanucUnits` writes G20/G21 (G70/G71 in system C) in a block of its own right after the O line, before the header and any setup, from the units of the program's first motion; a UNITS change after the first motion, or in a subprogram, is CMP313; main's mid-program G20/G21 and the units_default seeding of the header are gone. `NcComparer` accepts a units block in the compiled header that the source lacks (13 comparison rules), with a test. Expected strings of the CLI, job and Fanuc writer tests gained the G21 line. Controller-mapping 1, fanuc 3 and 10 rule 8.
- D277: a numeric label keeps its number, identifier labels and the conditional end follow above the greatest numeric label (main's numbering, markers replaced by citations); `FanucCompiler.WriteLine` checks that no N stands twice in one O program, labels and RAW lines with an N included (CMP318). Fanuc 1 and 10 rule 6, controller-mapping 6.
- D280, D213: `FanucRepeats` finds the REPEAT ranges of the file with their nesting depth (a subprogram called inside a repeat counts one deeper) and the variables the program uses; `FanucFlow.WriteRepeat` writes `#c = #c + 1`, `IF [#c LE k] GOTO n`, `#c = 0`; the program start clears every counter it needs; a JUMP that leaves a range clears its counter first, under the jump's condition (`IF [..] THEN #c = 0`). CMP314 without a counter for the depth, CMP315 where the program uses it. CMP304 is retired. A REPEAT's label is a jump target for the path checks and keeps its N rather than M99 (D210). Fanuc 7, controller-mapping 6.
- D339: `FanucFunctions` puts at most `m_codes_per_block` M codes (1 without the key) in a block; M00, M01, M02, M30, M98, M99, M198, the M wait codes of the [sync] mark range and the external call stand alone; with `m_codes_combinable` only listed codes may be the second or later code (an empty list: none); a spindle code (M3/M4/M5 and the CW/CCW/OFF of every spindle table) never shares a block with another M code (Mori NTX1000SZM p.253 note 2). Fanuc 1.
- D258: `FanucToolWords.FamilyCode` falls back to G96 S{value}, G97, and G50 S (system A) or G92 S (B, C, mill) where the role's table lacks VC, CSS_OFF or RPM_MAX; the CSS_OFF patch of `NakamuraJobCompileTests` is removed (its B half stays a TODO(question), D243). Machine-config 5 after [spindle.SUB], controller-mapping 4, fanuc 4 and 5.
- D146: G7.1 names the rotary axis of the workpiece holder (the `[[resource]] axis` or a rotary axis it owns), and the C word under CYLINDER is written with that letter. Fanuc 4.
- D143: an ARC with CENTER under CYLINDER is CMP317 and writes nothing else. Fanuc 4.
- D169: `==`/`!=` with a variable that holds a string is CMP316. Fanuc 7.
- D276: the five "TODO(question) of OutputFormat" references in FanucToolWords, FanucProgramFrame and FanucCompiler cite D276. The references in FanucMotion.cs are W2-1's file.
- D215: the FanucFlow.WriteCall marker is a citation: a name O+digits or digits is called by number, any other name has no Fanuc form.
- 03-open-questions: wave-3 rows 49, 61, 67, 69, 77, 78, 79 settled; row 175 records the CSS_OFF half as settled by D258.

Tests first: FanucProgramFrameTests, FanucRepeatTests and FanucFunctionsTests are new and name codes (CMP310, CMP313-318) that main lacks, so all of them failed there; the updated FanucFlowTests, FanucWriterRulesTests, FanucSystemATests, FanucToolPointTests, FanucToolWordsTests and the job tests failed on main on the G21 line, the REPEAT form and the M-code split.

Gaps closed 2026-10-27 (delegated):
- D277's uniqueness ERROR is CMP318: PLAN 2.1 reserved CMP312 for it, but main gives CMP312 to the bracket WARNING of D390.
- D339: a spindle code never shares a block with another M code, even one m_codes_combinable lists; D339 allows that pairing, but the only builder list of the documents (Mori NTX1000SZM p.253 note 2) forbids exactly it.
- D283: the units written are those of the program's first motion (or its end where it has none), so a UNITS before the first motion stands in the start block; a UNITS in a subprogram that changes the units is CMP313 as well, since the subprogram runs inside its caller.
- D280: the counters are cleared at the start of each program as well, since common variables survive a reset that stops a run inside a repeat.
- D257: the numbers a CALL of the file names count as used.

Review fixes, 2026-10-27:
- D213, D280: a RETURN inside a REPEAT range clears its counter before sub_end (`FanucFlow.ClearLeftRepeats`, now public), so the next call runs every pass. A conditional JUMP=END in a subprogram is always CMP302 and writes no clearing: before, the subprogram took an end label and wrote `IF [..] GOTO n` to an N that no line carried.
- D339: the functions of a block are collected first (`FanucBlock.Functions`) and placed by `FanucFunctions.Place`: from the first function that does not fit, it and every later one stand on lines of their own in order; where the block moves, all of them stand on lines before it (`FanucBlock.LinesBefore`, language 5 rule 3). Spindle stops and coolant OFF come before the named functions, spindle starts after them. Fanuc 10 rule 5.
- Mori data, both copies: TAILSTOCK M25/M26 (NTX1000SZM p.252, p.261-262); [spindle.TOOL] M13/M14 with M5 (p.258, p.324); M25/M26 out of m_codes_combinable (p.262 note 1).
- Code guidelines 10.2: `FanucRepeats.Of` takes the machine and its templates instead of a Func; the variables the program uses moved to `FanucRepeatVariables.cs`; no AsSpan (an M code and a variable index are read digit by digit). V0 is #0, never "#".
- The Fanuc README names FanucUnits, FanucProgramNumbers, FanucRepeats, FanucRepeatRange and FanucRepeatVariables.
- Subprograms write no G20/G21: they run inside their caller and inherit its units (D283 names the start of each program).

Found: machines/mori-ntx1000-mapps.toml has `TAILSTOCK = { FORWARD = "M13", BACK = "M14" }`, while NTX1000SZM p.253 note 2 names M13 and M14 as spindle (tool-spindle) rotation codes and p.252 gives the tailstock as M25/M26. Fixed in the review round above. Still open: p.257 note 2 asks M10, M210, M11 and M211 to stand in a block without other commands, while p.252 lists them as multiple M code function codes; the list keeps them.
