# Citation concordance: settled decisions D214 to D346

Status: 2026-10-26. The record's citations of these entries, resolved to the connector.

The settled record (`../rationale.md`, section "Answered (ninth round"), cites the manuals as they stood in a local extraction: German Fanuc editions, the iTNC 530 Klartext 340490-04, the Siemens Zyklen 01/2008 and a few other editions that the cnc-manuals connector either lacks or lists without readable text, and pages counted sometimes as PDF pages, sometimes as printed pages. This file gives, for every cited source, the connector's document (its title as `search_manuals` shows it, with the document number where the title alone is ambiguous) and the connector's page, which is always the PDF page that `read_pages` takes. One row stands for each citation in the entry's Question, Recommendation, Where and ANSWER/Evidence that names a manual and a page; a page named inside an Evidence note (for example "the TNC 640 repeats the list on page 66") gets its own row. Status: **same page** (the cited document is in the connector and the passage is on the cited page); **page moved** (same document, other page); **other edition** (the cited edition is not in the connector; the same passage is given in the twin edition the connector has); **not readable in the connector** (the document is listed but returns no text for the passage; a sibling edition is given where one carries it); **not found** (the passage is in no document of the connector). The mappings come from the audit reports 03, 04 and 05 of this folder and, where they give none, from reading the connector. The German Fanuc editions are mapped to their English twins (B-64484EN, EN-1, EN-2, B-64490EN-05, B-64304EN, B-63524EN) by finding the same passage. The iTNC 530 Klartext 340490-04 is mapped to the Klartext 340490-05 (and 606420-01). For the Siemens Zyklen 01/2008 the connector holds the earlier Siemens edition of the same book, "Programmieranleitung Zyklen (PGZ), Ausgabe 10.04", filed as an INDEX document (LY4401.10092), with readable text; it is given first where it carries the passage, followed by the sl sibling the audit reports name. 320 citations in 50 entries: 209 same page, 1 page moved, 47 other edition, 62 not readable in the connector (55 of them with a readable sibling that carries the text), 1 not found. The record itself is unchanged; this file corrects nothing in it.

## D214 PROGRAM=BEGIN and PROGRAM=END of a called external program

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| TNC 640 Klartextprogrammierung 892903-18, p. 252 | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 252 | same page |
| TNC 640 Klartextprogrammierung 892903-18, p. 253 | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 253 | same page |
| SINUMERIK 840D sl / 828D NC-Programmierung (CNC-SW 4.92, 06/2019), p. 350 | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 350 | same page |
| FANUC 30i-B Betriebshandbuch Drehen/Fraesen B-64484GE/02, PDF p. 304 (printed 270) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 297 | other edition |
| B-64484GE/02, PDF p. 333 (printed 299), table 13.2 (d) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 328 | other edition |
| B-64484GE/02, PDF p. 337 (printed 303), M99 in the main program | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 331 | other edition |

## D219 Fanuc T word: turret form or preload, by tool holder

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Mori Seiki PX-NT_4-K0DEEN, cache p. 313, §1-1 T-Funktion (Werkzeugspindel) | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 313 | same page |
| PX-NT_4-K0DEEN, cache p. 314–315, §1-2 T-Funktion (Revolver 2) | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 314–315 | same page |
| Nakamura-Tome 4810004ECL-1, cache p. 66–67, ch. 6 | Fanuc Programmierung Programmierhandbuch-1 (Nakamura-Tome 4810004ECL-1) | 66–67 | same page |
| 4810004ECL-1, cache p. 69, six-digit T form | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 69 | same page |
| 4810004ECL-1, cache p. 326, G340 cycle steps | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 326 | same page |
| 4810004ECL-1, cache p. 327–328, G340 T and A words, G340 T0199. A0299. | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 327–328 | same page |
| 4810004ECL-1, cache p. 331, G340 T0101. A02. | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 331 | same page |
| 4810004ECL-1, cache p. 332, G341 T | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 332 | same page |

## D220 CYCLE_CALL of a cycle defined without a position

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| iTNC 530 Zyklenprogrammierung 670 388-12, p. 54 | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 54 | same page |
| 670 388-12, p. 55 | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 55 | same page |
| TNC 640 Zyklenprogrammierung 892905-18, p. 64 (pages 64–66 the call forms) | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 64–66 | same page |
| 892905-18, p. 127, drilling example | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 127 | same page |
| Fanuc 0i-D Anwenderhandbuch Fräsen B-64304GE-2/01, PDF p. 73 (printed 49), G81 format, "X_ Y_ : Bohrungsposition" | 0i-D Bedienerhandbuch-Fraesen (B-64304EN-2-01) | 52 | other edition |
| B-64304GE-2/01, PDF p. 65 (printed 41), step 1 "Wenn ein Befehl zum Positionieren … existiert" | 0i-D Bedienerhandbuch-Fraesen (B-64304EN-2-01) | 39–40 (operation 1, positioning of X and Y); 52 (no drilling without X, Y, Z, R) | other edition; the conditional sentence itself not found in the English text |
| the same sentence in the 0i-D turning manual, PDF p. 137 | 0i-D Bedienerhandbuch-Drehen (B-64304EN-1-01) | 263–264 (operation 1, positioning); the conditional sentence not found | other edition |

## D221 Fanuc M99 P: return to a block of the caller

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| B-64484GE/02, cache p. 336 (printed 302), §13.3 return block number | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 331 | other edition |
| B-64484GE/02, cache p. 337 (printed 303), M99 in the main program, /M99Pn | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 331 | other edition |
| B-64484GE/02, cache p. 337 (printed 303), subprogram only, M99Pn | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 332 | other edition |

## D222 A datum inside a [workpiece] template

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Nakamura-Tome 4810004ECL-1, p. 133, M427/M428 | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 133 | same page |
| Nakamura-Tome 4810004ECL-2, p. 69-70, WT150 example | Fanuc Programmierung Programmierhandbuch-2 (4810004ECL-2) | 69–70 | same page |
| Liste M- und G-Codes Nakamura NTJX, p. 1 and 3 (datums, M427/M428) | Super-NTJX Programmierung Liste-M-G-Codes (2016) | 1–3 | same page |
| Liste M- und G-Codes Nakamura NTJX, p. 5 (G54 to G59) | Super-NTJX Programmierung Liste-M-G-Codes (2016) | 5 (chunk 4–6) | same page |
| Mori Seiki PX-NT_4-K0DEEN, p. 285, M303/M304 | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 285 | same page |
| PX-NT_4-K0DEEN, p. 145, G43 example | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 145 | same page |
| M-Funktionen Puma 2600SY, p. 2, 3, "M34 DREHMODUS LINKS" | Puma-2600SY M-Funktionen (Doosan) | 1–4 | same page |
| M-Funktionen Puma 2600SY, p. 2, 3, "M134 DREHMODUS RECHTS" | not in that sheet; M134 only as "right spindle select" in MX-Series CNC-Programmierbuch (Doosan) | 23 | not found |

