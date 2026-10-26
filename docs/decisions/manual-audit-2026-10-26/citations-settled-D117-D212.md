# Citation concordance: settled decisions D117 to D212

Status: 2026-10-26. The record's citations of these entries, resolved to the cnc-manuals connector.

How to read this file. The record (`../rationale.md`, section "Answered (ninth round", entries D117 to D212 that `01-settled-D117-D173.md` and `02-settled-D174-D212.md` audit) cites a local text extraction of the manuals and several editions the connector partly lacks: above all the German FANUC 30i-B volumes (B-64484GE/02, GE-1/02, GE-2/02, parameter manual B-64490GE/03), which the connector holds only as English twins. This file gives, for every cited source that names a manual and a page, the connector's document (its title as `search_manuals` shows it, with the edition code in brackets where the title alone is ambiguous) and the connector page. The mappings come from the two audit reports; the gaps were filled by reading the connector. The record itself is unchanged, and so is every statement in it. Status means: **same page**, the connector holds the cited document and the passage is on the cited page (a range may start or end one page earlier or later); **page moved**, same document, other page; **other edition**, the cited edition is not in the connector and the passage was found in the twin edition named, at the page given; **not readable in the connector**, the document is listed but has no text at that page (a readable alternative, where one exists, is named in the same row); **not found**, neither the document nor the passage could be located. Citations without a page (for example "Fanuc manual, not in the documents" in a Question) have no row; a citation repeated in ANSWER and Evidence has one row.

## D117 Axis words under SHIFT, TILT, TILT_AXIS and SETPOS

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| FANUC 30i-B Betriebshandbuch Drehen/Fräsen B-64484GE/02, PDF 216-217 (§7.3, G52) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 216-217 | other edition |
| Nakamura-Tome Programmierhandbuch (Walter Meier, Fanuc 0iTF/31iB/32iA), PDF 53 (G52) | Fanuc Programmierung Programmierhandbuch (Nakamura-Tome, DE 2019) | 52-53 | same page |
| FANUC 16i/18i-TB Benutzerhandbuch B-63524GE/01, PDF 130-131 (§7.3) | 16-18-TB Bedienerhandbuch (B-63524EN-01) | 133-134 | other edition |
| FANUC 16i/18i/21i-B Beschreibungen B-63522GE/02, PDF 123 (§7.2, G50 U W) | 16i-210is-B Beschreibungen (B-63522GE-02) | 123 | same page |
| B-64484GE/02, PDF 203 (§7.2, format G92 IP / G50 IP) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 204-205 | other edition |
| B-64484GE/02, PDF 207 (§7.2, incremental IP = coordinate-system shift, T) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 208 (the same rule also on 205) | other edition |
| B-64484GE/02, PDF 868 (§22.6, G68.2 format) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 889 | other edition |
| Haas VF-Serie Schulungsheft Bearbeitungszentren V1.3, PDF 4 (G-code list, G52 "nur absolut") | VF-Serie Schulungsheft-Bearbeitungszentren V1.3 | 4 | same page |

## D118 Number of arguments of each function (language 4.12)

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| SINUMERIK 840D sl NC-Programmierung 06/2019, p. 448-449 (Rechenfunktionen, ATAN2) | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW492, 2019) | 448-449 | same page |
| same manual, p. 431-432 (MINVAL, MAXVAL, BOUND) | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW492, 2019) | 431-432 | same page |
| B-64484GE/02, p. 489 (Table 16.4(a), macro functions) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 508 (Table 16.5(a)) | other edition |
| B-64484GE/02, p. 490 (ATAN[1]/10 raises PS1131) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 509 | other edition |
| B-64484GE/02, p. 2404 (Anhang H, alarm 1131) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 2445 (also 30i-B Wartung-Wartung (B-64485EN) 796-797) | other edition |
| HEIDENHAIN TNC 640 Klartextprogrammierung 10/2019, p. 275 (FN 6, 7, 8, 13) | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 275 | same page |

## D119 Values of ATAN2 and FRAC (language 4.12)

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| SINUMERIK High Level Language Trainingsdokumentation (2010), p. 241 (ATAN2) | Sinumerik High-Level-Language Trainingsdokumentation | 241-242 | same page |
| SINUMERIK 840D sl NC-Programmierung 06/2019, p. 449 (ATAN2) | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW492, 2019) | 449 | same page |
| B-64484GE/02, PDF 490 (ATAN two arguments, NAT, PS0119) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 509 | other edition |
| B-64484GE/02, PDF 2117 (parameter 6004 NAT) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 2158-2159 (also 30i-B Parameterhandbuch (B-64490EN-05) 340-341) | other edition |
| TNC 640 Klartextprogrammierung 10/2019, p. 325 (INT does not round) | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 325 | same page |
| same manual, p. 348 (FRAC/INT list, rounding example) | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 348 | same page |

## D122 Full circle and zero radius in the CENTER form of ARC

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| TNC 640 Klartextprogrammierung 10/2019, p. 159 (Kreisbahn C, circleDeviation) | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 159 | same page |
| same manual, p. 160 (Kreisbahn CR, full circle in two blocks) | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 160 | same page |
| B-64484GE/02, p. 84 (Befehl für einen Kreis, parameter 3410, PS0020) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 79-80 | other edition |
| SINUMERIK 840D sl NC-Programmierung 06/2019, p. 193, 195, 197 | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW492, 2019) | 193, 195, 197 | same page |
| Stama Programmieranleitung 840D (2004), cache p. 46 (no full circle with CR) | Sinumerik-840D Programmierung Programmieranleitung-MC-Baureihe MC3xx-5xx-S840D-PRG-DEU (Stama) | 46 | same page |
| SINUMERIK 840D sl / SINAMICS S120 Alarme, Diagnosehandbuch 08/2018, p. 216 (alarm 14040) | Sinumerik-840D-sl Parameter Alarme (BS.000503) | 216 | same page |

