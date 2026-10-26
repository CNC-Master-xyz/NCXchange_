# Manual audit of the decision record

Status: 2026-10-26. Every controller fact of the record that rests on a manual has been re-checked against the cnc-manuals connector, a knowledge base of 1283 control and machine manuals that the maintainer added on 2026-10-26. The September answers were taken from a local text extraction of the same library, whose page numbers drift. Nothing in `rationale.md` or `decisions.md` has been changed by this audit. This document says what the manuals change, and the thirteen reports in `manual-audit-2026-10-26/` give every fact with its document and its connector page.

## How it was done

There were 258 entries in scope:

- the 84 settled decisions of the ninth round;
- the 149 open drafts that cite a manual, including the eight older drafts the maintainer answered on 2026-10-07;
- the 25 drafts of the tenth round (D347 to D371), which the maintainer answered on 2026-10-07 and whose controller facts carry no page numbers;
- the five drafts of the eleventh round (D372 to D376).

Thirteen agents each took a share. For every controller fact (a code, a parameter and its meaning, a cycle signature, a default, what a control does in a case), an agent searched the connector, read the page around the hit and reported one of three verdicts:

- confirmed, with the connector page;
- wrong, with what the manual says instead;
- not found, with the searches that were run.

Statements of NCX design were skipped.

## The result in brief

- **Settled decisions (ninth round, 84):** none is reversed. Eight side statements are wrong and four decisions need a follow-up in the code (section 3).
- **Answers of 2026-10-07 (33):** one rests on a wrong fact (D361). One holds for every control but Siemens (D190), and one needs a fact added (D181). Three are supported or newly settled by the manuals (D151, D164, D351), and four hold with corrections to their wording (D349, D357, D358, D368). D121 and D162 stand as given (section 2).
- **Open proposals:** in about twenty of them, a fact the proposal relies on is wrong or now settled. They should be corrected before the maintainer confirms them (section 4).
- **The tool-point drafts D372 to D376:** D373 needs three corrections and D375 one change of wording. Since this audit, the open fact of the eleventh round depends on a machine parameter and on the builder's macro (section 4).
- **Citations:** most citations of the German Fanuc 30i-B and 0i-D editions need re-paging, because the connector holds only the English editions. Eleven cited manuals are in the connector without readable text (section 6).

## 2. Answers of 2026-10-07 that the manuals change

These are the maintainer's answers. Each item below needs his decision, and D361 needs a correcting draft.