## D226 Heidenhain Q204 that does not lift above the set-up clearance

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| TNC 640 Zyklenprogrammierung 892905-18, p. 87, cycle 200 | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 87 | same page |
| 892905-18, p. 96, cycle 203 | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 96 | same page |
| 892905-18, pages 87, 90, 92, 96, 97, 98, 102, 106, 114 and 125 (the Q204 sentence in ten cycles) | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 87, 90, 92, 96, 102, 106, 114, 125 found by search; 97 and 98 not hit separately | same page |
| TNC 640 User's Manual Cycle Programming 892905-28 (English), printed p. 102 and the cycle 200 / 204 pages | English edition not in the connector; German twin TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 87 (cycle 200), 102 (cycle 204) | other edition |
| TNC 128 Klartextprogrammierung 819494-15, p. 321, 323, 325, 338 | TNC128 Programmierung-Klartext NC-SW-771841-07 (819494-15) | 321, 323, 325, 338 (search hits; also 319) | same page |
| iTNC 530 Zyklenprogrammierung 670 388-12, p. 79 | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 79 | same page |
| 670 388-12, p. 80, Q204 definition | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 80 | same page |
| 670 388-12, p. 59, GLOBAL DEF | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 59 | same page |

## D227 Where the Klartext reader writes CYCLE=OFF

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| TNC 640 Zyklenprogrammierung 892905-18, p. 127 | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 127 | same page |
| iTNC 530 Zyklenprogrammierung 670 388-12, p. 80, cycle 200 example | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 80 | same page |
| 670 388-12, p. 53, prerequisites | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 53 | same page |
| "The TNC 640 repeats the list on page 64" | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 64 | same page |
| 670 388-12, p. 55, M89 cancel list | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 55 | same page |
| "The TNC 640 repeats the list on page 66" | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 66 | same page |
| 670 388-12, p. 278, coordinate transformations modal | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 278 | same page |

## D228 The MB of a PLANE, and PLANE RESET with TURN or MOVE

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| iTNC 530 Klartext-Dialog NC-SW 606420-01, p. 463, MB | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 463 | same page |
| 606420-01, p. 462, MOVE / TURN / STAY | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 462 | same page |
| TNC 640 Klartextprogrammierung 892903-18, p. 430, MB | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 430 | same page |
| 892903-18, "Page 429", MOVE / TURN / STAY | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 429 | same page |
| 606420-01, p. 448, PLANE RESET | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 448 | same page |
| 892903-18, p. 413, PLANE RESET | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 413 | same page |

## D241 Fanuc arc with a centre word left out

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| B-64484GE/02, PDF p. 84 (printed 50), section 4.4 | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 79 | other edition |
| Operator's Manual B-64484EN, PDF p. 79 | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 79 | same page |

## D242 Fanuc group 01 before the first G0 to G3, and the rate of G53

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Parameterhandbuch B-64490GE/03, PDF p. 202 (printed 190), parameter 3402 bit 0 | 30i-B Parameterhandbuch (B-64490EN-05) | 191 | other edition |
| B-64484GE/02, PDF p. 66 (printed 32), clear state item (4) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 62 | other edition |
| B-64484GE/02, PDF p. 200 (printed 166), section 7.1, G53 | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 201 | other edition |
| B-64484GE/02, PDF p. 201 (printed 167), G53.2, PS5372 | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 202 | other edition |
| B-64484GE/02, PDF p. 77 (printed 43), G00 nonlinear cases (G28, G53) | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 73 | other edition |
| Nakamura-Tome Programmierhandbuch (Walter Meier), Fanuc 0iTF/31iB/32iA, PDF p. 20, G list | Fanuc Programmierung Programmierhandbuch (Nakamura-Tome, DE 2019) | 20 | same page |
| the same, pp. 53-54, G53 detail page | Fanuc Programmierung Programmierhandbuch (Nakamura-Tome, DE 2019) | 53–54 | same page |

## D243 Fanuc G28 and G30 with an absolute word

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| B-64484GE/02, PDF p. 193 (printed 159), section 6.1, G28 | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 191–192 | other edition |
| Mori Seiki PX-NT 4-K0DEEN, PDF p. 124, section 2-18 cautions | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 124 | same page |
| Parameterhandbuch B-64490GE/03, PDF p. 62 (printed 50), parameter 1020 | 30i-B Parameterhandbuch (B-64490EN-05) | 59 | other edition |
| B-64484GE/02, PDF p. 67 (printed 33), note 8 | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 63 | other edition |
| Nakamura-Tome Programmierhandbuch (Walter Meier), PDF p. 111, two-channel header | Fanuc Programmierung Programmierhandbuch (Nakamura-Tome, DE 2019) | 111 | same page |

## D246 The words of Fanuc G68.1 and its NCX form

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Fanuc 30i/31i-A Programmierung, Kapitelauszug "4 PROGRAM FORMAT", printed p. 91 (image only) | 30i-A Programmierung-Kap-G68.1.1 | 1 (only the heading "4 PROGRAM FORMAT" is in the text layer) | not readable in the connector |
| B-64484GE/02, PDF p. 347 (printed 313), section 14.2, lathe G68.1 | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 340–341 | other edition |
| B-64484GE/02, PDF p. 74 (printed 40), table 3.2 (b), lathe G code list | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 70 | other edition |
| Fanuc 16i/18i-TB Benutzerhandbuch B-63524GE/01, PDF p. 238, 2D and 3D on the same codes, #5041 to #5048 | 16-18-TB Bedienerhandbuch (B-63524EN-01) | 241 | other edition |
| B-63524GE/01, PDF p. 241, the two-G68.1 example | 16-18-TB Bedienerhandbuch (B-63524EN-01) | 237–238 | other edition |
| Mori Seiki PX-NT 4-K0DEEN, PDF p. 175-177, section 2-35 | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 175–177 | same page |
| Nakamura-Tome Programmierhandbuch (Walter Meier), PDF p. 20-21, G68/G69, G358, G368/G369 | Fanuc Programmierung Programmierhandbuch (Nakamura-Tome, DE 2019) | 20–21 | same page |

## D247 G0 to G3 as the end of a Fanuc mill canned cycle

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| B-64484GE/02, p. 67 (printed 33), note 5 | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 63 | other edition |
| Bedienung Fräsen B-64484GE-2/02, p. 65 (printed 49), "Beenden" | 30i-B Bedienung-Fraesen (B-64484EN-2) | 59 | other edition |
| Betriebshandbuch Drehen B-64484GE-1/02, p. 94 (printed 78), "Beenden" | 30i-B Bedienung-Drehen (B-64484EN-1) | 94 | other edition |
| Fanuc 0i-D Bedienung Drehen B-64304GE-1/02, p. 104 (printed 84) | 0i-D Bedienerhandbuch-Drehen (B-64304EN-1-01) | 98 | other edition |
| B-64484GE-2/02, p. 73 (printed 57), G81 "Beenden" | 30i-B Bedienung-Fraesen (B-64484EN-2) | 66 | other edition |
| B-64484GE-2/02, pages 67, 69, 71, 74, 76, 83, 85, 86, 88, 90 and 92 (the same sentence for G73, G74, G76, G82 to G89) | 30i-B Bedienung-Fraesen (B-64484EN-2) | the "Cancel" note of each cycle, pp. 61–84 (for example G73 p. 61, G82 p. 68, G89 p. 84) | other edition |
| Nakamura-Tome Programming Manual (WY-250L) 4810004EEE, p. 283, chapter 14-7 | Fanuc Programmierung Programming-Manual (Nakamura-Tome 4810004EEE) | 283 | same page |
| 4810004EEE, p. 288, 14-7-6 item (2) | Fanuc Programmierung Programming-Manual (Nakamura-Tome 4810004EEE) | 288 | same page |
| Nakamura-Tome Programmierhandbuch (Walter Meier, 2019), p. 72 | Fanuc Programmierung Programmierhandbuch (Nakamura-Tome, DE 2019) | 72 | same page |

