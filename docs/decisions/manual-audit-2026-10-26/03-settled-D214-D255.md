# Manual audit 2026-10-26, part 03: settled D214 D255

Report of one audit agent, checked against the cnc-manuals connector (1283 manuals); kept as written. Page numbers are the connector's. The summary and the decisions it leads to are in `../manual-audit-2026-10-26.md`.

## Audit of D214–D255 (ninth-round entries) against cnc-manuals

**Result:** none of the 17 decisions changes. One stated fact is wrong: a side note in D247, where the record reverses the block-order rule. Most other findings are page drift.

**General note on pages:** two kinds of citation cannot be checked by page in the connector:
- **Fanuc German editions.** The record cites B-64484GE/02, B-64484GE-1/-2, B-64490GE/03 and B-64304GE-2/01. The knowledge base holds only the English twins (B-64484EN, B-64484EN-1/-2, B-64490EN-05, B-64304EN-1/-2). Wherever the record cites a GE page, I give the EN page.
- **iTNC 530 Klartext 340490-04 (2007).** It is listed, but read_pages returns nothing for it. D255's pages 251–253 can't be checked; the same text is in 606420-01.

---

### D214: HOLDS, CITATIONS TO FIX
- **TNC 640 CALL PGM** (called program runs to its end, then the caller continues; no M2/M30 allowed, FN 9 as substitute; Q parameters global): CONFIRMED, TNC640 Programmierung-Klartext 892903-18, p.252.
- **TNC 640 warning on M2/M30 in a called program, abort if END PGM is missing**: CONFIRMED, same manual, p.253.
- **Sinumerik: M2/M30 of a main program called as a subprogram act like M17, and the reverse**: CONFIRMED, 840D sl NC-Programmierung SW4.92 (2019), p.350. Also stated in Programmierhandbuch-Arbeitsvorbereitung 2013, p.184.
- **Fanuc M02/M30 end the main program and reset; parameter 3404 bits 5/4**: CONFIRMED in B-64484EN p.297. The record cites GE p.304.
- **Program-end table 13.2(d), with the "M02 M03" misprint and the /M02 /M30 /M99 caution**: CONFIRMED in B-64484EN p.328. The misprint is in the EN text too. The record cites GE p.333.
- **M99 in a main program returns to the program start**: CONFIRMED in B-64484EN p.331. The record cites GE p.337.

### D219: HOLDS
- **Mori Seiki NTX1000 tool spindle** (T9001 is a bare 4-digit tool number called to the change position; G361 exchanges it; T0 then G361 empties the spindle): CONFIRMED, Programmieranleitung NTX1000SZM PX-NT_4-K0DEEN, pp.313–314.
- **Mori Seiki turret 2**: CONFIRMED, same manual, pp.314–315.
  - The T word is tool number plus offset number, and tool number = turret station.
  - At spindle 2 the offset is +30: T0101–T1212 at spindle 1, T0131–T1242 at spindle 2.
- **Nakamura turret form T□□□□, T□□00 cancels**: CONFIRMED, Programmierhandbuch-1 4810004ECL-1, p.66.
- **Nakamura 6-digit form** (3+3 digits when the offset number exceeds 100 on a 120-tool ATC), switched by P5040 bit 1, P3032 and P3116 bit 6: CONFIRMED, same manual, p.69.
- **G340**: CONFIRMED, same manual, pp.326–328.
  - Step (12) is the T command that reads the offset.
  - T is always given; 4 digits, or 6 above offset 100.
  - A is 2 or 3 digits and needs a decimal point.
  - Example: G340 T0199. A0299.
- **G341 T with a bare magazine number**: CONFIRMED, same manual, p.332. I did not read p.331 (the G340 T0101. A02. example).

### D220: HOLDS, CITATIONS TO FIX
- **iTNC 530 CYCL CALL starts at the last programmed position**: CONFIRMED, Zyklen 670388-12, p.54.
- **iTNC 530 M99 and CYCL CALL POS**: CONFIRMED, same manual, p.55.
  - M99 travels to the block's position, then calls.
  - CYCL CALL POS needs 3 axes; its tool-axis coordinate acts like an extra datum shift.
- **TNC 640 call forms**: CONFIRMED, TNC640 Zyklen 892905-18, pp.64–66.
- **TNC 640 drilling example** (CYCL DEF 200, L X+10 Y+10 R0 FMAX M3, bare CYCL CALL, then M99 blocks): CONFIRMED, same manual, p.127.
- **Fanuc G81 "X_ Y_ = hole position" and the step-1 sentence "positioning only if commanded"**: NOT FOUND as cited, because the cited 0i-D German manual B-64304GE-2/01 is not in the knowledge base.
  - Searches run: "Positionieren des Werkzeugs an einer Bohrposition existiert", "positioning is performed if a command to position the tool at a hole position exists", G81 format queries on 0i-D.
  - Supporting text in the English 0i-D milling manual B-64304EN-2-01:
    - p.39–40: operation 1 of the cycle is positioning of X and Y.
    - p.52: drilling is skipped only in a block with no X, Y, Z, R or other axis. So a G81 block with only Z and R does drill.
  - The fact effectively holds; only the page is unconfirmed.

