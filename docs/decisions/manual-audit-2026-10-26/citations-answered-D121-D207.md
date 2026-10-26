# Citations of the answered drafts D121 to D207, re-paged to the connector

Status: 2026-10-26. A concordance for the eight entries of D109 to D207 that the maintainer answered on 2026-10-07 (D121, D151, D162, D164, D181, D190, D206, D207). Their text in `../rationale.md` is left as answered; this file gives, for each citation they carry, the document and page where the cnc-manuals connector holds the same passage. Of 43 citations, 23 are on the same page, 19 resolve in another edition, 1 has moved, none is unreadable or missing; D206 cites no manual.

How to read it: the first column names the citation as the record gives it, shortened to the document, its page and the fact it supports. The second and third columns give the connector document (its file name without `.pdf`, which `read_pages` accepts) and the connector page where that fact can be read. The status says how the two relate: "same page" means the cited document is in the connector and the fact is on the cited page; "page moved" means the same document, another page; "other edition" means the cited document is not readable in the connector (the German Fanuc editions B-64484GE/02 and GE-2, and the body of the Siemens Zyklen 01/2008) and the same passage was found in the edition named, the English Fanuc twin or the 840D Zyklen LY4401.10092 and 840D sl manuals; "not readable" and "not found" would mean the passage could not be checked at all. Notes in the status cell flag where the connector's text differs from what the record says.

## D121 Precision of coordinates the VM computes

| What the record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| Fanuc 30i-B Betriebshandbuch Drehen/Fräsen B-64484GE/02, printed 30-31 (PDF 64-65), §2.3 and §2.4: IS-A to IS-E table, parameter 1013 bits 0-3, IS-C/D/E optional, maximum stroke | `Fanuc_30i-B_Bedienung-Drehen-Fraesen_B-64484EN_EN` | 60-61 | other edition |
| SINUMERIK 840D sl/828D Grundfunktionen 07/2012, p. 335-336, §6.2.4: Eingabe-, Anzeige- and Rechenfeinheit, MD10200/MD10210, rounding example 97,3786 → 97,379 | `Siemens_Sinumerik-840Dsl-828D_Funktionshandbuch-Grundfunktionen_DE_2012` | 335-336 | same page |
| Same manual, p. 333-334: standard pre-assignment 1000 increments per mm and per degree | `Siemens_Sinumerik-840Dsl-828D_Funktionshandbuch-Grundfunktionen_DE_2012` | 333-334 (also 350) | same page |
| HEIDENHAIN Funktionsübersicht TNC 320/620/640/iTNC 530, 11/2015 (785107-15), p. 47, §2.2 Technische Daten: linear axes 0,1 µm, 0,01 µm with option 23 on the TNC 640 | `Heidenhain_TNC320-620-640-iTNC530_Funktionsuebersicht_785107-15_DE_2015` | 47 | same page |
| Same manual, option 23 "Display step" listed on p. 42-43 | `Heidenhain_TNC320-620-640-iTNC530_Funktionsuebersicht_785107-15_DE_2015` | 42 | same page |

## D151 Tolerance template without a rotary tolerance

| What the record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| SINUMERIK 840D sl NC-Programmierung 06/2019 (SW 4.92), p. 1125-1126: syntax CYCLE832(S_TOL, S_TOLM, S_OTOL), S_OTOL must be programmed (= 1 on 3-axis machines), tens digit of the machining type | `Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019` | 1125-1126 | same page (the S_OTOL table text is garbled in the connector; the same sentence reads cleanly in `Siemens_Sinumerik-840D-sl_Programmierung_BS.000508-DE-000-00_DE` p. 106) |
| Same manual, p. 1127-1128: plain-text names _OFF ... _ORI_PRECISION, three- and five-axis examples, deselection CYCLE832(0,0,1) and CYCLE832() | `Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019` | 1127-1128 | same page |
| HEIDENHAIN TNC 640 Zyklenprogrammierung, NC-SW 34059x-10, 10/2019, p. 373: cycle 32 TA, 0 or NO ENT uses the manufacturer's value, example 32.2 HSC-MODE:1 TA5 | `Heidenhain_TNC640_Zyklen_892905-18_NC-SW-34059x-10_DE_2019` | 373 | same page |
| HEIDENHAIN iTNC 530 Zyklen-Programmierung, NC-SW 340490-07, 2011, p. 316: TA range 0 to 179,9999, only the example with TA5 | `Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011` | 316 | same page |

## D162 Catalog cycle names beyond the documents

