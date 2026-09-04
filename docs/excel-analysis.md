# Excel Analysis — Sample Workbooks Structure & Rules

**Last Updated:** 2026-09-04  
**Status:** Phase 0.1 — Documentation of actual Excel structure vs. PLAN.md

This document describes the exact structure, formulas, and computed fields in each sample workbook (`samples/TIDP-STL.xlsx`, `samples/MIDP.xlsx`, `samples/Tracker.xlsx`, and supporting sheets). It serves as the specification bridge between Excel and the DIP system.

**Golden Rule**: Every computed value in this project must match the cached values in these Excel files to within acceptable precision (dates < 1 second, numbers < 0.0001 relative error).

---

## 1. File Inventory

| File | Size | Purpose | Source |
|---|---|---|---|
| `TIDP-STL.xlsx` | 340 KB | One discipline TIDP (Structural) | Nesma & Partners sample |
| `TIDP-STL-AFCO.xlsx` | 145 KB | Additional TIDP variant for testing | Nesma & Partners |
| `MIDP.xlsx` | 8.3 MB | Consolidated MIDP (all disciplines, ~10,598 docs) | TIDP consolidation |
| `Baseline.xlsx` | 157 KB | Baseline schedule activities (WBS breakdown) | Project 6 export |
| `PickLists.xlsx` | 50 KB | Numbering schemes (Discipline, Zone, Building, etc.) | Project codification |
| `Tracker.xlsx` | 11 MB | Master output file (MIDP + Aconex + computed columns + summaries) | System output |

---

## 2. TIDP-STL.xlsx Structure

### Purpose
Single-discipline TIDP (Tender Information Document Package) — Structural discipline (STL). Lists planned documents for a single trade with baseline activity mapping and two submission milestones (Submittal + Approval).

### Sheets

#### 2.1 TIDP_Sheet

**Purpose**: Planned documents for Structural discipline.

**Header Block** (Rows 3–11, Column A = Label, Column B = Value):
```
CLIENT              → Value in B3
PROJECT             → Value in B4
ORGANISATION        → Value in B5
DISCIPLINE          → Value in B6 = "Structural" (NOT "STL" — that comes from F05_Discipline)
APPROVER            → Value in B7
DATE CREATED        → Value in B8 (datetime or date)
DATE LAST UPDATED   → Value in B9
REVISION NUMBER     → Value in B10
DOCUMENT REFERENCE  → Value in B11
```

**Notes on Header**:
- Search for the *text* "DISCIPLINE" in column A; do not assume row 6.
- The discipline code (STL) and corporate name (Structural) are stored separately and inferred from the data rows.

**Data Table** (Named Range "TIDP" or found by searching for "DOCUMENT NUMBER" in column A):

Typical header row: **Row 16** in sample. But search for "DOCUMENT NUMBER" to find it reliably.

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

**Purpose**: Enumerated values (dropdowns) for the numbering scheme.

**Structure**: Columns for each picklist field (Project, Originator, Contract, DocType, Discipline, Zone, Building, DrawingType, Level, etc.). Each column has header + codes + descriptions.

**Example** (Discipline):
```
Discipline Code | Discipline Description
STL             | Structural
STR             | Structural (variant?)
ARC             | Architectural
ELE             | Electrical
MEC             | Mechanical
FIR             | Fire & LS
INF             | Infrastructure
FAC             | Façade
...
```

---

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

**Header Row**: The row containing "Document No" in column C (or column D in Tracker variant).

**Columns** (selected, per PLAN.md § 5.1.3):

