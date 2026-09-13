# Excel Analysis — Sample Workbooks Structure & Rules

**Last Updated:** 2026-09-04 (revised after reading actual `samples/*.xlsx` with openpyxl)  
**Status:** Phase 0.1 — Documentation of actual Excel structure vs. PLAN.md

This document describes the exact structure, formulas, and computed fields in each sample workbook (`samples/TIDP-STL.xlsx`, `samples/MIDP.xlsx`, `samples/Tracker.xlsx`, and supporting sheets). It serves as the specification bridge between Excel and the DIP system.

**Golden Rule**: Every computed value in this project must match the cached values in these Excel files to within acceptable precision (dates < 1 second, numbers < 0.0001 relative error).

## ⚠ Verified Totals & Structure (from actual files)

| Metric | Value | Source |
|---|---|---|
| MIDP total documents | **15,885** (Tracker filters to 15,883) | `MIDP.xlsx!MIDP` row count |
| Aconex History revisions | **25,246** | `MIDP.xlsx!Aconex History` |
| Aconex Latest revisions | **5,752** | `MIDP.xlsx!Aconex Latest` |
| Baseline activities | **1,375** | `Tracker.xlsx!Baseline` |
| Distinct disciplines | **9** — Architectural, Electrical, Façade, Fire & Life Safety, Infrastructure, Interior Design, Landscape, Mechanical, Structural | Corporate Summary rows 8–16 |
| Distinct Authors | **13** — AFCO, ALUTEC, DOKA, FallProtec, JINGGONG, NAP PMO, Nesma & Partners, Nesma PMO, Provisional Sum, RAWABI, Sana Al-Jazerah, Subcontractor - Unassigned, TKE | Corporate Summary rows 18+ |
| Report Date (sample) | 2026-08-30 (Sunday) | Corporate Summary C4 |
| Start Week (sample) | 2025-11-02 23:59:59 | Corporate Summary G4 |
| End Week (sample) | 2028-10-08 23:59:59 | Corporate Summary G5 |
| Weekly boundary | Week ends **Sunday 23:59:59** | verified: 2025-11-02 → 2025-11-09 |
| PV/EV weights | 0, 0.6, 0.9, 1.0 (Pending, Sub1, Sub2, Approved) | Corporate Summary row 6 |

---

## 1. File Inventory

| File | Size | Purpose | Source |
|---|---|---|---|
| `TIDP-STL.xlsx` | 340 KB | One discipline TIDP (Structural — 1,298 rows, but many blank) | Nesma & Partners sample |
| `TIDP-STL-AFCO.xlsx` | 145 KB | Additional TIDP variant for testing | Nesma & Partners |
| `MIDP.xlsx` | 8.3 MB | Consolidated MIDP: 15,885 docs + Aconex History (25,246) + Aconex Latest (5,752) + LISTS | TIDP consolidation |
| `Baseline.xlsx` | 157 KB | Baseline schedule activities (WBS breakdown, ~1,375 activities) | P6 export |
| `PickLists.xlsx` | 50 KB | Numbering schemes (Discipline, Zone, Building, etc.) | Project codification |
| `Tracker.xlsx` | 11 MB | Master output: Tracker report (15,883 filtered) + Aconex sheets + Baseline + Corporate Summary + Baseline Summary + Control Findings + Lists | System output |

---

## 2. TIDP-STL.xlsx Structure

### Purpose
Single-discipline TIDP (Tender Information Document Package) — Structural discipline (STL). Lists planned documents for a single trade with baseline activity mapping and two submission milestones (Submittal + Approval).

### Sheets

#### 2.1 TIDP_Sheet

**Purpose**: Planned documents for Structural discipline.

**Header Block** (Rows 3–11 for TIDP; Rows 3–10 for MIDP — layouts differ slightly):

TIDP-STL.xlsx (rows 3–11):
```
CLIENT              → B3 = "Qiddiya"
PROJECT             → B4 = "Qiddiya Performing Arts Center"
ORGANISATION        → B5 = "Nesma & Partners"
DISCIPLINE          → B6 = "Structural"        (NOT "STL" — that comes from F05)
APPROVER            → B7 = "BSBG"
DATE CREATED        → B8 (often blank)
DATE LAST UPDATED   → B9 (often blank)
REVISION NUMBER     → B10 = "00"
DOCUMENT REFERENCE  → B11 = "QF01012-NES-C04518-TDP-XX..."
```

MIDP.xlsx (rows 3–10 — **no DISCIPLINE row**, APPROVER moves to row 6):
```
CLIENT              → B3
PROJECT             → B4
ORGANISATION        → B5
APPROVER            → B6
DATE CREATED        → B7 = 2025-11-18
DATE LAST UPDATED   → B8 = 2026-08-30
REVISION NUMBER     → B9
DOCUMENT REFERENCE  → B10 = "QF01012-NES-C04518-MDP-GE..."
```

**Notes on Header**:
- **Always search by label text**, never assume row numbers (TIDP has 9 header rows, MIDP has 8, offsets differ).
- The MIDP header does NOT have a DISCIPLINE row (multi-discipline). Import the discipline per-document from column V (CORPORATE DISCIPLINE).
- The discipline code (STL) and corporate name (Structural) are stored separately and inferred from the data rows.

**Data Table** (Named Range "TIDP" or found by searching for "DOCUMENT NUMBER" in column A):

Confirmed header rows in samples:
- `TIDP-STL.xlsx!TIDP_Sheet` → **row 16** (1,298 total rows, but many are empty template rows)
- `MIDP.xlsx!MIDP` → **row 15** (~15,885 populated rows)

**Always find header by searching for the text "DOCUMENT NUMBER" in column A** — never assume the row number.

**Columns** (A–AH, descriptions per PLAN.md § 5.1.1):

| Col | Excel Header | System Field | Type | Formula/Calculation | Sample Value |
|---|---|---|---|---|---|
| A | DOCUMENT NUMBER | `DocumentNumber` | Text (calculated in system, not read) | = CONCATENATE(L,"-",M,…,U) | QF01012-NES-C04518-SDW-STL-00-BLAD03-0ZZ0204 |
| B | DOCUMENT TITLE | `Title` | Text | | Structural General Arrangement |
| C | EXTRACTED FROM MODEL | `ExtractedFromModel` | Text | | Yes / No / Revit |
| D | SCOPE AREA | `ScopeArea` | Text | | Building Structure |
| E | AUTHORING SOFTWARE | `AuthoringSoftware` | Text | | Revit 2024 |
| F | EXCHANGE FORMAT | `ExchangeFormat` | Text | | .dwg,.pdf |
| G | SCALE | `Scale` | Text | | 1:100 |
| H | DELIVERY MILESTONE | `DeliveryMilestone` | Date | | 2025-12-15 |
| I | PACKAGE NAME | `PackageName` | Text | | (often blank) |
| J | ACTIVITY ID | `ActivityId` | Text | Matches Baseline.ActivityCode | QP.E.ST.GEN.GEN.1200 |
| K | CLASSIFICATION CODE | `ClassificationCode` | Text | Uniclass code | FI_60_25 |
| L | PROJECT | `Field01_Project` | Text | Read-only copy | QF01012 |
| M | ORIGINATOR | `Field02_Originator` | Text | | NES |
| N | CONTRACT | `Field03_Contract` | Text | | C04518 |
| O | DOCUMENT TYPE | `Field04_DocType` | Text | | SDW (Shop Drawing) |
| P | DISCIPLINE | `Field05_Discipline` | Text | Discipline code (STL, STR, ARC, …) | STL |
| Q | AREA/ZONE | `Field06_Zone` | Text | **Preserve leading zeros** | 00, 01, 02, … |
| R | VENUE/BUILDING | `Field07_Building` | Text | Building/facility code | Z00000, BLAD03, CENT01 |
| S | DRAWING TYPE | `Field08A_DrawingType` | Text/Number | Single digit 0–9; read as text | 2 |
| T | LEVEL | `Field08B_Level` | Text | | ZZ, B1, L2, FL01 |
| U | SEQUENCE NUMBER | `Field08C_Sequence` | Text | **Preserve leading zeros as 4 digits** | 0204 (not 204) |
| V | CORPORATE DISCIPLINE | `CorporateDiscipline` | Text | Mapped from Field05_Discipline; used in reports | Structural |
| W | 01-AUTHOR | `DataExchange[0].Author` | Text | Stage 1 (Submittal) | John Smith |
| X | 01-GEOMETRICAL | `DataExchange[0].Geometrical` | Text | Level of Detail (LOD) | LOD400 |
| Y | 01-NON GEOMETRICAL | `DataExchange[0].NonGeometrical` | Text | | Information Data |
| Z | 01-DURATION (DAYS) | `DataExchange[0].DurationDays` | Integer | Planned review time | 14 |
| AA | 01-PREDECESSOR | `DataExchange[0].Predecessor` | Text | Reference to prior activity | QP.E.ST.GEN.GEN.1100 |
| AB | 01-EXCHANGE DATE | `DataExchange[0].ExchangeDate` | Date | **Formula**: `XLOOKUP(J, Baseline.ActivityCode, Baseline.Finish)` | 2025-12-29 |
| AC–AH | 02-AUTHOR, 02-GEOMETRICAL, 02-NON GEOMETRICAL, 02-DURATION, 02-PREDECESSOR, 02-EXCHANGE DATE | `DataExchange[1].*` | (same types) | Stage 2 (Approval); same XLOOKUP formula for date | … |