### D221: HOLDS, CITATIONS TO FIX
- **M99 Pn in a subprogram returns to block n of the caller**: CONFIRMED in B-64484EN p.331. P is ignored outside memory operation, and the return is slower. The record cites GE p.336.
- **/M99 Pn in a main program jumps to block n of the same program**: CONFIRMED in B-64484EN p.331. The record cites GE p.337.
- **Subprogram run on its own: M99 Pn returns to block n inside it**: CONFIRMED in B-64484EN p.332. The record cites GE p.337.
- Not in the record: B-64484EN p.1141 says M99 P with a sequence number is not allowed in DNC operation. This supports keeping it RAW.

### D222: HOLDS
- **Nakamura M427/M428** (machining mode R/L, given at program start, modal, none selected at power-on; issued automatically with M41/M91 and M441/M491 on WT/WY; no datum): CONFIRMED, 4810004ECL-1, p.133.
- **NTJX list** (G54 left spindle and G59 right spindle as a shop convention; M427 sub spindle, M428 main spindle): CONFIRMED, Super-NTJX Liste-M-G-Codes 2016, pp.1–3. I did not check p.5 (G54–G59 as coordinate systems 1–6).
- **Mori M303/M304** (select the target spindle per control system; kept until power-off; no datum): CONFIRMED, NTX1000SZM, p.285.
- **Doosan M34 / M134**:
  - M34 "Drehmodus links" is listed alone: CONFIRMED, Puma-2600SY M-Funktionen, pp.1–4.
  - M134 is NOT FOUND in that sheet's hit. It appears only as "right spindle select" in the Doosan MX-Series CNC-Programmierbuch, p.23.
- **Not checked** (supporting only): ECL-2 pp.69–70 (WT150 example) and Mori p.145.

### D226: HOLDS
- **Q204 takes effect only if programmed larger than Q200**: CONFIRMED, TNC640 Zyklen 892905-18, p.87 (cycle 200) and p.96 (cycle 203).
- **The same rule on TNC 128**: CONFIRMED, TNC128 Klartext 819494-15. Hits on pp.319, 321, 323, 325 and 338; I did not read each page.
- **English edition 892905-28**: NOT FOUND, not in the knowledge base. Search: the English sentence with lang EN; only the TNC 640 operation manual came back.
- **iTNC 530**: CONFIRMED, Zyklen 670388-12.
  - p.79: the retract goes to the clearance, or to the 2nd clearance "if entered"; no threshold is stated.
  - p.80: Q204 definition, range 0–99999.9999, PREDEF.
  - p.59: GLOBAL DEF 2nd set-up clearance.
  - So the record's own caveat stands: the iTNC 530 manual does not state what Q204 < Q200 does.

### D227: HOLDS, CITATIONS TO FIX
- **TNC 640 example: positioning block between CYCL DEF and CYCL CALL**: CONFIRMED, TNC640 Zyklen p.127. Same shape at p.89.
- **iTNC 530 example, blocks 11–14**: CONFIRMED, Zyklen 670388-12, p.80.
- **Prerequisites list (tool call before the cycle definition)**: CONFIRMED, iTNC p.53 and TNC 640 p.64.
- **M89 cancel list**: CONFIRMED for iTNC p.55 (M99, CYCL CALL POS, new CYCL DEF). The TNC 640 p.66 list is not identical: it names only M99 and CYCL DEF, without CYCL CALL POS. The record's "repeats the list on page 66" is slightly off. It does not matter here, since a tool change is not in either list.
- **Not checked**: p.278 (coordinate transformations modal).

### D228: HOLDS
- **MB definition** (retract path, incremental in the tool-axis direction, travelled before the swivel; MB MAX goes to just short of the limit switch): CONFIRMED in both manuals.
  - iTNC530 Klartext 606420-01, p.463.
  - TNC640 Klartext 892903-18, p.430.
- **MOVE / TURN / STAY split**: CONFIRMED.
  - iTNC p.462: TURN needs MB and F.
  - TNC 640 p.429: TURN needs only F.
- **PLANE RESET with MOVE or TURN positions the axes to the basic position**: CONFIRMED.
  - iTNC p.448: example 25 PLANE RESET MOVE ABST50 F1000.
  - TNC 640 p.413: same example with DIST50.