| What the record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| HEIDENHAIN TNC 640 Zyklenprogrammierung, NC-SW 34059x-10, 10/2019, contents, p. 87-125, 133-141, 173-206: the cycle titles 200 to 258 | `Heidenhain_TNC640_Zyklen_892905-18_NC-SW-34059x-10_DE_2019` | contents 8-12; 87-125, 133-141, 173-206 | same page |
| Same manual, p. 119-120: cycle 241 parameters Q426 to Q430, Q379, Q202, Q212, Q205, Q211, Q435/Q401, Q208 | `Heidenhain_TNC640_Zyklen_892905-18_NC-SW-34059x-10_DE_2019` | 119-120 | same page |
| SINUMERIK 840D sl NC-Programmierung 06/2019 (SW 4.92), p. 1119-1122: CYCLE830 parameters, VARI digits | `Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019` | 1119-1122 | same page |
| SINUMERIK 840D sl/840D/840Di sl Zyklen 01/2008, p. 8 (contents ch. 3) and 111-179: LONGHOLE, SLOT1 "Nuten auf einem Kreis", SLOT2 "Kreisnut", POCKET1-4, CYCLE76, CYCLE77 | `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` | 13-14 (contents), 117 (LONGHOLE), 122-123 (SLOT1), 130-131 (SLOT2), 136-153 (POCKET1-4), 174 (CYCLE76), 179 (CYCLE77) | other edition (the 2008 body has no readable text; its index, `Siemens_Sinumerik-840D_Programmierhandbuch-Zyklen_DE_2008` p. 426, gives the names) |
| HEIDENHAIN TNC 640 Zyklenprogrammierung, NC-SW 34059x-10, p. 134-135: cycle 206 computes the pitch from S and the feed, F = S x p beside Q206 | `Heidenhain_TNC640_Zyklen_892905-18_NC-SW-34059x-10_DE_2019` | 134-135 | same page |
| SINUMERIK Zyklen 01/2008, p. 66, 69: CYCLE840 ENC 1 / 11 / 0 / 20 and the feed derived from speed and pitch | `Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019` | 1129 | other edition (also `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` p. 70-73) |

## D164 Values for Q parameters that no NCX word carries

| What the record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| HEIDENHAIN iTNC 530 Zyklenprogrammierung (NC-SW 340490-07, 2011), p. 80 (cycle 200) and 89 (cycle 203): one pass when infeed ≥ depth, Q210, Q212 | `Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011` | 80, 89-90 | same page |
| Same manual, p. 82 (cycle 201) and 90 (cycle 203): Q208 = 0, Q205, Q256 range from 0,1 | `Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011` | 82, 89-90 | same page |
| HEIDENHAIN TNC 640 Zyklenprogrammierung (NC-SW 34059x-10, 10/2019), p. 101: cycle 203 Q208 = 0 retracts at Q206, Q256 from 0,000 | `Heidenhain_TNC640_Zyklen_892905-18_NC-SW-34059x-10_DE_2019` | 100-101 | same page |
| iTNC 530 Zyklenprogrammierung (340490-07), p. 57-59, §2.2 GLOBAL DEF: PREDEF, drilling globals for cycles 200 to 209, 240, 262 to 267 | `Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011` | 57-59 | same page |
| FANUC 30i-B Bedienungshandbuch Fräsen B-64484GE-2/02, PDF p. 66 (printed 50), G73: d in parameter 5114, retract in rapid | `Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN` | 60 | other edition |
| SINUMERIK Zyklen 01/2008, p. 62: "Der jeweils nicht benötigte Parameter wird im Aufruf weggelassen bzw. erhält den Wert Null" | `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` | 65-66 (CYCLE84), 73 (CYCLE840) | other edition |
| Same manual, p. 45: empty position N60 CYCLE81 (110, 102, , 35) | `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` | 53 | other edition |
| Same manual, p. 51: _VRT = 0 means 1 mm, _DTD = 0 means DTB, _DIS1 = 0 automatic, DAM = 0 no degression | `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` | 56-58, 61 | other edition |
| SINUMERIK 840D/810D Arbeitsvorbereitung 11/2006, PDF p. 130-131 (printed 2-40 to 2-41), uc.com Vorbelegungswert | `Siemens_Sinumerik-840D-810D_Programmierhandbuch-Arbeitsvorbereitung_DE_2006` | 130-132 (the quoted definition of the Vorbelegungswert is on 132) | page moved |

## D181 SINUMERIK drilling cycle facts