## D251 Feed of TOOL CALL and F AUTO

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| iTNC 530 Klartext-Dialog 606420-01, p. 205 | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 205 | same page |
| 606420-01, p. 180 | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 180 | same page |
| 606420-01, p. 462 | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 462 | same page |
| TNC 640 Klartextprogrammierung 892903-18, p. 429 | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 429 | same page |
| 892903-18, "Page 143", line-block dialog with F AUTO | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 143 | same page |
| 606420-01, p. 162 | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 162 | same page |
| TNC 640 Zyklenprogrammierung 892905-18, p. 89, Q206 | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 89 | same page |

## D252 When the M functions of a Klartext block act

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| TNC 640 Klartextprogrammierung 892903-18, p. 225, M table | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 225 (column lost in the text layer; the overview at 608–609 carries it) | same page |
| iTNC 530 Klartext-Dialog 606420-01, p. 352 | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 352 | same page |
| 606420-01, p. 353, M table | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 353 (the text layer does not keep the columns) | same page |
| 606420-01, p. 355, M91/M92 at block start | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 355 | same page |
| "page 356 'M104 wird wirksam am Satz-Ende'" | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 356 | same page |
| "page 368 'M140 wird wirksam am Satz-Anfang'" | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 368 | same page |
| "page 480 'M128 wird wirksam am Satz-Anfang, M129 am Satz-Ende'" | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 480 | same page |

## D254 Klartext LBL the main flow runs through, CC with one axis, PATTERN DEF surface

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| iTNC 530 Klartext-Dialog 606420-01, p. 273, subprograms | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 273 | same page |
| TNC 640 Klartextprogrammierung 892903-18, p. 247 | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 247 | same page |
| 606420-01, p. 218, CC | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 218 | same page |
| 892903-18, p. 158, CC | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 158 | same page |
| "The TNC 426/430 manual (page 169)", CC | TNC426-430 Programmierung-Klartext 340 135-17 | 169 | same page |
| iTNC 530 Zyklenprogrammierung 670 388-12, p. 64, PATTERN DEF | iTNC530 Zyklen 670 388-12 NC-SW-340490-07 | 64 | same page |
| TNC 640 Zyklenprogrammierung 892905-18, p. 75, PATTERN DEF | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 75 | same page |
| 892905-18, p. 74, retract between points | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 74 | same page |
| 892905-18, "Page 73", tool axis Z only | TNC640 Zyklen NC-SW-34059x-10 (892905-18) | 73 | same page |

## D255 Klartext CHF and RND: size, feed, and the incremental line after them

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| iTNC 530 Klartext-Dialog NC-SW 340490-04 (2007), p. 251, figure, chamfer dimensions | 340490-04 has no readable text; sibling iTNC530 Programmierung-Klartext NC-SW-606420-01 (text only, the figure is not in the text layer) | 216 | not readable in the connector |
| 340490-04, p. 251, figure, lower dimension chain 40 + 5 | as above, iTNC530 Programmierung-Klartext NC-SW-606420-01 (figure not in the text layer) | 216 | not readable in the connector |
| 340490-04, p. 251, text, CHF input, feed, notes, example 7–10 | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 216 | not readable in the connector |
| 340490-04, p. 252, RND | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 217 | not readable in the connector |
| 340490-04, p. 113, incremental coordinates | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 93 | not readable in the connector |
| 340490-04, p. 253, CC without coordinates, incremental CC | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 218 | not readable in the connector |
| iTNC 530 NC-Fehlermeldungen (2003), p. 85-86, errors 351 to 356 | iTNC530 Parameter-Alarme NC-Fehlermeldungen (2003) | 85–86 | same page |
| TNC 640 User's Manual Klartext 892903-28 (English), p. 156, CHF and figure | English edition not in the connector; German twin TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18), text only | 156 | other edition |
| 892903-28, overview p. 154 | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 154 | other edition |
| Hermle CNC-Seminar Grundkurs iTNC 530, p. 13 (drawing) | Allgemein Schulung CNC-Seminar-Grundkurs-iTNC-530 (Hermle) | 13 (the drawing is not in the text layer) | not readable in the connector |
| Hermle Grundkurs, p. 15 (program) | Allgemein Schulung CNC-Seminar-Grundkurs-iTNC-530 (Hermle) | 15 | same page |

## D256 Fanuc M29 before G84: part of CYCLE=TAP or a machine function

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Bedienung Fräsen B-64484GE-2/02, p. 109 (printed 93), three ways into rigid tapping | 30i-B Bedienung-Fraesen (B-64484EN-2) | 103 | other edition |
| B-64484GE-2/02, p. 111 (printed 95), "- M29", PS0203/PS0204 | 30i-B Bedienung-Fraesen (B-64484EN-2) | 104 | other edition |
| B-64484GE-2/02, p. 111 (printed 95), worked example `M29 S1000;` `G84 …` | 30i-B Bedienung-Fraesen (B-64484EN-2) | 105 | other edition |
| Betriebshandbuch Drehen B-64484GE-1/02, p. 113 (printed 97), three entries with G84 and G88 | 30i-B Bedienung-Drehen (B-64484EN-1) | 112 | other edition |
| B-64484GE-1/02, p. 116 (printed 100), example under G98/G99 | 30i-B Bedienung-Drehen (B-64484EN-1) | 115 | other edition |
| Doosan Puma MX/TT Programming Manual MXTTPGE68, p. 116 (printed 115), section 9.2 | Puma-MX-TT Programmierung-Drehen-Fraesen (Doosan, 2006) returns no text for these pages; the same M29 rules are in MX-Series CNC-Programmierbuch (Doosan), section 5.4 | 84 | not readable in the connector |
| Nakamura-Tome 4810004ECL-1, p. 294 to 296, chapter 17 | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 294–296 | same page |
| Nakamura-Tome Programmierhandbuch (Walter Meier, 2019), p. 23, M-code list (M29, M429) | Fanuc Programmierung Programmierhandbuch (Nakamura-Tome, DE 2019) | 23 | same page |
| the same, p. 76, rigid tapping with M29 S | Fanuc Programmierung Programmierhandbuch (Nakamura-Tome, DE 2019) | 75–76 | same page |
| Mori Seiki Programmieranleitung NT-NTX (2017), p. 246, M329 in the M-code list | NT-NTX-Serie Programmierung Programmieranleitung-NT-NTX (2017) | 245–246 | same page |
| the same, "referring to 476", M329 G84 / G88 chapter | NT-NTX-Serie Programmierung Programmieranleitung-NT-NTX (2017) | 475–476 (chapter 2-9, examples to 482) | same page |
| the same, "section titles on 21" | NT-NTX-Serie Programmierung Programmieranleitung-NT-NTX (2017) | 21 (table of contents, 2-9) | same page |
| the same, example on 291, `M329 S400;` | NT-NTX-Serie Programmierung Programmieranleitung-NT-NTX (2017) | 290–291 | same page |
| the same, example on 304, `M329 S300;` | NT-NTX-Serie Programmierung Programmieranleitung-NT-NTX (2017) | 303–304 | same page |

