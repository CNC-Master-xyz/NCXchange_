# Manual audit 2026-10-26, part 05: settled D312 D346

Report of one audit agent, checked against the cnc-manuals connector (1283 manuals); kept as written. Page numbers are the connector's. The summary and the decisions it leads to are in `../manual-audit-2026-10-26.md`.

## Audit of D312, D321, D322, D326, D327, D334–D337, D340–D346 against the cnc-manuals connector

**Bottom line:** no entry's decision changes. None of the 16 entries has a controller fact the manuals contradict in a way that alters the decision. The problems found are about citations:
- Several cited manuals cannot be read through the connector.
- A few cited documents or pages are wrong.
- In D335 and D345 the record's reasoning is weaker than the manuals. The manuals back the decision more strongly than the record claims.

**Two cited manuals have no readable text.** Both are listed by list_documents, but read_pages returns "Nothing found" for every page:
- **iTNC 530 Klartext NC-SW 340490-04 (2007, 799 p.):** cited in D321, D336, D337 and D345.
- **Siemens 840D Programmierhandbuch Zyklen 01/2008 (430 p.):** cited in D334, D335 and D341. Only its index chunk is indexed.

Every page cited in these two manuals is unverifiable as a page. Where the same fact appears in a sibling edition, I give that page instead.

---

### D312 – HOLDS
- **CONFIRMED – Mori Note 4 (M34/M35 in both programs, sync M code in the block right before):** NTX1000SZM PX-NT_4-K0DEEN, p.539–540.
- **CONFIRMED – Mori Note 5 (M36 in either program, not needed in both):** same manual, p.540. Note 1 limits M34/M35/M36 to the NT-S, NT-SZ and NT-SZM series (p.539).
- **CONFIRMED – Mori transfer example:** p.547–548.
  - M100 then M34 stand in both columns.
  - M36 stands once, in the turret-2 column, between M104 and M105.
- **CONFIRMED – 2017 NT/NTX revision repeats the notes:** p.556–557.
- **CONFIRMED – Nakamura back-milling codes:** 4810004ECL-1, p.302 and p.303–304.
  - M442 and M443 must stand behind a wait code.
  - M443 must not be programmed on the opposite turret side.
  - Example 2 (p.304) says M442/M443 belong in one program only, and entering them in both is superfluous.
  - The same text is repeated at p.306.

### D321 – HOLDS, CITATIONS TO FIX
- **iTNC 530 340490-04 pages 239, 197 and 211: NOT FOUND** (manual not readable). The same facts are in readable editions:
  - **CONFIRMED – principal plane is set by the spindle axis at TOOL CALL:** iTNC 530 340490-05 p.197 and 606420-01 p.203.
  - **CONFIRMED – speed-only TOOL CALL that skips the tool number and spindle axis with NO ENT:** 340490-05 p.161 and 606420-01 p.163.
  - The tool-axis field is part of the TOOL CALL dialog.
- **CONFIRMED – TNC 640 principal plane sentence:** TNC 640 Klartext 892903-18, p.141.
- **CONFIRMED – TNC 640 speed-only cases versus cases that run the tool-change macro, including a changed tool-axis direction:** same manual, p.128.
- **CONFIRMED – DIN/ISO principal plane table with G17/G18/G19:** iTNC 530 DIN/ISO 340490-04, p.226.
- **CONFIRMED – DIN/ISO example "N20 T1 G17 S2000":** same manual, p.103. Page 432 not checked.

### D322 – HOLDS, CITATIONS TO FIX
- **Fanuc citation needs fixing.** The record cites "B-64484GE/02 p.1108–1109"; that German manual is not in the knowledge base.
  - The same text is in Fanuc 30i-B Bedienung Drehen-Fräsen **B-64484EN, p.1144–1145** (§4.5).
  - **CONFIRMED – M198 call formats:**
    - M198 P… L…
    - M198 with a file name and L
    - the FS16 form M198 Pxxxx yyyy, repeat count before the 4-digit number
  - **CONFIRMED – parameter 6030** sets the external-call M code; if it is set to anything other than M198, M198 runs as an ordinary M code.
  - **CONFIRMED – no USB memory** can be called this way.
  - Parameter 6030 detail (p.2163–2164 of the same manual): when 6030 is 0, M198 is used.