| Col | Excel Header | System Field | Type | Notes |
|---|---|---|---|---|
| A | File | `FileType` | Text | pdf, dwg |
| B | File Name | `FileName` | Text | Uploaded filename |
| C | Document No | `AconexDocNo` | Text | **May contain extra spaces** after hyphens and trailing `-PDF`. E.g., `"QF01012- NES- C04518- SDW- STR- 00- BLAD05- 2FL0101-PDF"` |
| D | Revision | `Revision` | Text | "00", "01", "02", … (always read as text, preserve leading zero) |
| E | Title | `Title` | Text | |
| F | Status | `AconexStatus` | Text | "A - Approved", "Issued For Approval", "Responded", "Terminated", "No Longer in Use", … |
| G | Review Status | `ReviewStatus` | Text/Enum | "Terminated", (blank) |
| H | Date Modified | `DateModified` | DateTime | **Critical**: Has sub-second precision (e.g., "2025-12-15 14:23:47.123456"). Preserve in DB as `timestamp without time zone`. |
| I | Type | `Type` | Text | "Shop Drawing", "Calculation", "Report" |
| J | Discipline | `Discipline` | Text | "Structural", "Electrical", … (full name, not code) |
| K | Area - Geographical / Zone | `Area` | Text | Geographic zone |
| L | Venue - Building / Facilities | `Venue` | Text | Facility code |
| M | Floor Level | `FloorLevel` | Text | |
| N | Transmittal In | `TransmittalIn` | Text | Transmittal reference (blank or number) |

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

**Purpose**: Status mapping table (old reference; superseded by Tracker Lists sheet).

**Columns**: Aconex Status → Unified Status

| Aconex Status | Unified |
|---|---|
| A - Approved | Approved |
| B - Approved with Comments | Approved |
| C - Revise and Resubmit | Rejected |
| … | … |

---

## 4. Tracker.xlsx Structure

### Purpose
Master output file containing MIDP + Aconex + engine-computed columns + summary reports.

### Sheets

#### 4.1 Tracker Sheet

**Identical structure to MIDP sheet, plus computed columns**:

| Column | Formula | System Calculation | Notes |
|---|---|---|---|
| DateModified (new) | `MAXIFS(Aconex.DateModified, AconexDocNo_normalized=this.DocumentNumber)` | For each document, find max DateModified from Aconex revisions | null if no Aconex match |
| LatestRow (index) | Index of row in Aconex with DateModified = max | Used to read other fields from Aconex | |
| Revision | `Aconex[LatestRow].Revision` | Read from Aconex latest row | |
| AconexStatus | `IF(AconexRevision.IsTerminated, "Terminated", AconexRevision.AconexStatus)` | Override if Terminated | |
| Status (Unified) | `VLOOKUP(AconexStatus, Lists, UnifiedStatus)` | Map Aconex → Approved/Rejected/UnderReview/Withdrawn | |
| SubmissionsCount | `IF(ISNUMBER(VALUE(Revision)), VALUE(Revision) + 1, NULL)` | Parse "00" → 1, "01" → 2, etc. | |
| SubmissionDate | `MINIFS(Aconex.DateModified, AconexDocNo=this, Aconex.Revision=this.Revision)` | First occurrence of this revision | |
| Transmittal | `Aconex[LatestRow].TransmittalIn` | Read from Aconex; blank if "0" | |
| ActualStart | `MINIFS(Aconex.DateModified, AconexDocNo=this)` | Earliest submission for this doc | |
| ActualFinish | `IF(Status="Approved", DateModified, NULL)` | Filled only if Status is Approved | |
| PlannedStart | `IF(ScheduleMode=Baseline, XLOOKUP(ActivityId, Baseline.ActivityCode, Baseline.Finish), DeliveryMilestone)` | Baseline mode: Submittal Finish; Working Plan mode: DeliveryMilestone | |
| PlannedFinish | `IF(ScheduleMode=Baseline, XLOOKUP(ActivityId_Approval, Baseline.ActivityCode, Baseline.Finish), PlannedStart + 28)` | Baseline: Approval Finish; Working Plan: DeliveryMilestone + 28 days | |

**Sample Row** (from Tracker.xlsx):
```
QF01012-NES-C04518-SDW-STR-00-BLAD05-2FL0101 | Structural GA | … | STR | Structural |
01-Author | LOD400 | … | 01-ExchangeDate |
… (02-Stage columns) |
2025-12-22 09:15:30.654321 | 01 | Issued For Approval | UnderReview | 2 | 2025-12-15 14:23:47 | TR002 |
2025-12-15 14:23:47 | 2025-12-22 09:15:30 | null |  (wait for approval) | 2025-12-29 | null |
```