## D259 A pattern kept RAW under MCALL

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| SINUMERIK Zyklen Programmierhandbuch 01/2008, p. 91, hole patterns without a drilling cycle, alarm 62100 | Sinumerik-840D Programmierhandbuch-Zyklen (2008) is listed without readable text (only its index); the same text in the earlier edition Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04); alarm 62100 and its continuation also in Sinumerik-840D-sl Parameter Alarme (BS.000503) | 94; 708 | not readable in the connector |
| Zyklen 01/2008, p. 88, MCALL | as above; Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04); the MCALL rules also in Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 91; 539–540 | not readable in the connector |
| Zyklen 01/2008, p. 92-93, HOLES1 and its example (N20 … N40 MCALL CYCLE81 … N50 HOLES1 … N60 MCALL) | as above; Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04) (same example); HOLES1 (sl) in Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 95–97; 657–658 | not readable in the connector |
| Zyklen 01/2008, p. 95, HOLES2 | as above; Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04); HOLES2 (sl) in Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 99; 659 | not readable in the connector |
| Zyklen 01/2008, p. 98, CYCLE801 (seven parameters) | as above; Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04) (seven parameters); CYCLE801 (sl, fourteen parameters) in Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 102; 1114–1115 | not readable in the connector |
| NC-Programmierung 06/2019 (CNC-SW 4.92), p. 539-540 | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 539–540 | same page |
| Zyklen 01/2008, p. 46, CYCLE81 drills with the feed of the calling program | as above; Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04) ("mit dem im aufrufenden Programm programmierten Vorschub") | 51 | not readable in the connector |
| NC-Programmierung 06/2019, p. 1114-1116, CYCLE801 and CYCLE802 | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 1114–1116 | same page |

## D261 Type of a Siemens DEF, a DEF without a value, and the declaration in the Siemens output

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| NC-Programmierung 06/2019 (SW 4.92), p. 404, LUD/PUD/GUD, MD11120 | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 404 | same page |
| the same, p. 405-408, DEF syntax, "Defaultwert = 0.0" on page 406, LUD error on page 408 | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 405–408 | same page |
| the same, p. 431, implicit conversions | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 431 | same page |
| the same, p. 398-399, R parameters | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 398–399 | same page |
| the same, p. 414, initial values (FRAME, CHAR, STRING, AXIS) | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 414 | same page |
| Diagnosehandbuch Alarme 08/2018, 6FC5398-6BP40-6AA2, p. 191, alarm 12550 | Sinumerik-840D-sl Parameter Alarme (BS.000503); the 08/2018 Diagnosehandbuch is not in the connector under that title | 191 | same page |
| the same, p. 176, alarm 12200 | Sinumerik-840D-sl Parameter Alarme (BS.000503) | 176 | same page |

## D262 MOVE=MOVE on CYCLE800

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Arbeitsvorbereitung 03/2013, 18.1.38, p. 712-714, CYCLE800 table, footnote 3 | Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 712–714 | same page |
| Zyklen 01/2008, 3.16.3, p. 225-226, pre-sl _ST and _DIR | Sinumerik-840D Programmierhandbuch-Zyklen (2008) without readable text; the same pre-sl table in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04); the sl table in Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 218–219; 712–714 | not readable in the connector |
| Zyklen 01/2008, 3.16, p. 214-215, prose behind _DIR, minus and plus 180 degrees apart | as above; "Schwenken, Richtung _DIR" in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04); "Richtung (minus/plus)" in Sinumerik-840Dsl-828D Bedienhandbuch-Fraesen (2013) | 214; 485–486 | not readable in the connector |
| Bedienhandbuch Fräsen 03/2013, p. 419, tool tip tracking | Sinumerik-840Dsl-828D Bedienhandbuch-Fraesen (2013) | 419 | same page |
| Bedienhandbuch Fräsen 03/2013, p. 488, "Richtung" row | Sinumerik-840Dsl-828D Bedienhandbuch-Fraesen (2013) | 488 | same page |
| Postprozessorinformation G350 (Grob), G352-2188, p. 21, `CYCLE800(…,200000,…,1,0)` | G350 G352-2188 is listed without readable text; the same block in G550 Technische-Information Postprozessorbeschreibung G552-2183 | 20–22 | not readable in the connector |
| G352-2188, p. 25, `CYCLE800(…,100000,…,-1,0)` | as above, G550 Technische-Information Postprozessorbeschreibung G552-2183 | 23–24 | not readable in the connector |
| CYCLE800 Schwenkfunktion im JOG-Betrieb (Correa Fox) v47, p. 20 (printed 55), `220000 … ,0,,1` and `200001 … ,1,,1` | FOX-M Bedienung-CYCLE800-Schwenken-JOG v47 (Correa) | 19–20 | same page |

## D265 The tool table of D10: its form, the default kind, the warning block

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Mori Seiki PX-NT_4-K0DEEN, p. 204, G361 D omitted = rotary tool, defaults for B and T | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 204–205 | same page |
| PX-NT_4-K0DEEN, p. 145, "G361 B0 D1." | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 145 | same page |
| PX-NT_4-K0DEEN, p. 179-184, "G361 B45.0 D0." and the other D0. blocks | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 179–184 (B45.0 D0. on 182) | same page |
| PX-NT_4-K0DEEN, p. 114, "G361 B0.;" | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 114–115 | same page |
| ANSWER: "G361 B0 ;" on pages 448, 458 and 513 | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 448, 458, 513 | same page |
| ANSWER: "G361 (B90.0) D1. T5.;" on pages 210-211 | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 210–211 | same page |
| DMG MORI CTX/GMX, Technik und Programmierung SINUMERIK 840D sl V2.1, p. 99-101, TC() "Werkzeugtyp" | CTX-GMX Technik-Programmierung-Sinumerik840Dsl V2.1 (DMG MORI, 2015) is listed but returns no text | 99–101 | not readable in the connector |

## D268 Klartext form of a helix of at most one turn

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| iTNC 530 Klartext-Dialog NC-SW 340490-04 (2007), p. 265, helix only in polar coordinates | 340490-04 has no readable text; iTNC530 Programmierung-Klartext NC-SW-340490-05 | 224–225 (606420-01: 230–231) | not readable in the connector |
| 340490-04, p. 266, helix input and example `CP IPA-1800 IZ+5 DR-`, IPA range | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 225–226 (606420-01: 231–232) | not readable in the connector |
| 340490-04, p. 261, overview of the polar path functions | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 221 (606420-01: 227) | not readable in the connector |
| 340490-04, p. 254, C block inputs | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 213 | not readable in the connector |
| TNC 640 Klartextprogrammierung 892903-18, p. 169, helix | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 170 | page moved |

## D273 F on every block (heidenhain 8 rule 2)

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| iTNC 530 Klartext-Dialog NC-SW 340490-04 (2007), p. 196, "Vorschub F", Wirkungsdauer, F30000 | 340490-04 has no readable text; iTNC530 Programmierung-Klartext NC-SW-340490-05 | 160 | not readable in the connector |
| 340490-04, p. 251-252, F in CHF and RND | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 210–211 | not readable in the connector |
| 340490-04, p. 143, "Mögliche Vorschubeingaben" | iTNC530 Programmierung-Klartext NC-SW-340490-05 (feed functions table) | 102–103 | not readable in the connector |