| What the record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| SINUMERIK 840D sl NC-Programmierung 06/2019 (SW 4.92), p. 1082-1094: sl signatures of CYCLE81 to CYCLE86, MPIT | `Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019` | 1082-1094 | same page |
| Same manual, p. 1129: sl CYCLE840 signature, MPIT | `Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019` | 1129-1130 | same page |
| SINUMERIK Zyklen 01/2008, p. 18: an omitted supply parameter means zero | `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` | 25 | other edition (the same rule for every call: `Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019` p. 505) |
| Same manual, p. 50-55: CYCLE83 VARI 0/1, FDEP/FDPR, DAM | `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` | 56-60 | other edition |
| Same manual, p. 58-62: CYCLE84 motion sequence G331/G332 at SST/SST1, MPIT 3-48, PIT, sign, alarm 61001 | `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` | 63-66 | other edition |
| Same manual, p. 65-69: 2008 CYCLE840 signature, ENC 0/20/11/1, feed from pitch and speed, last feed active again | `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` | 70-77 | other edition |
| SINUMERIK 840D sl NC-Programmierung 06/2019, p. 1119-1122: CYCLE830 parameters, _DP by _AMODE ones digit, S_FB | `Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019` | 1119-1123 | same page |
| SINUMERIK Zyklen 01/2008, p. 46, 49, 72, 77, 80, 83, 86: motion sequences of CYCLE81, 82, 85 to 89 | `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` | 51 (81), 54-55 (82), 78-79 (85), 81-82 (86), 85 (87), 87 (88), 89 (89) | other edition |

## D190 Motions of an expanded cycle call (ExpandCycles)

| What the record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| HEIDENHAIN iTNC 530 Zyklenprogrammierung (670 388-12, NC-SW 340490-07), p. 81, 82: cycle 201 retract at F, Q208 = 0 | `Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011` | 81-82 | same page |
| Same manual, p. 111, 113: cycles 206 and 207 retract to the safety clearance, then FMAX | `Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011` | 111, 113 | same page |
| Same manual, p. 79, 83, 87: cycles 200, 202, 203 | `Heidenhain_iTNC530_Zyklen_670_388-12_NC-SW-340490-07_DE_2011` | 79, 83, 87 | same page |
| FANUC 30i-B Bedienungshandbuch Fräsen B-64484GE-2, PDF p. 62 (printed 46): Table 5.1 (a) retract column and the six operations | `Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN` | 56-57 | other edition |
| Same manual, G85 text (record p. 75/84, printed 59/68): back to point R at feed | `Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN` | 76 | other edition |
| Same manual, G83 text: rapid to d short of the previous end point, parameter 5115 | `Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN` | 68-69 | other edition |
| Same manual, parameter 5114 for G73 | `Fanuc_30i-B_Bedienung-Fraesen_B-64484EN-2_EN` | 60 | other edition |
| FANUC 30i-B Betriebshandbuch Drehen/Fräsen B-64484GE/02, PDF p. 1185 (printed 1151): tool-retract switch and the six operations | `Fanuc_30i-B_Bedienung-Drehen-Fraesen_B-64484EN_EN` | 1228-1229 | other edition |
| SINUMERIK Programmierhandbuch Zyklen 01/2008, p. 61 (CYCLE84): G332 at SST1 to reference plane + SDIS, then G0 | `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` | 65 | other edition |
| Same manual, p. 73 (CYCLE85): G1 at RFF to reference plane + SDIS, then G0 | `Index_Sinumerik-840D_Zyklen_Sinumerik-840D-Zyklen_LY4401.10092_DE_2004` | 78-79 | other edition (holds for the 840D cycles; the 840D sl CYCLE85 retracts at FR to the retract plane, `Siemens_Sinumerik-840Dsl-828D_Bedienhandbuch-Fraesen_DE_2013` p. 308, as audit part 08 reports) |

## D206 --machine: the file at that path or the machine of that name

The record cites no manual page: the proposal says the library does not answer this, and the maintainer's answer cites none.

## D207 Where ncx looks for the cycle catalog of a machine

| What the record cites | Connector document | Connector page | Status |
| --- | --- | --- | --- |
| SINUMERIK 840D sl NC-Programmierung 06/2019 (SW 4.92), p. 572-573, §3.4.1.4: fixed search path for a subprogram call, walked for each call, no recursion | `Siemens_Sinumerik-840Dsl_Programmierhandbuch-NC-Programmierung_SW492_DE_2019` | 572-573 | same page |