**Document Number Generation Rule**:
```
DocumentNumber = $"{F01}-{F02}-{F03}-{F04}-{F05}-{F06}-{F07}-{F08A}{F08B}{F08C}"
Example:
  F01=QF01012, F02=NES, F03=C04518, F04=SDW, F05=STL, F06=00, F07=BLAD03, F08A=2, F08B=ZZ, F08C=0204
  → QF01012-NES-C04518-SDW-STL-00-BLAD03-2ZZ0204
```

**Sample Row** (from TIDP-STL.xlsx, row 17):
```
| QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0001 | Structural General Arrangement | 
| Yes | Foundation Area | Revit 2024 | .dwg,.pdf | 1:100 | 2025-12-15 | | 
| QP.E.ST.GEN.GEN.1000 | FI_60_25 | 
| QF01012 | NES | C04518 | SDW | STL | 00 | Z00000 | 0 | ZZ | 0001 | Structural |
| John Smith | LOD400 | Info Data | 14 | QP.E.ST.GEN.GEN.0900 | 2025-12-29 |
| Jane Doe | LOD300 | Info Data | 7 | QP.E.ST.GEN.GEN.1000 | 2026-01-05 |
```

#### 2.2 Baseline Sheet (in TIDP-STL.xlsx)

**Purpose**: WBS schedule from P6; used to link documents to activity milestones (Submittal/Approval dates).

**Header Row**: Row with "Activity Code" in column A. Typical: Row 2.

**Columns**:

| Col | Excel | System | Type | Notes |
|---|---|---|---|---|
| A–G | WBS Level 1–7 | `WbsLevels[0..6]` | Text | Hierarchical breakdown (e.g., "QP" → "QP.E" → "QP.E.ST") |
| H | Package | `Package` | Text | Concatenated WBS: "QP \| QP.E \| QP.E.ST \| …" (pipe-separated) |
| I | Activity Code | `ActivityCode` | Text | Unique key; e.g., "QP.E.ST.GEN.GEN.1000" |
| J | Activity | `Type` | Enum | "Submittal" or "Approval" |
| K | Original Duration | `OriginalDuration` | Integer | Days |
| L | Start | `Start` | Date | Plan start |
| M | Finish | `Finish` | Date | Plan end (used in XLOOKUP for 01-EXCHANGE DATE) |
| N | Used | (computed) | Boolean | Indicates if MIDP references this ActivityCode |

**Sample Rows** (from Baseline.xlsx or embedded in TIDP-STL.xlsx Baseline sheet):
```
QP | QP.E | QP.E.ST | QP.E.ST.GEN | QP.E.ST.GEN.GEN | QP.E.ST.GEN.GEN | QP.E.ST.GEN.GEN.1000
→ QP \| QP.E \| QP.E.ST \| QP.E.ST.GEN \| QP.E.ST.GEN.GEN

QP \| QP.E \| QP.E.ST \| QP.E.ST.GEN \| QP.E.ST.GEN.GEN | QP.E.ST.GEN.GEN.1000 | Submittal | 7 | 2025-11-15 | 2025-12-29 | Yes
QP \| QP.E \| QP.E.ST \| QP.E.ST.GEN \| QP.E.ST.GEN.GEN | QP.E.ST.GEN.GEN.1070 | Approval | 14 | 2025-12-29 | 2026-01-05 | No
```

**Important**: A Package (e.g., `QP.E.ST.GEN.GEN`) has exactly two activities: one Submittal (Activity Code ending in 00, 70, etc.) and one Approval (code = Submittal code + 70 or pattern). They are linked by Package, not by numeric ID.

#### 2.3 Picklists Sheet

**Purpose**: Enumerated values (dropdowns) — both the numbering scheme and the general
document attributes.

**Structure** (verified against `samples/PickLists.xlsx!Pick_Lists` on 2026-09-08):
group labels in **row 4**, column headers in **row 5**, data from **row 6** down.
Two kinds of list sit side by side:

* **numbering lists** — a code column plus a description column
  (`Ab. 05` / `5 - Discipline`)
* **value lists** — a single column whose header is the list's own name
  (`Authoring Software`, `Author`, …)

**⚠ Correction to the earlier reading of this sheet.** The first pass located lists by
the row-4 FIELD labels, which finds only nine of them, and cannot find Drawing Type or
Level at all: the standalone workbook labels their group once as
`FIELD 08 - SERIAL NUMBER`, not as `FIELD 08A` / `FIELD 08B`. Every list is therefore
located by its **row-5 header text** instead. That reaches all **17**:

| Cols | Header (row 5) | List | Rows | First value |
|---|---|---|---|---|
| A/B | `Ab. 01` / `1 - Project Name` | Project | 1 | `QF01012` "Qiddaya Performing Arts Center" |
| C/D | `Ab. 02` / `2 - Originator Name` | Originator | 1 | `NES` "Nesma and Partners" |
| E/F | `Ab. 03` / `3 - Contract Reference` | Contract | 1 | `C04518` "Contract Number" |
| G/H | `Ab. 04` / `4 - Document Type` | DocType | 94 | `AGD` "Agenda" |
| I/J | `Ab. 05` / `5 - Discipline` | Discipline | 98 | `ACO` "Acoustic" |
| K/L | `Ab. 06` / `6 - Area/Zone` | Zone | 29 | `00` "Overall" (leading zero kept) |
| M/N | `Ab. 07` / `7 - Venue/Building` | Building | 12 | `Z00000` "Overall" |
| O/P | `Ab. 08A` / `8A - Drawing Type` | DrawingType | 10 | `0` "General (…)" |
| Q/R | `Ab. 08B` / `8B - Level` | Level | 16 | `00` "Non-Specific ( For Blade )" |
| S | `8C - Sequence Number` | — | — | prose, not codes — **not imported** |
| T | `Authoring Software` | AuthoringSoftware | 13 | `Revit` |
| U | `File/Exchange Format` | ExchangeFormat | 26 | `.rvt` |
| V | `Scope Area` | ScopeArea | 31 | `Design Management` (cells carry trailing spaces — trim) |
| W/X | `Code` / `Status` | SuitabilityCode | 14 | `S0 (WIP)` "Initial status or WIP" |
| Y | `Scale` | Scale | 16 | `NTS` |
| Z/AA | `Classification ID` / `Classification` | Classification | 10 | `FI_60_25` "Drawing" |
| AB | `Corporate Discipline` | CorporateDiscipline | 9 | `Architectural` |
| AC | `Author` | Author | 12 | `Nesma & Partners` |

Single-column lists store the value in `Code` with an empty `Description`. `Discipline`
keeps the code → corporate-name mapping the importers use; `CorporateDiscipline` is only
the dropdown's value set. Status Mapping is **not** in this workbook — it is seeded
(22 rows) and edited from the Lists page.

