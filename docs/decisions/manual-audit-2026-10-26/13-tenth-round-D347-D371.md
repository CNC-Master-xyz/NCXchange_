# Manual audit 2026-10-26, part 13: tenth round D347 D371

Report of one audit agent, checked against the cnc-manuals connector (1283 manuals); kept as written. Page numbers are the connector's. The summary and the decisions it leads to are in `../manual-audit-2026-10-26.md`.

Audit of D347–D371 (rationale.md, cam-release1-drafts worktree) against the cnc-manuals connector. Read-only; no file edited.

## Summary of changes the manuals force

1. **D361 – FACT WRONG (biggest finding).** The Nakamura "M429 = rigid tapping on the lower path" fact is contradicted. In the Nakamura manual M429 is the work-counter increment. The proposed `[[cycle]] channel = 2, before = "M429 S{rpm}"` would therefore write a work-counter M code where a tap is meant.
2. **D351 / D352 – NEWLY SETTLED.** The manuals give the native WORKPIECE() order for every shape. The Siemens `[stock]` templates can now be written exactly, but the BOX form does not take two corners, so the template must compute values.
3. **D368 – FACT WRONG in the reasoning.** Fanuc does fix which way the precision levels run. The maintainer's "no" still stands.
4. **D349 – FACT WRONG (labels only).** On the 30i-B, G61–G64 are modal group 15, not group 13. G61.1 does not appear on the 30i-B.
5. **D357 / D358 – partly wrong.**
   - The 0i-F Plus has a programmable `,D` clearance on G73/G83.
   - On the TNC 640, the re-entry after the chip-break retract uses Q256.
   - Siemens manuals disagree on what `_VRT = 0` means.

## D347 – HOLDS
No controller facts.

## D348 – HOLDS
No controller facts.

## D349 – FACT WRONG (labels; the decision stands)
- **Fanuc group number: WRONG.**
  - 30i-B G-code lists put G61 exact stop, G62 corner override, G63 tapping mode and G64 cutting mode in group **15**, on both the M and T series (30i-B Bedienung-Fraesen p.27–28; 30i-B Bedienung-Drehen p.27; 30i-B Bedienung-Drehen-Fraesen p.66).
  - Group 13 is G96/G97 on M.
  - This affects fanuc.md 3, the comment at FanucBuilder.cs:320 and the draft text. The modal split itself is unaffected.
- **G64 as the power-on state: CONFIRMED.** Cutting mode G64 is set at power-on or system clear (30i-B Bedienung-Drehen-Fraesen p.181). This supports a start value.
- **G61.1: NOT FOUND.**
  - It does not appear in the 30i-B G-code list.
  - An exact-code search ("G61.1", Fanuc) returned no hits.
  - The TOLERANCE row that "keeps G61.1" rests on an unverified code.
- **Siemens groups: CONFIRMED.** Group 10 holds G60, G64 and G641–G645, with G60 as the standard default. Group 11 is G9 (blockwise). Group 12 is G601–G603 (840D sl Programmierhandbuch-NC-Programmierung SW492 p.1230). G64/G641–G645 switch G60 off (same manual p.297).
- **iTNC 530 M90: CONFIRMED in part.**
  - M90 smooths corners at constant path speed (iTNC530 Benutzerhandbuch-Klartext 2005 p.267).
  - "Needs following-error mode" and "dropped on the TNC 640" were not searched; they rest on D286.
- **CYCLE832 touching group 10: NOT FOUND.** Searched "CYCLE832 G642 G64 G60".

## D350 – HOLDS
No controller facts beyond D349.

## D351 – NEWLY SETTLED BY THE MANUALS
- **iTNC 530 BLK FORM: CONFIRMED.**
  - Cuboid only, sides parallel to X, Y and Z.
  - MIN point absolute; MAX point absolute or incremental.
  - Needed only for the graphic test.
  - Source: iTNC530 Programmierung-Klartext 606420-01 p.95–97.
- **TNC 640 BLK FORM: CONFIRMED.**
  - `BLK FORM CYLINDER` takes axis, D/R, L, an optional DIST and an optional DI/RI. It has no X/Y centre.
  - `BLK FORM ROTATION` takes DIM_D/DIM_R and LBL.
  - Source: TNC640 Programmierung-Klartext 892903-18 p.91–93.
- **Siemens blank in the active zero offset: CONFIRMED.**
  - A blank defined in the program always refers to the currently valid zero offset (840D sl NC-Programmierung SW492 p.1021).
  - It is created in the active zero offset and does not move when another offset is selected (840Dsl-828D Bedienhandbuch-Fraesen 2013 p.211).
  - Main programs should start with a swivel to the basic position before WORKPIECE (Bedienhandbuch-Drehen 2013 p.479).