## D127 How long 'directly after a HOME' lasts for SETPOS

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| Parameterhandbuch FANUC 30i-B B-64490GE/03, p. 225 (parameter 3700 CRF/NRF/CNM) | 30i-B Parameterhandbuch (B-64490EN-05) | 213-214 | other edition |
| B-64484GE/02, p. 1136 (Cs program restart, reference point after power-on) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 1173 | other edition |
| Nakamura-Tome NT Nurse Bedienungsanleitung (Fanuc 16/21i), p. 303 (M91, G28 H0, G50 C0; second G50 C0) | Fanuc-16-21i Bedienung NT-Nurse-Bedienungsanleitung (4800001EBG) | 303 | same page |
| same manual, p. 292 (G50 C0 / G50 C12,5) | Fanuc-16-21i Bedienung NT-Nurse-Bedienungsanleitung (4800001EBG) | 292 | same page |
| same manual, p. 215 (G362 example, M98 P9000 then G50 C0) | Fanuc-16-21i Bedienung NT-Nurse-Bedienungsanleitung (4800001EBG) | 215 | same page |
| Nakamura-Tome Präzisionsdrehzentrum Programmierhandbuch (1/2), p. 226 (contents of O9000) | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 226 | same page |
| Muratec Programming Manual TE0001E-08, p. 166 (M68; G28 H0; G50 C-90.) | Allgemein Programmierung Programming-Manual (Muratec, TE0001E-08) | 165-166 | same page |
| same manual, p. 168 (G28 H0: G50 X170. Z104. C22.5) | Allgemein Programmierung Programming-Manual (Muratec, TE0001E-08) | 168 | same page |

## D131 Words covered by the C= check of VM 3.8 rule 4

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| Nakamura-Tome Präzisionsdrehzentrum Programmierhandbuch (1/2), p. 227 (M91 rule, right/wrong table) | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 225-227 | same page |
| same manual, p. 226 (§14-1, M91 in its own block) | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 226 | same page |
| NT Nurse Bedienungsanleitung (Fanuc 16/21i), p. 303 (M91, G28 H0, G50 C0) | Fanuc-16-21i Bedienung NT-Nurse-Bedienungsanleitung (4800001EBG) | 303 | same page |
| B-64484GE/02, Anhang H, p. 2391 (alarm 0197) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 2429 (also 30i-B Wartung-Wartung (B-64485EN) 778-779) | other edition |
| Monforts, Angetriebene Werkzeuge mit C-Achse über Hauptantrieb, p. 5 (M5, M21, G28 C0) | Fanuc-16i-18i-21i Programmierung AWZ-C-Achse-Hauptantrieb (Monforts) | 5 | same page |
| same document, p. 4 (S, M3, M4, M5 go to the tool spindle under M21) | Fanuc-16i-18i-21i Programmierung AWZ-C-Achse-Hauptantrieb (Monforts) | 4 | same page |