Counts and first values above are asserted per list in `PicklistImporterTests`.

## 3. MIDP.xlsx Structure

### Purpose
Master MIDP: All disciplines consolidated (~10,598 documents), with Aconex History and computed columns for the Tracker workflow.

### Sheets

#### 3.1 MIDP Sheet

**Identical structure to TIDP-STL.xlsx TIDP_Sheet**, except:
- **Rows**: ~10,598 data rows (vs. ~40 in TIDP-STL).
- **Column L (PROJECT)**: Always "QF01012" (single project in this sample).
- **Column P (DISCIPLINE)**: Varies (STL, STR, ARC, ELE, MEC, FIR, INF, FAC, …).
- **Column V (CORPORATE DISCIPLINE)**: Mapped to broader categories (Structural, Electrical, etc.) — used in reports.
- **Header Row**: Row 15 (different from TIDP-STL which uses row 16).
- **Numbering**: No formulas in DocumentNumber column (column A) — values are pre-calculated or manually entered.

**First few rows** (sample):
```
Row 1–14: (likely merged cells, title, metadata)
Row 15: DOCUMENT NUMBER | DOCUMENT TITLE | … | CORPORATE DISCIPLINE | 01-AUTHOR | …
Row 16+: Data rows
```

#### 3.2 Aconex History Sheet

**Purpose**: Raw export from Aconex software (all submissions, approvals, rejections, revisions, etc.); basis for the Tracker computed columns.

**Header Row**: **Row 11** in `MIDP.xlsx!Aconex History` (starts with blank col A, then "File" in col B). Also row 11 in `Tracker.xlsx!SHD_History`. Data starts at row 12.

**Columns** (from `MIDP.xlsx!Aconex History`, starting col B — col A is blank sentinel):

| Col | Excel Header | System Field | Type | Notes |
|---|---|---|---|---|
| B | File | `FileType` | Text | pdf, dwg |
| C | File Name | `FileName` | Text | Uploaded filename (e.g., `"QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0001.pdf"`) |
| D | Document No | `AconexDocNo` | Text | **May contain extra spaces** after hyphens and trailing `-PDF`. E.g., `"QF01012- NES- C04518- SDW- STR- 00- BLAD05- 2FL0101"` |
| E | Revision | `Revision` | Text | "00", "01", "02", … (always read as text, preserve leading zero) |
| F | Title | `Title` | Text | |
| G | Status | `AconexStatus` | Text | See full list below |
| H | Review Status | `ReviewStatus` | Text/Enum | See full list below |
| I | Date Modified | `DateModified` | DateTime | **Critical**: Has sub-second precision (e.g., `2025-12-02 14:56:39.02700`). Preserve in DB as `timestamp without time zone`. |
| J | Type | `Type` | Text | "Shop Drawing", "Calculation", "Report" |
| K | Discipline | `Discipline` | Text | "Structural", "Electrical", … (full name, not code) |
| L | Area - Geographical / Zone | `Area` | Text | e.g. "00 - All Areas" |
| M | Venue - Building / Facilities | `Venue` | Text | |
| N | Floor Level | `FloorLevel` | Text | |
| O | Transmittal In | `TransmittalIn` | Text | Transmittal reference (blank or number) |

**Verified unique Status values** (12 in MIDP Aconex History):
```
A - Approved
B - Approved with Comments    ← PLURAL "Comments" (Lists sheet has typo "Comment" singular)
C - Revise and Resubmit
D - Rejected
E - Review Not Required
Issued For Approval
Issued for Action
Issued for information
No Longer In Use              ← capital "In" (not "in Use")
Open
QA Checked
Responded
```

**Verified unique Review Status values** (8):
```
A - Approved
B - Approved with Comments
C - Revise and Resubmit
D - Rejected
E - Review Not Required
Pending
QA Checked
Terminated
```

**⚠ Case/Wording Discrepancies to Handle**:
- Status data uses `"B - Approved with Comments"` (plural), but `Tracker.xlsx!Lists` maps `"B - Approved with Comment"` (singular). Import must normalize (compare case-insensitive + treat `Comment`/`Comments` as equivalent, OR add both variants to StatusMapping seed).
- `"No Longer In Use"` uses capital `I`, not `"No Longer in Use"` as sometimes written in older docs.

**Computed Columns** (Formulas in Tracker.xlsx; **must calculate in system**):

| Header (in Tracker) | Formula | System Calculation |
|---|---|---|
| Document No Final | `SUBSTITUTE(SUBSTITUTE(C, " ", ""), "-PDF", "")` (case-insensitive) | `NormalizeDocNo(AconexDocNo)`: Remove all spaces, remove trailing `-PDF` (case-insensitive) |
| Document Length | `LEN(Document No Final)` | Used to filter `XXX` (length 3) |
| Terminated | `COUNTIFS(DocNoFinal=this, Revision=this, DateModified≥this, ReviewStatus="Terminated") + COUNTIFS(..., Status="Closed") + COUNTIFS(..., "No Longer in Use") > 0` | For this row: is there another row with same DocNoFinal, same Revision, and later DateModified with status Terminated/Closed/No Longer in Use? |
| Latest | `MAXIFS(DateModified, DocNoFinal=this) = this.DateModified` | Is this the row with max DateModified for this DocNoFinal? (if tie, take first) |
| In MIDP | `IF(Terminated, TRUE, COUNTIF(MIDP.DocumentNumber, DocNoFinal)≥1)` | Terminated? Or DocumentNumber found in MIDP sheet? |

**Example Rows** (Aconex History):

Row 100 (hypothetical):
```
pdf | Drawing.pdf | QF01012- NES- C04518- SDW- STR- 00- BLAD05- 2FL0101-PDF | 00 | Structural GA | 
A - Approved | | 2025-12-15 14:23:47.123456 | Shop Drawing | Structural | Qiddiya | BLAD05 | L2 | TR001
```

After normalization: `DocNoFinal = "QF01012-NES-C04518-SDW-STR-00-BLAD05-2FL0101"` (length 44)

Row 105 (same document, later revision):
```
pdf | Drawing_rev1.pdf | QF01012- NES- C04518- SDW- STR- 00- BLAD05- 2FL0101-PDF | 01 | Structural GA | 
Issued For Approval | | 2025-12-22 09:15:30.654321 | Shop Drawing | Structural | Qiddiya | BLAD05 | L2 | TR002
```

DocNoFinal same, Revision "01", DateModified later.

#### 3.3 Aconex Latest Sheet (in MIDP or Tracker)

**Purpose**: Filtered view of Aconex History showing only Latest + terminated revisions (for cross-reference).

**Identical structure to Aconex History**, but filtered by `Latest = TRUE OR Terminated = TRUE`.

#### 3.4 LISTS Sheet (in MIDP)

**Purpose**: **Legacy** DC workflow status mapping. `Tracker.xlsx!Lists` is authoritative for new data.

**Columns**: `DC Status | Status` and `Type`

Actual entries (10 rows):

| DC Status | Unified Status |
|---|---|
| Submitted to Site | Under Review |
| Site Rejected | Rejected |
| Submitted to Client | Under Review |
| For Information | Approved |
| A - Approved | Approved |
| B - Approved with Comment | Approved |
| C - Revise and Resubmit | Rejected |
| D - Rejected | Rejected |
| E - Review Not Required | Approved |
| QA Rejected | Rejected |

Type column: `Shop Drawing`, `Calculation`, `Report`

**⚠ Note the singular "Comment" in this legacy sheet vs. plural "Comments" in modern Aconex data.**

---

## 4. Tracker.xlsx Structure

### Purpose
Master output file containing MIDP + Aconex + engine-computed columns + summary reports.

### Sheets

#### 4.1 Tracker Sheet

**Purpose**: A **report view** (not the raw MIDP columns). One row per MIDP document with a de-normalized subset of MIDP fields + computed columns + "Selected Revision" columns.

**Header Row**: **Row 11** (data starts row 12). ~15,883 rows.

**Full column layout** (from `Tracker.xlsx!Tracker`, cols B–AE, col A blank sentinel):