- **WORKPIECE() parameter order: NEWLY SETTLED** (SW492 p.1021–1023).
  - The syntax is `WORKPIECE("WP","RefP","ZeroOffset","Type",Par5…Par12)`, alone in its block.
  - Definitions persist over reset and are cleared at power-up.
  - Every shape starts with Par5 = bit parameter, Par6 = Z0 reference point, Par7 = Z1 length, Par8 = ZB machining dimension. Then:

| Shape | Par9 | Par10 | Par11 | Par12 |
|---|---|---|---|---|
| CYLINDER | d0 (outer diameter) | — | rotation | — |
| PIPE | d0 | wall / inner diameter d1 | rotation | — |
| RECTANGLE | W | L | rotation | — |
| BOX | X0 | Y0 | X1 | Y1 |
| N_CORNER | number of corners | width across flats | rotation | — |

  - Bits 4–7 switch X1, Y1, Z1 and ZB between incremental and absolute.
- **What this changes in D351:**
  - A BOX blank is given by Z0 (the top) plus a length Z1, not by min/max Z. X1/Y1 are incremental unless bits 4/5 are set.
  - The `[stock]` BOX template needs computed values, for example bits 112 with `{max_z}` as Z0 and absolute `{z}`. The manual's own example uses 112.
  - The CYLINDER form has no X/Y centre, so CMP013 for an off-centre cylinder is correct.

## D352 – HOLDS, one premise weakened
- **"Eleven, twelve and thirteen arguments for BOX": CONFIRMED as printed, but they are misprints.**
  - 11 arguments: Bedienhandbuch-Fraesen 2013 p.211.
  - 12 arguments: 840D-sl Programmierung BS.000508 p.77.
  - 13 arguments, with an extra comma before "BOX": Bedienhandbuch-Drehen 2013 p.479.
  - The syntax table allows exactly Par5–Par12, so BOX always has 12 arguments.
- A later reader can now tell what to expect, so the argument against reading the blank in is weaker. Keeping the readers RAW in release 1 still stands.
- The other facts (TNC DIM_D/LBL, RI, PIPE, N_CORNER, blank only for the graphic) are CONFIRMED above. The iTNC 530 can cancel the dialog (p.97).

## D353 – HOLDS
- **Heidenhain F: CONFIRMED.** F is the speed of the tool centre on its path (iTNC530 Benutzerhandbuch-Klartext 2005 p.162).
- **Siemens path feed: CONFIRMED.** "Im Regelfall" it is made of all geometry axes taking part and refers to the cutter centre (840Dsl-828D Programmierhandbuch-Grundlagen 2013 p.104).

## D354 – HOLDS; delivered state still open
- **1403 bit 5 HTG: CONFIRMED.**
  - Data type is bit path.
  - 0 = feed along the tangent of the arc; 1 = feed along the axes including the linear axis.
  - Sources: 30i-B Bedienung-Drehen-Fraesen p.2072; 0i-D Connection Manual Vol1 p.474.
  - 0 is the case explained first, as the draft says.
- **FGROUP: CONFIRMED.**
  - The helix example has X/Y as path axes and Z synchronous.
  - After `FGROUP()` the basic state from a machine datum applies.
  - Source: Programmierhandbuch-Grundlagen 2013 p.103–106.
- **Parameter 6030: CONFIRMED.** It holds the M code for external subprogram calls; M198 when 0 (30i-B Parameterhandbuch p.348).
- **Delivered state of 1403#5: NOT FOUND.** Searched "1403 HTG …" in the parameter manuals.

## D355 – HOLDS
- **FDPR counts from the reference plane: CONFIRMED.**
  - Glossary: first depth relative to the reference plane (840D Programmierhandbuch-Zyklen 2006 p.373–375).
  - Mask: first depth relative to Z0 (Bedienhandbuch-Fraesen 2013 p.311).
- **CYCLE83 signature: CONFIRMED** (SW494 EN p.1072).
- **Q202 counts from Q203, with re-approach at Q200 above the depth: CONFIRMED** (iTNC530 Zyklen 670388-12 p.87–90).
- **Fanuc G83 Q from point R: CONFIRMED** (30i-B Bedienung-Fraesen p.68–69).
- **`_DAM = 0` means equal infeeds: NOT FOUND.**
  - The manuals give only the sign convention (>0 amount, <0 factor; SW494 p.1074, Arbeitsvorbereitung 2013 p.646) and "DF = 100 % keeps the infeed" (Bedienhandbuch-Fraesen p.311).

