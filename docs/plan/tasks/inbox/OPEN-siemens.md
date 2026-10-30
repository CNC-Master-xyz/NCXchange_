# OPEN-siemens: decisions D412-D425 (Siemens, core, config, analytics), 2026-10-30

Branch open-siemens, worktree `.worktrees/open-siemens`, from main 1ecacb6. Brief: `.worktrees/openitems/BRIEF.md`.

## Done

- D412: `SpindleSpeed.RpmOf` takes the end radius for HOME and RETRACT, like RAPID; VM 8 sentence.
- D413: comments of `AnalyzeCommand`/`AnalyzeJob`, architecture 10 names analyze after an ERROR.
- D414: `state_channels` on every function table (`FunctionTable.StateChannels`, `ForState`, `StateBinding`; `FunctionStates.Of` moved out of `ExpansionRules`); `ChannelBinding.BoundTableOf` and the reader's `FunctionChoice` bind per state; Mori NTX `state_channels = { OFF = 1 }`; machine-config 5, VM 3.8 rule 2a.
- D415: DMG `MAIN_CHUCK_RUNNING`/`SUB_CHUCK_RUNNING = { OPEN = "M433"/"M333" }`, the `OPEN_RUNNING` states removed; machine-config 5.
- D416: comment, VM 5 list cites D416 (the coolant channel was already listed with D130).
- D417: CMP504 WARNING for two RAW lines of one unit with the same N; siemens 1.
- D418: CMP596 is an ERROR where a RAW line stands before the next block that writes the dropped F/S, else the WARNING; siemens 12 rule 5.
- D419: comment, siemens 12 rule 8.
- D420 (revised by D430, see below): `SiemensRawCall.IsCall`; originally the next motion wrote the full frame (datum where the program set one, CYCLE800() where TILT_OFF exists, chain again, TRANS alone); file-level RAW frame keys include calls; siemens 11 rule 8.
- D421: reader writes the CYCLE800 entries again after a datum, TILT with MOVE=STAY; compiler holds the swivel (`HeldSwivel`) and writes neither CYCLE800() at the datum nor the restated entries; siemens 4. Cited 840D sl Programming Manual NC Programming 2020, connector p.305-306, p.653-654.
- D422: reader keeps every block from an MCALL it keeps RAW to the closing MCALL (or the unit end, whose M30/M17/RET stays the end) RAW; siemens 11 rule 5.
- D423, D425: comments; language 4.7 sentence.
- D424: G973 RAW word plus WARNING RDR511.
- D403 (Siemens reader part, asked for by the coordinator): `src/Ncx.Readers/ComputedNumbers.cs` copied byte for byte from open-hh (identical on open-fanuc). `SiemensNumbers.Of` and `SiemensNumbers.Round` delegate to it; `LeastDecimals`, `DecimalsOf` and the per-file `Decimals` helpers of SiemensArcs, SiemensCorners, SiemensPatterns, SiemensPolar and SiemensTilt are gone, so every computed number (arc centre, chamfer and rounding point, polar point, pattern position, CYCLE800 angle) keeps six decimals, trailing zeros dropped. Controller-mapping 1 (TILT row, CYCLE800) says so instead of "at least three decimals".
- Expectations changed by D403: `SiemensMotionTests.ChfAndChr_AreExpandedIntoLines` and `SiemensReaderTests.Read_HandWrittenProgram_...` read the CHF=2 chamfer corner as 8.585786 / 1.414214 (38.585786 / 1.414214 in the hand-written program) instead of 8.586 / 1.414: 2 / sqrt(2) = 1.41421356..., which the old rule cut to the three decimals of the source words and D403 keeps to six. No other Siemens expectation changed; the corpus round trips are unchanged, since the compilers' [format] decimals decide what reaches the control.
- Stale S-3, S-8, S-9 (VM572 doc, VM 3.7 and VM 5 text), S-10; dangling refs in the Siemens, Core, Config, Analytics and Cli tests.
- 03-open-questions: rows W3-1, 91, 94, 137, 151, 160, 161 (case c), 189-192 set; six rows added at the end.

## Review fixes (after merging main, 2026-10-30)

- D422 performance: the RAW modal ranges of a unit are found by one walk per unit (`SiemensModalRanges`, cached in `SiemensUnits.ModalRanges`), and `NextInUnit`/`PreviousInUnit` step in constant time; 4000 lines with ranges read in well under a second (`Convert_LongProgramWithRawModalRanges_TakesWellUnderASecond`), where the look-back per block took 15 s.
- D430 (revises D420): `WriteFullFrame` and the datum/TRANS/CYCLE800() re-write are gone. After a RAW call the frame is unknown; the next frame word of the program is written in its replacing form (the chain from its first entry with TRANS), and an entry the compiler cannot restate is the WARNING CMP538 instead of the ERROR CMP531. `SiemensRawCall.IsCall` is false for lines that name a frame instruction or variable, for TCO*/TOROT*/TOFRAME*/PAROT*, and for a label alone. The reader leaves the chain unknown after a RAW call (`SiemensCalls.CallsAProgram`). The D420 tests are replaced by D430 tests (DREH, NEWCOORD, L100, CALL, AROT RPL=30, SCALE X2 Y2, TOFRAME, TCOFR, LABEL:, SHIFT X={$R43} with TI_ON(1)).
- D424: see the corrected gap note below.
- CMP700 (and the job compiler's move messages) name the state that `state_channels` binds, "[spindle_sync] OFF", not the whole table.
- `SiemensRawCall.cs` is in the Siemens compiler README.

## Left over

- D422: a subprogram that arms an MCALL without a closing MCALL leaves it armed in the caller after its return (NC Programming 2020 p.528-529); the reader's ranges end at the end of the unit and do not carry into the caller. Not blocking.

## Gap choices

- D415: no VM validation rejects a chuck-open word while the spindle turns, and none was added (a `requires` rule would insert a spindle stop, which the decision does not ask for).
- D417: the numbering already passes over every RAW N, so a generated N never equals one; the WARNING covers two RAW lines with the same N in one unit.
- D418: "the next block that writes it" is read as the next motion, CYCLE_CALL, CALL or CYCLE=OFF of the section.
- D420/D430: the neutral list is the compiler's own (`SiemensRawCall`); SiemensCycles.AfterRaw keeps D343 (every RAW line makes the modal call unknown), the list is not shared with it. The D420 full-frame re-write was removed by D430.
- D422: an MCALL kept RAW because `Define` fails on its parameters is not detected as a range start (only subprogram MCALLs, names no catalog cycle has, and pattern ranges).
- D424: corrected after the review. The earlier note here, that an S after G973 still reads as VC and is "written back as the same S", was wrong: the compiler wrote it back as `G96 S1500`. Now the reader takes G973 as the feed type of group 15, so an S after it is RPM until the next code of the group (NC Programming 2020, connector p.98, p.101), and the compiler records a RAW G973 as the surface speed off with G95 and writes that RPM as S, with no G96 (round-trip test `RoundTrip_G973_IsWrittenBackWithoutG96AfterIt`). NCX itself still holds CSS=ON after G973, since it has no form for "off without the limit".