| Col | Header | System Origin | Notes |
|---|---|---|---|
| B | Type | `Document.F04DocType` | "SDW", "CAL", "REP"… |
| C | Discipline | `Document.CorporateDiscipline` | "Structural", "Electrical"… |
| D | Document No | `Document.DocumentNumber` | Generated `F01-F02-…-F08A F08B F08C` |
| E | Title | `Document.Title` | |
| F | Delivery Milestone | `Document.DeliveryMilestone` | |
| G | Activity ID | `Document.ActivityId` | Baseline key |
| H | Package Name | `Document.PackageName` | Often blank |
| I | Building | `Document.F07Building` | |
| J | Level | `Document.F08BLevel` | |
| K | Trade | `Document.F05Discipline` | Short discipline code (STL, STR, ARC…) |
| L | Author | `Document.Exchanges[0].Author` | 01-AUTHOR |
| M | *(spacer, blank)* | — | |
| N | # of Submissions | `Snapshot.SubmissionsCount` | Latest revision + 1 |
| O | Revision | `Snapshot.Revision` | Latest revision string ("01") |
| P | Aconex Status | `Snapshot.AconexStatus` | Latest (or "Terminated" override) |
| Q | Status | `Snapshot.Status` (Unified) | Approved/Rejected/UnderReview/Withdrawn |
| R | Submission Date | `Snapshot.SubmissionDate` | First DateModified for this revision |
| S | Date Modified | `Snapshot.DateModified` | Latest DateModified |
| T | Transmittal | `Snapshot.Transmittal` | From TransmittalIn |
| U | *(spacer, blank)* | — | |
| V | Sel Rev | *(user-picked revision)* | "Selected Revision" column set — chosen revision, not latest |
| W | Sel Status | | |
| X | Sel Sub Date | | |
| Y | Sel Date Modified | | |
| Z | Sel Transmittal | | |
| AA | *(spacer, blank)* | — | |
| AB | Planned Start | `Snapshot.PlannedStart` | |
| AC | Planned Finish | `Snapshot.PlannedFinish` | |
| AD | Actual Start | `Snapshot.ActualStart` | min DateModified across all revisions |
| AE | Actual Finish | `Snapshot.ActualFinish` | If Status = Approved, latest DateModified; else null |

**Computed Column Formulas** (recreated as engine rules — see PLAN.md § 5.4.1):

| Column | Formula (Excel logic) | System Calculation |
|---|---|---|
| DateModified (S) | `MAXIFS(Aconex.DateModified, AconexDocNo_normalized=this.DocumentNumber)` | max over Aconex rows; null if none |
| Revision (O) | `Aconex[LatestRow].Revision` | Latest revision from Aconex |
| AconexStatus (P) | `IF(latest.IsTerminated, "Terminated", latest.AconexStatus)` | Override if Terminated |
| Status (Q) | `VLOOKUP(AconexStatus, Lists, UnifiedStatus)` | Map via StatusMapping |
| # Submissions (N) | `IF(ISNUMBER(VALUE(Revision)), VALUE(Revision)+1, NULL)` | Parse "00" → 1, "01" → 2 |
| Submission Date (R) | `MINIFS(Aconex.DateModified, AconexDocNo=this, Aconex.Revision=latest.Revision)` | First submission of latest revision |
| Transmittal (T) | `latest.TransmittalIn` (blank if 0) | |
| Actual Start (AD) | `MINIFS(Aconex.DateModified, AconexDocNo=this)` | Earliest across all revisions |
| Actual Finish (AE) | `IF(Status="Approved", DateModified, NULL)` | Only when approved |
| Planned Start (AB) | Baseline mode: `XLOOKUP(ActivityId, Baseline.ActivityCode WHERE Type=Submittal, Baseline.Finish)`; WorkingPlan: `DeliveryMilestone` | |
| Planned Finish (AC) | Baseline mode: `XLOOKUP(Package matching, Baseline.ActivityCode WHERE Type=Approval, Baseline.Finish)`; WorkingPlan: `PlannedStart + WorkingPlanApprovalDays (28)` | |

**Sample Row 12 (actual data)**:
```
B: Type = "SDW"
C: Discipline = "Structural"
D: Document No = "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004"
E: Title = "BLADE 4 - Welding Details"
F: Delivery Milestone = 2025-12-19
G: Activity ID = "QP.E.ST.GEN.GEN.1000"
I: Building = "Z00000"
J: Level = "ZZ"
K: Trade = "STL"
L: Author = "JINGGONG"
N: # of Submissions = 2
O: Revision = "01"
P: Aconex Status = "B - Approved with Comments"    ← plural
Q: Status = "Approved"
R: Submission Date = 2026-04-18 10:20:36.551
S: Date Modified = 2026-04-26 13:04:49.041
T: Transmittal = "BSBG-TRANSMIT-000156"
V: Sel Rev = "00"      W: Sel Status = "B - Approved with Comments"
X: Sel Sub Date = 2026-03-31 11:35:50.696   Y: Sel Date Modified = 2026-04-09 11:53:00.743
Z: Sel Transmittal = "BSBG-WTRAN-003904"
AB: Planned Start = 2025-12-14
AC: Planned Finish = 2025-12-28
AD: Actual Start = 2026-03-31 11:35:50.696
AE: Actual Finish = 2026-04-26 13:04:49.041
```

#### 4.2 SHD_History & SHD_Latest Sheets

Same as Aconex History / Aconex Latest in MIDP.xlsx (copy or link).

#### 4.3 Baseline Sheet

Same as Baseline in TIDP-STL.xlsx (copy or link).

#### 4.4 Corporate Summary Sheet

**Purpose**: Wide dashboard sheet with 5 panels side-by-side. **Groups by Discipline AND by Author** (both in the same sheet, stacked).

**Header panels** (row 1–2 titles, row 7 column headers):

| Panel | Columns | Header (row 7) fields |
|---|---|---|
| Timeline (weekly) | B–G | Week, From, To, Weekly Planned, Weekly Submitted, Weekly Approved |
| Progress | I–M | Disciplines, Total Drawings, Planned, Submitted, Approved |
| Quality | O–U | Disciplines, Approved, Rejected, Under Review, Withdrawn, Total Revisions, Quality |
| Planned Value | W–AB | Disciplines, Pending, Sub 1, Sub 2, Approved, Planned |
| Earned Value | AD–AI | Disciplines, Pending, Sub 1, Sub 2, Approved, Completed |

**Header values** (rows 4–6):
- C4: Report Date = `2026-08-30`
- C5: Current Week = `2026-08-30 23:59:59`
- G4: Start Week = `2025-11-02 23:59:59`
- G5: End Week = `2028-10-08 23:59:59`
- Totals row 5 (J/K/L/M): Total=15883, Planned=4493, Submitted=5531, Approved=3834
- Weight row 6 (cols X/Y/Z/AA for Planned Value; AE/AF/AG/AH for Earned Value): `0, 0.6, 0.9, 1.0`

**Weekly rows 8–end** (weeks 1 through ~150):
- Week 1: 2025-10-26 → 2025-11-02, Planned=11, Submitted=0, Approved=0
- Week 2: 2025-11-02 → 2025-11-09, Planned=0, Submitted=0, Approved=0
- Week 5: 2025-11-23 → 2025-11-30, Planned=12, Submitted=65, Approved=0 ✓
- Week 7: 2025-12-07 → 2025-12-14, Planned=307, Submitted=112, Approved=4

**Discipline rows 8–16 (I–AI)** — verified cached values:

| Discipline | Total | Planned | Submitted | Approved | Rejected | UnderReview | Withdrawn | TotRevs | Quality | Pending | Sub1 | Sub2 | PVApp | Planned% | EVPend | EVSub1 | EVSub2 | EVApp | Completed% |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Architectural | 1955 | 133 | 370 | 222 | 87 | 61 | 0 | 633 | 0.3881 | 1822 | 10 | 0 | 123 | 0.0660 | 1585 | 91 | 57 | 222 | 0.1677 |
| Electrical | 2998 | 252 | 858 | 624 | 188 | 46 | 0 | 1054 | **0.6190** | 2746 | 6 | 0 | 246 | 0.0833 | 2140 | 193 | 41 | 624 | 0.2591 |
| Façade | 1009 | 300 | 98 | 63 | 35 | 0 | 0 | 181 | 0.3481 | 709 | 62 | 0 | 238 | 0.2727 | 911 | 27 | 8 | 63 | 0.0856 |
| Fire & Life Safety | 751 | 55 | 264 | 105 | 134 | 25 | 0 | 348 | 0.3251 | 696 | 4 | 0 | 51 | 0.0711 | 487 | 90 | 69 | 105 | 0.2944 |
| Infrastructure | 346 | 48 | 80 | 62 | 15 | 3 | 0 | 144 | 0.4397 | 298 | 0 | 0 | 48 | 0.1387 | 266 | 15 | 3 | 62 | 0.2130 |
| Interior Design | 1152 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | (null) | 1152 | 0 | 0 | 0 | 0 | 1152 | 0 | 0 | 0 | 0 |
| Landscape | 882 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | (null) | 882 | 0 | 0 | 0 | 0 | 882 | 0 | 0 | 0 | 0 |
| Mechanical | 1513 | 293 | 527 | 130 | 298 | 99 | 0 | 685 | 0.2218 | 1220 | 30 | 0 | 263 | 0.1857 | 986 | 316 | 81 | 130 | 0.2594 |
| Structural | 5277 | 3412 | 3334 | 2628 | 299 | 407 | 0 | 5041 | 0.5671 | 1865 | 27 | 0 | 3385 | **0.6445** | 1943 | 432 | 274 | 2628 | **0.5939** |

**Author rows 18+** — same layout, one row per Author (13 authors):
```
AFCO, ALUTEC, DOKA, FallProtec, JINGGONG, NAP PMO, Nesma & Partners,
Nesma PMO, Provisional Sum, RAWABI, Sana Al-Jazerah,
Subcontractor - Unassigned, TKE
```

Sample: AFCO row: 40 / 32 / 24 / 24 (Total/Planned/Submitted/Approved)

**Formulas** (recreated as engine rules):

1. **Week End**: Sunday 23:59:59. Formula: `= date + (7 - WEEKDAY(date, 2)) days at 23:59:59`
   - Example: 2025-11-04 (Tue) → 2025-11-09 (Sun) 23:59:59
   - **Note**: Even a Sunday input goes to the next Sunday when `WEEKDAY(sun,2)=7`, giving `+0` days — so *Sunday stays on itself* at 23:59:59.

2. **Weekly Counts** (for each week W with (From,To] boundaries):
   - `WeeklyPlanned  = Count(PlannedStart  in (From,To])`
   - `WeeklySubmitted = Count(ActualStart  in (From,To])`
   - `WeeklyApproved  = Count(ActualFinish in (From,To])`

3. **Progress (per group — Discipline or Author, at Current Week C)**:
   - Total = Count(all docs)
   - Planned = Count(PlannedStart ≤ C)
   - Submitted = Count(ActualStart ≤ C)
   - Approved = Count(ActualFinish ≤ C)

4. **Quality (per group)**:
   - Approved, Rejected, UnderReview, Withdrawn — count by Unified Status (Withdrawn = 0 in all rows here)
   - TotalRevisions = Σ(SubmissionsCount) − Withdrawn
   - Quality = Approved / (TotalRevisions − UnderReview) ; null if denominator is 0

5. **Planned Value (per group, weights 0/0.6/0.9/1.0)**:
   - Pending = Count(PlannedStart > C or null)
   - Approved (PV Approved) = Count(PlannedFinish ≤ C)
   - Sub2 = 0 (currently unused — reserved for later PV rules; verified in sample all Sub2=0)
   - Sub1 = Count(PlannedStart ≤ C) − Approved − Sub2
   - Planned% = (Sub1·0.6 + Sub2·0.9 + Approved·1.0) / Total

6. **Earned Value (per group, same weights)**:
   - Pending = Count(Status ≠ Approved AND SubmissionsCount is null)
   - Sub1 = Count(Status ≠ Approved AND SubmissionsCount = 1)
   - Sub2 = Count(Status ≠ Approved AND SubmissionsCount ≥ 2)
   - Approved = Count(Status = Approved)
   - Completed% = (Sub1·0.6 + Sub2·0.9 + Approved·1.0) / Total

**Grouping**:
- `CorporateDiscipline` (column V in MIDP) drives the discipline breakdown.
- Author = column W (01-AUTHOR) drives the author breakdown. Only the first exchange's author is used (not the second stage).

#### 4.5 Baseline Summary Sheet

**Purpose**: Baseline package progress (Submittal, Approval, status breakdown).

**Structure**:
- **Discipline Summary**: For each discipline, Total packages, Submitted, Approved, Revise & Resubmit, Rejected, UnderReview
- **Package Detail**: For each Submittal activity (Package), Total docs, Submitted, Approved, …, PackageStatus (Unused, Pending, Partial, Submitted)

**Example Row** (package QP.C.PS.GEN.GEN):
```
Package: QP.C.PS.GEN.GEN (Submittal)
Total docs in this package: 45
Submitted: 45
Approved: 0
Revise & Resubmit: 0
Rejected: 0
UnderReview: 45
PackageStatus: Submitted  (all submitted, none approved yet)
```

#### 4.6 Control Findings Sheet

**Purpose**: Four reports highlighting anomalies.

1. **Aconex vs MIDP — Delivered but Unplanned**:
   ```
   Rows from Aconex Latest where:
   - DocNoFinal ≠ "XXX" (length ≠ 3)
   - Not in MIDP (InMidp = FALSE)
   - IsLatest = TRUE
   
   Columns: Document No, Revision, Title, Status (Unified), DateModified
   ```

2. **Unplanned in MIDP**:
   ```
   Rows from Tracker where:
   - PlannedStart IS NULL (no Baseline activity mapped)
   
   Columns: Type, Discipline, Document No, Title, PlannedStart, Author (from 01-AUTHOR)
   ```

3. **Unused Baseline Packages**:
   ```
   Submittal activities from Baseline where no docs reference ActivityCode
   
   Columns: Package, ActivityCode, Activity, OriginalDuration, Finish, Document Count
   ```

4. **Duplicate Document Numbers**:
   ```
   Rows from MIDP grouped by DocumentNumber with Count > 1
   
   Columns: Document No, Count, Disciplines, Titles
   ```

#### 4.7 Lists Sheet

**Purpose**: Master status mapping (definitive, not MIDP LISTS sheet).

**Columns**: Aconex Status | Unified Status

---

## 5. Baseline.xlsx & PickLists.xlsx

### 5.1 Baseline.xlsx

**Identical to Baseline sheet within TIDP/MIDP/Tracker** (standalone file for convenience).

### 5.2 PickLists.xlsx

**Identical to Picklists sheet within TIDP** (standalone file for reference).

---

## 6. Discrepancies Between PLAN.md and Actual Excel

**As of 2026-09-04** — after verifying by reading the actual xlsx files with openpyxl:

### ✓ Confirmed against real data
- Document numbering formula: F01-F02-F03-F04-F05-F06-F07-F08A F08B F08C (no spaces in production) — e.g. `QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004`
- DateModified sub-second precision preserved (e.g. `2025-12-02 14:56:39.02700`)
- Baseline linking: ActivityCode + Package with Submittal/Approval pair
- Discipline code vs. corporate name: Separate fields; F05="STL" → CorporateDiscipline="Structural"
- Exchange stages: 01 (Submittal), 02 (Approval) — two blocks W–AB and AC–AH
- Leading zeros: Zone (00), Sequence (0004) preserved in samples
- Weekly boundary: Week ends Sunday 23:59:59 — verified 2025-11-02→2025-11-09 (Week 2)
- Report Date 2026-08-30: Electrical Quality=0.6190, Structural Planned%=0.6445, Structural Completed%=0.5939 ✓ match test targets
- PV/EV weights = 0, 0.6, 0.9, 1.0 ✓

### ❌ PLAN.md corrections needed (fixed here; PLAN.md must be updated)