| Draft | What the answer rests on | What the manuals say | Source (connector page) |
|---|---|---|---|
| **D361** | Nakamura `M429` is rigid tapping on the lower path, so `[[cycle]] channel = 2, before = "M429 S{rpm}"` | **Wrong.** `M429` is the work-counter increment. With WTW and WTS, spindle rigid tapping is valid for the upper turret only, and no manual shows a lower-path rigid-tapping code. | Super NTJX Programming Manual-1 4810004EDG-1, p.79, p.148-149, p.310-312 |
| **D190** | Change 2: TAP and REAM feed out only to `CLEARANCE`, then rapid to `SAFE` | Holds for TAP everywhere, and for REAM on Heidenhain and Fanuc. On an 840D sl, `CYCLE85` feeds out at `FR` all the way to the retract plane. | 840D sl Bedienhandbuch Fräsen 2013 p.308, Drehen 2013 p.287 |
| **D181** | The SINUMERIK drilling facts of the sl manual | Holds, with one addition. The ones digit of `_AMODE` in `CYCLE830` is coded the other way round from `CYCLE81` to `86`: 0 is incremental and 1 is absolute. So an omitted `_AMODE` makes `_DP` incremental. `CYCLE830` also codes `VARI` as 0 to 3 of its own. | 840D sl NC-Programmierung SW4.92 p.1120-1123 |
| **D151** | Optional `ON_ROTARY`, `{rotary}` dropped from `ON`; the iTNC 530 left to check | Supported. The open part is settled: a cycle 32 carrying only `T` is accepted, and the control inserts `HSC-MODE` and `TA` as 0 when it reads the program in. Alarm 6066 forbids a rotary tolerance while the HSC filter is off. So the T-only `ON` is the safe iTNC form. On Siemens, `CYCLE832` has two parameters up to SW 7.5. With a rotary tolerance an `ORISON` may have to follow, depending on `CUST_832`. | iTNC 530 Zyklen 340490-07 p.315; iTNC 530 NC-Fehlermeldungen 2003 p.309; 840D sl Programmierung BS.000508 p.106-108 |
| **D164** | Q210 = 0, Q212 = 0, Q208 = MAX on 203; Q256 from the machine key | Holds, with two additions. The TNC 640 cycles 200 and 203 carry `Q395` (the depth reference: tool tip or the cylindrical part), another position no NCX word fills, plain value 0. The Siemens `uc.com` precedent only pre-fills an input screen; the control itself passes 0 for an omitted position. | TNC 640 Zyklen 892905-18 p.88-89, p.100; 840D sl NC-Programmierung SW4.92 p.505 |
| **D351** | `STOCK` by its bounding box; Siemens `WORKPIECE()` templates | Newly settled. The native order is `WORKPIECE("WP","RefP","ZeroOffset","Type",Par5..Par12)`. For `BOX` the parameters are the bits, Z0 (the top), Z1 (a length), ZB, X0, Y0, X1, Y1. The template computes values rather than passing two corners. The cylinder form has no X/Y centre, so `CMP013` for an off-centre cylinder is right. | 840D sl NC-Programmierung SW4.92 p.1021-1023 |
| **D349** | The Fanuc pair `G61`/`G64` of group 13; `G61.1` stays with `TOLERANCE` | The answer stands, but its labels are wrong. On the 30i-B, `G61` to `G64` are modal group 15 (group 13 is `G96`/`G97` on M), and `G61.1` does not appear in the 30i-B G-code list. `G64` is the power-on and clear state. | 30i-B Bedienung-Fräsen B-64484EN-2 p.27-28; B-64484EN p.181 |
| **D368** | No Fanuc precision level R as a tolerance carrier | The answer stands; its reasoning is wrong. Fanuc does fix the direction: level 1 is speed and level 10 is precision. Parameters 11682 and 11683 are the nano-smoothing tolerances, not the ends of that range; the level ends are 13610 to 13615. | 30i-B B-64484EN p.1677-1678; Parameterhandbuch p.655 |
| **D357, D358** | Fanuc 5114 and 5115 have no address in the program; the family defaults | Partly wrong. The 0i-F Plus programs the clearance with `,D` on `G73` and `G83`. On the TNC 640, the re-entry after a chip-break retract goes to `Q256` above the last depth. The Siemens manuals disagree on `_VRT = 0`: 1 mm in the programming manuals, no retract and a one-revolution dwell in the 2013 operating manual. | 0i-F Plus Bedienung-Fräsen p.40; TNC 640 Zyklen p.97-98; 840D sl Bedienhandbuch Fräsen 2013 p.312 |
| D121, D162 | | Stand as given; every fact confirmed. | reports 06 and 07 |

## 3. Settled decisions: what to correct

No decision of the ninth round is reversed.

### Wrong side statements

None of these touches what the code does.

- **D247:** the Nakamura rule for a group 01 code and a cycle code in one block is stated the wrong way round. The code read later wins: group 01 first means the cycle runs. Nakamura WY-250L 4810004EEE p.288. The sentence is to be corrected wherever it was copied.
- **D119:** an `EXP` overflow is `PS0111`, not `PS0119`. The same page shows `ATAN` with both arguments 0 as `PS0119`, which supports the ATAN2(0,0) error. B-64484EN p.509.
- **D117:** the Nakamura example is `G52 U2.75 W1.75`, not `W1.25`.
- **D118:** Fanuc `PRM` also has a one-argument form.
- **D265:** the claim that no `G361 B90.0;` block exists is wrong. Mori NTX1000SZM p.205-206.
- **D268:** the `IPA` range of ±5400 belongs to the 2004/2005 software. NC-SW 340490-04 and -05 take ±99 999.9999.
- **D163:** the threshold for the number of infeeds assumes `Q212 = 0`.
- **D227:** the TNC 640 `M89` list on p.66 omits `CYCL CALL POS`.

### Follow-ups in the code on main

- **D256:** the manuals confirm the three ways into rigid tapping. The rule that the line must stand immediately before the cycle block is too strict, though: the manual forbids only an `S` word or axis motion between `M29` and `G84`. Doosan programs put `M177` in between, and Mori writes `M329 S400` with a positioning move. The reader keeps such programs RAW today, which is safe but needlessly so. The rigid-tapping M code is itself parameter 5210, which supports the per-machine `before`. Sources: B-64484EN-2 p.103-105; Doosan MX-Series p.84, p.131; Mori NTX1000SZM p.281-282; Parameterhandbuch p.307.
- **D344:** `RET` may not stand in a Siemens subprogram declared with `SAVE` (NC-Programmierung SW4.92 p.523), so the compiled `RET(...,$P_STACK)` needs an ERROR or a fallback there.
- **D346:** reading `DIV` as integer division is shown only for operands that are not negative. Either keep `DIV` with operands that may be negative RAW, or record the assumption.
- **D341:** the caveat that the cycle chooses the hole order rests on a manual the connector cannot read. Treat it as unverified.

## 4. Open proposals the manuals correct

These are proposals, not answers. They are corrected before the maintainer confirms them.

