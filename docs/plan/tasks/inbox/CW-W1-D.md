# CW-W1-D Cycle catalogs: schema and data

Phase: code wave 1 | Branch: `w1-d` | Depends on: nothing | Size: L

## Goal

The cycle-catalog half of the decisions of 2026-10-07 and 2026-10-27 (`.worktrees/codewave/PLAN.md`, W1-D): D114 (loader), D115, D140, D141, D156, D157, D162, D164, D177, D181, D270, D356, D358, D359, D361, D362, D377, D379, D380.

## Scope

- Core: `CycleEntry`, `CycleCatalog`, `DrillingFamily.*`.
- Config: `src/Ncx.Config/Cycles/*`.
- Data: `cycles/*.toml`, `cycles/README.md`; the `[[cycle]]` data of `mori-ntx1000-mapps.toml` and `nakamura-ntjx.toml` (both copies).
- Spec: machine-config 6 and 10, controller-mapping 5 and 8, siemens 7 and 12 rule 5, fanuc 6 and 9 rule 9, heidenhain 5 and 7 rule 7, language 4.7.1, 12-phase-2 P2-03.

## Log

- Claude (agent), 2026-10-27:
  - D157: `CycleEntry.RuleWords` is computed from the params and the new `CycleEntry.Family` for every entry; the for_axis lending of `CycleCatalog.Override` is gone. The Heidenhain reader's "built-in family" test (`HeidenhainCycles.Define`) now asks `DrillingFamily.Names` instead of the rule word, so that its behaviour does not change with the computed words.
  - D164, D358, D270: new entry keys `defaults` (native name to number or literal, kept as text) and `title`; `cycles/heidenhain.toml` and the built-in Heidenhain family carry `Q210=0` (200 and 203), `Q212=0`, `Q208=MAX` (203) and the four BOHREN.h titles. `CycleCatalogLoader.CheckHeldDistances` reports CFG156 and CFG158; the caller that reads `[cycles] chip_break_lift`/`peck_clearance` (W1-G, D357) has to call it.
  - D114: CFG159 for a params word of the machine-axis form.
  - D177: the Fanuc mill keeps `P` on G82, G84 and REAM; REAM takes the new key `dwell_native = "G89"`, which `FindNative` reads back. The lathe REAM writes `dwell_native = "G85"`.
  - D359: TAP_COMPENSATING entries on the Fanuc mill (G84) and lathe (G84, G88 along X); the lathe TAP along X gets `before = "M29 S{rpm}"` (B-64484EN-1 p.112: `M29 S` before G84 or G88). `CycleCatalog.FindNativeByBeforeLine` is the choice rule; `Find` hides an entry without a before key where an entry of its native sets `before = ""`.
  - D361, D377: entry keys `channel` and `available`, CFG157 in a catalog file, `CycleCatalog.ChannelEntries` and `ForChannel`; Mori `TAP` (G84 and G88) `before = "M329 S{rpm}"`, `SYNC_TAP` out of `[func]`; the Nakamura comment cites D377 and 4810004EDG-1 p.312.
  - D181, D356, D362, D379, D380: the full sl signatures from the cnc-manuals connector (SW4.92, pages in `cycles/siemens.toml` and siemens 7); PECK = FDPR; CYCLE83 `defaults` VARI = 0, which `FindNative` assumes for a left-out fixed value; CYCLE84 and CYCLE840 map no CYCLE_F (`CycleEntry.DerivedFrom` gives PITCH); DEEP_HOLE with 41 positions, `CYCLE_F = "S_FB"`, fixed `_AMODE = 1`, defaults `_AMODE = 0`; SLOT (SLOT1) and CIRC_SLOT (SLOT2); `DrillingFamily.IsoCoarsePitch` and `Cycle830DepthIsAbsolute` for the reader and compiler of W2-7. The Siemens compiler test of PECK flips from CMP571 to `MCALL CYCLE83(5,0,5,-40,,,5,,,,,1)`.
  - Gap closed 2026-10-27 (delegated): D177's REAM-to-G89 needs the catalog to know G89; the entry key is `dwell_native` (the native written for a CYCLE_DWELL other than 0, read back as the entry), in the form of `native`.
  - Gap closed 2026-10-27 (delegated): D157 gives AXIS to "every family"; it is a rule word of an entry that maps DEPTH and not AXIS, so turning cycles without a depth carry none.
  - Gap closed 2026-10-27 (delegated): D164 lists `Q210=0` "on 203"; cycle 200 has the same unmapped Q210 (BOHREN.h writes 0), so DRILL and DRILL_DWELL take `defaults = { Q210 = 0 }` too.
  - Gap closed 2026-10-27 (delegated): D358's CFG156/CFG158 run in `CycleCatalogLoader.CheckHeldDistances(catalog, setKeys, line, diagnostics)`; the key-to-native map is Heidenhain chip_break_lift to Q256, Siemens chip_break_lift to _VRT and peck_clearance to _DIS1.
  - Gap closed 2026-10-27 (delegated): D379's "fixed NUM/_DMODE/_AMODE" is `fixed = { NUM = 1, _DMODE = 1000, _AMODE = 2 }` on SLOT (plane digit 0 keeps the active plane); CIRC_SLOT fixes `NUM = 1` only. D380's ones digit is `fixed = { _AMODE = 1 }` with `defaults = { _AMODE = 0 }`, so the compiler writes an absolute `_DP` now and the reader converts the incremental form once W2-7 applies the digit rule.
  - Gap closed 2026-10-27 (delegated): D362's catalog mark is `CycleEntry.DerivedFrom(word)`; D361's per-channel pick is `CycleCatalog.ForChannel(channel)`; D359's choice rule is `CycleCatalog.FindNativeByBeforeLine`.
  - Found while doing it: the shipped DEEP_HOLE mapped CYCLE_DWELL to DTB, which on CYCLE830 is the dwell at every drilling depth; the dwell at the final depth is `_DTD` (SW4.92 p.1120), which the entry now maps.
  - Left for other packages: the CMP004 of D377 and the M29 default (W2-1, W2-4); the compilers' use of `defaults`, `title`, `DerivedFrom`, `dwell_native` (W2-4, W2-6, W2-7); the reader's digit rules of D379 and D380 (W2-7); the call of `CheckHeldDistances` once `[cycles] chip_break_lift` exists (W1-G).
  - Review fixes: the Fanuc compiler's `CheckCarried` treats `CYCLE_DWELL=0` as carried (D362, dropped silently), so a converted drilling program with `Q211=0` or `DTB=0` compiles to G81/G83 again; a dwell other than 0 stays with W2-4. The nested ternary in `CycleCatalog.CarriesFixedValues` and the tuple switch in `CycleCatalogLoader.HeldNative` are plain ifs. The Mori comment cites NTX1000SZM PX-NT 4-K0DEEN p.467 and p.472 for M329, the Nakamura comment 4810004EDG-1 p.148-149 for M429.
  - Gap closed 2026-10-27 (delegated): CIRC_SLOT fixes `NUM = 1` and `_DMODE = 1000`, the thousands digit 1 being "Funktionen SLOT2 ab SW 2.5" (SW4.92 p.1057), as for SLOT1; a SLOT2 without it stays RAW, so its geometry is never dropped (D379 (4), D5).

