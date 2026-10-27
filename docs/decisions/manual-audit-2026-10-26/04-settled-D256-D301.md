# Manual audit 2026-10-26, part 04: settled D256 D301

Report of one audit agent, checked against the cnc-manuals connector (1283 manuals); kept as written. Page numbers are the connector's. The summary and the decisions it leads to are in `../manual-audit-2026-10-26.md`.

## Audit of D256–D301 (ninth round, settled) against the cnc-manuals connector

**Bottom line:** no settled decision has a wrong controller fact at its core, so none of the 17 decisions has to be reversed. I found three statements in answers that the manuals contradict or overstate:
- **D256 (matters most):** the Fanuc rigid-tapping rule is stated too strictly, which affects how the reader folds M29.
- **D265:** a claim about which G361 example blocks exist is wrong.
- **D268:** an IPA input range is attributed to the wrong software edition.

Many page numbers drift, mostly because the cited edition is not in the knowledge base and only a sibling edition is.

**Editions that are not searchable.** Several cited documents are either missing from the knowledge base or listed without readable text: read_pages returns "Nothing found" and search never hits their body text. These are:
- iTNC 530 Klartext NC-SW 340490-04 (2007).
- Siemens Zyklen 01/2008: only its index pages are searchable.
- Grob G350 G352-2188.
- Doosan Puma-MX-TT, pages 114–117.
- The German Fanuc 30i-B editions B-64484GE-1, GE-2 and GE/02: the knowledge base has the English B-64484EN-1, EN-2 and EN.
- DMG CTX "Technik und Programmierung V2.1".
- Siemens alarm manual DA 08/2005.

Where I could, I confirmed the same facts in a sibling edition and give those pages below.

---

### D256 Fanuc M29 before G84 — HOLDS, CITATIONS TO FIX (one overstated rule affects the reader)
- **Three ways into rigid mode — CONFIRMED.** M29 S before the tapping block, M29 S in the tapping block, or G84 alone with parameter 5200 bit 0 set (the `before = ""` case).
  - 30i-B Bedienung Fräsen (B-64484EN-2): p.103. The record cites p.109 of the German edition.
  - 30i-B Bedienung Drehen (B-64484EN-1): p.112, also naming G88. The record cites p.113.
  - Parameter manual B-64490EN-05: p.303 (5200 bit 0).
- **PS0203 / PS0204 — CONFIRMED.** An S command and axis movement between M29 and G84 gives PS0203; M29 inside a tapping cycle gives PS0204. Fräsen p.104 (record: 111); Drehen p.114.
- **Worked example `M29 S1000;` then `G84 Z-100.0 R-20.0 F1000;`, G94/G95 and G98/G99 — CONFIRMED.** Fräsen p.105; Drehen p.115 (record: 116).
- **Doosan facts — CONFIRMED, but in a different document.** M29 comes before the G code; `M29 S****P_` alone in a block; only in G97/G98; alarm under G96; "G84(G88) cycle that don't use the M29 code will be activated with normal tapping".
  - Found in Doosan MX-Series CNC-Programmierbuch, p.84, section 5.4.
  - The cited Puma MX/TT MXTTPGE68 p.116 / section 9.2 cannot be read in the knowledge base.
- **Nakamura facts — CONFIRMED.** M29 S replaces M3/M4; on the MATA-BEI sub spindle M29 replaces M53. 4810004ECL-1 p.294–295.
- **Mori M329 — CONFIRMED.** M-code list in NT-NTX DE 2017, p.245–246. `M329 S400;` before G84 on p.290–291. Further examples on p.480–482 and p.521.
- **"So the line must stand immediately before the cycle block" — WRONG as stated.** The manual forbids only an S command or axis motion between M29 and G84. Three other cases exist:
  - The Fanuc manual itself allows M29 S inside the G84 block (method 2, p.103).
  - Doosan programs put an M code between them: `M29S500P12` / `M177` / `G84…` (MX-Series p.131; Puma-MX-TT p.171).
  - Mori writes M329 S together with a move, `Z30.0 M329 S400;`, before G84 (NTX1000SZM p.281–282).
- **What this changes:** the decision stands, but the reader rule and its consequence clause are too narrow. These programs would be read as a G84 without the line, and so wrongly as TAP_COMPENSATING:
  - M29 S in the G84 block itself;
  - a non-S, non-motion block such as M177 between M29 and G84;
  - M329 combined with a positioning move.
- **Supporting facts not in the record:**
  - The rigid-tapping M code is itself a parameter, No. 5210 (30i-B parameter manual p.307). This backs the machine-file override.
  - Doosan's form needs P (`M29 S P`).
  - Nakamura NTJX MATA-BEI needs M792 directly before the M29 block (Programming Manual-1 4810004EDG-1, p.311).