## D133 Bare SKIP under a switch list

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE/02, PDF 331 (§13, BDT1 = "/ oder /1", //3 wrong) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 327-328 | other edition |
| B-64484GE/02, PDF 332 (note 1, the 1 may be left out; case 4) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 328 | other edition |
| SINUMERIK 840D sl / 828D Grundlagen 03/2013, PDF 40 (§2.2, Ausblendebenen) | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 40 | same page |

## D144 The form of n in `CYCLE:<controller>=n`

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| Mori Seiki Programmieranleitung NTX1000 SZM (PX-NT_4-K0DEEN), PDF 80 (§2-1, G-code list) | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 80 | same page |
| FANUC 30i-B Betriebshandbuch Drehen B-64484GE-1/02, PDF 30 (Tabelle 2 (a), G71/G73) | 30i-B Bedienung-Drehen (B-64484EN-1) | 28 | other edition |
| B-64484GE-1/02, PDF 27 (Tabelle 2 (a), G20/G21 = G70/G71 in C) | 30i-B Bedienung-Drehen (B-64484EN-1) | 25 | other edition |
| SINUMERIK 840D sl Zyklen Programmierhandbuch 01/2008, PDF 12 (§1.1 overview of cycles) | Sinumerik-840D Programmierhandbuch-Zyklen (2008): no text at this page; the same overview is in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (Index, LY4401.10092, 2004) | 19 (in the Index 2004 edition) | not readable in the connector |

## D147 Which of A, B, C resolve to the workpiece holder's axis

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| Mori Seiki Programmieranleitung NTX1000 SZM, p. 74-76 (axis list; A axis note on 76) | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 74-76 | same page |
| same manual, p. 566 (§4-1, 4-2, A axis and BW) | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 566 | same page |
| DOOSAN PUMA 2600 LSY II Spezifikation und Wartung (2020), PDF 56 (axis assignment) | PUMA-2600-LSY-II Benutzerhandbuch-Spezifikation-Wartung | 56 (the heading only; the axis names are in the figure, which has no text) | same page |
| same manual, PDF 86 (B travel 830 mm, rapid 30 m/min) | PUMA-2600-LSY-II Benutzerhandbuch-Spezifikation-Wartung | 86 | same page |
| same manual, PDF 200 (lubrication item, B-Achsenführungsbahn) | PUMA-2600-LSY-II Benutzerhandbuch-Spezifikation-Wartung | 200 | same page |
| same manual, PDF 242 (travel table, note 3) | PUMA-2600-LSY-II Benutzerhandbuch-Spezifikation-Wartung | 242 | same page |
| Nakamura-Tome NT NURSE Bedienungsanleitung (4800001EBG), PDF 242 (G359, Z and B) | Fanuc-16-21i Bedienung NT-Nurse-Bedienungsanleitung (4800001EBG) | 242 | same page |
| Nakamura-Tome Programmierhandbuch (DE 2019), PDF 133-139 (NTJ100 chapter, B angle) | Fanuc Programmierung Programmierhandbuch (Nakamura-Tome, DE 2019) | 132-137 | same page |
| same manual, PDF 24 (M list, "lineare B-Achse rechts") | Fanuc Programmierung Programmierhandbuch (Nakamura-Tome, DE 2019) | 24 | same page |
| DMG MORI CTX/GMX Technik und Programmierung Sinumerik 840D sl V2.1 (2015), PDF 29-36 (L701..L708, C1..C4) | CTX-GMX Technik-Programmierung-Sinumerik840Dsl V2.1: no text. The L-cycle list is readable in CTX-GMX Technik-Programmierung-ShopTurn3G V2.1 (index) and in CTX-NTX Trainingshandbuch-Sinumerik-Operate (A-D213DE-1) | ShopTurn3G V2.1 index 253-254; Trainingshandbuch 76 | not readable in the connector |
| same manual, PDF 38, 100-102 and 119 (B1 change point, ANG B1 -30/210) | CTX-GMX Technik-Programmierung-Sinumerik840Dsl V2.1: no text; no readable alternative found | none | not readable in the connector |
| Hermle M-Funktionen C 400 ... C 60 (2015), PDF 3 (M25/M26, M27/M28) | Allgemein Programmierung M-Funktionen-C400-C12-C60 (Hermle) | 3 | same page |

## D148 Start values of a callee's V1..V33

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE/02, PDF 427 (§16.1, local variables start as <Null>) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 448 | other edition |
| B-64484GE/02, PDF 505 (§16.9, macro call versus subprogram call) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 524 | other edition |
| B-64484GE/02, PDF 511 (§16.9.1, levels of local variables) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 529 | other edition |

## D153 Padded placeholders against numbers of another width

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE-1/02, PDF 149 (§5.1.2, T code, parameters 5028, 5002, 3032) | 30i-B Bedienung-Drehen (B-64484EN-1) | 149 | other edition |
| Doosan NC Programming for Turning Centers, Fanuc 30 Series, PDF 19 (§3.14, T0021) | Allgemein Programmierung NC-Programming-Turning-Centers-Fanuc-30-Series (Doosan): no text layer | none | not readable in the connector |
| same manual, PDF 73 (G74 example, T202 ... T200) | Allgemein Programmierung NC-Programming-Turning-Centers-Fanuc-30-Series (Doosan): no text layer | none | not readable in the connector |
| Nakamura-Tome NT-IPS Bedienungsanleitung (4800554EAG), PDF 27 ("T101 oder T0101") | NTY3 Software-Tools NT-IPS-Bedienungsanleitung (4800554EAG) | 27 | same page |
| B-64484GE/02, PDF 433 (§16.2, #10001-#10999, #11001-#11999) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 453 | other edition |

## D160 Fanuc lathe drilling: a lathe catalog and an axis key

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE-1/02, p. 92 (Table 4.3 (a)) | 30i-B Bedienung-Drehen (B-64484EN-1) | 92 | other edition |
| B-64484GE-1/02, p. 93 (Table 4.3 (b)) | 30i-B Bedienung-Drehen (B-64484EN-1) | 93 | other edition |
| B-64484GE-1/02, p. 96 (G83/G87 format, RTR, G83.5/G83.6) | 30i-B Bedienung-Drehen (B-64484EN-1) | 95 | other edition |
| B-64484GE-1/02, p. 98 (without Q a plain drilling cycle) | 30i-B Bedienung-Drehen (B-64484EN-1) | 96-97 | other edition |
| B-64484GE-1/02, p. 100 (G85/G89, return at twice the feed) | 30i-B Bedienung-Drehen (B-64484EN-1) | 99 | other edition |
| B-64484GE-1/02, p. 30 (lathe G-code list, G81/G82 FS15, no G86) | 30i-B Bedienung-Drehen (B-64484EN-1) | 28 | other edition |
| Nakamura-Tome Präzisionsdrehzentrum Programmierhandbuch (1/2), 4810004ECL-1, p. 238 (Table 14-1) | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 238 | same page |

## D161 NCX names of Fanuc G92 and G76

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE-1/02, p. 40 (§4.1.2, G92 format, F = lead) | 30i-B Bedienung-Drehen (B-64484EN-1) | 38 | other edition |
| B-64484GE-1/02, p. 41 (G92 operations, back to A) | 30i-B Bedienung-Drehen (B-64484EN-1) | 39 | other edition |
| B-64484GE-1/02, p. 84-85 (§4.2.7, G76 two blocks, figure 4.2.7 (b)) | 30i-B Bedienung-Drehen (B-64484EN-1) | 83-85 | other edition |
| B-64484GE-1/02, p. 30 (G76 in group 00) | 30i-B Bedienung-Drehen (B-64484EN-1) | 28 | other edition |
| Programmierhandbuch Zyklen SINUMERIK 840D sl/840D/840Di sl (01/2008), p. 351-352 (CYCLE97) | Sinumerik-840D Programmierhandbuch-Zyklen (2008): no text at these pages (its index lists CYCLE97 at 351); readable in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (Index, LY4401.10092, 2004) | 316-317 (in the Index 2004 edition) | not readable in the connector |

## D163 Heidenhain 200 and 203: which Q values select the NCX cycle

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| TNC 640 Zyklenprogrammierung (NC-SW 34059x-10, 10/2019), p. 96 (203 without chip break) | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 96 | same page |
| same manual, p. 97 (203 with chip break) | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 97 | same page |
| same manual, p. 98 (203 with chip break and decrement; step 9) | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 98 | same page |
| same manual, p. 100-101 (Q213 "Anzahl Spanbrüche vor Rückzug") | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 100-101 | same page |
| iTNC 530 Zyklenprogrammierung (NC-SW 340490-07, 2011), p. 87 (203 step 3 and step 6) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 87 | same page |
| same manual, p. 90 (Q213 0..99999, Q256 from 0,1) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 89-90 | same page |
| same manual, p. 80 (cycle 200, Q211) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 80 | same page |
| same manual, p. 79 (cycle 200 sequence) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 79 | same page |
| SINUMERIK 840D sl Zyklen Programmierhandbuch 01/2008, p. 51 (CYCLE83, VARI) | Sinumerik-840D Programmierhandbuch-Zyklen (2008): no text at this page; readable in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (Index, LY4401.10092, 2004) | 56-57, 61 (in the Index 2004 edition) | not readable in the connector |
| FANUC 30i-B Bedienungshandbuch Fräsen B-64484GE-2/02, PDF 66 (G73, parameter 5114) | 30i-B Bedienung-Fraesen (B-64484EN-2) | 60 | other edition |
| B-64484GE-2/02, PDF 75 (G83, parameter 5115) | 30i-B Bedienung-Fraesen (B-64484EN-2) | 68-69 | other edition |

## D168 AND and OR evaluate both operands (language 4.12)

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE/02, p. 489 (Table 16.4 (a), AND/OR/XOR bitwise) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 508 | other edition |
| B-64484GE/02, p. 493 (operator priority) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 512 | other edition |
| B-64484GE/02, p. 501 (§16.8.3, complex conditional expression) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 520 | other edition |
| SINUMERIK 840D sl/828D Arbeitsvorbereitung 03/2013, p. 71-72 (§1.4, logical operators) | Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 71-72 | same page |
| TNC 640 Klartextprogrammierung 892903-18, p. 310 (§9.7, SQL WHERE) | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 310 | same page |
| same manual, p. 279 (FN 9 to FN 12) | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 279 | same page |

## D171 Where syncPartner and syncPhase stand

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| SINUMERIK 840D sl Arbeitsvorbereitung BS.000504 (08/2018), p. 594 (§13.5, LS and FS) | Sinumerik-840D-sl Programmierung (BS.000504) | 594 | same page |
| same manual, p. 595-597 (§13.5.1, COUPON syntax, COUPONC) | Sinumerik-840D-sl Programmierung (BS.000504) | 595-597 | same page |
| same manual, p. 603-604 ($AA_COUP_ACT, $AA_COUP_OFFS) | Sinumerik-840D-sl Programmierung (BS.000504) | 603-604 | same page |
| Nakamura-Tome Programmierhandbuch Band 1, 4810004ECL-1, p. 123-124 (M92, M93, M96, M97) | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 122-124 | same page |
| Nakamura-Tome Liste der Warn- und Alarmmeldungen, 4830001EEJ, p. 78 (alarm A11.0) | Fanuc Parameter-Alarme Liste-Warn-Alarmmeldungen (4830001EEJ) | 78 | same page |

## D173 Variable map of a Siemens machine

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| SINUMERIK 840D sl NC-Programmierung 06/2019, p. 398-399 (R parameters) | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW492, 2019) | 398-399 | same page |
| same manual, p. 400-401 (RG[<n>], MD15800) | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW492, 2019) | 400-401 | same page |
| SINUMERIK 840D sl Zyklen Programmierhandbuch 01/2008, p. 18 (N10 R1=5 R2=0.1 R3=1 R4=0) | Sinumerik-840D Programmierhandbuch-Zyklen (2008): no text at this page; the same example is in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (Index, LY4401.10092, 2004) | 25 (in the Index 2004 edition) | not readable in the connector |