## D275 The following block whose RPM TOOL CALL takes

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Klartext-Dialog iTNC 530 NC-SW 340490-04, p. 197, speed-only TOOL CALL with NO ENT | 340490-04 has no readable text; iTNC530 Programmierung-Klartext NC-SW-340490-05 | 161 | not readable in the connector |
| 340490-04, p. 211, TOOL CALL dialog, example `20 TOOL CALL 5.2 Z S2500 F350 …` | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 177 | not readable in the connector |
| 340490-04, p. 212, TOOL DEF preselection with tool tables | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 177 | not readable in the connector |
| 340490-04, p. 199, TOOL DEF with L and R | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 163 | not readable in the connector |
| "The TNC 640 manual repeats this form on page 126" | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 126 | same page |
| Klartextprogrammierung (TNC 640), p. 129, TOOL CALL example, TOOL DEF preselection | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 129 | same page |
| Postprozessorinformation G550 (Grob, iTNC 530/TNC 640 Klartext), p. 23, blocks 16, 44, 56 | G550 Technische-Information Postprozessorbeschreibung G552-2365 (Grob) | 22–23 | same page |
| Informationen zum Erstellen eines Postprozessors, Hersteller Heidenhain (Hermle), p. 9, sections 14 and 15 | Allgemein Technische-Information Postprozessor-Heidenhain V2 (Hermle, 2016) | 9 | same page |
| the same, "Page 5", controls iTNC 530 and TNC 640 | Allgemein Technische-Information Postprozessor-Heidenhain V2 (Hermle, 2016) | 5 | same page |

## D281 {axes} of the Fanuc [home] template and G91

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| B-64484GE/02, p. 222, G17.1 example program `N10 G91 G28 X0 Y0 Z0 … N80 …` | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 222–223 (the program stands in a figure that the text layer does not carry) | other edition |
| B-64484GE/02, p. 305, M-code example `G28G91X0Y0Z0` | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 298 | other edition |
| B-64484GE/02, p. 732, spindle-synchronisation example `N0020 G28 G91 C0` | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 709 | other edition |
| B-64484GE/02, p. 193, G28 format | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 191–192 | other edition |
| B-64484GE/02, p. 66, 67, notes 4 and 8, item 1(5) parameter 3402 bit 3 | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 62–63 | other edition |
| Nakamura-Tome 4810004ECL-1, p. 55, section 4-7-1 G28 | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 55 | same page |
| 4810004ECL-1, p. 294, sample programs `G28 U0 W0;` `G28 H0;` | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 294 | same page |

## D287 Speed of RETRACT in the runtime estimate

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Klartext-Dialog iTNC 530 NC-SW 340490-04, p. 317, M140 MB without F at rapid | 340490-04 has no readable text; iTNC530 Programmierung-Klartext NC-SW-340490-05 | 362 (606420-01: 368) | not readable in the connector |
| 340490-04, p. 318, example blocks 250 and 251, MB MAX, FN 18 ID230 | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 362 | not readable in the connector |
| Klartextprogrammierung (TNC 640), p. 238, moveBack 200903 | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 238 | same page |
| Conversational Programming User's Manual TNC 640, English 892903-28, p. 237 | English edition not in the connector; German twin TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 238 | other edition |
| Informationen zum Erstellen eines Postprozessors (Hermle), p. 9, section 17 | Allgemein Technische-Information Postprozessor-Heidenhain V2 (Hermle, 2016) | 9–10 | same page |
| the same, "page 11", M140 MB MAX recommended | Allgemein Technische-Information Postprozessor-Heidenhain V2 (Hermle, 2016) | 11 | same page |

## D288 Spindle whose rpm turns a feed per revolution into a feed per minute

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| DMG MORI GMX-Baureihe, Programmierung Arbeitsblätter (SINUMERIK 840D), p. 25, `SETMS(1)`, `G95 S1=2000` | GMX-Baureihe Programmierung-Arbeitsblaetter (DMG MORI, 2005) returns no text before page 48; the same SETMS(1) / G95 S1= pattern stands on its readable pages | 49–52 | not readable in the connector |
| the same, p. 1, 21, 22, 23, SETMS(4) / SETMS(3) with G96 or G95 on the work spindle | GMX-Baureihe Programmierung-Arbeitsblaetter (DMG MORI, 2005); same pattern readable at SETMS(3) G96 S3= and SETMS(4) G96 S4= | 48, 53 | not readable in the connector |
| SINUMERIK Grundlagen (03/2013), p. 101, G95 refers to the master spindle | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 101 | same page |
| Grundlagen (03/2013), p. 82-84, SETMS, its own block, functions of the master spindle | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 82–84 | same page |
| FANUC 0i-D Connection Manual (Function) Vol. 1 B-64303EN-1/02, p. 867, PC2SLC caution | 0i-D Connection-Manual-Function-Vol1 (B-64303EN-1-02) | 867 | same page |
| FANUC 0i-D Bedienerhandbuch Drehen/Fräsen B-64304GE/02, p. 1159, appendix K.12 | 0i-D Bedienung-Drehen-Fraesen (B-64304EN) | 1177–1178 | other edition |
| Nakamura-Tome 4810004ECL-1, p. 43, section 4-3-1 G99 | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 43 | same page |
| Programmieranleitung Nakamura Super NTJ / Fanuc 18i-TB (Walter Meier), p. 9-10, M408 and M414 | Fanuc-18i-TB Programmierung Programmieranleitung-NC-Wort-Adresse (Nakamura-Tome, 2009) | 8–10 | same page |

## D289 The X radius of CSS along a motion that changes X

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| B-64484GE/02, 9.3, p. 236-237 (printed 202-203), constant surface speed | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 234–236 | other edition |
| B-64484GE/02, 9.3 Beschränkungen, p. 239 (printed 205), CSS for G00, example N14–N16 | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 239 (G00 rule), 240–241 (example) | other edition |
| Nakamura-Tome 4810004ECL-1 (2013), 5-2, p. 61-62 | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 61–62 | same page |

## D293 Siemens call of a name outside the file

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| NC-Programmierung 06/2019 (SW 4.92), 3.2.3.1, p. 534 | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 534 | same page |
| the same, 3.4.1.4, p. 572, search path | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 572 | same page |
| the same, 3.4.1.4, p. 573, search rules | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 573 | same page |
| Diagnosehandbuch Alarme 08/2018, p. 191, alarm 12550 | Sinumerik-840D-sl Parameter Alarme (BS.000503) | 191 | same page |
| NC-Programmierung 06/2019, 3.2.3.9, p. 545-546, CALLPATH | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 545–546 | same page |

## D296 Axes of the CYCLE800 rotary-axes mode

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Arbeitsvorbereitung 03/2013, 18.1.38, p. 713-714, _MODE bits 7/6, footnote 2 | Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 713–714 | same page |
| Bedienhandbuch Fräsen 03/2013, p. 488, swivel mask rows | Sinumerik-840Dsl-828D Bedienhandbuch-Fraesen (2013) | 488 | same page |
| Zyklen 01/2008, 3.16, p. 218, "Rundachsen direkt" | Sinumerik-840D Programmierhandbuch-Zyklen (2008) without readable text; the 10.04 edition predates the direct mode; the direct mode ("Beim direkten Schwenken werden die gewünschten Positionen der Rundachsen angegeben") in Sinumerik-840Dsl-828D Bedienhandbuch-Fraesen (2013) | 484 | not readable in the connector |
| Zyklen 01/2008, 3.16, p. 219, direct mode machine-dependent, release in the commissioning menu | as above; no readable sibling found | – | not readable in the connector |
| Zyklen 01/2008, 3.16, p. 214-215, rotary axis 1 and 2 of the swivel data record | as above; rotary axis 1 or 2 as direction reference in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04) (without the worked B/C example); "Richtung (minus/plus)" in Sinumerik-840Dsl-828D Bedienhandbuch-Fraesen (2013) | 214; 485–486 | not readable in the connector |

