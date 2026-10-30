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
- D420: `SiemensRawCall.IsCall`; after a RAW call the next motion writes the full frame (datum where the program set one, CYCLE800() where TILT_OFF exists, chain again, TRANS alone); file-level RAW frame keys include calls; siemens 11 rule 8.
- D421: reader writes the CYCLE800 entries again after a datum, TILT with MOVE=STAY; compiler holds the swivel (`HeldSwivel`) and writes neither CYCLE800() at the datum nor the restated entries; siemens 4. Cited 840D sl Programming Manual NC Programming 2020, connector p.305-306, p.653-654.
- D422: reader keeps every block from an MCALL it keeps RAW to the closing MCALL (or the unit end, whose M30/M17/RET stays the end) RAW; siemens 11 rule 5.
- D423, D425: comments; language 4.7 sentence.
- D424: G973 RAW word plus WARNING RDR511.
- Stale S-3, S-8, S-9 (VM572 doc, VM 3.7 and VM 5 text), S-10; dangling refs in the Siemens, Core, Config, Analytics and Cli tests.
- 03-open-questions: rows W3-1, 91, 94, 137, 151, 160, 161 (case c), 189-192 set; six rows added at the end.

## Gap choices

- D415: no VM validation rejects a chuck-open word while the spindle turns, and none was added (a `requires` rule would insert a spindle stop, which the decision does not ask for).
- D417: the numbering already passes over every RAW N, so a generated N never equals one; the WARNING covers two RAW lines with the same N in one unit.
- D418: "the next block that writes it" is read as the next motion, CYCLE_CALL, CALL or CYCLE=OFF of the section.
- D420: the neutral list is the compiler's own (`SiemensRawCall`); SiemensCycles.AfterRaw keeps D343 (every RAW line makes the modal call unknown), the list is not shared with it. The datum is written before the motion only where the program set an ORIGIN before.
- D422: an MCALL kept RAW because `Define` fails on its parameters is not detected as a range start (only subprogram MCALLs, names no catalog cycle has, and pattern ranges).
- D424: the reader's feed type is not changed by G973, so an S after it under a former G96 still reads as VC (written back as the same S).