### D259 Pattern kept RAW under MCALL — HOLDS, CITATIONS TO FIX
- **Alarm 62100 "Kein Bohrzyklus aktiv", acknowledge and continue — CONFIRMED** in the 840D sl alarm manual (BS.000503), p.708. The cited Zyklen 01/2008 p.91 is not searchable; only its index is, and the index lists "Bohrbildzyklen ohne Bohrzyklusaufruf" at printed p.91, HOLES1 92, HOLES2 95, CYCLE801 98, which agrees with the record.
- **MCALL is also called on an S or F block under G0/G1, or on G0/G1 alone — CONFIRMED.** NC-Programmierung 2019, p.539–540.
- **CYCLE801 with 14 parameters — CONFIRMED.** p.1114–1115.

### D261 Siemens DEF type and declaration — HOLDS
- **Definition rules, LUD/PUD scope, MD11120 — CONFIRMED.** p.404.
- **Default value 0.0 — CONFIRMED,** p.406. Also, p.416 states outright "kein Initialisierungswert … impliziter Initialisierungswert = 0.0". So "the only place the manual puts a number" is slightly wrong; the fact is stronger than the record says.
- **REAL→INT the only lossy conversion, rounding at 0.5 — CONFIRMED.** p.431.
- **Alarm 12550 — CONFIRMED,** p.191. **Alarm 12200 — CONFIRMED,** p.176. Both in the alarm manual BS.000503, at the pages cited.
- **No initial value for FRAME — CONFIRMED,** also in the alarm manual p.178–179.

### D262 MOVE=MOVE on CYCLE800 — HOLDS, CITATIONS TO FIX
- **_ST digits, _DIR (-1 / +1 / 0 = compute frame only, default -1), footnote 3 — CONFIRMED.** Arbeitsvorbereitung 2013, p.712–714.
- **Tracking option and a separate direction row — CONFIRMED.** Bedienhandbuch Fräsen 2013, p.488.
- **Zyklen 01/2008 p.214–215 and 225–226 — NOT FOUND:** the body text is not searchable.
- **Grob examples — CONFIRMED, but in another document.** G350 G352-2188 has no text in the knowledge base. The same pattern is in G550 G552-2183: 200000 with +1 on p.20–22, 100000 with -1 on p.23–24.
- **Correa FOX-M `220000 … ,0,,1` — CONFIRMED.** p.19–20.

### D265 Tool table default kind — HOLDS (one side claim wrong)
- **D omitted means a rotary tool; D0./D1./D2. (M436/M437); B defaults to 90, T to 3 s — CONFIRMED.** NTX1000SZM p.204.
- **"The manual contains no block `G361 B90.0;`" — WRONG.** p.205–206 prints `G361 B90.0;` and `G361 B90.0 Q1.;` (coupling versus brake clamp). This does not change the decision. The same page also requires every G361 address to carry a decimal point, so `D0.` is the right form.
- **DMG TC() fourth parameter "Werkzeugtyp" — NOT FOUND.** Searched "TC(P1…P6) Werkzeugtyp", "Werkzeugtyp angetriebenes Werkzeug Drehwerkzeug" and others with manufacturer DMG MORI.

### D268 Klartext helix — HOLDS, CITATIONS TO FIX (one detail wrong)
- **Helix only in polar coordinates; incremental IPA plus IZ; example `CP IPA-1800 IZ+5 DR-` — CONFIRMED.** Klartext 340490-05 (2008), p.224–226; 606420-01, p.230–232; DIN-ISO 340490-04, p.247–248. TNC 640 Klartext 892903-18, p.170 (record: 169). The cited 340490-04 Klartext p.265–266 is not searchable.
- **C block takes centre plus end-point coordinates — CONFIRMED.** -05 p.213.
- **"IPA takes -5400 to +5400" for NC-SW 340490-04 — WRONG for that edition.** DIN-ISO 340490-04 p.248 and Klartext -05 p.226 give -99 999.9999 to +99 999.9999. The ±5400 limit, with "more than 15 turns → program section repeat", is from the older 340422 (2004, p.216) and 340490 (2005, p.231). This does not affect the decision; it matters only if the compiler caps IPA.

### D273 F on every block — HOLDS, CITATIONS TO FIX
- **A numeric F is modal; FMAX acts only in its own block, then the last numeric F applies again — CONFIRMED.** iTNC Klartext -05, p.160 (record: p.196 of -04). The same wording is in 340422 p.150 and 340490 p.162.
- **Extra detail:** if FMAX stands before an APPR block, it also applies to the approach (-05 p.102–103).

### D275 Following block for RPM in TOOL CALL — HOLDS, CITATIONS TO FIX
- **Speed-only TOOL CALL using NO ENT — CONFIRMED.** -05 p.161 (record: 197).
- **`TOOL CALL 5.2 Z S2500 F350…`; TOOL DEF as preselection with no speed — CONFIRMED.** -05 p.177 (record: 211–212).

