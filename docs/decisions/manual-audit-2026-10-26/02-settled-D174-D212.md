# Manual audit 2026-10-26, part 02: settled D174 D212

Report of one audit agent, checked against the cnc-manuals connector (1283 manuals); kept as written. Page numbers are the connector's. The summary and the decisions it leads to are in `../manual-audit-2026-10-26.md`.

## Audit of D174 to D212 (ninth-round, settled) against the cnc-manuals connector

**Summary.** No controller fact in these 17 entries is wrong, and no decision changes. What needs fixing is the citations, and this applies to every entry that cites Fanuc:

- **The cited Fanuc editions are no longer in the knowledge base.** The record cites the German 30i-B manuals ("Betriebshandbuch Drehen B-64484GE-1/02", "Drehen/Fräsen B-64484GE/02", "Fräsen B-64484GE-2", "0i-D B-64304GE", "16i/18i-TB B-63524GE"). The connector now holds the English editions instead: Fanuc_30i-B_Bedienung-Drehen_B-64484EN-1, ...-Drehen-Fraesen_B-64484EN, ...-Fraesen_B-64484EN-2, 0i-D B-64304EN(-1), 16-18-TB B-63524EN-01.
- **Their page numbers drift, and not by a fixed amount.** In the lathe volume the early pages are off by 2 to 3, and from about p.76 onward they match. In the turning/milling volume the drift is −5 near the front and up to +51 at the parameter section. In the milling volume it is −6 to −10.
- **Four cited documents are listed in the knowledge base but return no text,** so their citations cannot be checked:
  - iTNC 530 Klartext NC-SW 340490-04 (2007)
  - SINUMERIK 840D Programmierhandbuch Zyklen 2008
  - DMG MORI CTX/GMX ShopTurn 3G V2.1
  - Biglia Smart-Turn T140-00379
- **One cited document was not found at all:** the BURKHARDT+WEBER MCX 800 manual.

---

### D174 Fanuc G-code systems B and C: HOLDS, CITATIONS TO FIX
- **CONFIRMED:** three systems A/B/C, selected by bits 6 (GSB) and 7 (GSC) of parameter 3401. 30i-B Drehen EN-1, **p.24** (cited p.26).
- **CONFIRMED:** units G20/G21 in A and B are G70/G71 in C. **p.25** (cited 27).
- **CONFIRMED:** G50 in A is G92 in B and C. **p.26** (cited 28).
- **CONFIRMED** (one table, **p.28**, cited 30):
  - G70..G76 in A and B are G72..G78 in C.
  - G90/G92/G94 are G77/G78/G79 in B and G20/G21/G24 in C.
  - G98/G99 in A are G94/G95 in B and C.
  - G80..G89 are the same in all three systems.
- **CONFIRMED:** same table in the 0i-D lathe manual, B-64304EN-1 **p.32** (cited 34–35).
- **CONFIRMED:** same table in 16-18-TB B-63524EN-01, **p.66–67** (cited 64).

### D175 Words of the Fanuc turning cycles: HOLDS, CITATIONS TO FIX
- **CONFIRMED:** on G90 and G94, R is the taper amount and must be a radius value. **p.37 and p.45** (cited 39/48). The sign table is on **p.38** (cited 40).
- **CONFIRMED:** F on G92 is the thread lead. **p.38–39**.
- **CONFIRMED:** X(U), Z(W) and R are modal and shared by G90/G92/G94. **p.49** (cited 53).
- **CONFIRMED:** Q shifts the thread start angle in 0.001° steps; it is not modal and is taken as 0 if absent. On G76/G78 it works only in the FS15 format. The two-start example uses Q0/Q180000. **p.33–34** (cited 35–36).
- **CONFIRMED:** G71 is written as two blocks, U(Δd) R(e) then P Q U W F S T. Note 1 says the meaning depends on whether P and Q are present. **p.53–54** (cited 56–57).
- **CONFIRMED:** G73 first block is W(Δk) U(Δi) R(d). **p.72–73** (cited 73).
- **CONFIRMED:** G74 is R(e), then X Z P(Δi) Q(Δk) R(Δd) F. **p.80**, matches. The e/Δd note is on **p.81**, matches.
- **CONFIRMED:** G75 format, **p.82**, matches.
- **CONFIRMED:** G76 format is on **p.83–85** (cited 84). Note 1 (meaning decided by X/Z) and note 3 (P, Q, R modal) are on **p.86**, matches.