## D174 Fanuc G-code systems B and C: cycle numbers

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE-1/02, p. 30 (group 00, 01, 05 rows of Table 2 (a)) | 30i-B Bedienung-Drehen (B-64484EN-1) | 28 | other edition |
| B-64484GE-1/02, p. 27 (group 06 units row) | 30i-B Bedienung-Drehen (B-64484EN-1) | 25 | other edition |
| B-64484GE-1/02, p. 28 (G50 = G92 in B and C) | 30i-B Bedienung-Drehen (B-64484EN-1) | 26 | other edition |
| B-64484GE-1/02, p. 26 (systems A/B/C, parameter 3401 GSB/GSC) | 30i-B Bedienung-Drehen (B-64484EN-1) | 24 | other edition |
| FANUC 0i-D Bedienerhandbuch Drehen B-64304GE-1, p. 34-35 | 0i-D Bedienerhandbuch-Drehen (B-64304EN-1-01) | 32 | other edition |
| FANUC 16i/18i-TB Bedienungshandbuch B-63524GE-01, p. 64 | 16-18-TB Bedienerhandbuch (B-63524EN-01) | 66-67 | other edition |

## D175 Words of the Fanuc turning cycles

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE-1/02, p. 39 (G90 X(U) Z(W) R F, R = taper) | 30i-B Bedienung-Drehen (B-64484EN-1) | 37 | other edition |
| B-64484GE-1/02, p. 40 (table 4.1.1.2 (a), sign of R) | 30i-B Bedienung-Drehen (B-64484EN-1) | 38 | other edition |
| B-64484GE-1/02, p. 48 (G94, R a radius value) | 30i-B Bedienung-Drehen (B-64484EN-1) | 45 | other edition |
| B-64484GE-1/02, p. 53 (modal X(U), Z(W), R) | 30i-B Bedienung-Drehen (B-64484EN-1) | 49 | other edition |
| B-64484GE-1/02, p. 35-36 (§3.3, Q thread start angle) | 30i-B Bedienung-Drehen (B-64484EN-1) | 33-34 | other edition |
| B-64484GE-1/02, p. 56-57 (G71 two blocks, note 1) | 30i-B Bedienung-Drehen (B-64484EN-1) | 53-54 | other edition |
| B-64484GE-1/02, p. 73 (G73 first block) | 30i-B Bedienung-Drehen (B-64484EN-1) | 72-73 | other edition |
| B-64484GE-1/02, p. 80 (G74 format) | 30i-B Bedienung-Drehen (B-64484EN-1) | 80 | other edition |
| B-64484GE-1/02, p. 81 (note 1, e and Δd) | 30i-B Bedienung-Drehen (B-64484EN-1) | 81 | other edition |
| B-64484GE-1/02, p. 82 (G75 format) | 30i-B Bedienung-Drehen (B-64484EN-1) | 82 | other edition |
| B-64484GE-1/02, p. 84 (G76 second block) | 30i-B Bedienung-Drehen (B-64484EN-1) | 83-85 | other edition |
| B-64484GE-1/02, p. 86 (G76 notes 1 and 3) | 30i-B Bedienung-Drehen (B-64484EN-1) | 86 | other edition |