- **CONFIRMED – Nakamura memory-card call:** 4810004ECL-1, p.152.
  - Parameters 0020 = 4, 0138.7 = 1, 3404.2 = 1, 6030 = 200.
  - The call is M200 P<repeats><program number>.
- **CONFIRMED – NTJX code list:** Liste M-G-Codes 2016, p.2 (chunk p.1–3).
  - M100 bis M199 are wait functions.
  - M200 is the program call from memory card.
- **CONFIRMED – M198 as rendezvous in the NTJX header:** Fanuc-18i-TB NC-Wort-Adresse 2009, p.19–20 ("SYNCHRONISATION MIT *U*" and "*O*").
- **CONFIRMED – M198 as bar-end query in the WY template:** WY 31i-A manual, p.21.
- **CONFIRMED – Mori NTX1000:**
  - M-code list p.236: M98 is the subprogram call; M198 is an option, the call in the external I/O device; M100–M197 are operation synchronization; M200 is the chip conveyor.
  - p.283: M198 usage and restrictions.

### D326 – HOLDS
- **CONFIRMED – MSG syntax and behaviour:** Grundlagen 840D sl/828D 03/2013, §14.1 (chunk p.365–367).
  - Syntax MSG("text"[,Ausführung]).
  - At most 124 characters, shown in two lines of 62.
  - The << operator may build the text from variables.
  - Ausführung 0 is the default; 1 makes a main-run block of its own and interrupts continuous-path mode.
  - MSG() clears the message; without clearing, the display stays until the next message.
- **CONFIRMED – examples:** p.368.
- **CONFIRMED – statement-list row (type P, modal):** p.440–441.

### D327 – HOLDS
All in Arbeitsvorbereitung 03/2013:
- **CONFIRMED – four REPEAT forms and the label search rule:** p.102–104.
- **CONFIRMED – Example 4 (P=3/P=2) and Example 5 (ZENTRIERBOHRER, POS_1/POS_2, REPEAT after the tool changes):** p.106.
- **CONFIRMED – limits of repetitions:** p.107.
  - Each repetition occupies a subprogram level and is shown as one.
  - M17 or RET aborts the repetition.
  - Mixing jumps with repetitions makes processing purely sequential.
- **CONFIRMED – "REPEAT-Anweisung sollte hinter den Verfahrsätzen stehen":** p.108.

### D334 – HOLDS, CITATIONS TO FIX
- **Zyklen 01/2008 pages 44–47: NOT FOUND** (manual not readable).
  - The index chunk (p.426) lists "relative Bohrtiefe 47" and "Rückzugsebene 46", which is consistent with the cited pages.
- **CONFIRMED in substitute sources:**
  - Bedienhandbuch Fräsen 03/2013, p.303:
    - the cycle assumes the retract plane lies before the reference point;
    - identical planes, or a retract plane behind the reference point, give "Referenzebene falsch definiert" and the cycle does not run;
    - the direction of the safety distance is determined by the cycle.
  - Alarm 61101 with that text: ISO-Fräsen 2012, p.197–198.
  - NC-Programmierung SW4.92 2019, p.1082–1083: SDIS is "additiv auf Bezugspunkt, ohne Vorzeichen"; DPR is "Bohrtiefe (ink)".
- **NOT FOUND:**
  - "DPR ohne Vorzeichen einzugeben" in a readable manual;
  - the worked example CYCLE81(110,100,2,,65) (fts search, no hit);
  - "DPR wins when both are given".