| # | PLAN.md says | Reality | Fix |
|---|---|---|---|
| 1 | MIDP ≈ 10,598 docs (§ 5.1.2) | **15,885 rows in MIDP.xlsx!MIDP; Tracker filters to 15,883** | Change all references; test target = 15,883 |
| 2 | MIDP header row = 15 | ✓ row 15 (accurate) | (no change) |
| 3 | Aconex header "row with Document No in col C" | **Row 11**, col D (in MIDP.xlsx); col E (in Tracker) — col A/B are spacer/File | Reword to "row 11; search by 'Document No' anywhere in row" |
| 4 | Status "B - Approved with Comments" (plural in PLAN table) | Aconex data uses **plural** ("Comments"), but `Tracker.xlsx!Lists` maps **singular** ("Comment") | Seed StatusMapping with BOTH variants; comparator = case-insensitive + trim + normalize whitespace |
| 5 | Status "No Longer in Use" (lowercase i) | Data uses **"No Longer In Use"** (capital I) | Update seed value |
| 6 | Status list has 13 entries | Actual: **12 in Status, 8 in Review Status**. Missing from PLAN.md seed: `Issued for Action`, `Open`, `Responded` | Add mappings (see § 7 below) |
| 7 | Tracker sheet = "same as MIDP + computed" | Tracker is a **narrower report view** with just 22 meaningful columns (Type, Disc, DocNo, Title, Milestone, ActivityID, Package, Building, Level, Trade, Author + computed) — NOT full MIDP W–AH exchange data | Restate: Tracker sheet is a report view; MIDP row is preserved via `Document` entity |
| 8 | Corporate Summary = discipline-only | Corporate Summary **also groups by Author** (13 authors listed in rows 18+, same schema as disciplines) | Add Author rollup to CorporateSummaryEngine |
| 9 | Corporate Summary schema (in § 5.4.2) | 5 panels: Timeline / Progress / Quality / Planned Value / Earned Value — with a `Sub 2` slot always = 0 in current data | Restate the wide-table layout as-is |
| 10 | PLAN.md test target: "Electrical 2998/858/624/46 Quality ≈ 0.619" | ✓ Verified. Also add: PV Planned% = 0.0833, EV Completed% = 0.2591 | Extend test target set |
| 11 | PLAN.md § 5.1.4 says Baseline header at row containing "Activity Code" | ✓ verified — row 2 in TIDP-STL, row 2 in Baseline.xlsx, **row 5 in Tracker.xlsx** | Emphasize: always search by "Activity Code" |
| 12 | PLAN.md § 5.1.5 says Tracker has SHD_History / SHD_Latest / Lists / Corporate Summary / Baseline Summary / Control Findings | ✓ all present | (no change) |
| 13 | Missing from PLAN: "Selected Revision" columns are in Tracker (V–Z) | These are stored per-doc as user picks and rendered on the document detail page | Add `SelectedRevisionId` field to Document (or query param on tracker page) |
| 14 | Missing from PLAN: MIDP LISTS sheet has legacy statuses ("Submitted to Site", "Submitted to Client", "For Information", "Site Rejected") | These come from an older DC workflow; **Tracker.xlsx!Lists is authoritative** — but seeder should also insert legacy mappings for backfill imports | Add legacy statuses to StatusMapping seed as deprecated |
| 15 | MIDP header block layout matches TIDP | Actually **MIDP omits DISCIPLINE row and APPROVER shifts to row 6** | Read header block by label, not by row |

### ✚ Found while building the engines (Phases 5.1–5.2, 2026-09-05)

| # | Finding | Evidence | Decision |
|---|---|---|---|
| 16 | The document-number normaliser rejected any number whose last segment is not 7 characters, returning `XXX`. **332 documents in `Tracker.xlsx!Tracker` have a 6- or 8-character last segment** (histogram: 6→240, 7→15,551, 8→92) and **174 of them carry real Aconex history** — e.g. `QF01012-NES-C04518-CAL-CIV-00-Z00000-000004`. Those documents would have shown blank Revision/Status/dates in every report. | Last-segment histogram over the Tracker sheet; the workbook computes full values for the affected rows | `AconexHistoryImporter.NormalizeDocNo` now checks the shape only (8 non-empty dash-separated segments). Foreign-contract numbers (`QF01012-BSB-C02310-…`) are structurally valid and normalise to themselves; they never match a MIDP document, which is exactly what the `InMidp` flag already records. PLAN.md § 5.1.3's "BSB report → XXX" is therefore a *filtering* statement, not a normalisation rule. |
| 17 | **Terminated revisions are excluded from every per-document aggregation.** `SHD_History!Document No Final` is `XXX` for 600 of 600 terminated rows, and every Tracker formula (`MAXIFS`/`MINIFS`/`XLOOKUP`) matches on that column. Including them moves Submission Date and Actual Start onto withdrawn submissions. | `Tracker!R12` matches `SHD_History[Document No_Revision]`, which is built from `Document No Final`; cross-tab of `Terminated` × `Document No Final` = 600/600 | `TrackerEngine` drops `IsTerminated` rows from the matching set. PLAN.md § 5.4.1's `AconexStatus = latestRow.IsTerminated ? "Terminated"` branch is kept — it is what `Tracker!P12` does — but is unreachable while the data holds this invariant. |
| 18 | `SHD_History!Document No Final` is a **static column with no formula**, and it is not always derivable from the row. Rows 34/35 and 36 of `Tracker.xlsx` hold byte-identical `Document No` values (verified char-by-char), yet 34/35 read `XXX` and 36 reads the real number. | `HasFormula = false` on column Q; identical character codes for D34/D35/D36 | Treated as stale data in the sample workbook, not a rule. The Phase 5.1 sample test therefore feeds the engine the workbook's own `Document No Final`/`Terminated` columns — same inputs, same outputs — while the importer's normalisation is covered separately by `NormalizeDocNoTests`. |

| 19 | A document whose Exchange 01 author is blank belongs to **no author row**. The sheet's author panel counts per author name, so 10 such documents fall out of the author breakdown while still counting in their discipline and in the totals row. | The 13 author rows do not sum to 15,883; adding an "(Unassigned)" row was the only difference in an otherwise exact reproduction of the sheet | `CorporateSummaryEngine` groups authors only over documents that have one. The discipline breakdown and the totals row are unaffected. |

| 20 | **The Baseline Summary's `C-Revise and Resubmit` and `D-Rejected` columns read 0 in every row — the sheet's own formula is broken.** `COUNTIFS(MIDP[Aconex Status], H$4)` compares the status against the header text, which drops the spaces around the dash (`"C-Revise and Resubmit"`) while the data holds `"C - Revise and Resubmit"`, so the criteria never match. Structural shows 0 here while the Corporate Summary reports 299 rejected. | `Baseline Summary!H8` formula vs the Aconex status values; every H/I cell in the sheet is 0 | Implemented as PLAN.md § 5.4.3 intends — the real status counts — rather than reproducing the defect, confirmed with the product owner on 2026-09-05. Every other column is asserted against the sheet exactly; these two are asserted against the status counts and cross-checked against the Corporate Summary's Rejected total. |
| 21 | The Baseline sheet holds 683 `Submittal` rows, 680 `Approval` rows and **12 rows with a blank Activity type**. Only the two named types are activities. | Activity-column histogram over `Tracker.xlsx!Baseline`; the Baseline Summary lists exactly the 683 Submittal codes | `BaselineImporter` already skips untyped rows; the test workbook reader was defaulting them to Submittal and now skips them too. |
| 22 | **The MIDP sheet and the Tracker sheet are different populations.** `Tracker.xlsx!Tracker` holds 15,883 rows and is what the Corporate Summary's totals are computed from; importing `MIDP.xlsx!MIDP` through `MidpImporter` yields **15,724** rows in the effective document set (rows without a CORPORATE DISCIPLINE are skipped, and intra-file duplicates collapse). The gap is the same whether the workbook lands in the Live layer or the Draft one. | Counted during the v3 run (`docs/refactor-log.md`, R2 deviation 10) | `CorporateSummarySampleTests` keeps asserting the sheet's own numbers by feeding the engine the Tracker sheet directly. `DraftLayerReportsTests` instead proves the layer is invisible to the reports: the same workbooks imported into a Draft-target folder and a Live-target folder produce byte-identical Corporate Summary, Baseline Summary and Control Findings payloads. |