## D298 Handedness of the holder frame under SUB_frame

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Nakamura-Tome 4810004ECL-1, ch. 21, p. 324, Z-axis mirror | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 323–324 | same page |
| 4810004ECL-1, 21-1, p. 325, G360/G361 with M700 printed twice | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 325 | same page |
| 4810004ECL-1, p. 104, M700/M701 in the M-code list | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 104 | same page |
| Mori Seiki PX-NT-4-K0DEEN (2011), M functions 1-19, p. 272-273, M396/M395, M75/M76, M584/M585 | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 272–273 | same page |
| PX-NT-4-K0DEEN, 2-25 G51.1/G50.1, p. 148 | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 148 | same page |
| DMG MORI CTX/NTX Schulung Strukturprogrammierung Teil 1 (2016), 3.6 Gegenspindel: Einstellungen, p. 56 | CTX-NTX Schulung-Strukturprogrammierung-Teil1 (DMG MORI, 2016) returns no text for this page; the same section 3.6 in CTX-NTX Trainingshandbuch-Sinumerik-Operate A-D213DE-1 (DMG MORI, 2018) | 51–56 | not readable in the connector |
| Bedienhandbuch Drehen 03/2013, p. 527, "Funktion Rückseite", Z-mirrored zero offset | Sinumerik-840Dsl-828D Bedienhandbuch-Drehen (2013) | 526–527 | same page |
| Bedienhandbuch Drehen 03/2013, p. 539, "Gegenspindel mit/ohne Spiegelung (bei G-Code)" | Sinumerik-840Dsl-828D Bedienhandbuch-Drehen (2013) | 539 | same page |
| Grundlagen 03/2013, 12.7 MIRROR/AMIRROR, p. 348-349 | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 345–350 (the G41/G42 note on 349–350) | same page |

## D300 G961 for the G96 of a VC template

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Grundlagen 03/2013, p. 93, section 6.3 | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 93 | same page |
| Grundlagen 03/2013, p. 83, "Interpretation des S-Werts" | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 83 | same page |
| Grundlagen 03/2013, p. 95, switching CSS off | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 95 | same page |
| Grundlagen 03/2013, p. 500-501, table 16-14 group 15 | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 500–501 | same page |

## D301 One Siemens D for OFFSET:LEN and OFFSET:RAD

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Grundlagen 03/2013, p. 73, section 5.6, D and D0 | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 73 | same page |
| Grundlagen 03/2013, p. 74, G41/G42/G40 and "Art der D-Programmierung" | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 74 | same page |
| Grundlagen 03/2013, p. 265, section 10.1 | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 265 | same page |
| Grundlagen 03/2013, p. 76, section 5.8 TOFFL/TOFF/TOFFR | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 76 | same page |
| Diagnosehandbuch Sinumerik 840D sl/840D/840Di/810D (DA), 08/2005, p. 196, alarm 14090 | Sinumerik840D Diagnosehandbuch (2005) is listed but returns no text for this page; alarm 14090 is not in Sinumerik-840D-sl Parameter Alarme (BS.000503) either (14088 is followed by 14091 on page 221) | – | not readable in the connector |
| the same, p. 116-117, alarm 10750 | Sinumerik840D Diagnosehandbuch (2005) returns no text for these pages; alarm 10750 in Sinumerik-840D-sl Parameter Alarme (BS.000503) | 136–137 | not readable in the connector |

## D312 Words bound to every channel that several channel programs write

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Mori Seiki PX-NT_4-K0DEEN, p. 539, Note 4 (and Note 1) | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 539–540 | same page |
| PX-NT_4-K0DEEN, p. 540, Note 5 | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 540 | same page |
| PX-NT_4-K0DEEN, p. 547-548, transfer example | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 547–548 | same page |
| Mori Seiki Programmieranleitung NT/NTX (2017 revision), p. 556-557 | NT-NTX-Serie Programmierung Programmieranleitung-NT-NTX (2017) | 556–557 | same page |
| Nakamura-Tome 4810004ECL-1, p. 302, 304, chapter 19 rules, example 2 closing line | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 302, 304 (repeated 306) | same page |
| 4810004ECL-1, p. 303-304, examples 1 and 2 | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 303–304 | same page |

## D321 WORKPLANE change without a TOOL on Heidenhain

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Klartext-Dialog iTNC 530 NC-SW 340490-04, p. 239, principal plane set by the spindle axis at TOOL CALL | 340490-04 has no readable text; iTNC530 Programmierung-Klartext NC-SW-340490-05 | 197 (606420-01: 203) | not readable in the connector |
| Klartextprogrammierung (TNC 640), p. 141 | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 141 | same page |
| Klartextprogrammierung (TNC 640), p. 128, speed-only cases and tool-change macro cases | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 128 | same page |
| 340490-04 Klartext, p. 197, speed-only TOOL CALL with NO ENT | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 161 (606420-01: 163) | not readable in the connector |
| 340490-04 Klartext, p. 211, tool axis field of the TOOL CALL dialog | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 177 | not readable in the connector |
| DIN/ISO-Programmierung iTNC 530 NC-SW 340490-04, p. 226, principal plane table G17/G18/G19 | iTNC530 Programmierung-DIN-ISO NC-SW-340490-04 | 226 | same page |
| DIN/ISO 340490-04, p. 103, "N20 T1 G17 S2000" | iTNC530 Programmierung-DIN-ISO NC-SW-340490-04 | 103 | same page |
| DIN/ISO 340490-04, "page 432", "N20 T1 G18 S2000" | iTNC530 Programmierung-DIN-ISO NC-SW-340490-04 | 432–433 (also 430–431) | same page |

## D322 Fanuc code of an external CALL: M98 or M198

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| B-64484GE/02, p. 1108–1109 (printed 1074–1075), §4.5 M198 | 30i-B Bedienung-Drehen-Fraesen (B-64484EN) | 1144–1145 | other edition |
| Nakamura-Tome 4810004ECL-1, p. 152, §8-5 memory-card call | Fanuc Programmierung Programmierhandbuch-1 (4810004ECL-1) | 152 | same page |
| Liste M- und G-Codes NTJX (Walter Meier/KOST, 2016), p. 2 | Super-NTJX Programmierung Liste-M-G-Codes (2016) | 2 (chunk 1–3) | same page |
| Programmieranleitung NC-Wort/Adresse, Super NTJX (Fanuc 18i-TB, Walter Meier 2009), p. 19–20 | Fanuc-18i-TB Programmierung Programmieranleitung-NC-Wort-Adresse (Nakamura-Tome, 2009) | 19–20 | same page |
| "WY manual p. 21", M198 bar-end query | Fanuc-31i-Model-A Programmierung Programmieranleitung-NC-Wort-Adresse (Nakamura-Tome WY series, 2008) | 21 | same page |
| Mori Seiki PX-NT_4-K0DEEN, p. 236, M-code list | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 236 | same page |
| PX-NT_4-K0DEEN, p. 283, §1-24 M198 | NT-NTX-Serie Programmierung Programmieranleitung-NTX1000SZM PX-NT 4-K0DEEN | 283 | same page |