## D176 Fanuc G74..G76 take no contour range

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE-1/02, p. 56 (G71 ns/nf) | 30i-B Bedienung-Drehen (B-64484EN-1) | 53 | other edition |
| B-64484GE-1/02, p. 73 (G73 ns/nf) | 30i-B Bedienung-Drehen (B-64484EN-1) | 72-73 | other edition |
| B-64484GE-1/02, p. 76 (G70 ns/nf) | 30i-B Bedienung-Drehen (B-64484EN-1) | 76 | other edition |
| B-64484GE-1/02, p. 80 (G74 format) | 30i-B Bedienung-Drehen (B-64484EN-1) | 80 | other edition |
| B-64484GE-1/02, p. 82 (G75 format) | 30i-B Bedienung-Drehen (B-64484EN-1) | 82 | other edition |
| B-64484GE-1/02, p. 84 and 86 (G76 P, Q, R; note 1) | 30i-B Bedienung-Drehen (B-64484EN-1) | 83-85, 86 | other edition |

## D178 Q parameter lists of the Heidenhain catalog cycles

All rows: HEIDENHAIN iTNC 530 Benutzer-Handbuch Zyklenprogrammierung (670 388-12, NC-SW 340490-07), connector document iTNC530 Zyklen 670 388-12 NC-SW-340490-07.

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| p. 86 (cycle 202 example) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 86 | same page |
| p. 78 (cycle 240 example) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 78 | same page |
| p. 90 (cycle 203 example) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 90 | same page |
| p. 94 (cycle 204 example) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 94 | same page |
| p. 98 (cycle 205 example) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 98 | same page |
| p. 101 (cycle 208 example) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 101 | same page |
| p. 104 (cycle 241 example) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 104 | same page |
| p. 118 (cycle 209 example and pitch Q239) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 118 | same page |
| p. 112 (cycle 206, Q206) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 112 | same page |
| p. 115 (cycle 207, Q239) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 115 | same page |
| p. 149 (cycle 251, Q207) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 149 | same page |
| p. 153 (cycle 252, Q207) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 153 | same page |
| p. 158 (cycle 253, Q207) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 158 | same page |
| p. 163 (cycle 254, Q207) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 163 | same page |
| p. 167 (cycle 256, Q207) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 167 | same page |
| p. 171 (cycle 257, Q207) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 171 | same page |
| p. 123 (cycle 262, Q207 and Q239) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 123 | same page |
| p. 214 (cycle 275, Q207) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 214 | same page |
| p. 264 (cycle 230, Q207) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 264 | same page |
| p. 268 (cycle 231, Q207) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 268 | same page |
| p. 273 (cycle 232, Q207) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 273 | same page |
| p. 318 (cycle 225, Q207=750) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 318 | same page |
| p. 456 (cycle 213 in the programming example) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 456 | same page |
| p. 82 (cycle 201, Q208 = 0) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 82 | same page |

## D180 TOOL CALL S without a tool number on the iTNC 530

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| HEIDENHAIN iTNC 530 Klartext-Programmierung (NC-SW 340490-04), p. 197 (§5.1, programmed speed change) | iTNC530 Programmierung-Klartext NC-SW-340490-04: no text; the same procedure is in iTNC530 Programmierung-Klartext NC-SW-340490-05 | 161 (in 340490-05; also NC-SW-606420-01 p. 163) | not readable in the connector |
| HEIDENHAIN TNC 640 Klartextprogrammierung (892903-18), p. 123 (speed-only TOOL CALL cases) | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 123 | same page |