### Facts wrong

| Draft | Correction | Source |
|---|---|---|
| D129 | The manuals split by machine family. On lathe-based mill-turns the bare spindle words are the work spindle's. On mill-based ones (Hermle MT, TNC 640 turning) they drive the tool spindle, and the work spindle has `M303` to `M305`. So the manuals do not point at `MAIN`, and the recommendation stands for mills. | Hermle M-Funktionen 2015 p.1-5; TNC 640 Zyklen p.415 |
| D137 | On Siemens, software limits and reference points of a facing axis are always radius values, while program words under `DIAMON` are diameter. The Fanuc counter-case is wrong: position switches 6930 and following are diameter values too. | Siemens Funktionshandbuch Grundfunktionen 2012 p.901-902; Fanuc Parameterhandbuch p.397-398 |
| D177 | "No `G82` on a lathe" holds for the standard Series 16/18 format only. The Series 15 format gives a lathe `G81` to `G89`. | B-64484EN-1 p.297-299 |
| D192 | "The path-feed reading is not what the controls use" is wrong for Siemens, where the slowest participating axis bounds the path speed. Parameter 1422 is 16i/18i only; the 30i-B uses 1430 or 1432. | Funktionshandbuch p.1510; Parameterhandbuch B-64490EN p.89 |
| D223 | The `.P-n` suffix is Fanuc's own convention with parameter 138#0 set, not a Nakamura rule, so it is not to be restricted to that builder. | B-64484EN p.1146-1147, p.1424-1425 |
| D244 | The current English 30i-B edition allows `G52` in `G68.2` mode. Keeping such blocks RAW stays safe, but the WARNING must not claim a limit of the control. | B-64484EN p.948-949 |
| D249 | Main's reading of `M98 P51002` holds for the 30i program format. In the Series 15 format the same block calls program 1002 once. | B-64484EN-1 p.245-246 |
| D278 | The axis words of the first contour block select type I or type II of `G71`/`G72`. Type II needs an option, and `PS0325` is raised if X does not move. A compiler that writes both axes in that block silently selects type II. | B-64484EN-1 p.55-57 |
| D279 | "`G74` has no `Q`" is wrong on a control with peck rigid tapping. Also, `Q` is one modal value shared by `G76`/`G87` and `G73`/`G83`. | B-64484EN-2 p.64-65, p.110 |
| D305 | Nakamura three-unit machines ignore wait codes when one unit runs alone, so the ERROR is an NCX choice, not what machines do. | Nakamura Band 1 4810004ECL-1 p.127 |
| D315 | `[0, 99]` is exactly the documented Siemens range for a multi-channel system (at most 100 marks); a single-channel system has only mark 0. | NC-Programmierung 06/2019 p.491-492 |
| D323 | A spindle reversal contains a stop (`thrdPreSwitch`, `thrdWaitingTime`), which supports main's "stop plus start". | TNC 640 Zyklen p.137 |
| D329 | Fanuc has a second waiting-code range for high-speed waits (8114, 8115) that must not overlap the first. | Parameterhandbuch p.455; B-64484EN p.1038 |
| D338 | `QL` and `QR` already exist in iTNC 530 NC-SW 340490-05. | Klartext 340490-05 p.283 |
| D134 | Not every bit of MD20112 defaults to "keep": its default is 400H. The conclusion about the tool is unchanged. | Measuring Cycles HMIsl 2009 p.452 |
| D123 | The default of SD42440 is unverified and may be the reverse of what the proposal says. The conclusion is unchanged. | Funktionshandbuch p.1704 |
| D159 | The `G4 P` unit is an increment, as proposed. In addition, `X` and `U` without a decimal point also count increments, so the compiler writes them with one. The Mori NL series accepts `P` with a decimal point. | B-64484EN p.188; Mori NL PX-NL-A0DEEN p.75 |

### Newly settled or softened

- **D125:** a Siemens frame `X` under `DIAMON` is a radius by default, switchable by MD20360 bit 3. So it is a machine setting, not a fixed rule. Funktionshandbuch p.901-903.
- **D216, D240:** Siemens machine-frame values of a facing axis are radius by default, so `$AA_IM` likely needs a factor of 2. One counter-hint: `PRESETONS` takes `DIAMON` into account. Funktionshandbuch p.901-902; Programmierung BS.000504 p.310-311.
- **D218:** settled. The `F` of a drilling cycle stays in force after `G80`. B-64484EN-1 p.93; Nakamura WY-250L p.283.
- **D126:** the Siemens quote used against the MACHINE frame cannot be checked, and the newer 840D sl manuals describe direct swivelling in machine axis positions. Soften the verdict.
- **D213:** soften to "not settled". The Siemens example jumps to a label before the `REPEAT`, not past it.