## D356 – UNVERIFIABLE
- **First depth through FDEP or FDPR: CONFIRMED** as the parameter definitions.
- **What CYCLE83 does with both empty: NOT FOUND.**
  - Searches: "CYCLE83 FDEP FDPR _DAM", "FDPR" (fts), "first drilling depth FDEP FDPR …" (EN), the semantic query on degression, and a cycles-doctype query.
  - The full 2006/2008 Zyklen manual bodies are not readable in the connector.

## D357 – FACT WRONG in part
- **5114 / 5115: CONFIRMED.**
  - 5114 is the G73 return value; 5115 is the G83 clearance.
  - Sources: 0i-F Parameter p.286–287; 30i-B Bedienung-Fraesen p.60 and p.69.
- **"Not programmable on Fanuc at all": WRONG for the 0i-F Plus.**
  - There, `,D` programs the clearance. Without it, 5114 applies for G73 and 5115 for G83 (5174 for small-hole G83) (0i-FPlus Bedienung-Fraesen p.40).
  - It holds for the 30i-B: B-64484EN names only the parameters (p.56–61).
- **Siemens absent `_DIS1` = computed by the cycle: CONFIRMED** (SW494 p.1074; Arbeitsvorbereitung 2013 p.646).
- **Siemens `_VRT = 0` = 1 mm: CONFIRMED in the programming manuals** (SW494 EN p.1072; Arbeitsvorbereitung 2013 p.646).
  - But the 2013 Bedienhandbuch-Fraesen p.312 says V2 = 0 means no retract and a one-revolution dwell. The manuals disagree.
- **Heidenhain peck re-approach = Q200: CONFIRMED on the iTNC 530** (cycle 203, Zyklen p.87).
  - On the TNC 640 with chip breaking, the re-entry after the full retract goes to Q256 above the last depth (TNC640 Zyklen 892905-18 p.97–98).
- **Siemens default and Fanuc numbers:** a Fanuc key "a Heidenhain file never needs" holds for the iTNC only. The Siemens default is ambiguous.

## D358 – FACT WRONG in part
- **iTNC 530 Q256 input range 0.1–99999.9999: CONFIRMED** (iTNC530 Zyklen 670388-12 p.90).
- **TNC 640 Q256 range from 0: CONFIRMED** (TNC640 Zyklen p.101).
- **`_VRT` at position 15 and `_DIS1` at 17: CONFIRMED** (SW494 p.1072–1073).
- **Q208 as retract feed: CONFIRMED** (TNC640 Zyklen p.101).
- **"Neither G73 nor G83 has an address": TRUE for the 30i-B, WRONG for the 0i-F Plus (`,D`).** A Fanuc compiler could write `,D` from the key on such a control.

## D359 – HOLDS
- **M29 and the three ways into rigid mode: CONFIRMED.** M29 S before the block, M29 in the block, or G84 always rigid when parameter 5200#0 = 1 (30i-B Bedienung-Fraesen p.103; 30i-B Parameterhandbuch p.303).
- **Cycle 206: CONFIRMED.** No pitch parameter; Q206 feed with F = S × p (iTNC530 Zyklen p.112).
- **Cycle 207: CONFIRMED.** Q239 pitch, no Q206/Q211, feed computed from the speed (p.114–115).
- **CYCLE840: CONFIRMED.** The pitch comes from MPIT/PIT only for ENC 0, 20 and 11; ENC 1 takes it from the programmed feed (SW494 p.1116).

## D360 – HOLDS
- **PS0203: CONFIRMED.** An S command or axis movement between M29 and G84 raises it (30i-B Bedienung-Fraesen p.104 and p.108).
- Whether an M98 in between is legal: NOT FOUND.

## D361 – FACT WRONG
- **Mori Seiki `M329 S_` before G84: CONFIRMED** (NT-NTX Programmieranleitung NTX1000SZM 2011 p.467 and p.472).
- **Nakamura M29 / M429: WRONG.**
  - M29 is the rigid tapping mode (Super NTJX Programming-Manual-1 4810004EDG-1 p.310–312).
  - M429 is "work counter data increment" (same manual p.79 and p.148–149; also WY-250L and NTY3).
  - With WTW/WTS, spindle rigid tapping is valid only for the upper turret (p.312).
  - No manual shows a lower-path rigid tapping code.
- **What it changes:** drop the M429 channel-2 table, or re-source it from the machine. The `channel` key loses its only case. Either way the answer needs a rule for a path with no rigid tapping.
- **Additional fact:** on the 30i-B, the rigid tapping M code itself is parameter 5210 (30i-B Parameterhandbuch p.307). This supports a per-machine `before`.