### ✚ Found while removing the layers (Stage 1, 2026-09-11)

| # | Finding | Evidence | Decision |
|---|---|---|---|
| 23 | **The sequence field is not four digits for every document type.** The sample MIDP uses three digits for `ANL`, `CAL` and `REP` and four for `BIM`, `MOD`, `SDW`, `TDP` and `VMU`; padding everything to four rewrote the three-digit types into numbers that exist nowhere. | Modal serial length per type over `MIDP.xlsx!MIDP` column A, asserted in `MidpNumberingSampleTests.Widths_ByType_MatchTheSheetsOwnNumbers` | Width is data: `DocumentTypeSerials`, seeded with those eight and editable from /lists. Unlisted types default to three (`SerialWidths.DefaultWidth`). `NormalizeSequence` pads only when the value is shorter and never truncates. |
| 24 | **Column A is the number of record, and 16 rows contradict their own fields.** Recomposing the number from the eight field cells disagrees with column A on 16 of 15,885 rows: 4 where the `AREA/ZONE` cell is one higher than the zone in the number (rows 2819, 2820, 3115, 3116 — Aconex holds the column A form, 6 events under `-04-`, none under `-05-`), and 12 where the sheet kept a three-digit serial on an `SDW` (rows 1699, 1700, 1726, 1727, 4033, 15659–15665 — Aconex holds the three-digit form). A further 19 rows write the zone as a single digit (`-FAC-0-BLAD02-`) where Aconex holds `-00-`; that one is normalisation, not a contradiction. | `MidpNumberingSampleTests`, which derives the disagreement set from the sheet's own ZONE/SEQUENCE cells and asserts it equals what the parser reports | The number is taken from column A after structural normalisation only (whitespace out, upper case, zone padded to two digits — `DocumentNumbering.NormalizeNumber`). The recompose is kept as a **cross-check**: a disagreement is reported as a warning on import and becomes a control finding, never a silent rewrite. Composing is still what the editor does when an engineer changes a field. |
| 25 | **Ordering Aconex events by `Date Modified` alone reports a superseded revision as the document\'s state** whenever a reviewer responds to an older revision after a newer one was issued. | `TrackerEngineTests.Latest_PrefersHigherRevision_WhenLowerRevisionHasLaterResponse` | `RevisionOrder`: a numeric revision outranks a non-numeric one, numeric revisions compare by value, non-numeric compare ordinally; `Date Modified` descending breaks the remaining tie, then source order. `TrackerEngineSampleTests` still reproduces the sheet with zero disagreements. |
| 26 | **Ten raw Aconex values lost their history to an export suffix.** `-CAD`, `-PDF-CAD`, `-11`, `-PDF.`, `--PDF` and `(00)-PDF.pdf` all failed the 8-segment check, and the old rule only knew about a trailing `-PDF`. Seven other values (`SDW-000008` … `SDW-000014`) are two segments and are genuinely not document numbers. | `select distinct "AconexDocNo" ... where "DocNoFinal" = \'XXX\'` over the imported sample — 17 distinct values, 10 recoverable | `NormalizeDocNo` collapses runs of hyphens and keeps the first eight segments, which covers every suffix without a list to maintain. No substitution happens inside a segment, so `…-2L20001(00)` keeps its parenthetical and simply matches no document. The `XXX` sentinel is gone: the column is nullable and the absence of a number is `null`. |

**EVM vs Corporate Summary — two definitions of "earned"** (Phase 5.5). The Corporate Summary's Earned Value credits a document whose unified status is Approved; EVM credits it only once it was approved **on or before the report date** (PLAN.md § 7: "كل Actual يُفلتر ≤ R"). At ReportDate 2026-08-30 the sample holds **19 documents approved later**, so EVM's EV% is slightly below the summary's Completed% for the disciplines containing them, while PV% matches exactly. Both are intended: the summary reports current state, EVM reports state as at the report date.

**Control Findings formulas** (read directly from `Tracker.xlsx`, Phase 5.4). The four block counts in row 3 are **29 / 136 / 190 / 57**:

1. Aconex vs MIDP — `FILTER(SHD_History[...], (Document Length<>3) * (In MIDP=FALSE) * (Latest=TRUE))`. "Document Length <> 3" is the workbook's test for the `XXX` sentinel. The Status column shows the **raw** Aconex status, not the unified one — the engine returns both.
2. Unplanned in MIDP — `FILTER(MIDP[...], MIDP[Planned Start]="")`.
3. Unused Baseline Packages — `FILTER('Baseline Summary' package rows, Total Drawings = 0)`; the 190 matches the Unused count of § 4.5.
4. Duplicate Document No — `FILTER(MIDP[...], COUNTIF(MIDP[Document No], MIDP[Document No])>1)`, i.e. **every row** of a duplicated number, not one row per group.

**Verified formulas** (read directly from `Tracker.xlsx`, Phase 5.1): `Tracker!N12` `IFERROR(VALUE(Revision)+1,"")` · `O12`/`P12`/`T12` XLOOKUP on `Document No Final & "_" & Date Modified` · `P12` substitutes `Lists!$B$14` when the matched row is `Terminated` · `R12` `MINIFS(Date Modified, Document No_Revision, DocNo & "_" & Revision)` · `S12` `MAXIFS(Date Modified, Document No Final, DocNo)` · `AD12` `MINIFS(...)` same criteria · `AE12` `IF(Status=Approved, Date Modified, "")` · `AB12`/`AC12` Baseline `XLOOKUP(ActivityId → Finish)` and `XLOOKUP(Package & "Approval" → Finish)`, else Delivery Milestone and +28 days. `SHD_History!Terminated` = any row of the same `Document No`+`Revision` with `Date Modified >=` this row having `Review Status = "Terminated"` or `Status = "Closed"`.

### ✚ Found while comparing the 02.TIDPs folder (2026-09-13)

| # | Finding | Evidence | Decision |
|---|---|---|---|
| 27 | **The two DURATION headers carry a line break inside the cell**, so the column labels are literally `01-DURATION \n(DAYS)` and `02-DURATION \n(DAYS)` — the template wraps them over two lines. `DocumentRowParser.FindColumn` trimmed the ends but not the middle, so both compared unequal to `01-DURATION (DAYS)` and mapped to `-1` for the life of the code. `ReadExchange`'s `duration > 0` guard turned that into a null duration on every row of every file rather than into an error anyone would see. **No computed number was ever wrong**: both columns are empty in all 16,269 rows of the folder and in `samples/TIDP-STL.xlsx`, so `BudgetWeight` was already correctly 1 (PLAN.md § 5.2 defaults it to the Exchange 01 duration, or 1). | Header scan over all 36 workbooks in `src/Dip.Api/02.TIDPs` plus `samples/TIDP-STL.xlsx`: these are the only two of the 34 labels with embedded whitespace, and the header block (column A, rows 1–14) is clean in all 37. Raw cell scan of columns Z and AF: 0 numeric values; the 74 non-blank cells in `01.NAP/EL-Electrical` hold a single non-breaking space (`\u00a0`). | Header matching now collapses internal whitespace runs (`NormalizeHeader`), so a label is matched on its words and not its layout — `char.IsWhiteSpace` covers the non-breaking space these sheets also use. Pinned by `DocumentColumnMapTests`, which asserts all 34 columns are found and that the durations land on Z (26) and AF (32). PLAN.md needs no change: this was our lookup failing, not the Excel contradicting a rule. |
| 28 | **A shared file name is not shared content.** `QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx` exists under `01.NAP/AR-Architectural`, `08. Provisional Sum` and `11. NAP PMO/AR-Architectural` and the three hold **completely disjoint** document numbers (1,317 / 184 / 69, zero overlap in any pair). The same is true of the MEC and STR pairs under `01.NAP` and `11. NAP PMO`. Across the whole folder: 16,269 data rows, 15,938 distinct numbers, and the **only** cross-workbook duplicates are 331 numbers shared by `01.NAP/EL-Electrical` and `11. NAP PMO/EL-Electrical` — identical in all 34 columns, so it does not matter which file first-file-wins awards them to. | Row-by-row comparison keyed by DOCUMENT NUMBER over all 36 workbooks, comparing all 34 columns by normalised header label | Confirms the folder sync's key: the path, never the file name (`docs/refactor-log.md` R11). No change to the first-file-wins ownership rule — it has almost nothing to resolve. |
| 29 | **`08. Provisional Sum`'s ARC workbook is mislabelled internally.** Its `DOCUMENT REFERENCE` is the unfilled template placeholder `QF01012-NES-C04518-TDP-XXX-00-000000-000001` and its `DISCIPLINE` header reads `Architectural`, but all 184 of its rows are `SDW-ACO-…` (acoustic). Likewise `06.Subcontractor - Unassigned/…-KNL-….xlsx` carries `DISCIPLINE = Architectural`. | Header block vs row contents, all 36 workbooks | The folder and the file name are the source of truth for owner, discipline tag and sequence; the workbook header is used only for the corporate `Discipline` the importer resolves. This is the evidence behind that split. |