### D176 Fanuc G74..G76 take no contour range: HOLDS
- **CONFIRMED:** only G70–G73 use P/Q as the ns/nf contour range. G71 **p.53**, G73 **p.72–73**, G70 **p.76**.
- **CONFIRMED:** on G74/G75, P is Δi and Q is Δk. **p.80 and 82**.
- **CONFIRMED:** on G76, P is m/r/a and Q is Δdmin in the first block; P is k and Q is Δd in the second. **p.83–85**.

### D178 Q parameter lists of the Heidenhain catalog cycles: HOLDS
- **CONFIRMED, every page matches the citation.** The document is iTNC530_Zyklen_670_388-12. I checked each NC-example block for parameter order:

  | Cycle | Page |
  |---|---|
  | 240 | 78 |
  | 200 | 80 |
  | 201 | 82 |
  | 202 | 86 |
  | 203 | 90 |
  | 204 | 94 |
  | 205 | 98 |
  | 208 | 101 |
  | 241 | 104 |
  | 206 | 112 |
  | 207 | 115 |
  | 209 | 118 |
  | 251 | 149 |
  | 252 | 153 |
  | 253 | 158 |
  | 254 | 163 |
  | 256 | 167 |
  | 257 | 171 |

- **CONFIRMED:** Q207 is the milling feed ("Vorschub Fräsen") on cycles 251–257. Cycle 206 takes its feed from Q206; cycles 207 and 209 take the pitch from Q239. Q208 = 0 falls back to the reaming feed, p.82.
- **CONFIRMED** (spot check): Q207 also appears on 262 (p.123) and 225 (p.318).

### D180 TOOL CALL S without a tool number on the iTNC 530: HOLDS, CITATIONS TO FIX
- **NOT FOUND at the cited page:** iTNC 530 Klartext 340490-04 p.197. The document returns no text.
- **CONFIRMED in other iTNC 530 manuals:** a speed change by a TOOL CALL that carries only S, skipping the tool-number and axis prompts with NO ENT.
  - iTNC530 Programmierung-Klartext NC-SW-340490-05, **p.161**
  - The same text is in 606420-01 p.163 and 340490 (2005) p.163.
- **CONFIRMED:** the TNC 640 case list (which TOOL CALL forms change only the speed, which run the tool-change macro). TNC640 Klartext 892903-18, **p.123**, matches.

### D182 FN 18 ids of the iTNC 530 for SYS_ names: HOLDS, CITATIONS TO FIX
- **NOT FOUND at the cited pages:** 340490-04 p.623–629. The document returns no text.
- **CONFIRMED in iTNC530 Klartext 340490-05, p.306–312:**
  - Groups are selected by ID, number and index.
  - Group 50: NR4 is DL, NR5 is DR; "Ohne Index" returns the active tool's data.
  - Group 200: NR1/NR2 are radius and length "incl. Delta-Werte".
  - ID240 NR1 IDX1..9 and ID270 NR1 IDX1..9 use the axis order X Y Z A B C U V W.
  - The worked example `55 FN 18: SYSREAD Q25 = ID210 NR4 IDX3` stands as its own block, **p.312**.

### D184 ARC without feed: HOLDS, CITATIONS TO FIX
- **CONFIRMED:** parameter 1404 bit 7 FC0. With a block (G01, G02, G03, etc.) whose F is 0, setting 0 raises PS0011.
  - 30i-B Drehen-Fräsen EN, **p.2072–2073** (cited 2021).
  - Also in Parameterhandbuch B-64490EN-05, p.82.
  - In G93, PS1202 fires regardless of the setting (p.2073).
- **CONFIRMED:** the circular feedrate is F, controlled along the tangent. **p.79–80** (cited 85).
- **CONFIRMED:** Siemens alarm 10860, Parameter/Alarme BS.000503 **p.152**, matches.
  - Also: Funktionshandbuch Grundfunktionen 2012 p.1518 says that with G96 and no F value, 10860 is raised, and not for G0 blocks.
- **CONFIRMED:** 0i-D alarm 0011 is in B-64304EN p.1085 (cited B-64304GE p.1424).
- **CONFIRMED:** Nakamura alarm 0011 is in 4830001EER **p.262** (cited 4830001EEJ p.282, which is a different edition).
- **CONFIRMED:** Mazak Matrix 2 G93 formats and alarm 816 when F is omitted, H740PB6043G **p.125**, matches.
- **CONFIRMED:** TNC 640 Kreisbahn C/CR dialogues ask for F. **p.159–160**, matches.
- **Caveat kept:** the step from "F = 0" to "F never set" remains the record's own inference.

### D187 Position after the call of a catalog cycle: HOLDS, CITATIONS TO FIX
- **CONFIRMED:** operation 4 returns the tool to start point A.
  - G90 **p.36** (cited 38)
  - G92 **p.39** (cited 41)
  - G94 **p.44** (cited 47)