## D182 FN 18 ids of the iTNC 530 for SYS_ names

The cited edition (NC-SW 340490-04) is listed in the connector but returns no text; every row resolves to iTNC530 Programmierung-Klartext NC-SW-340490-05.

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| iTNC 530 Klartext-Programmierung (NC-SW 340490-04), p. 623 (§11.8, FN 18 opening) | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 306 | not readable in the connector |
| same manual, p. 627, 628 (groups 240 and 270) | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 310-311 | not readable in the connector |
| same manual, p. 624, 625 (group 50, "Ohne Index") | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 307-308 | not readable in the connector |
| same manual, p. 626, 629 (group 200; example `55 FN 18: SYSREAD Q25 = ID210 NR4 IDX3`) | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 309, 312 | not readable in the connector |

## D184 ARC without feed

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE/02, p. 2021 (parameter 1404 bit 7 FC0) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 2072-2073 (also 30i-B Parameterhandbuch (B-64490EN-05) 82) | other edition |
| B-64484GE/02, p. 85 (§4.4, circular feed along the tangent) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 79-80 | other edition |
| SINUMERIK 840D sl Parameter, Alarme BS.000503 (08/2018), p. 152 (alarm 10860) | Sinumerik-840D-sl Parameter Alarme (BS.000503) | 152 | same page |
| FANUC 0i-D Bedienerhandbuch Drehen/Fräsen B-64304GE/01, p. 1424 (alarm 0011) | 0i-D Bedienung-Drehen-Fraesen (B-64304EN) | 1085 | other edition |
| Nakamura-Tome Liste der Warn- und Alarmmeldungen, 4830001EEJ, p. 282 (alarm 0011) | Fanuc Parameter-Alarme Liste-Warn-Alarmmeldungen (4830001EEJ) | 282 (also Super-NTJX Parameter-Alarme Warning-Alarm-Messages (4830001EER) 262) | same page |
| Mazatrol Matrix 2 Programmieranleitung EIA/ISO H740PB6043G, p. 125 (§7-13, G93) | Mazatrol-Matrix2 Programmieranleitung-EIA-ISO H740PB6043G | 125 | same page |
| TNC 640 Klartextprogrammierung 892903-18, p. 159-160 (Kreisbahn C, CR) | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 159-160 | same page |

## D187 Position after the call of a catalog cycle

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE-1/02, p. 38 (G90 operation 4) | 30i-B Bedienung-Drehen (B-64484EN-1) | 36 | other edition |
| B-64484GE-1/02, p. 41 (G92 operation 4) | 30i-B Bedienung-Drehen (B-64484EN-1) | 39 | other edition |
| B-64484GE-1/02, p. 47 (G94 operation 4) | 30i-B Bedienung-Drehen (B-64484EN-1) | 44 | other edition |
| B-64484GE-1/02, p. 53 (modal data, U-8/U-16 example) | 30i-B Bedienung-Drehen (B-64484EN-1) | 49 | other edition |
| B-64484GE-1/02, p. 74 (G73 note 3) | 30i-B Bedienung-Drehen (B-64484EN-1) | 73 | other edition |
| B-64484GE-1/02, p. 76 and 77 (G70 return, "Rückkehr zum Zyklusstartpunkt") | 30i-B Bedienung-Drehen (B-64484EN-1) | 76-77 | other edition |
| B-64484GE-1/02, p. 87 (Table 4.2.7 (a), G76 legs) | 30i-B Bedienung-Drehen (B-64484EN-1) | 87 (the table heading only; the table is an image) | not readable in the connector |
| B-64484GE-1/02, p. 85 (figure 4.2.7 (a), loop from A to A) | 30i-B Bedienung-Drehen (B-64484EN-1) | 85 (the caption only; the figure is an image) | not readable in the connector |
| B-64484GE-1/02, p. 61 and 63 (G71 type II returns) | 30i-B Bedienung-Drehen (B-64484EN-1) | 57-58 | other edition |
| B-64484GE-1/02, p. 72 (G72 type II return) | 30i-B Bedienung-Drehen (B-64484EN-1) | 71 | other edition |

## D189 A drilling-axis word in a CYCLE_CALL block

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| iTNC 530 Zyklenprogrammierung (670 388-12), p. 55 (CYCL CALL POS) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 55 | same page |
| same manual, p. 54, 55 (CYCL CALL, M99) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 54-55 | same page |
| FANUC 30i-B Bedienungshandbuch Fräsen B-64484GE-2, p. 75 (G83 format, Z from R to the bottom) | 30i-B Bedienung-Fraesen (B-64484EN-2) | 68-69 (the format block has no text; the same Z definition is readable at 63 for G76, 70 for the small-hole G83, 102 for rigid G84) | other edition |
| B-64484GE-2, p. 84 (G85 format) | 30i-B Bedienung-Fraesen (B-64484EN-2) | 76 (the format block has no text) | other edition |
| iTNC 530 Zyklenprogrammierung (670 388-12), p. 79, 80 (cycle 200 notes and step 1) | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 79-80 | same page |