### The tool-point drafts

- **D373:**
  - The TNC 640 rule is on p.123, not p.128.
  - The iTNC 530 index facts are on p.165-166 and p.663, not p.173.
  - `H`, `H+1`, `H+2` with parameter 11419 hold under offset memory A or B only; under memory C the cutting-point command uses one `H` (11269#3) or `H` plus `D`.
  - The library does hold the iTNC 530 Technisches Handbuch (iTNC530-HSCI 60642x-04). It narrows the open fact without settling it. The change macro replaces the T strobe. Parameter MP7480.0 chooses between output on a tool-number change and output at every `TOOL CALL`. The index is passed separately in W266, and FN18 ID61 lets the macro see that the tool is already in the spindle. So whether `TOOL CALL 5.1` with tool 5 in the spindle runs the change macro depends on MP7480.0 and on the builder's macro. Sources: p.1251, p.1600, p.1609-1614, p.1736.
- **D375:**
  - The TNC 640 page says the program delta comes from the `TOOL CALL` or from the correction table and that the last programmed value acts; it does not say the table overwrites the program.
  - The Siemens case is stronger than the draft says: `TOFFL`, `TOFF` and `TOFFR` survive a change of tool or edge. Grundlagen 2013 p.78.
- **D372, D374, D376:** hold.

## 5. Facts still open after the audit

- D253 (c), the order of a shift, a rotation and a second shift on Heidenhain. Parts (a), (b) and (d) are confirmed; (c) needs a test on the machine.
- D356, what `CYCLE83` does with both `FDEP` and `FDPR` empty.
- D354, the delivered state of Fanuc parameter 1403 bit 5.
- D373 and D369, the indexed `TOOL CALL` on the iTNC 530, which now rests on MP7480.0 and the builder's macro (section 4).
- D159, whether the dwell `P` of a canned cycle follows the same unit setting as `G4 P`.
- D346, how `DIV` rounds a negative operand.

## 6. Citations

- **Fanuc:** the connector does not hold the German editions the record cites: B-64484GE/02, GE-1/02 and GE-2/02, the parameter manual B-64490GE/03, the 0i-D B-64304GE and the 16i/18i-TB B-63524GE. It holds the English editions (B-64484EN, EN-1, EN-2, B-64490EN-05, B-64304EN, B-63524EN-01). The pages drift by different amounts through the book, from about -5 near the front to +51 in the parameter section, so every such citation is re-paged individually. The reports give the English page for each.
- **Listed in the connector but without readable text:**
  - iTNC 530 Klartext NC-SW 340490-04 (2007)
  - the body of the Siemens 840D Programmierhandbuch Zyklen of 01/2008 and of 2006 (only their index pages read)
  - DMG ShopTurn 3G V1.0 and V2.1
  - DMG Strukturprogrammierung Teil 2
  - DMG CTX "Technik und Programmierung V2.1"
  - Biglia Smart-Turn T140-00379
  - Doosan "NC Programming for Turning Centers, Fanuc 30 Series"
  - Grob G350 G352-2188
  - Siemens Diagnoseanleitung 08/2005

  Citations into them move to the sibling editions the reports name.
- **Not in the connector at all:** the Burkhardt+Weber MCX 800 manual, and the English TNC 640 cycles manual 892905-28.

## 7. What follows

1. The maintainer decides on the items of section 2. D361 needs a correcting draft, since the lower-path rigid-tapping table cannot stand. D190 needs a choice: a REAM retract target per control, or the Siemens difference accepted.
2. The open proposals of section 4 are corrected in their proposed answers. They are proposals, not answers.
3. Citation pass: every entry is re-cited from the reports, with no change of substance. For settled entries the recorded ANSWER is not rewritten. The corrected citations are added to the entry only if the maintainer wants that, or are kept here.
4. The code follow-ups of section 3 (D256, D344, D346) go into one small implementation wave.

Progress, 2026-10-26:
- **Steps 1 and 2 are done.**
  - D377 corrects D361 and D378 sets out the choice on D190, both under "Twelfth round" in `rationale.md`, with rows 227 and 228 in `../implementation/03-open-questions.md`.
  - The 23 open proposals of section 4 were rewritten after their facts were checked again in the connector. Each now reads "corrected 2026-10-26 after the manual audit" and carries a "Corrected:" line. Three of them now depart from their draft's Recommendation: D223, D244 and D278.
  - D373 and D375, which are unanswered, were corrected in place.
- **One further finding:** Heidenhain's REAM retract also differs by generation. The iTNC 530 and the older TNC 620 feed out to `Q200` and then go to `Q204` at rapid. The TNC 640 and TNC 128 feed all the way to `Q204`. D378 takes this into account.
- **Steps 3 and 4 are open.**