### D241: HOLDS, CITATIONS TO FIX
- **I, J, K are signed incremental vectors; I0, J0, K0 can be omitted; PS0020 with parameter 3410 is the only alarm named**: CONFIRMED in B-64484EN p.79. This matches the record's English page. The record also cites GE p.84.

### D242: HOLDS, CITATIONS TO FIX
- **Parameter 3402 bit 0: G00 or G01 at power-on/clear**: CONFIRMED in B-64490EN-05 p.191. The record cites GE/03 p.202.
- **Clear-state explanation, item (4)**: CONFIRMED in B-64484EN p.62. The record cites GE p.66.
- **G53** (moves at rapid traverse, one-shot, absolute only, ignored if incremental): CONFIRMED in B-64484EN p.201.
- **G53.2 as the feed variant, PS5372**: CONFIRMED in B-64484EN p.202.
- **Nakamura: G53 only at rapid and in absolute coordinates**: CONFIRMED, Nakamura Programmierhandbuch DE 2019, pp.53–54.

### D243: HOLDS, CITATIONS TO FIX
- **G28 words are the intermediate position in the absolute system (absolute or incremental programming); both moves at rapid; stored per axis**: CONFIRMED in B-64484EN pp.191–192. The record cites GE p.193.
  - p.191 adds: to return directly, use an incremental zero or an absolute command to the current position. This agrees with the decision.
- **Mori cautions** (G28 U0 V0 W0 instead of G28 X0 Y0 Z0; G330 instead of G28 A0): CONFIRMED, NTX1000SZM p.124.
- **Parameter 1020: U, V, W, H are incremental only for axes named X, Y, Z, C**: CONFIRMED in B-64490EN-05 p.59. The record cites GE p.62.
- **G code system A: absolute or incremental by address word, not G90/G91** (note 8): CONFIRMED in B-64484EN p.63.
  - Stronger support in B-64484EN p.225, note 3: incremental programming is not possible for axes named A or B under system A. This directly backs reading G28 B0 as an absolute word.
- **Nakamura example with G28 U0, G28 W0, G28 B0**: CONFIRMED, Nakamura DE 2019, p.111.

### D246: HOLDS, CITATIONS TO FIX
- **Lathe G68.1 Xp Yp Zp I J K R, cancelled by G69.1** (centre, axis direction, angle): CONFIRMED in B-64484EN pp.340–341. The record cites GE p.347.
- **Lathe G code list**: CONFIRMED in B-64484EN p.70. The record cites GE p.74.
  - G68.1 and G69.1 are group 17.
  - G68 and G69 are group 04 (double-turret mirror image / balance cutting).
  - The general format list at B-64484EN p.2394 agrees.
- **Mori G68.1** (X Y Z centre, I0 J1 K0 for the Y axis, R = 90 + B-axis angle, nesting with G43/G49): CONFIRMED, NTX1000SZM pp.175–176.
- **Not checked**: the 30i-A excerpt, 16i-TB B-63524GE, and Nakamura G358/G368.

### D247: HOLDS; ONE SUPPORTING FACT WRONG
- **A group 01 code in a canned cycle cancels it, same state as G80** (note 5): CONFIRMED in B-64484EN p.63. The record cites GE p.67.
- **To cancel, use G80 or a group 01 code**: CONFIRMED.
  - 0i-D milling B-64304EN-2-01, p.42.
  - 30i-B milling B-64484EN-2, p.59. The record cites GE-2 p.65.
- **A group 01 code with G81 in one block cancels G81**: CONFIRMED in 0i-D milling p.52. Same wording for G89 in 30i-B turning EN-1, p.309.
- **Nakamura WY-250L: G80 or a group 01 code cancels**: CONFIRMED, 4810004EEE, p.283.
- **Nakamura WM: group 01 cancel keeps R and the initial point**: CONFIRMED, Nakamura DE 2019, p.72.
- **WRONG: the Nakamura rule for a group 01 code and a cycle code in one block.** Source: 4810004EEE p.288, 14-7-6 item (2).
  - The manual says the cancel happens when the group 01 code is read, so the code written later wins.
    - Group 01 code first, cycle code after it: the drilling cycle runs.
    - Cycle code first, group 01 code after it: X/Z position in group 01 mode, R is ignored, F is only stored.
  - The record says the opposite ("cycle first: the cycle runs; group 01 first: the axes position…").
  - What it changes: not the decision (main stays). Only this caveat in the ANSWER, and any text copied from it into fanuc 6, fanuc 9 or controller-mapping 5, needs reversing.