## D193 Modulo rotary axis or travel-limited rotary axis

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| Fanuc 0i-D Connection Manual (Function) Vol.1 B-64303EN-1-02, p. 137-138 (§1.5.4, ROAx) | 0i-D Connection-Manual-Function-Vol1 (B-64303EN-1-02) | 137-138 | same page |
| Heidenhain TNC 640 Technisches Handbuch (892899-18), p. 801 (CfgAxis, isModulo) | TNC640 Technisches-Handbuch NC-SW-34059x-09 (892899-18) | 801 | same page |
| Heidenhain TNC 640 Aktuelle Information Nr. 6, p. 11 (extendedLimitCtrl) | TNC640 Aktuelle-Information-Nr6 | 11 | same page |
| SINUMERIK 840D sl NC-Programmierung (SW 4.92, 2019), p. 603 (CALCPOSI, Rundachsen) | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW492, 2019) | 603 | same page |
| SINUMERIK 840D sl/840D/840Di sl Zyklen (01/2008), p. 253 (CYCLE800 Winkelbereich, modulo 0..360) | Sinumerik-840D Programmierhandbuch-Zyklen (2008): no text at this page; the same sentence is in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (Index, LY4401.10092, 2004) | 242 (in the Index 2004 edition) | not readable in the connector |
| SINUMERIK 840D sl Arbeitsvorbereitung BS.000504 (08/2018), p. 644 (MODAXVAL) | Sinumerik-840D-sl Programmierung (BS.000504) | 644 | same page |
| WFL Millturn, Programmierung Sinumerik 840D, Kapitel 15, p. 9 (MCC1ZERO) | Millturn Zyklen-Kap15-Maschinenzyklen (WFL) | 9 | same page |

## D194 Verbs of 'spindle OFF before a LINE'

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE-2/02, p. 72 ("Spindeldrehung", the G81 section) | 30i-B Bedienung-Fraesen (B-64484EN-2) | 66 | other edition |
| B-64484GE-2/02, p. 82 ("Spindeldrehung", the G84 section) | 30i-B Bedienung-Fraesen (B-64484EN-2) | 74 | other edition |
| B-64484GE-2/02, cache p. 66, 68, 70, 72, 74, 75, 82, 84, 86, 87, 89, 91 (the paragraph for G73, G74, G76, G81, G82, G83, G84, G85, G86, G87, G88, G89) | 30i-B Bedienung-Fraesen (B-64484EN-2) | 60, 62, 64, 66, 67, 69, 74, 76, 77-78, 79, 81, 83 | other edition |
| B-64484GE-2/02, p. 108 (§5.3, standard and rigid mode) | 30i-B Bedienung-Fraesen (B-64484EN-2) | 102 | other edition |
| B-64484GE-2/02, p. 109 (M29 S, parameter 5200 bit 0) | 30i-B Bedienung-Fraesen (B-64484EN-2) | 102-103 | other edition |
| Programmierhandbuch Zyklen SINUMERIK 840D sl/840D/840Di sl, p. 61 (CYCLE84 sequence) | Sinumerik-840D Programmierhandbuch-Zyklen (2008): no text at this page; readable in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (Index, LY4401.10092, 2004) | 65 (in the Index 2004 edition) | not readable in the connector |

## D198 Which states of a function table trigger its expansion rule

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| DMG MORI CTX/GMX ShopTurn 3G V2.1 (2015), p. 210 and 212 (M436/M433/M437, M336/M333/M337) | CTX-GMX Technik-Programmierung-ShopTurn3G V2.1: only the index pages have text | none | not readable in the connector |
| Biglia Smart-Turn Programmierhandbuch T140-00379-IM1 (2010), p. 48 (§5.14, M68/M69) | Smart-Turn Programmierhandbuch T140-00379-IM1 (Biglia): no text | none | not readable in the connector |
| BURKHARDT+WEBER MCX 800 HVP Betriebsanleitung Band 1 (2019), p. 370-371 (§6.9.2, Tabelle 6-8) | MCX800-HVP Betriebsanleitung-Band1 (Burkhardt+Weber): only the index pages (559-566) have text; the index lists the M functions at 6-17 to 6-24 | none | not readable in the connector |
| Hermle M-Funktionen C 400 ... C 60 (2015), p. 2-5 (M51/M52, M25-M28) | Allgemein Programmierung M-Funktionen-C400-C12-C60 (Hermle) | 2-5 | same page |

## D204 Triggering block of a catalog cycle's expansion rule

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| FANUC 30i-B Drehmaschinensystem B-64484GE-1/02, p. 101 (example under 4.3.4: M51 ... G80 M05, M50) | 30i-B Bedienung-Drehen (B-64484EN-1) | 100 (the same example also on 96-97) | other edition |
| B-64484GE-1/02, p. 93 (modal G83..G89, clamp M code cancelled by G80) | 30i-B Bedienung-Drehen (B-64484EN-1) | 93 | other edition |
| B-64484GE-1/02, p. 94 (clamp code output, "Beenden") | 30i-B Bedienung-Drehen (B-64484EN-1) | 94 | other edition |
| B-64484GE-1/02, p. 102 (improved M code output) | 30i-B Bedienung-Drehen (B-64484EN-1) | 100-101 | other edition |
| M-Funktionen Puma 2600SY (Doosan), p. 4 (M291, M292) | Puma-2600SY M-Funktionen (Doosan) | 1-4 | same page |
| B-64484GE-1/02, p. 96 (RTR, G83.5/G83.6) | 30i-B Bedienung-Drehen (B-64484EN-1) | 95 | other edition |

## D208 Fanuc I J K: absolute CENTER or CENTER:IX

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE/02, cache p. 84 (§4.4, I J K always incremental) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 78-79 | other edition |
| B-64484GE/02, cache p. 231 (Tabelle 8.4 (a), diameter and radius values) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 230-231 | other edition |
| SINUMERIK 840D sl/828D Grundlagen 03/2013, cache p. 170 (§8.3.6, DIAMON and I J K) | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 170 | same page |