- **Nuance for the reader:** Zyklenpakete appendix SW4.7 SP2 HF1 (2015), p.20–21.
  - DPR = 0 or empty together with DP given means absolute DP applies (CYCLE81(7,5,2,0,0) uses DP = 0).
  - Neither DP nor a non-zero DPR gives alarm 61019.

### D335 – HOLDS, CITATIONS TO FIX
- **WRONG (helpfully): the record says no page names _A, _B, _C as first, second and third rotation.** One page does:
  - NC-Programmierung SW4.92 2019, p.1112–1113: _A is "1. Drehung laut Einstellung in Parameter <_MODE>", _B the 2nd, _C the 3rd.
  - The _MODE bit table there reads "1.Drehung _A um X/Y/Z" (bits 1–0), "2.Drehung _B" (bits 3–2), "3.Drehung _C" (bits 5–4).
  - The 27 = ZYX example is on p.1114.
  - This supports the decision more strongly than the record's indirect reasoning does.
- **CONFIRMED – each axis-wise rotation builds on the previous one; spatial angles turn about Z first, then Y; projection angles defined:** Bedienhandbuch Fräsen 03/2013, p.484. Page 488 (screen labels) not checked.
- **CONFIRMED – Heller mode 54 / X−90 call and mode 192 call:** Heller HF PA.000105-DE-01, chunk p.127–129. The record cites p.128.
- **CONFIRMED – the "Formenbau" five-axis example with mode 27:** it is in KB document "Siemens_Sinumerik-840D-sl_Programmierung_BS.000508", p.125–126. The record names it "Handbuch Formenbau".
- **CONFIRMED – PLANE SPATIAL rotation orders A-B-C and C-B-A:** TNC 640 Klartext 892903-18, p.414–415.
- **NOT FOUND:** Zyklen 01/2008 p.217–218 and 225–226 (manual not readable). INDEX PGZ 10.04 p.218 not checked.

### D336 – HOLDS, CITATIONS TO FIX
- **iTNC 530 340490-04 pages 609 and 662: NOT FOUND** (manual not readable).
- **CONFIRMED – FN 9 to FN 12 words EQU/NE/GT/LT with the cited example blocks:**
  - 606420-01 p.301 (the cited page matches);
  - 340490-05 p.292.
- **CONFIRMED – KUGEL program with FN 11 … GT and FN 12 … LT:** 340490-05 p.341–343; Benutzerhandbuch 340490 (2005) p.580–582.

### D337 – HOLDS, CITATIONS TO FIX
- **iTNC 530 340490-04 pages 302, 313 and 320: NOT FOUND** (manual not readable). The same text in readable editions:
  - **CONFIRMED – a modal M function is cancelled by another M function or automatically at program end:** 340490-05, p.345–346.
  - **CONFIRMED – M142 standard behaviour:** modal program information is reset when a new program is selected, on M2/M30/END PGM depending on MP7300, and when a cycle is redefined with its base values. DIN/ISO 340490-05 p.320; 340490-04 DIN/ISO p.284; Benutzerhandbuch 340422 p.257.
  - **CONFIRMED – M136 default is mm/min:** DIN/ISO 340490-04 p.277; Benutzerhandbuch 340422 p.251.
- **CONFIRMED – TNC 640 basic rule:** Klartext 892903-18, p.224.
- **CONFIRMED – TNC 640 M136 default mm/min and "M136 heben Sie auf, indem Sie M137 programmieren":** same manual, p.232.

### D340 – HOLDS
- **(1) CONFIRMED – an I/J/K of value 0 may be left out, but the second centre word must be given:** Grundlagen 03/2013, p.197 (repeated p.202).
- **(2) CONFIRMED – G1 is the only standard (SAG) entry of G group 1; MD20150 is configurable for every row:**
  - Grundlagen 03/2013 p.494–495;
  - NC-Programmierung SW4.92 p.1226;
  - EN SW4.94 2020 p.1290;
  - ONE p.1294 (per its table of contents).
  - Main's G0 start is wrong, as the record already states; the decision takes the D242 key branch.