- **CONFIRMED:** the modal rule and the U-8/U-16 repeat example. **p.49** (cited 53).
- **CONFIRMED:** G73 note 3, return to A. **p.73** (cited 74).
- **CONFIRMED:** G70 returns to the cycle start point. **p.76–77**, matches.
- **CONFIRMED:** G72 type II returns temporarily to the start point. **p.71** (cited 72).
- **CONFIRMED:** G71 type II. **p.57–58** (cited 61/63).
- **NOT FOUND:** the G76 leg table 1(R)…4(R) on p.86–87. The table and its figure are images with no text.
- **NOT FOUND:** where G71 type I, G72 type I, G74 and G75 leave the tool. The recommendation's exception still covers "the one-shot cycles of G70 to G76" in full, so for those four cycles it rests on no page. The record already says so. The decision still holds, since any mid-cycle return comes back to A.

### D189 A drilling-axis word in a CYCLE_CALL block: HOLDS
- **CONFIRMED:** CYCL CALL POS behaviour, iTNC530 Zyklen 670388-12 **p.55**, matches:
  - Three axes must always be programmed.
  - The tool-axis coordinate shifts the start position like an extra datum shift.
  - Positioning logic depends on Q203.
  - The approach is made with R0.
- **CONFIRMED:** CYCL CALL and M99 semantics. **p.54–55**.
- **CONFIRMED:** cycle 200 positions the spindle axis itself. **p.79–80**.
- **CONFIRMED:** on Fanuc the Z of G83/G85 is the distance from point R to the hole bottom.
  - 30i-B Fräsen EN-2, G83 at p.69–70 (cited 75/84).
  - Same in the lathe volume, p.95.