### D281 {axes} in the Fanuc home template — HOLDS, CITATIONS TO FIX
- **Several G codes of different groups may share a block; parameter 3402 bit 3; in system A, absolute/incremental is chosen by the address word — CONFIRMED.** 30i-B Drehen/Fräsen B-64484EN, p.62–63 (record: 66–67).
- **`G28G91X0Y0Z0` — CONFIRMED.** p.298 (record: 305).
- **G17.1 example program `N10 G91 G28 X0 Y0 Z0 … N80 …` — NOT FOUND as text.** p.222 does carry the G17.1 example, but the program sits in a figure. Searches: "N10 G91 G28 X0 Y0 Z0 …", "G17.1 O1000 O2000 G91 G28".

### D287 RETRACT speed — HOLDS, CITATIONS TO FIX
- **M140 MB without F moves at rapid; acts only in its block; example `F125 M140 MB 50 F750` — CONFIRMED.**
  - TNC 640 Klartext 892903-18, p.238 (also gives moveBack No. 200903).
  - iTNC Klartext -05, p.362 (record: p.317 of -04).

### D288 Spindle for feed per revolution — HOLDS
- **G95 refers to the master spindle — CONFIRMED.** Siemens Grundlagen 2013, p.101. The same manual, p.93, also says the G96 cutting speed always acts on the master spindle, which supports "CSS follows the same spindle".
- **PC2SLC caution — CONFIRMED.** Fanuc 0i-D Connection Manual Vol.1, p.867.
- **Nakamura M414 — consistent.** It is used on lower-turret G99 cuts (4810004ECL-1, p.292–293). I did not read the exact description line on the cited Walter Meier pages.
- **DMG GMX sheet p.25 (`SETMS(1)`, `G95 S1=2000`) — NOT FOUND by search.** The GMX Arbeitsblätter do show the SETMS(n) / S_n= pattern, at p.48.

### D289 CSS radius — HOLDS
- **The G00 block uses its end position for CSS — CONFIRMED.** 30i-B B-64484EN, p.239, matching the record.

### D293 Siemens call of a name outside the file — HOLDS
- **L123, L0123 and L00123 are different programs; a call needs its own block — CONFIRMED.** p.534.
- **Fixed search path ending in user, builder and standard cycle directories — CONFIRMED.** p.572–573.
- **CALLPATH — CONFIRMED.** p.545.

### D296 Axes of CYCLE800 direct mode — HOLDS
- **_MODE bits 7/6 = 11 → _A, _B, with footnote 2 — CONFIRMED.** Arbeitsvorbereitung 2013, p.713–714.
- **"Name Rundachse 1 / 2"; Z = rotation in the plane; direction "nicht bei Schwenkmodus direkt" — CONFIRMED.** Bedienhandbuch Fräsen, p.488.
- **Zyklen 01/2008 p.214–219 — NOT FOUND:** the body text is not searchable.

### D298 Holder frame is a Z mirror — HOLDS
- **Nakamura Z mirror reverses Z direction, G02/G03 and G41/G42 — CONFIRMED.** 4810004ECL-1, p.323–324.
- **Mori M396/M395 for Z, with a separate M584/M585 pair for Y — CONFIRMED.** p.272–273; NT-NTX 2017 p.280.
- **G51.1 on one axis reverses G02/G03 and G41/G42 — CONFIRMED.** p.148.
- **ShopTurn back side stored in a Z-mirrored zero offset — CONFIRMED.** Bedienhandbuch Drehen 2013, p.526–527.
- **Not checked:** DMG Schulung p.56 and Grundlagen p.348–349.

### D300 G961 for the G96 of a VC template — HOLDS
- **G96/G961/G962 and G97/G971/G972/G973 meanings; S required on first selection only; G94/G95 also switch CSS off — CONFIRMED.** Grundlagen 2013, p.92–95.
- **Group 15 membership — CONFIRMED** also in NC-Programmierung 2019, p.1231–1232.

### D301 One Siemens D for OFFSET:LEN and OFFSET:RAD — HOLDS, CITATIONS TO FIX
- **D activates the edge record including length; D0 deactivates it; G41/G42 needed in addition — CONFIRMED.** Grundlagen 2013, p.73.
- **A changed D takes effect in its own block — CONFIRMED.** p.265.
- **Alarms 14090 and 10750 (DA 08/2005, p.196, p.116–117) — NOT FOUND:** that edition is not in the knowledge base, and an fts search for 14090 returns nothing.

---

### Entries whose decision would change
None. Implementation follow-ups:
- **D256:** widen the reader fold so a G84 is not wrongly read as compensating-chuck tapping (TAP_COMPENSATING) when M29 S stands in the G84 block, when a non-S, non-motion M code sits between M29 and G84, or when M329 shares a block with a move. Only the reader side and the consequence clause change.
- **D268:** correct the IPA input range to the 340490-04 / -05 value (±99 999.9999), or attribute the ±5400 limit to the 2004/2005 software.
- **D265:** strike the claim that no `G361 B90.0;` block exists.
- **Citations:** re-point the D256 Fanuc and Doosan citations, the D268/D273/D275/D287 iTNC citations (to Klartext -05), the D281 pages, and the D262 Grob citation (to G550), as listed above.