- **(3) CONFIRMED – frames:**
  - a replacing frame instruction refers to the last called settable zero offset: Grundlagen p.321;
  - G500 switches off the settable frames only; a bare TRANS/ROT/SCALE/MIRROR deletes the programmable frames: Grundlagen p.354;
  - $P_ACTFRAME chain with $P_IFRAME and $P_PFRAME as separate components: Arbeitsvorbereitung p.297.

### D341 – HOLDS, CITATIONS TO FIX
- **CONFIRMED – CYCLE801 with 14 parameters** (VARI 0 grid / 1 frame, shear angles, _HIDE up to 198 characters, e.g. "1,3"): NC-Programmierung SW4.92, p.1114–1116.
- **CONFIRMED – CYCLE802 with 33 parameters:** p.1116–1119.
  - _XA/_YA/S_ABA: 1 = absolute, 2 = incremental; the first position is always absolute.
  - _VARI hundreds are "nur für Aufruf aus Jobshop".
  - p.1119 note: unneeded X1/Y1/A1 to X8/Y8/A8 may be omitted, but the alternative codes must be complete for all nine.
- **NOT FOUND:** classic CYCLE801 and HOLES1 at Zyklen 01/2008 p.92–99 (manual not readable). This leaves unverified the record's warning that the cycle chooses the hole order from the position reached before the call.
  - Searched: "CYCLE801 Reihenfolge … Eckpositionen" and "HOLES1 SPCA SPCO …"; only the sl signatures turned up (2013 Arbeitsvorbereitung p.657, 2019 p.1045).

### D342 – HOLDS, CITATIONS TO FIX
- **CONFIRMED – feed type per code:** Grundlagen 03/2013, p.92–93.
  - G96 switches G95 on; G961 uses G94; G962 uses G94 or G95.
  - G97 switches off with feed type G95; G971 with G94; G972 with G94 or G95; G973 switches off without the speed limit.
- **CONFIRMED – G97 without a preceding G96 acts like G95; LIMS does not act under G971:** p.95–96.
- **CONFIRMED – G973 is "Umdrehungsvorschub ohne Spindeldrehzahlbegrenzung (G97 ohne LIMS für ISO-Modus)":** group 15 table, p.501.
- **CONFIRMED – alarm 12060: in a non-syntax-determining group (5 to n) the last function in the block wins:** "Parameter Alarme BS.000503", p.168.
  - The page matches, but the record names this document "Diagnosehandbuch (Alarme), 08/2018". The KB title and date differ.

### D343 – HOLDS
- **CONFIRMED – MCALL acts "Auch über Programmebenen hinweg"; only the last MCALL acts:** NC-Programmierung SW4.92, p.539.
- **CONFIRMED – Example 1 with N10910 MCALL:** p.527.
- **CONFIRMED – on a multi-level return, an MCALL deselect in a skipped level leaves the modal subprogram active:** p.526 and Arbeitsvorbereitung 03/2013 p.181.
- Page 166 (SAVE) not checked.

### D344 – HOLDS, CITATIONS TO FIX (one caveat)
- **CONFIRMED – M17 and M30 are treated as equal in a subprogram:** NC-Programmierung SW4.92, p.522.
- **CONFIRMED – RET:** p.523.
  - It must stand in its own block.
  - It "kann nur in Unterprogrammen verwendet werden, die nicht mit dem Attribut SAVE definiert wurden".