### ⚠ Still to verify (only via tests)
- **Aconex DocNo Normalization** boundary cases: 3 spaces? tabs? trailing `-DWG`? — only 3 patterns confirmed so far
- **Sub 2 always 0**: In every discipline/author row the Sub2 columns (Z, AG) are 0 — either a data gap or Sub2 is defined but not populated. **Confirm the intended definition of Sub2** with the customer before implementing.
- **`# of Submissions` when Revision non-numeric**: In sample, Revision is always a 2-digit number. Behavior when it's e.g. "P00" or "A" unknown — assume null.
- **Interior Design / Landscape have 0 across the board**: Why? Are these disciplines gated (no baseline activities allocated) or is it truly zero-progress? Confirm with data owner.

---

## 6b. Final StatusMapping Seed (authoritative)

Combining `Tracker.xlsx!Lists` + observed status values + legacy `MIDP.xlsx!LISTS`:

| Aconex / Source Status | Unified | Notes |
|---|---|---|
| A - Approved | Approved | |
| B - Approved with Comments | Approved | plural — modern Aconex |
| B - Approved with Comment | Approved | **singular — legacy Tracker Lists** (dedupe) |
| C - Revise and Resubmit | Rejected | |
| D - Rejected | Rejected | |
| E - Review Not Required | Approved | |
| Issued For Approval | UnderReview | (capital F in "For") |
| Issued for Action | UnderReview | Added — observed in data, not in either Lists sheet |
| Issued for information | Approved | |
| For Review | UnderReview | |
| No Longer In Use | Withdrawn | capital I |
| No Longer in Use | Withdrawn | (dedupe defensive) |
| Under Review | UnderReview | |
| QA Rejected | Rejected | |
| QA Checked | UnderReview | |
| Terminated | Withdrawn | (only appears in Review Status; used as computed override) |
| Open | UnderReview | Added — observed in data |
| Responded | UnderReview | Added — observed; treat as awaiting response |
| Submitted to Site | UnderReview | Legacy MIDP LISTS |
| Site Rejected | Rejected | Legacy MIDP LISTS |
| Submitted to Client | UnderReview | Legacy MIDP LISTS |
| For Information | Approved | Legacy MIDP LISTS |

**Matching rule**: case-insensitive + Trim + collapse internal whitespace before compare. Unknown status → null + Warning in ImportBatch.Log.

## 7. Rules to Implement (Summary of Calculations)

### 7.1 Document Ingestion
1. **TIDP/MIDP Import**:
   - Header block: Search for label text, not row number
   - Data table: Find "DOCUMENT NUMBER" header, then read rows below
   - Generate DocumentNumber = L-M-N-O-P-Q-R-S+T+U (no spaces)
   - Preserve leading zeros: Zone PadLeft(2), Sequence PadLeft(4)
   - Extract CorporateDiscipline from picklist mapping

2. **Aconex Import**:
   - Normalize DocNo: Remove all spaces, remove trailing "-PDF" (case-insensitive)
   - Calculate IsTerminated: Latest row with this DocNo + Revision has status Terminated/Closed/No Longer in Use
   - Calculate IsLatest: This row has max DateModified for this DocNo
   - Calculate InMidp: After normalization, DocNoFinal exists in MIDP.DocumentNumber (or IsTerminated)

### 7.2 Tracker Computation
1. **Per Document**:
   - Find all Aconex rows matching normalized DocumentNumber
   - Use row with max DateModified (break ties by taking first)
   - Extract Revision, AconexStatus, DateModified, SubmissionDate (min), ActualFinish (if Approved)
   - Lookup PlannedStart/Finish from Baseline or DeliveryMilestone

2. **Status Unification**:
   - VLOOKUP AconexStatus → UnifiedStatus (see 5.3)
   - If no Aconex match, Status = null

### 7.3 Corporate Summary
1. **Week Calculation**:
   - Week end = last day (Sunday) of the week
   - Formula: `Min(PlannedStart, ActualStart, 2025-11-04) → WeekEnd → StartWeek`
   - Formula: `Max(PlannedStart, ActualFinish) → WeekEnd → EndWeek`

2. **Weekly Buckets**:
   - For each week, count docs where PlannedStart (or ActualStart for submitted) falls within

3. **Discipline Roll-Up**:
   - Group by CorporateDiscipline
   - Total, Planned, Submitted, Approved, Status counts
   - Quality % = Approved / (TotalRevisions - UnderReview)
   - PlannedValue % and EarnedValue %: Use weights (0, 0.6, 0.9, 1.0)

### 7.4 EVM (SPI)
- PV (Planned Value) = Σ weight × factor where factor = 1 if PlannedFinish ≤ ReportDate, 0.6 if PlannedStart ≤ ReportDate, else 0
- EV (Earned Value) = Σ weight × factor where factor = 1 if Approved, 0.9 if 2+ submissions, 0.6 if 1 submission, else 0
- SPI = EV / PV

---

## 8. Test Expectations (Verification Checklist)

| Scenario | Expected Result | Sample Value |
|---|---|---|
| Import TIDP-STL.xlsx | ~40 docs, Structural discipline, all DocumentNumbers valid | QF01012-NES-C04518-SDW-STL-00-… |
| Import MIDP.xlsx | ~10,598 docs, multiple disciplines, leading zeros preserved | Sequence=0001, Zone=00 |
| Parse Aconex docno `"QF01012- NES- C04518-…-PDF"` | Normalize to `"QF01012-NES-C04518-…"` | |
| Compute Tracker for 200 random docs | Match cached values in Tracker.xlsx (dates ≤ 1 sec diff, values exact) | Revision, Status, DateModified |
| Corporate Summary Structural, ReportDate 2026-08-30 | Total 2998, Submitted 858, Approved 624, Rejected 46, Quality ≈ 0.619 | |
| Baseline Summary QP.C.PS.GEN.GEN.1850 | Total docs = 100, Status = Pending (none submitted) | |
| EVM Structural, SPI = EV/PV | ~0.92 (sample value, weight=1 per doc) | |

---

## 9. Known Limitations & Future Enhancements

- **Aconex API**: Currently import via exported CSV/Excel. Future: live sync via Aconex API.
- **Service Account Drive**: Currently API key. Future: Service Account for persistent auth.
- **ActualEffort & CPI**: Baseline has Original Duration, but no Actual Hours recorded yet → CPI unavailable.
- **Model Health**: No Clash/Navisworks integration yet.
- **Status Mapping Override**: Picklists sheet has one status mapping; endpoint to customize per project (not yet in Phase 0–5).

---

## 10. References

- **PLAN.md § 5** — Detailed entity & engine specifications
- **PLAN.md § 5.1** — Excel file descriptions (mirror of sections 2–4 here)
- **PLAN.md § 5.4** — Engine formulas (implemented as rules above)
- **CLAUDE.md** — Hard rules for development

---


---

**Document Version**: 0.2 (revised during the v3 refactor)  
**Last Verified**: 2026-09-08  
**Next Review**: when a new sample workbook arrives
