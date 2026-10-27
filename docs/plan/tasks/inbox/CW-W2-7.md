# CW-W2-7 Siemens cycles, reader and compiler

Phase: code wave 2 | Branch: `w2-7` | Depends on: W1-D, W1-G, W2-1 | Size: L (13 decisions)

## Goal

The Siemens cycle half of the decisions of package W2-7 of `.worktrees/codewave/PLAN.md`: D162, D164 (Siemens VARI defaults), D181 (with CMP576), D188 (Siemens marker), D224 (Siemens), D279 (Siemens, CMP575), D355 (FDPR), D356, D358 (`_VRT`, `_DIS1`), D362 (CMP574), D379, D380, D387 (N11), and the W2-7 lines of `gaps/W1-D.md`.

## Scope

- Reader: `src/Ncx.Readers/Siemens/SiemensCycles.cs`, the new `SiemensCycleForms.cs`.
- Compiler: `src/Ncx.Compilers/Siemens/SiemensCycles.cs`, the new `SiemensCycles.Positions.cs`, `DiagnosticCodes.Siemens.cs` (CMP574-CMP576).
- Core and data: `src/Ncx.Core/Machine/DrillingFamily.Siemens.cs`, `cycles/siemens.toml`.
- Spec: controllers/siemens.md 7, 11 rule 5, 12 rule 5; controller-mapping 5; 03-open-questions rows 104-106, 133, 134.
- Tests: the Siemens cycle tests of `Ncx.Readers.Tests` and `Ncx.Compilers.Tests`; the CYCLE83 line of the hand-written program in `Ncx.Acceptance/Examples/SiemensReaderTests.cs`.

## Log

- Claude (agent), 2026-10-27:
  - Tests first: 11 of the new or changed reader tests failed on the old code (direct call without CYCLE=OFF, CYCLE83 with FDEP/_DAM read as PECK, _DTD, _VRT, MPIT, CYCLE830 both depth modes and its message, SLOT1, D387). The new compiler tests use the new codes CMP574-CMP576 and did not build against the old code; the two PECK tests (D356) flipped.
  - D181: CYCLE_DWELL of PECK and CHIP_BREAK is `_DTD` (position 16, "Verweilzeit auf Endbohrtiefe", SW4.92 p.1086-1087), in `DrillingFamily.Siemens` and `cycles/siemens.toml`; a CYCLE83 that sets FDEP or a _DAM other than 0 stays RAW; MPIT of CYCLE84/CYCLE840 reads as the ISO 261 coarse pitch (`DrillingFamily.IsoCoarsePitch`), a size outside the table stays RAW; a CYCLE840 with ENC = 1 stays RAW where the entry does not fix ENC.
  - D164, D181: an empty VARI of CYCLE83 is 0 through `defaults` (FindNative, W1-D); the reader marker became a comment. The reader's message for a call no entry fits names the fixed values of the entries of that native instead of "VARI of CYCLE83".
  - D224: a direct call reads as CYCLE=, CYCLE_CALL and CYCLE=OFF; the compiler writes nothing for that CYCLE=OFF (test).
  - D279: `CYCLE:SIEMENS=n` without a catalog signature is the ERROR CMP575 instead of a positional call.
  - D355, D356: PECK is FDPR; without PECK or with PECK = 0 the compiler writes FDPR = |DEPTH - SURFACE|, or |DEPTH - CLEARANCE| without SURFACE.
  - D358: the compiler writes `_VRT` from `[cycles] chip_break_lift` on the CYCLE83 of VARI = 0 and `_DIS1` from `peck_clearance` where set (millimetres, divided by 25.4 under UNITS=INCH); the reader carries `_VRT`/`_DIS1` without the WARNING where they equal the key, or the control's own value (_VRT 0 or 1 mm, _DIS1 0) where the key is left out.
  - D362: the compiler checks the words of the CYCLE block once, at the block: CMP574 for a CYCLE_F that TAP and TAP_COMPENSATING derive from PITCH, nothing for CYCLE_DWELL=0 or PECK=0 without a position, CMP571 otherwise; rule words (CYCLE_RETRACT, AXIS) are skipped at the calls.
  - D379: the reader converts SLOT1 only in the sl form (fixed `_DMODE` 1000, NUM empty/0/1, RAD and INDA empty or 0, in a direct call `_CPA`/`_CPO` the position reached; under MCALL the positions come from the blocks, Bedienhandbuch Drehen 840D sl/828D 2013, connector p.412-413, "nur bei Einzelposition"), the `_AMODE` ones digit 0, 1 or 2 (1 incremental); the compiler writes SLOT1/SLOT2 as a direct call at each position after the G0 motion, with `_CPA`/`_CPO` the call position.
  - D380: the reader takes CYCLE830 with `_AMODE` 0, 1 or omitted, DEPTH = `_DP` for 1 and RFP less |`_DP`| for 0 or omitted (`SiemensCycleForms.DepthIsIncremental`); the compiler writes the fixed `_AMODE` 1 with an absolute `_DP` and S_FB at position 18 (test).
  - D387: a literal F on a position block under MCALL of a cycle that takes the modal F writes the CYCLE block again with that CYCLE_F before the CYCLE_CALL and keeps ControlFeed; the compiler writes a CYCLE_F that changes inside the armed range as the F of the position block (`X20 F200`), so the round trip closes (N11, tests on both sides).
  - D188: the compiler marker became a comment (the ERROR CMP572 stood already).
  - Gap closed 2026-10-27 (delegated): CMP576 (reserved for "D181 ENC") is the WARNING for the positions the control needs that no NCX word fills, LENG, WID, FFP1 of SLOT1, AFSL, WID, RAD, FFP1 of SLOT2, _SDAC and _SV1 of CYCLE830 (SW4.92 p.1053, 1055, 1121), once at the CYCLE block; the ENC case of TAP_COMPENSATING's CYCLE_F is CMP574 (D362), as the gap note asks.
  - Gap closed 2026-10-27 (delegated): D387's re-emitted CYCLE block is left out where the F equals the CYCLE_F the cycle has; N11 closes with the F on the position block, no new MCALL.
  - Gap closed 2026-10-27 (delegated): SLOT1/SLOT2 are written as direct calls at each position (not MCALL), since `_CPA`/`_CPO` count only for a single position.
  - Gap closed 2026-10-27 (delegated): the SLOT1 `_AMODE` ones digit 1 ("Z1 (ink)", SW4.92 p.1054) reads with the incremental rule of D380.
  - Left: cycles/README.md needed no change for these decisions; a CIRC_SLOT test of its own (it runs the SLOT code path).