## D362 – HOLDS
Same 206/207/CYCLE840 facts as D359, with pages as above.

## D363 – HOLDS
No controller facts.

## D364 – HOLDS
- **Fanuc 3410 / PS0020: CONFIRMED** (30i-B Bedienung-Drehen-Fraesen p.79–80; Parameterhandbuch p.196).
- **iTNC MP7431 range 0.0001–0.016 mm: CONFIRMED**, with error 357 (iTNC530 Programmierung-Klartext p.669; Parameter-Alarme p.86).
  - The 0.005 default and circleDeviation 200901 were not checked.
- **Siemens MD21000 / MD21010 and alarm 14040: CONFIRMED** (840D-sl Parameter Alarme p.216).

## D365 – HOLDS
No new control facts checked.

## D366 – HOLDS
- **Heidenhain angle resolution: CONFIRMED.** Input resolution and display step reach 0.0001° on angle axes (iTNC530 Benutzerhandbuch-Klartext 2004 p.629–630). It is set per axis by MP7290.
- **Siemens MD10210: CONFIRMED.** Default 1000 increments per degree (Funktionshandbuch-Grundfunktionen 2012 p.333).

## D367 – HOLDS
- **19581 (nano-smoothing tolerance): CONFIRMED** (30i-B Bedienung-Drehen-Fraesen p.128).
- **11682 / 11683 and rotary 11684 / 11685: CONFIRMED** (30i-B Parameterhandbuch p.654–655).
- **G05.1 / G08 / G05 in a block of their own: CONFIRMED** (30i-B Bedienung-Drehen-Fraesen p.636).
- **The G05.1 Q1 format carries no tolerance value: CONFIRMED** (p.635).

## D368 – FACT WRONG (the answer stands; the record changes)
- **`G05.1 Q1 [R]` with level 1–10: CONFIRMED** (p.635).
- **Option needed: CONFIRMED.**
- **Level kept after the mode is cancelled: CONFIRMED** (0i-D Connection Manual Vol1 p.261).
- **Reset defaults: CONFIRMED.** 11687 is the precision-level default for AI contour and 11686 the smoothing default; 0 keeps the level, 1–10 forces it at reset or power-on (30i-B Parameterhandbuch p.655).
- **"The control fixes no direction": WRONG.**
  - Fanuc defines level 1 as emphasis on velocity and level 10 as emphasis on precision (30i-B Bedienung-Drehen-Fraesen p.1677–1678; 0i-D Connection Manual p.262–263, parameters 13610/13611).
- **"Two ends 11682/11683": WRONG for this R.** Those are the nano-smoothing tolerances. The AI-contour precision-level ends are acceleration and time-constant parameters (13610–13615).
- **Effect:** correct the "against" reasons in fanuc.md 4 and controller-mapping 1. The decision not to use R stands.

## D369 – part (1) HOLDS; the answer's question is UNVERIFIABLE
- **Indexed tools exist: CONFIRMED.**
  - iTNC 530: index up to MP7262 (0–9), programmed index passed to the PLC in W266 (iTNC530-HSCI Technisches Handbuch p.1600).
  - TNC 640: indices 1–9 with separate tool-life monitoring (TNC640 Technisches Handbuch 2021 p.3062–3063).
- **Whether `TOOL CALL 5.1` with tool 5 in the spindle runs the change macro: NOT FOUND.** The manuals point to the machine maker's PLC.

## D370 – HOLDS
No control facts.

## D371 – HOLDS
No control facts.

Note: the cycle 32.2-without-TA question from your list does not appear in D347–D371, so it was not searched.

## Entries whose draft or answer would change
- **D361:** the Nakamura M429 channel-2 table is wrong.
- **D351:** the Siemens `[stock]` templates must follow the settled WORKPIECE order (BOX: bits, Z0, Z1, ZB, X0, Y0, X1, Y1).
- **D352:** the "cannot tell how many arguments" premise falls away; the release-1 decision stands.
- **D368:** fix the level-direction and parameter-end statements in the record; the "no" stands.
- **D349:** Fanuc group 15 instead of 13 in the docs and comments; G61.1 is unverified in the TOLERANCE row.
- **D357 / D358:** the Fanuc `,D` address on the 0i-F Plus, the TNC 640 Q256 re-entry and the ambiguous Siemens `_VRT = 0` default all bear on the "family default" wording.
- **Still open:** D356 (empty FDEP and FDPR), D354 (delivered state of 1403#5) and D369 (indexed TOOL CALL).