### D193 Modulo rotary axis or travel-limited rotary axis: HOLDS, CITATIONS TO FIX
- **CONFIRMED:** Fanuc ROAx (1008#0), 1260, ROTx/ROSx (1006). 0i-D Connection Manual B-64303EN-1-02 **p.137** (and p.32–33), matches.
- **CONFIRMED:** Heidenhain isModulo 300102 under CfgAxis, and CfgRollOver. TNC640 Technisches Handbuch 892899-18 **p.801** (overview table), matches. The full entry is on p.1602.
- **CONFIRMED:** "Diese Überwachung wirkt nicht für Modulo-Achsen". TNC640 Aktuelle Information Nr. 6 **p.11**, matches.
- **CONFIRMED:** CALCPOSI does not monitor modulo rotary axes. Siemens NC-Programmierung SW4.92 **p.603**, matches.
- **CONFIRMED:** MODAXVAL with MD30330/MD30340. BS.000504 **p.644**, matches.
- **NOT FOUND:** the CYCLE800 sentence telling you to enter 0 to 360 for modulo axes ("Bei Moduloachsen … 0 und 360 grd"), cited at Siemens Zyklen 01/2008 p.253. The document returns no text, and searches on $TC_CARR30 found only alarm texts. The decision does not depend on this sentence.
- **CONFIRMED:** WFL Millturn C1/C2 are defined like linear axes, not as modulo axes. Kap. 15 **p.9**, matches.

### D194 Verbs of 'spindle OFF before a LINE': HOLDS, CITATIONS TO FIX
- **CONFIRMED:** every canned cycle carries a paragraph saying the spindle must be turning first ("Spindle rotation … before specifying"). 30i-B Fräsen EN-2: G73 p.60, G82 p.67, G83 p.69, G85 p.76, G88 p.81 (cited 66–91 and 72/82).
- **CONFIRMED:** section 5.3 rigid tapping: standard mode uses M03/M04/M05, rigid mode drives the spindle as a servo. **p.102** (cited 108). The M29 and parameter 5200#0 detail is in Drehen-Fräsen p.1102.
- **CONFIRMED** in another document: the CYCLE84 sequence (SPOS at POSS, axis mode, G331, G332, rewrite of speed and SDAC). Index Sinumerik-840D Zyklen LY4401.10092 **p.65**.
- **NOT FOUND at the cited source:** "Programmierhandbuch Zyklen 840D sl" p.61. The document returns no text.
- **ARC part:** no page, as the record itself says.

### D198 Which states of a function table trigger its expansion rule: HOLDS, CITATIONS TO FIX
- **CONFIRMED:** Hermle lists M51 (internal coolant on, block start) and M52 (off, block end) as separate rows with different effects. M25–M28 are also separate rows. Hermle M-Funktionen C400–C60 **p.3–5**, matches.
- **NOT FOUND:** DMG ShopTurn 3G V2.1 p.210 (M436/M433/M437) and Biglia Smart-Turn p.48 (M68/M69). Both documents return no text.
- **NOT FOUND:** BURKHARDT+WEBER MCX 800 Tabelle 6-8. No such document was found.
- This is supporting argument only; the decision does not rest on a controller fact.

### D204 Triggering block of a catalog cycle's expansion rule: HOLDS, CITATIONS TO FIX
- **CONFIRMED:** the worked example M51 … G83 … M31, C positions repeating M31, then "G80 M05", then M50. It appears on p.96 and again under §4.3.4 on **p.100** (cited 101).
- **CONFIRMED:** G83–G85 and G87–G89 are modal; the clamp M code acts modally and is cancelled by G80. **p.93**, matches.
- **CONFIRMED:** the control outputs the clamp code before the rapid to R and the unclamp code after the retract; G80 or a group-01 code cancels the cycle. **p.94**, matches.
- **CONFIRMED:** improved M-code output, §4.3.5. **p.100–101** (cited 102).
- **CONFIRMED:** RTR bit 2 of parameter 5101, and G83.5/G83.6. **p.95** (cited 96).
- **CONFIRMED:** Doosan list "M291 ZYKLUS ENTLEEREN(G83/G88)", "M292 … SPÄNEBRECHEND". Puma-2600SY M-Funktionen **p.1–4**.

### D208 Fanuc I J K: absolute CENTER or CENTER:IX: HOLDS, CITATIONS TO FIX
- **CONFIRMED:** I, J, K are always incremental from the start point, whatever G90/G91; the end point follows G90/G91; I0/J0 may be omitted; I/J/K alone gives a full circle. 30i-B Drehen-Fräsen EN **p.78–79** (cited 84).
- **CONFIRMED:** Table 8.4(a): under diameter programming, the radius designation in circular interpolation (R, I, K) is a radius value. **p.230–231**, matches.
- **CONFIRMED:** Siemens DIAMON uses diameter for I/J/K only when they are written with AC; incremental I/J/K are always radius. Grundlagen 03/2013 **p.170**, matches.

### D210 Kind of a Fanuc O section that no block of the file calls: HOLDS, CITATIONS TO FIX
- **CONFIRMED:** Table 13.2(d): M02/"M03" for the main program, M99 for a subprogram. The caution that /M02, /M30 and /M99 are not program ends follows it. 30i-B Drehen-Fräsen EN **p.328** (cited 333).
  - The "M03" misprint is present in the English edition too.
- **CONFIRMED:** M99 in a main program, and running a subprogram on its own. **p.331–332** (cited 337).
- **CONFIRMED:** 16i-MB table, in English, lists M02/M30 for the main program. B-63534EN-02 **p.202–203**, where it is table 12.2(d) (cited p.199, as table 12.2(b)).

### D211 Missing program end in Fanuc and Siemens sources: HOLDS, CITATIONS TO FIX
- **CONFIRMED:** program components and "a program section … ends with a program end code". **p.320–321** (cited 323–324).
- **CONFIRMED:** alarm 5010 END OF RECORD "also generated when the percentage at the end of the NC program is input". **p.2453–2454** (cited 2412).
- **CONFIRMED:** Siemens program end M2/M17/M30. Grundlagen 2013 **p.37**, matches.
- **CONFIRMED:** Okuma 2937, code 1. OSP-P200 LG37-005-R4 **p.342**, matches.

### D212 REPEAT without TIMES: HOLDS, CITATIONS TO FIX
- **CONFIRMED:** with no P, the program part is repeated exactly once. Siemens NC-Programmierung SW4.92 **p.478–479**, matches.
- **CONFIRMED:** M98 with L omitted is called once. 30i-B Drehen-Fräsen EN **p.335**, matches.
- **CONFIRMED:** M198 with the count omitted takes it as 1. **p.1144–1145** (cited 1108).
- **CONFIRMED:** TNC 640 program-part repeats and REP entry. 892903-18 **p.249–250** (cited 248–249).

---

**Entries whose decision would change:** none. All 17 entries hold.

**Partly unsupported, without changing the decision:**
- D187: the start-point return for G71/G72 type I and G74/G75 has no page.
- D193: the CYCLE800 0–360 sentence cannot be checked.
- D198: the DMG and Biglia citations cannot be checked, and the B+W manual was not found.
- D180 and D182: cite a text-less iTNC 340490-04 manual; re-cite them to 340490-05 (p.161 and p.306–312).
- Every Fanuc citation: re-cite to the English B-64484EN-family editions with the pages given above.
