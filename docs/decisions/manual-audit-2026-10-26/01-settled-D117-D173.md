# Manual audit 2026-10-26, part 01: settled D117 D173

Report of one audit agent, checked against the cnc-manuals connector (1283 manuals); kept as written. Page numbers are the connector's. The summary and the decisions it leads to are in `../manual-audit-2026-10-26.md`.

## Audit of D117–D173 against the cnc-manuals connector

**Bottom line: no decision changes.** None of the 17 entries has a wrong controller fact that would alter what the code on main does. I found three small factual slips (D117, D118, D119), many page drifts, and three cited documents the connector cannot read.

**One cause behind most of the page drift.** The connector does not hold the German FANUC 30i-B manuals the record cites (B-64484GE/02, GE-1/02, GE-2/02 and the parameter manual B-64490GE/03). It holds the English editions: B-64484EN (turning/milling), EN-1 (turning), EN-2 (milling) and B-64490EN-05 (parameters).
- The turning volume EN-1 lines up with the record within about two pages.
- The combined volume B-64484EN drifts a lot. Every citation into it needs re-paging; the new pages are given below.

---

### D117 Axis words under SHIFT, TILT, TILT_AXIS, SETPOS — HOLDS, CITATIONS TO FIX
- **G52 origin is stated in the workpiece system; the block after G52 must be absolute: CONFIRMED.** B-64484EN p.216-217 (the record's page). 16/18-TB: the connector has B-63524EN-01, not GE/01; the passage is at p.133-134 there, not 130-131.
- **Nakamura writes G52 with U/W, which mean the same as X/Z: CONFIRMED with a slip.**
  - Source: Nakamura-Tome Fanuc Programmierhandbuch DE 2019, p.52-53. The Super NTJX and WY-series manuals say the same.
  - The manual prints `G52 U2.75 W1.75`, not `W1.25` as the evidence claims. `G52 U5.3 W3.5` is not in the extracted text.
  - The same page also remarks that G52 is not recommended on lathes.
  - The cancel form `U0./W0.` or `X0./Z0.`, "G50 overwrites active G52" and "next block absolute" are all confirmed.
- **System A `G50 U(u) W(w)`, under which the current position becomes (x+u, z+w): CONFIRMED.** B-63522GE-02 p.123.
- **T series: an incremental G50 IP shifts the system: CONFIRMED.** B-64484EN p.205; the record cites PDF 203 and 207. EN p.207 is G10 L2.
- **G68.2 X Y Z = origin (0 when left out), I J K = Euler angles, G53.1 after the G68.2 block: CONFIRMED.** B-64484EN p.889 (§22.6.1.1); the record cites 868, which is tool posture control in EN.
- **Haas: G52 is absolute only: CONFIRMED.** Haas VF Schulungsheft V1.3, p.4.

### D118 Number of arguments of each function — HOLDS, CITATIONS TO FIX (one minor slip)
- **Siemens SIN() etc., ATAN2(,): CONFIRMED.** 840D sl PGH 06/2019 p.448-449.
- **MINVAL/MAXVAL take exactly two arguments, BOUND three: CONFIRMED.** Same manual, p.431-432.
- **Fanuc macro function table: CONFIRMED, with a slip.**
  - The table is at B-64484EN p.508 (Table 16.5(a)); the record cites p.489-490 and §16.4.
  - One argument for SIN…ADP, POW[#j,#k], ATAN in its one- and two-argument forms: confirmed.
  - Slip: PRM is not two-argument only. `PRM[#j]` (one argument) also exists, as do the `/[#l]` forms. Harmless for NCX, which has no PRM.
- **The ATAN[1]/10 example raises PS1131: CONFIRMED.** EN p.509.
- **PS1131 is about bracket balance: CONFIRMED.** 30i-B Wartung B-64485EN p.796-797; the record cites the German alarm appendix p.2404.
- **Addition:** the same alarm list has PS1120 "ILLEGAL ARGUMENT FORMAT" for a wrong argument to ATAN or POW. It is still a program alarm, which fits the entry's point that the manuals do not decide parse-time versus evaluation-time.
- **TNC 640 FN 6/7 take one value, FN 8 LEN and FN 13 ANG take two: CONFIRMED.** Klartext 10/2019 p.275.

### D119 Values of ATAN2 and FRAC — HOLDS, CITATIONS TO FIX (one wrong detail)
- **Siemens ATAN2: four quadrants, angle referred to the 2nd value; ATAN2(30,-80) = 159.444: CONFIRMED.** HLL Training p.241-242 and PGH 06/2019 p.449.
- **Fanuc ATAN[#j,#k] = ATAN[#j]/[#k], point (#k,#j), NAT=0 gives 0..360 (225.0), NAT=1 gives -180..180: CONFIRMED.** B-64484EN p.509; the record cites 490.
- **Parameter 6004 bit 0 NAT: CONFIRMED.** Parameter manual B-64490EN-05 p.340-341; the record cites GE p.2117.
- **PS0119 for ASIN/ACOS outside -1..1 and for LN ≤ 0: CONFIRMED.** EN p.509.
- **PS0119 for an EXP overflow: WRONG.** EN p.509 gives PS0111 "OVERFLOW: FLOATING" for EXP. The decision is unaffected: it is still an error.
- **New, supports the decision:** EN p.509 says ATAN with #j=0 and #k=0 raises PS0119. So Fanuc itself treats ATAN2(0,0) as an error, which backs the recommendation against the C-library value 0.
- **TNC FRAC/INT wording, "INT rundet nicht", and the rounding example: CONFIRMED.** Klartext 10/2019 p.325 and 348; the softkey list is also on p.199.

### D122 Full circle and zero radius in the CENTER form of ARC — HOLDS, CITATIONS TO FIX
- **TNC full circle = same end coordinates; circleDeviation (200901) max 0.016 mm; smallest circle 0.016 mm: CONFIRMED.** Klartext 10/2019 p.159.
- **CR form needs two blocks for a full circle: CONFIRMED.** p.160.
- **Fanuc G02 I_ gives 360°, G02 R_ gives 0° and no movement, parameter 3410 / PS0020: CONFIRMED.** B-64484EN p.79-80; the record cites PDF 84.
- **Siemens: full circle with no end point (p.193); CR= and AR= cannot make a full circle (p.195, 197): CONFIRMED.**
- **Alarm 14040, first cause "start = end with radius programming": CONFIRMED.** 840D sl Parameter/Alarme BS.000503 p.216.

### D127 How long "directly after a HOME" lasts for SETPOS — HOLDS, CITATIONS TO FIX
- **Parameter 3700 CRF/NRF/CNM, PS0303, PS0224: CONFIRMED, with nuance.**
  - Source: B-64490EN-05 p.213-214; the record cites GE/03 p.225.
  - CRF (bit 0) actually enables reference position setting at an arbitrary position under Cs contour control. The PS0303 "at least once since switching into Cs mode" rule applies when that function is used.
  - NRF=0 makes the first G00 after the switch do a reference return by itself.
  - CNM covers only zero-travel commands.
  - None of this changes the decision.
- **Cs coordinate establishment needs at least one reference return after power-on: CONFIRMED.** B-64484EN p.1173; the record cites 1136.
- **NT Nurse example with a second G50 C0 after probing and G00 C#500: CONFIRMED.** NT Nurse p.303.
- **O9000 contents (M91; M27; G28 H; M28; M99): CONFIRMED.** Präzisionsdrehzentrum Programmierhandbuch-1, p.226.
- The p.292, p.215 and Muratec citations were not re-read.

### D131 Words covered by the C= check of VM 3.8 rule 4 — HOLDS, CITATIONS TO FIX
- **Nakamura: M91 must stand alone; the right/wrong table with G28H0 and G0H-10.: CONFIRMED.** Programmierhandbuch-1, p.225-227.
- **Order M91, G28 H0, G50 C0: CONFIRMED.** NT Nurse p.303.
- **Alarm 0197 "C-AXIS COMMANDED IN SPINDLE MODE" (a Cs movement while the switching signal is off): CONFIRMED.** B-64484EN p.2429; the record cites 2391. Also in Wartung p.778-779.
- **Monforts: M21 selects C-axis mode, then G28 C0; S/M3/M4/M5 go to the tool spindle: CONFIRMED.** Monforts AWZ C-Achse, p.4 and the examples on p.10 and 27-30.

### D133 Bare SKIP under a switch list — HOLDS, CITATIONS TO FIX
- **Fanuc: BDT1 = "/ or /1"; the 1 may be left out unless a block has several skips; //3 is wrong: CONFIRMED.** B-64484EN p.327-328; the record cites 331-332.
- **Siemens: / and /0 are level 1, /1 is level 2 … /9 is level 10; one level per block: CONFIRMED.** Grundlagen 03/2013 p.40, as cited. Also PGH 2019 p.53.

### D144 The form of n in CYCLE:<controller>=n — HOLDS, CITATIONS TO FIX
- **Mori NTX G83.5/G87.5/G83.6/G87.6: CONFIRMED.** NTX1000SZM manual p.451-452 and the NT-NTX 2017 manual p.456. I did not re-read the G-code list at p.80.
- **G71 is a turning roughing cycle in systems A/B and G73 in system C: CONFIRMED.** B-64484EN-1 p.28 (the record cites p.30).
- **Fanuc also uses G71 for a grinding cycle: CONFIRMED with a small discrepancy.** The record says the traverse grinding G71 is group 01. The EN table shows group 00; the group-01 entry in that block is G73 oscillation grinding. The extracted table is partly misaligned, so this is low confidence and does not matter for the decision.
- **G70/G71 = inch/mm in system C: NOT re-verified** (cited p.27; EN p.26-27 start at G32/G50).
- **Siemens HOLES1/2, LONGHOLE, SLOT1, POCKET1-4: CONFIRMED.** The cited Zyklen 01/2008 body is not readable in the connector; only its index pages are (they list HOLES1 92, LONGHOLE 112, POCKET1-4 132-151). Confirmed in the Index-branded 840D Zyklen 2004 instead.

### D147 Which of A, B, C resolve to the workpiece holder's axis — HOLDS
- **Mori: `G00 A_` gives the Z target of the ejector/headstock-2 slide: CONFIRMED.** NTX1000SZM p.566, also p.541.
- **Doosan 2600SY II: B travel 830 [1350] mm, rapid 30 m/min: CONFIRMED.** Spec manual p.86. Oddity: the lubrication page (p.200) heads its B item "B-Achse (Rundachse)". That is a mislabel in the document; the spec table clearly shows a linear slide.
- **Nakamura G359 on Z and B (NC sub spindle): CONFIRMED.** NT Nurse p.242.
- **DMG L703-L708 switching C2/C3/C4: CONFIRMED** through the index of the CTX/GMX ShopTurn3G V2.1 manual.
- **Hermle machines carry rotary A and C: CONFIRMED** via the Hermle CAM-Seminar, p.12. The M-code document itself was not re-read.

### D148 Start values of a callee's V1..V33 — HOLDS, CITATIONS TO FIX
- **Locals with no argument start as <null>: CONFIRMED.** B-64484EN p.448; the record cites 427.
- **A macro call changes the local-variable level, a subprogram call does not: CONFIRMED.** EN p.524; the record cites 505.
- **Levels 0-5, values saved at the call and restored at M99: CONFIRMED.** EN p.529; the record cites 511.

### D153 Padded placeholders against numbers of another width — HOLDS, CITATIONS TO FIX
- **Offset number = last 1/2/3 digits (parameter 5028); LGN in 5002; up to 8 digits by 3032: CONFIRMED.** B-64484EN-1 p.149; B-64484EN p.251-252.
- **Doosan "NC Programming for Turning Centers, Fanuc 30 Series" (T202/T200, T0021, lower two digits): NOT FOUND.** The document is in the connector's list, but read_pages returns nothing and searches for T0021, T0101/T0121 and "lower two digits" get no hits from it. It seems to have no text layer. The other Doosan manuals use six-digit T codes (T01001) and do not help.
- **Nakamura NT-IPS "T101 oder T0101": CONFIRMED.** p.27.
- **#10001-#10999 / #11001-#11999 with index 1..999: CONFIRMED.** B-64484EN p.453; the record cites 433.

### D160 Fanuc lathe drilling: a lathe catalog and an axis key — HOLDS
- **Lathe canned cycles: G80, G83/84/85 drilling along Z, G87/88/89 along X; the pairs differ only in axis: CONFIRMED.** EN-1 p.92-93 (Tables 4.3(a)/(b)).
- **Without Q a plain drilling cycle with rapid retract; RTR bit 2 of parameter 5101; G83.5/G87.5 and G83.6/G87.6 name the two behaviours: CONFIRMED.** EN-1 p.95-97 (the record cites 96 and 98).
- **G85/G89 return at twice the cutting feed: CONFIRMED.** EN-1 p.99 (the record cites 100).
- **G81/G82 only as FS15-T format codes; no G86: CONFIRMED.** EN-1 p.28 (the record cites 30).
- **Nakamura Table 14-1 with the Q footnote: CONFIRMED.** Programmierhandbuch-1, p.238.

### D161 NCX names of Fanuc G92 and G76 — HOLDS
- **G92 format and its one-pass, four-move nature: CONFIRMED.** EN-1 p.39-41.
- **G76 two-block format with P(m)(r)(a) Q(Δdmin) R(d): CONFIRMED.** EN-1 p.83-85.
- **CYCLE97 takes TDEP, FAL, NRC, NID, NUMT: CONFIRMED** in the Index 840D Zyklen 2004, p.316-317. The cited Zyklen 01/2008 p.351-352 is not readable in the connector; its index does list CYCLE97 at 351.

### D163 Heidenhain 200 and 203: which Q values select the NCX cycle — HOLDS
- **TNC 640 cycle 203, three behaviours on p.96/97/98: CONFIRMED.**
  - Without chip break, the tool retracts to Q200 after every infeed.
  - With chip break, it backs off by Q256 until Q213 is reached, then retracts at Q208.
- **Q213 "Anzahl Spanbrüche vor Rückzug", range 0..99999: CONFIRMED.** p.100.
- **Q256 range: as stated.** TNC 640 gives 0.000 to 99999.999; iTNC 530 gives 0.1 to 99999.9999 (p.89-90), which is the value the record attributes to iTNC.
- **iTNC 203 step 3 "Falls Spanbruch eingegeben": CONFIRMED.** p.87.
- **The "at least ceil(|Q201|/Q202)" threshold: correct.** By the step-5 wording, Q213 = n-1 would force a full retract before the last infeed, so Q213 ≥ n is required. Caveat for the rule text: the formula assumes Q212 = 0. With a decrement (Q212/Q205) the number of infeeds is different.
- **Siemens CYCLE83 VARI 0 = Spänebrechen, 1 = Entspänen: CONFIRMED** in the Index 840D Zyklen 2004, p.56-57 and 61. The cited 01/2008 p.51 is not readable.
- The Fanuc milling G73/G83 pages were not re-read; the parameters 5114 and 5115 are confirmed in EN-2 p.89-90 and EN-1 p.95-96.

### D168 AND and OR evaluate both operands — HOLDS, CITATIONS TO FIX
- **Fanuc AND/OR/XOR are 32-bit bitwise operations in the multiplication/addition groups: CONFIRMED.** B-64484EN p.508; the record cites 489.
- **Three-level priority: CONFIRMED.** EN p.512; the record cites 493.
- **Complex conditional expressions: CONFIRMED.** EN p.520; the record cites 501.
- **Siemens AND/OR/NOT/XOR over truth values, a separate B_ set, and the parentheses note: CONFIRMED.** AV 03/2013 p.71-72.
- **TNC AND/OR only in SQL WHERE: CONFIRMED.** Klartext 10/2019 p.310.

### D171 Where syncPartner and syncPhase stand — HOLDS
- **COUPON(<FS>,<LS>,<POSFS>), LS under optional parameters, COUPONC takes over M3/M4 S: CONFIRMED.** 840D sl Programmierung BS.000504 p.594-597.
  - Nuance: the explicit short form without the leading spindle is named only for COUPOF, COUPOFS, COUPRES and COUPDEL.
- **Nakamura M92/M93 (tailstock-quill meaning first) and M96/M97 at the right unit: CONFIRMED.** Programmierhandbuch-1, p.122-124.
- **Alarm A11.0: CONFIRMED.** Warn- und Alarmmeldungen 4830001EEJ, p.78.
- $AA_COUP_ACT/$AA_COUP_OFFS (p.603-604) were not re-read.

### D173 Variable map of a Siemens machine — HOLDS
- **R is a REAL field; R[10] or R10; <n> runs 0..MD28050-1; examples R0, R[1], R3, R7 with no leading zeros: CONFIRMED.** PGH 06/2019 p.398-399.
- **RG[<n>], name settable by MD15800: CONFIRMED.** p.400-401.
- **Zyklen 01/2008 p.18 (R1=5 … in one block): NOT FOUND.** The document body is not readable in the connector.

---

### Entries whose decision would change
None.

### Worth fixing in the record
1. **D119:** EXP overflow is PS0111, not PS0119. Add that Fanuc ATAN with both arguments 0 raises PS0119, which supports the ATAN2(0,0) error.
2. **D117:** the Nakamura example is `G52 U2.75 W1.75`, not `W1.25`.
3. **D118:** PRM also has a one-argument form.
4. **D163:** the infeed-count rule ignores Q212.
5. **All 30i-B citations:** re-page them to the B-64484EN / EN-1 / B-64490EN-05 editions the connector holds, using the pages given above.
6. **Unreadable sources:** Doosan "Fanuc 30 Series" and the Siemens Zyklen 01/2008 body cannot be read through the connector. For D153, D161, D163, D144 and D173, cite the alternative sources named above instead.