### D251: HOLDS
- **FAUTO is the feed defined in the TOOL CALL block** (line-block dialog): CONFIRMED, iTNC530 Klartext 606420-01, p.205.
- **TOOL CALL F holds until a positioning block or TOOL CALL sets a new feed**: CONFIRMED, same manual, p.180.
- **PLANE: FAUTO is the feed from the TOOL CALL block**: CONFIRMED, iTNC p.462 and TNC640 Klartext p.429.
- **Feed may be entered in TOOL CALL and in every positioning block**: CONFIRMED, iTNC p.162.
- **Q206 accepts FAUTO**: CONFIRMED, TNC640 Zyklen p.89.
- **Not checked**: TNC 640 p.143.

### D252: HOLDS
- **TNC 640 table of block start / block end**: CONFIRMED, TNC640 Klartext 892903-18.
  - p.225 shows the table; its text layer loses the column for M8–M30.
  - The overview table at pp.608–609 confirms row by row: start for M3, M4, M8, M13, M14; end for M0, M1, M2, M5, M6, M9, M30.
- **iTNC 530 rule** (some M functions act at block start, others at block end, regardless of order; up to two per block): CONFIRMED, iTNC530 Klartext 606420-01, p.352.
- **iTNC 530 table**: present on p.353, but the text layer does not keep the columns, so it cannot be read there. The TNC 640 overview settles the assignment.
- **M140 acts at block start**: CONFIRMED, iTNC 606420-01 p.368.
- **M91/M92 act at block start**: CONFIRMED, iTNC DIN-ISO 606420-01 p.309. I did not open the Klartext page the record cites (p.355).
- **Not checked**: M104 (p.356) and M128/M129 (p.480).

### D254: HOLDS (point 2 still open, as the record says)
- **(1) Subprograms before M2/M30 run at least once without a call**: CONFIRMED.
  - iTNC530 Klartext 606420-01, p.273.
  - TNC640 Klartext 892903-18, p.247.
- **(2) CC with exactly one axis**: NOT FOUND, as the record already says. The manuals give only:
  - both plane coordinates;
  - no coordinates, which takes the last programmed position;
  - an incremental coordinate, relative to the last programmed tool position.
  - Sources: iTNC p.218 and TNC 640 p.158.
- **(3) A PATTERN DEF surface Z other than 0 acts in addition to Q203**: CONFIRMED.
  - iTNC Zyklen 670388-12, p.64, with POS1 (X+25 Y+33,5 Z+0).
  - TNC640 Zyklen, p.75 (POS and ROW). POS1 must be absolute.
- **TNC 640 p.74 rules**: CONFIRMED.
  - Between points the tool retracts to the greater of the spindle-axis coordinate at the call and Q204.
  - If the PATTERN DEF surface is higher than the cycle's, both clearances are computed from it.
- **p.73: use PATTERN DEF with tool axis Z only**: CONFIRMED.
- Note: the retract rule between points (the higher of the two) is not a plain shift of SAFE by the point's Z. That rule is NCX design and outside this audit, but worth a look when the RAW is lifted.

### D255: HOLDS, CITATIONS TO FIX (point 1 rests on a figure)
- **Cited manual 340490-04, pp.251–253**: NOT FOUND. The document is listed but returns no text, and fts searches on "Fasen-Abschnitt Länge der Fase CHF 12 F250" found no hits in it.
- **(2) A feed in a CHF block acts only there; the earlier feed applies again**: CONFIRMED in iTNC530 Klartext 606420-01.
  - p.216: CHF, same example 7–10, "do not begin a contour with CHF", the cut-off corner is not approached.
  - p.217: RND, same feed rule.
- **(3) Incremental coordinates count from the last programmed tool position**: CONFIRMED, 606420-01 p.93. The incremental CC rule is on p.218.
- **(1) The chamfer value is the leg along each line**: UNVERIFIABLE through the connector.
  - The text says only "Länge der Fase"; TNC 640 p.156 has the same text.
  - The record's proof is the dimensioned figure, which the text layer does not carry.
  - Supporting shop practice: Hermle iTNC 530 Grundkurs, p.15, programs L X+180 / CHF 10 / L Y+20. The drawing on p.13 is not readable as text.
- **Errors 354/355/356 "Fase zu groß"**: CONFIRMED, iTNC530 NC-Fehlermeldungen 2003, pp.85–86.

---

**Decisions that would change: none.**

Fixes the record still needs:
- **D247:** reverse the block-order caveat (Nakamura 4810004EEE p.288).
- **D214, D221, D241–D243, D246, D247:** Fanuc pages cite German editions that are not in the knowledge base; English pages are given above.
- **D226:** the English 892905-28 edition is not in the knowledge base.
- **D255:** the 340490-04 Klartext manual is unreadable in the knowledge base; cite 606420-01 pp.93, 216–218 instead.
- **D227:** the TNC 640 p.66 M89 list omits CYCL CALL POS.