## D326 A MESSAGE word for Siemens MSG

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Grundlagen 03/2013, p. 367, section 14.1 MSG | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 365–367 | same page |
| Grundlagen 03/2013, p. 368, examples | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 368 | same page |
| Grundlagen 03/2013, p. 440, statement list row | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 440–441 | same page |

## D327 Place and name of a Siemens REPEAT range lowered to a SUB

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Arbeitsvorbereitung 03/2013, p. 102-104, section 1.11 four forms | Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 102–104 | same page |
| Arbeitsvorbereitung 03/2013, p. 106-107, examples 4 and 5 | Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 106–107 | same page |
| Arbeitsvorbereitung 03/2013, p. 107, "Weitere Informationen" | Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 107 | same page |
| Arbeitsvorbereitung 03/2013, p. 108, "REPEAT-Anweisung sollte hinter den Verfahrsätzen stehen" | Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 108 | same page |

## D334 Sign of DPR and SDIS in the SINUMERIK drilling cycles

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Zyklen 01/2008, p. 44, CYCLE81 parameter table, SDIS and DPR "ohne Vorzeichen einzugeben" | Sinumerik-840D Programmierhandbuch-Zyklen (2008) without readable text; the same table in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04); the sl parameters in Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 51; 1082–1083 | not readable in the connector |
| Zyklen 01/2008, p. 45, example `CYCLE81(110,100,2,35)`, `(110,102,,35)`, `(110,100,2,,65)` | as above; the same example in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04) | 53 | not readable in the connector |
| Zyklen 01/2008, p. 46, RFP and RTP, error 61101 | as above; Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04); also Sinumerik-840Dsl-828D Bedienhandbuch-Fraesen (2013), and alarm 61101 in Sinumerik-840Dsl-828D Programmierhandbuch-ISO-Fraesen (2012) | 52–53; 303; 197–198 | not readable in the connector |
| Zyklen 01/2008, p. 47, SDIS direction set by the cycle, DP and DPR, "if both are given the depth is derived from DPR" | as above; Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04); SDIS direction also in Sinumerik-840Dsl-828D Bedienhandbuch-Fraesen (2013) | 52–53; 303 | not readable in the connector |

## D335 CYCLE800 angle modes against TILT

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Zyklen 01/2008, p. 217-218, swivel modes, each rotation builds on the previous, spatial angles Z then Y | Sinumerik-840D Programmierhandbuch-Zyklen (2008) without readable text; the same text in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04); also Sinumerik-840Dsl-828D Bedienhandbuch-Fraesen (2013) | 215; 484 | not readable in the connector |
| Zyklen 01/2008, p. 225-226, call, _MODE coding, example 27 = zyx, _A/_B in spatial mode | as above; the same table in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04); the sl table (_A, _B, _C as 1st, 2nd, 3rd rotation) in Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 218–219; 1112–1114 | not readable in the connector |
| INDEX / SINUMERIK 840D/840Di/810D/FM-NC Programmieranleitung Zyklen (PGZ), Ausgabe 10.04, p. 218 (printed 3-218) | Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04) | 218 | same page |
| HELLER HF-Baureihe Programmieranleitung PA.000105-DE-01 (2019), p. 128 | HF Programmierung Programmieranleitung-PA (Heller PA.000105-DE-01, 2019) | 127–129 | same page |
| SINUMERIK "Handbuch Formenbau von 3- bis 5-Achs-Simultanfräsen", p. 125, CYCLE800 mode 27 at N7 | Sinumerik-840D-sl Programmierung (BS.000508); the connector does not carry the title "Handbuch Formenbau" | 125–126 | same page |
| Bedienhandbuch Fräsen 03/2013, p. 484, swivel modes | Sinumerik-840Dsl-828D Bedienhandbuch-Fraesen (2013) | 484 | same page |
| Bedienhandbuch Fräsen 03/2013, p. 488, "Achsreihenfolge", "Drehung um X/Y/Z" | Sinumerik-840Dsl-828D Bedienhandbuch-Fraesen (2013) | 488 | same page |
| TNC 640 Klartextprogrammierung 10/2019, p. 414-415, PLANE SPATIAL orders | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 414–415 | same page |

## D336 Comparison words of FN 10 and FN 11

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Klartext-Dialog iTNC 530 NC-SW 340490-04, p. 609, section 11.6 jump table | 340490-04 has no readable text; iTNC530 Programmierung-Klartext NC-SW-340490-05 (also 606420-01, p. 301) | 292 | not readable in the connector |
| Klartext-Dialog iTNC 530 NC-SW 606420-01, p. 301 | iTNC530 Programmierung-Klartext NC-SW-606420-01 | 301 | same page |
| 340490-04, p. 662, KUGEL example with FN 11 … GT, FN 12 … LT, FN 9 … EQU | iTNC530 Programmierung-Klartext NC-SW-340490-05 (also iTNC530 Benutzerhandbuch-Klartext NC-SW-340490, pp. 580–582) | 341–343 | not readable in the connector |

## D337 Feed per minute at the start of a Klartext program

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Klartext-Dialog iTNC 530 NC-SW 340490-04, p. 302, modal M functions cancelled later or at program end | 340490-04 has no readable text; iTNC530 Programmierung-Klartext NC-SW-340490-05 | 345–346 | not readable in the connector |
| 340490-04, p. 320, M142 standard behaviour | iTNC530 Programmierung-Klartext NC-SW-340490-05 (DIN/ISO twins: iTNC530 Programmierung-DIN-ISO NC-SW-340490-04, p. 284; NC-SW-340490-05, p. 320) | 364 | not readable in the connector |
| 340490-04, p. 313, M136 default mm/min, M137 cancels | iTNC530 Programmierung-Klartext NC-SW-340490-05 (DIN/ISO twin: iTNC530 Programmierung-DIN-ISO NC-SW-340490-04, p. 277) | 358 | not readable in the connector |
| Klartextprogrammierung (TNC 640), p. 224, basic rule | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 224 | same page |
| the same, "Page 232", M136 default and M137 | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 232 | same page |

## D340 SINUMERIK counterparts of D241, D242 and D244

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Grundlagen 03/2013, p. 197, I/J/K of value 0 may be left out | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 197 (repeated 202) | same page |
| Grundlagen 03/2013, p. 494-495, G group 1 table | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 494–495 | same page |
| Grundlagen 03/2013, legend 517 | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 517 | same page |
| SINUMERIK 840D sl NC programming 10/2020 (English), p. 1290 | Sinumerik-840Dsl Programming-Manual-NC-Programming (SW4.94, 2020) | 1290 | same page |
| SINUMERIK ONE NC-Programmierung 10/2020 (SW 6.14), p. 1294 | Sinumerik-ONE Programmierhandbuch-NC-Programmierung (SW6.14, 2020) | 1294 | same page |
| Grundlagen 03/2013, p. 321 and 354, frames | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 321, 354 | same page |
| Arbeitsvorbereitung 03/2013, p. 297 (also 275), $P_ACTFRAME | Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 297, 275 | same page |