## D210 Kind of a Fanuc O section that no block of the file calls

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE/02, cache p. 333 (Tabelle 13.2 (d), VORSICHT) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 328 | other edition |
| B-64484GE/02, cache p. 337 (§13.3, M99 in the main program) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 331-332 | other edition |
| B-64484GE/02, cache p. 337 (§13.3, "Nur Unterprogramm") | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 331-332 | other edition |
| FANUC 16i/18i-MB Bedienungshandbuch B-63534GE/02, cache p. 199 (Tabelle 12.2 (b)) | 16-18-MB Bedienerhandbuch (B-63534EN-02) | 202-203 (there table 12.2 (d)) | other edition |

## D211 Missing program end in Fanuc and Siemens sources

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| B-64484GE/02, cache p. 323-324 (ch. 13, program components, program section) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 320-321 | other edition |
| B-64484GE/02, cache p. 61 (§1.7, program end M02 or M30) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 57 | other edition |
| B-64484GE/02, cache p. 2412 (Anhang H, alarm 5010) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 2453-2454 | other edition |
| the 0i-D volume, cache p. 1447 (alarm 5010) | 0i-D Bedienung-Drehen-Fraesen (B-64304EN) | 1109 | other edition |
| SINUMERIK 840D sl/828D Grundlagen 03/2013, cache p. 37 (§2.2, Programmende) | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 37 | same page |
| Okuma Alarm- und Fehlerliste OSP-P200L/P20L LG37-005-R4, cache p. 342 (alarm 2937) | OSP-P200 Alarm-Fehlerliste LG37-005-R4 (Okuma) | 342 | same page |

## D212 REPEAT without TIMES

| Record cites | Connector document | Connector page | Status |
|---|---|---|---|
| SINUMERIK 840D sl / 828D NC-Programmierung (06/2019, SW 4.92), p. 479 (REPEAT without P) | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW492, 2019) | 478-479 | same page |
| B-64484GE/02, p. 335 (M98 note 2) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 335 | other edition |
| B-64484GE/02, p. 1108 (§4.5, M198 without L) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 1144-1145 | other edition |
| HEIDENHAIN TNC 640 Klartextprogrammierung (892903-18), p. 248, 249 (CALL LBL, REP) | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 249-250 | page moved |

## Summary

220 citations mapped: 102 same page, 1 page moved, 97 other edition, 20 not readable in the connector, 0 not found. Of the 20 not readable, 12 have a readable alternative named in the row (the iTNC 530 340490-05 edition, the Index-branded 840D Zyklen 2004, the DMG L-cycle index); 8 have none (two Doosan Fanuc 30 Series pages, the DMG 840D sl V2.1 B1 pages, DMG ShopTurn 3G, Biglia Smart-Turn, B+W MCX 800 HVP, the two G76 image pages of B-64484EN-1).

## Found while re-paging

None of these changes a decision or contradicts the record; they correct or complete the two audit reports. Nothing was fixed in the record.

1. **D193:** the CYCLE800 sentence "Bei Moduloachsen ist der Verfahrbereich zwischen 0 und 360 grd einzutragen", which `02-settled-D174-D212.md` lists as NOT FOUND, is in the connector: Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (Index, LY4401.10092, 2004), p. 242, under "Winkelbereich $TC_CARR30[n] .. $TC_CARR33[n]". The record's statement holds, in an earlier edition than the one it cites.
2. **D198:** the BURKHARDT+WEBER MCX 800 HVP Betriebsanleitung Band 1 (2019), which the audit reports as "not found at all", is in the connector (MCX800-HVP Betriebsanleitung-Band1). Only its index pages (559-566) have text, so Tabelle 6-8 still cannot be checked.
3. **D184:** the audit says the Nakamura alarm list 4830001EEJ is "a different edition" and re-cites 4830001EER p. 262. 4830001EEJ is in the connector, and alarm 0011 FEED ZERO (COMMAND) stands on p. 282, as the record cites.
4. **D117:** the audit maps both B-64484GE/02 PDF 203 and 207 to B-64484EN p. 205. The format (GE 203) is on EN p. 204-205; the T-series sentence that ends "(Coordinate system shift)" (GE 207) is on EN p. 208, in the CAUTION after "Changing by setting a workpiece coordinate system". Both EN pages carry the incremental rule.
5. **D189:** the audit cites B-64484EN-2 p. 69-70 for the G83 meaning of Z. On EN-2 the format blocks of the standard G83 (p. 68) and of G85 (p. 76) have no text layer; p. 70 is the small-hole peck drilling cycle (G83 with I). The wording "Z: distance from point R to the bottom of the hole" can be read on EN-2 p. 63 (G76), p. 70 (small-hole G83) and p. 102 (rigid G84), and on EN-1 p. 95 for the lathe G83.
6. **D127:** a nuance on NT Nurse p. 292, which the audit did not re-read. The page opens "Neueinstellung der C-Achsen-Koordinate über G50 entsprechend dem programmierten Wert auf der Grundlage der Position, an der die Spannstange das Werkstück berührt": the C coordinate is set at the position found by the work-navigator touch, not simply where the axis stands. The record already limits the page to that one function, so its use of the page holds.
7. **D147:** the Doosan axis-assignment page (PDF 56) carries the axis names only in a figure with no text; the connector confirms only the heading there. The linear B axis can be read in text on p. 86 and in the travel table on p. 242; the names C1 and C2 cannot be read on p. 56.