#### 4.2 SHD_History & SHD_Latest Sheets

Same as Aconex History / Aconex Latest in MIDP.xlsx (copy or link).

#### 4.3 Baseline Sheet

Same as Baseline in TIDP-STL.xlsx (copy or link).

#### 4.4 Corporate Summary Sheet

**Purpose**: Weekly progress, discipline breakdown, S-curve (planned vs. earned value).

**Structure**: 
- **Header** (rows 1–5): ReportDate, ScheduleMode, etc.
- **Weekly Data** (table): Columns Week Ending, WeeklyPlanned, WeeklySub, WeeklyApproved, CumulativePlanned, CumulativeSub, CumulativeApproved
- **Discipline Breakdown** (wide table): For each discipline (Structural, Electrical, …), columns Total, Planned, Submitted, Approved, Rejected, UnderReview, Withdrawn, Quality%, PlannedValue%, EarnedValue%

**Example** (discipline row for Structural):
```
Discipline: Structural
Total: 1245
Planned (PlannedStart ≤ ReportDate): 1100
Submitted (ActualStart ≤ ReportDate): 850
Approved (ActualFinish ≤ ReportDate): 620
Quality (Approved / (TotalRevisions - UnderReview)): 0.62
PlannedValue% (S1*0.6 + S2*0.9 + Approved*1.0) / Total: 0.645
EarnedValue% (same weights, but using Status & SubmissionsCount): 0.594
```

**Formulas to implement**:
1. **Week End Calculation**: Week ends on Sunday 23:59:59. Formula: `= DATE(YEAR(date), MONTH(date), DAY(date) + (7 - WEEKDAY(date, 2)))`
   - Example: 2025-11-04 (Tuesday) → 2025-11-09 (Sunday)
2. **Weekly Counts**: For each week, count documents where PlannedStart falls in (PriorSundayEnd, ThisSundayEnd]
3. **Discipline Grouping**: Group Tracker rows by CorporateDiscipline, then apply the rules

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

**As of 2026-09-04**, after review:

### ✓ Confirmed
- ✓ Document numbering formula: F01-F02-F03-F04-F05-F06-F07-F08A F08B F08C (no spaces in production)
- ✓ DateModified precision: sub-second (microseconds in Aconex)
- ✓ Baseline linking: ActivityCode + Package-based; Submittal/Approval pair
- ✓ Status mapping: 12 statuses → 4 unified (Approved, Rejected, UnderReview, Withdrawn)
- ✓ Discipline code vs. corporate name: Separate fields; e.g., F05_Discipline="STL" → CorporateDiscipline="Structural"
- ✓ Exchange stages: Two DataExchange blocks (01 Submittal, 02 Approval)
- ✓ ReportDate: Default to TODAY(); configurable per Project
- ✓ Leading zeros: Zone (00), Sequence (0004) preserved

### ⚠ Needs Verification
- **Aconex DocNo Normalization**: Exact rule for removing spaces and `-PDF` suffix — verified on 3 samples, assumed universal
- **Weekly Boundary**: Week ends on Sunday 23:59:59 — verify exact formula for 2025-11-02 to 2025-11-09
- **SPI Weights**: Default weights (Pending 0%, Sub1 60%, Sub2 90%, Approved 100%) — confirm if any override in Corporate Summary
- **Unused Baseline Packages**: Rule for "Unused" (Total count = 0 in MIDP documents) — needs integration test
- **PromoteBatch Snapshot JSON**: Format for storing pre-promotion state — confirm exact JSON structure

### ❌ Corrections to PLAN.md
- **None identified yet** (PLAN.md appears to match Excel structure closely)

---

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

**Document Version**: 0.1 (Phase 0.1)  
**Last Verified**: 2026-09-04  
**Next Review**: After Phase 1.2 (database schema finalized)