## D341 Signatures of CYCLE801 and CYCLE802

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| NC-Programmierung 06/2019 (SW 4.92), p. 1114-1116, CYCLE801 with fourteen parameters | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 1114–1116 | same page |
| the same, p. 1116-1119, CYCLE802 with thirty-three parameters, closing note | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 1116–1119 | same page |
| Zyklen 01/2008, p. 98-99, classic CYCLE801, "Ablauf" (hole order from the last position reached), example N15/N20 | Sinumerik-840D Programmierhandbuch-Zyklen (2008) without readable text; the same section 2.3.4 in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04) | 102–103 | not readable in the connector |
| Zyklen 01/2008, p. 92-94, HOLES1, "Ablauf", example with two HOLES1 calls | as above; section 2.3.2 in Sinumerik-840D Zyklen Sinumerik-840D-Zyklen (INDEX LY4401.10092 = Siemens PGZ 10.04); the sl signature in Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 95–97; 657 | not readable in the connector |

## D342 Feed type after G97, G971, G972 and G973

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Grundlagen 03/2013, p. 93, section 6.3 | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 92–93 | same page |
| Grundlagen 03/2013, p. 95-96, switching CSS off, G97 without G96, LIMS | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 95–96 | same page |
| Grundlagen 03/2013, p. 500-501, G group 15, G973 | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 501 | same page |
| SINUMERIK 840D sl Diagnosehandbuch (Alarme), 08/2018, p. 168, alarm 12060 | Sinumerik-840D-sl Parameter Alarme (BS.000503) | 168 | same page |

## D343 Modal call after a RAW line that calls a subprogram

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| NC-Programmierung 06/2019 (SW 4.92), p. 539-541, MCALL | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 539–541 | same page |
| the same, p. 527, example 1, N10910 MCALL | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 526–527 | same page |
| Arbeitsvorbereitung 03/2013, p. 181, multi-level return, MCALL in a skipped level | Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 181 | same page |
| Arbeitsvorbereitung 03/2013, p. 166, section 1.24.2.4 SAVE | Sinumerik-840Dsl-828D Programmierhandbuch-Arbeitsvorbereitung (2013) | 166 | same page |

## D344 JUMP=END inside a SINUMERIK subprogram

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| NC-Programmierung 06/2019 (SW 4.92), p. 522-523, M17, RET, SAVE restriction | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 522–523 | same page |
| Grundlagen 03/2013, p. 365, program end M2, M17, M30 | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 365 | same page |
| NC-Programmierung 06/2019, p. 524-525 (level parameter 526), parameterised RET | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 524–526 | same page |
| the same, p. 527, `RET("N10900", ,$P_STACK)` | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 527 | same page |
| Listenhandbuch Systemvariable SW 4.5 (2012), p. 51, $P_STACK | Sinumerik-840Dsl Listenhandbuch-Systemvariable SW45 (2012) | 51 | same page |
| Diagnosehandbuch (Alarme) 08/2018, p. 220-221, alarms 14080 and 14091 | Sinumerik-840D-sl Parameter Alarme (BS.000503) | 220–221 | same page |

## D345 R0, RL and RR on Klartext arc blocks

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Klartext-Dialog iTNC 530 NC-SW 340490-04, p. 216, radius compensation, effect and cancel | 340490-04 has no readable text; iTNC530 Programmierung-Klartext NC-SW-340490-05 (606420-01: 194–195) | 188–189 | not readable in the connector |
| 340490-04, p. 217, input of the compensation ("Beliebige Bahnfunktion programmieren") | iTNC530 Programmierung-Klartext NC-SW-340490-05, whose text reads "Die Radiuskorrektur geben Sie in einen L-Satz ein" (606420-01: 196) | 190 | not readable in the connector |
| 340490-04, p. 250, straight line L with "Radiuskorrektur RL/RR/R0" | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 209 | not readable in the connector |
| 340490-04, "Pages 254 to 256", C, CR and CT without a compensation entry | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 213 (C), 214–215 (CR), 216 (CT) | not readable in the connector |
| 340490-04, "The overview table on page 249" | iTNC530 Programmierung-Klartext NC-SW-340490-05 | 208 | not readable in the connector |
| Klartextprogrammierung (TNC 640), p. 134, compensation of any path function, effect in a straight-line block | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 134 | same page |
| "the English edition, page 134" (TNC 640) | English edition not in the connector; German twin TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 134 | other edition |
| Klartextprogrammierung (TNC 640), p. 155, L section | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 155 | same page |
| the same, "pages 159 to 162", C, CR and CT sections | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 159–162 | same page |
| the same, p. 260, `9 APPR CT X+2 Y+30 CCA90 R+5 RL F250` | TNC640 Programmierung-Klartext NC-SW-34059x-10 (892903-18) | 260 | same page |

## D346 SINUMERIK DIV and RET in a main program

| Record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| NC-Programmierung 06/2019 (SW 4.92), p. 448-449, DIV and MOD, examples | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 448–449 | same page |
| the same, "precedence 452" | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 452 | same page |
| the same, p. 391, synchronized actions DIV and MOD | Sinumerik-840Dsl Programmierhandbuch-NC-Programmierung (SW4.92, 2019) | 391 | same page |
| SINUMERIK High Level Language, Trainingsdokumentation 2010, p. 242 | Sinumerik High-Level-Language Trainingsdokumentation (2010) | 242 | same page |
| Diagnosehandbuch (Alarme) 08/2018, p. 221, alarm 14091 index 1 | Sinumerik-840D-sl Parameter Alarme (BS.000503) | 221 | same page |
| Grundlagen 03/2013, p. 365, M17 in a main program | Sinumerik-840Dsl-828D Programmierhandbuch-Grundlagen (2013) | 365 | same page |

## Found while re-paging

Not fixed here; listed for the maintainer.

- **D345, CT and the compensation word.** The record says the 340490-04 Klartext pages 254 to 256 give C, CR and CT with no compensation entry, and that a search of the Klartext manuals found no arc block carrying RL or RR. The quick reference for the same software level, iTNC530 Lotse-Klartext NC-SW-340490-04 (connector pages 27–28), lists "Radiuskorrektur RR/RL/R0" among the inputs of "Kreisbahn CT mit tangentialem Anschluss" (its C and CR entries list none). The full manuals the connector can read (340490-05 p. 216, TNC 640 p. 161–162) do not list it for CT, so the decision ("on the L block only") is not affected; the statement about CT holds for the full manuals only.
- **D246, page order in the 16i-TB twin.** Not a wrong fact, but the English B-63524EN-01 has the order the other way round from the record's German pages: the two-G68.1 example comes first (237–238), the 2D/3D statement with #5041 to #5048 later (241).

Passages the audit reports could not check and the connector does carry (no fact changes; they back the record):

- **D334:** "DPR ohne Vorzeichen einzugeben", the worked example `CYCLE81(110,100,2,,65)` and "Wird sowohl ein Wert für DP als auch für DPR eingegeben, so wird die Endbohrtiefe von DPR abgeleitet" are in the PGZ 10.04 edition (INDEX LY4401.10092), pages 51–53. Report 05 lists all three as not found.
- **D341:** the warning that CYCLE801 chooses the hole order from the last position reached before the call is in the PGZ 10.04 edition, pages 102–103, and the HOLES1 "Ablauf" on page 95. Report 05 leaves it unverified.
- **D259:** the 62100 behaviour of a hole pattern without a drilling cycle and the HOLES1 example with MCALL stand in the PGZ 10.04 edition, pages 94 and 97.
- **D335:** the "INDEX PGZ 10.04 p. 218" that report 05 did not check is that INDEX document, page 218, as cited.