- **CONFIRMED – parameterized RET forms, target may be block number, label or string, levels 1–15:** p.524–526.
- **CONFIRMED – RET("N10900", ,$P_STACK) example:** p.527.
- **CONFIRMED – M2/M30 act like M17 when a main program is called as a subprogram:** Grundlagen p.365.
- **CONFIRMED – $P_STACK is the current program level:** Listenhandbuch Systemvariable SW4.5, p.51.
- **CONFIRMED – alarm 14091 index 10 (too many return levels):** BS.000503, p.221. Alarm 14080 (p.220) not checked.
- **Caveat the answer omits:** the compiled form RET("<end label>",,$P_STACK) is illegal in a subprogram declared with SAVE (p.523). The compiler needs an ERROR or a fallback (CMP594) there.
  - The rule on p.523 is stated for RET in general; the manual does not say whether the parameterized form is exempt.

### D345 – HOLDS, CITATIONS TO FIX
- **iTNC 530 340490-04 pages 216, 217, 250 and 254–256: NOT FOUND** (manual not readable).
- **WRONG (detail): "both controls accept RL/RR on any path function" is wrong for the iTNC 530.** The readable editions say the compensation is entered in an L block ("Die Radiuskorrektur geben Sie in einen L-Satz ein"): 340490-05 p.190; 606420-01 p.196.
  - Its effect starts with a straight-line block; it is cancelled by L … R0, DEP, PGM CALL or PGM MGT: 340490-05 p.188.
- **Stronger evidence than the record cites:** both controls state that radius compensation must not be activated in an arc block and must first be programmed in a straight-line block.
  - TNC 640 892903-18, p.141–142.
  - iTNC 530 340490-05 p.198; 606420-01 p.204; DIN/ISO 340490-04 p.226–227.
- **CONFIRMED – TNC 640 "beliebige Bahnfunktion", effect only in a straight-line block, cancelled by L R0, DEP or PGM MGT:** p.134. Pages 155, 159–162 and 260 not checked.
- "On the L block only" stands, and is better founded than the record says.

### D346 – HOLDS (one sub-fact unverifiable)
- **CONFIRMED – DIV is "Ganzzahl-Division"; MOD returns the remainder:** NC-Programmierung SW4.92, p.448.
- **CONFIRMED – DIV result type INT from INT or REAL operands; 7 DIV 4.1 = 1; 7 MOD 4.1 = 2.9:** p.449.
- **CONFIRMED – synchronized actions: DIV is INT/INT ⇒ INT; 3 MOD 4 = 3:** p.391.
- **CONFIRMED – 3 DIV 4 = 0:** High Level Language training 2010, p.242.
- **NOT FOUND – rounding direction for negative operands** (toward zero or floor). All examples are positive.
  - Searched: "3 DIV 4 = 0 Ganzzahl-Division" plus reading the operator pages.
  - INT(a/b) is proven only for non-negative operands.
- **CONFIRMED – alarm 14091 index 1 is RET in the main program level; remedy "RET-Befehl durch M17/M30 ersetzen":** BS.000503, p.221. The record's document title differs, as in D342.
- **CONFIRMED – M17 in a main program acts like M2/M30:** Grundlagen p.365.

---

### Entries whose decision would change
None. Three need a follow-up within the existing decision:
- **D344:** a subprogram declared with SAVE cannot use RET, so it needs an ERROR or fallback instead of RET(...,$P_STACK).
- **D346:** reading DIV as INT(a/b) is only shown for non-negative operands. Consider keeping DIV with possibly negative operands RAW, or record the assumption.
- **D341:** the expansion caveat about the cycle choosing the hole order rests on an unreadable manual. Treat it as unverified until it is checked.

Citation fixes worth making:
- **D322:** replace the Fanuc citation with B-64484EN p.1144–1145.
- **D321, D336, D337, D345:** re-cite the iTNC 530 340490-04 pages to 340490-05 or 606420-01.
- **D334, D335, D341:** re-cite the Zyklen 01/2008 pages to readable sl manuals.
- **D335:** cite NC-Programmierung SW4.92 p.1112–1113.
- **D342, D346:** the alarm citation is to the document titled "Parameter Alarme BS.000503" in the knowledge base.
