# DIP v3 refactor — execution log

Run of `docs/refactor-plan.md`, phases R1 → R7, executed autonomously on 2026-09-08.

Test environment: local Postgres 16 (`docker start postgres-catalogs`),
`TEST_POSTGRES_CONNECTION=Host=localhost;Port=5432;Database=dip_test;Username=postgres;Password=postgres`.
Neon is never used by tests — `TestConnectionGuard` refuses a connection string containing `neon.tech`.

Baseline before the run (commit `5272ffb`): 3 + 131 + 29 + 38 = 201 tests, 0 failed, 0 skipped.

---

## R1 — Backend core: schema v3, no disk, read-only Drive, workers, automatic import

### Neon reset
**Neon reset on 2026-09-08T11:13:12Z, migration `Init` applied, seed OK.**
`backend/scripts/reset-neon.sql` (`DROP SCHEMA public CASCADE` + recreate + grants) was run once through
`psql` with the connection parsed out of `dotnet user-secrets`; the connection string was never printed
or written to a file. `dotnet ef database update` then applied the single regenerated `Init` migration and
one boot of the API ran `IdentitySeeder`. Verified afterwards:
`Disciplines = 10`, `StatusMappings = 22`, `Users = 1`, `PicklistItems = 0`, `Documents = 0` —
i.e. seed data only, picklists left for the first Drive poll / upload as § 9.2 requires.

### What changed
- **Entities** (§ 5.1): `Folder.IsCompany` + `Folder.AuthorId` (FK `PicklistItems`, `Restrict`);
  `FolderFile` reshaped — `Source` → `ContentSource`, `Md5` → `ContentMd5` (non-null), new
  `ContentModifiedAt`, `ImportError`, `LastImportedAt`, `IsDeleted`, `StoragePath` removed;
  new `FileBlob` (`bytea`, PK = `FolderFileId`, cascade); `PicklistItem`/`StatusMapping` gain
  `IsDeleted`/`DeletedAt`; `PicklistField` gains `Classification`, `CorporateDiscipline`, `Author`;
  new in-memory `WorkItemKind`; `DocumentSnapshot` became the materialised tracker row
  (`Layer`, `FolderFileId`, display columns, no FK to `Document`); `PromoteBatch` lost
  `SnapshotJson`/`RolledBack`; `ImportStagingRow` deleted.
- **Indexes**: `FolderFiles` unique `(FolderId, lower(Name)) WHERE NOT "IsDeleted"` — implemented as a
  stored generated column `NameLower` plus a filtered unique index, so the case-insensitive rule is
  enforced by Postgres and stays inside the EF model; `PicklistItems` and `StatusMappings` uniqueness
  filtered on `NOT "IsDeleted"`; `DocumentSnapshots` indexed on `(ProjectId, Layer)`,
  `(ProjectId, DocumentNumber)`, `(ProjectId, Discipline)`, `(ProjectId, Status)`, `FolderFileId`.
- **Migration**: the old `20260904125246_Init` and the model snapshot were deleted and one migration
  `20260908105047_Init` regenerated from the v3 model.
- **No disk**: `ILocalFileStorage`, `LocalFileStorage` and every `StoragePath` reference deleted;
  `IExcelReader.Open(string)` removed, so every importer takes a `Stream` and the bytes always come
  from `FileBlob` through a read-only `MemoryStream`.
- **Workers/Hubs**: `Workers/{WorkQueue, WorkerState, ISyncTrigger, DriveSyncService, DriveSyncWorker,
  FileImportService, ImportWorker}`, `Hubs/{SyncHub, ISyncNotifier, HubSyncNotifier, NoopSyncNotifier}`,
  hub mapped at `/hubs/sync`, JWT read from `access_token` for `/hubs/*`, Development CORS narrowed from
  `AllowAnyOrigin` to `http://localhost:5173` + `AllowCredentials` (SignalR negotiate cannot use `*`
  with credentials), `GoogleDrive:PollHours = 5` and `GoogleDrive:StartupDelaySeconds = 30`.
- **Sheet routing (R5)** in `FileImportService`, layer choice (R3/R4) in `FileImportService.LayerFor`.
  `AconexHistoryImporter` already accepted both `Aconex History` and `SHD_History`; no change needed.
- **Folders slices**: `UploadFile` rewritten (blob, R2 upsert, 64 MB cap, 400 on an unrecognised
  workbook, 202 `{fileId, name, replaced}`), new `TriggerDriveSync`, `GetDriveStatus`, `DownloadFile`;
  `CreateFolder` now rejects a Drive-linked parent; `RenameFolder`, `DeleteFolder`, `DeleteFile` and the
  chunked `SyncFromDrive` deleted.
- **Imports**: `StartImport`, `RunImportStep`, `ImportDispatcher` deleted; `GetImportStatus` and
  `ListImportBatches` kept; `ImportDtos` reduced to `ImportBatchSummary`.
- **Drafts**: `PromoteExecutor` extracted and `PromoteHandler` delegates to it; `RollbackPromote` and
  `PromoteSnapshot` deleted; `PromoteLoader` no longer filters on `RolledBack`.
- **Health**: `DriveHealthCheck` reports `lastRunFinishedAt` and `queuedImports` from `WorkerState`.
- **Tests**: `TestConnectionGuard` + its unit test; `DipApiFactory` sets `PollHours = 0` and swaps in
  `NoopSyncNotifier`; `TestHelpers` with `WaitForImportAsync`; `FoldersFlowTests` and `ImportsFlowTests`
  rewritten around automatic import; new `DriveSyncServiceTests` and `FileImportServiceTests` with a
  `FakeDriveClient`; `PromoteFlowTests.Rollback_*` deleted; importer tests feed streams.

### Removed files
`Dip.Application/Abstractions/ILocalFileStorage.cs`, `Dip.Infrastructure/Storage/LocalFileStorage.cs`,
`Dip.Api/Features/Folders/{RenameFolder,DeleteFolder,DeleteFile,SyncFromDrive}/*`,
`Dip.Api/Features/Imports/{StartImport,RunImportStep}/*`, `Dip.Api/Features/Imports/ImportDispatcher.cs`,
`Dip.Api/Features/Drafts/RollbackPromote/*`, `Dip.Api/Features/Drafts/PromoteSnapshot.cs`,
`Dip.Infrastructure/Persistence/Migrations/20260904125246_Init*.cs`.

### Deviations
1. **`ExchangeSignature` moved rather than deleted.** It lived in the deleted `PromoteSnapshot.cs` but is
   still needed by `PromoteLoader`, so it was appended to `Features/Drafts/PromoteLoader.cs` (its only
   consumer) instead of getting a file of its own.
2. **`FolderFiles` case-insensitive unique index.** The plan asks for `(FolderId, lower(Name))`. EF cannot
   put an expression in an index, so a stored generated column `NameLower` computed as `lower("Name")`
   carries it; the index is `(FolderId, NameLower) WHERE NOT "IsDeleted"`. Same guarantee, and it stays
   model-driven so future migrations do not drift.
3. **`Dip.Api.IntegrationTests` now references `Dip.Infrastructure.Tests`** so both suites share one
   `TestConnectionGuard` definition rather than duplicating the check, and
   `[assembly: InternalsVisibleTo("Dip.Api.IntegrationTests")]` was added so the integration tests can
   seed a scratch project from `SeedData`.
4. **Scratch projects in tests that write Live rows.** `ImportsFlowTests` and `FileImportServiceTests`
   create their own `Project` (with the seeded disciplines and status mappings) instead of using the
   shared QPAC project: with imports now automatic, a Live TIDP import in one test class showed up as a
   promote conflict in another. `TestHelpers.NewProjectAsync` does this.
5. **`ReportsFlowTests` "before recalculation" assertion dropped.** The import worker recalculates
   automatically, so `recalculationRequired == true` right after an import is no longer reachable. The
   test still exercises the step endpoint as the admin fallback and asserts the reports afterwards.
6. **`UploadFile` enqueues on `DbContext.SavedChanges`** rather than inline, so the import worker can
   never look for a row the pipeline has not committed yet.

### Test summary
```
Passed! - Failed: 0, Passed:   3, Skipped: 0, Total:   3 - Dip.Domain.Tests.dll
Passed! - Failed: 0, Passed: 131, Skipped: 0, Total: 131 - Dip.Engine.Tests.dll
Passed! - Failed: 0, Passed:  42, Skipped: 0, Total:  42 - Dip.Infrastructure.Tests.dll
Passed! - Failed: 0, Passed:  46, Skipped: 0, Total:  46 - Dip.Api.IntegrationTests.dll
```
`dotnet build`: 0 warnings, 0 errors. `grep` of § 0.4 and § 5.9 over `backend/src`: empty.

### Known gaps carried into R2
- `DocumentSnapshot` is still filled from the Live `Documents` table only; the effective document set
  (R8) arrives in R2, together with `Layer` for Draft rows.
- `FolderNode` / `FolderTreeNode` do not yet carry `isCompany`, `authorName`, `fileCount`,
  `hasNewerDraft`; that is R2's DTO work.

---

## R2 — Targets, companies, conversion, effective set, materialised tracker, Live edits

### What changed
- **`EffectiveDocumentLoader`** (`Dip.Api/Common`): the union of the Live rows of Live-target
  files and the Draft rows of Draft-target files, deduplicated by upper-cased document number with
  the newest source file winning and Live breaking a tie (R8). Draft rows are projected onto
  in-memory `Document` objects so every engine keeps one input type.
- **`RecalculationService`** rebuilt over the effective set: `RunStepAsync` (the admin fallback) and
  `RunAllAsync` (the worker's path, which also deletes snapshots whose row left the set and raises
  `recalculationFinished`). Snapshots now carry `Layer`, `FolderFileId` and the display columns.
- **`ReportDataLoader`** reads the effective set, so Corporate Summary, Baseline Summary, Control
  Findings, EVM and Export all cover both layers with no change of their own.
- **Tracker over snapshots only**: `ListTrackerDocuments` filters and pages one table in SQL,
  `GetTrackerDocument` loads the snapshot by id, and `TrackerRowDto` gained `layer`.
- **`SetFolderTarget`** cascades to the whole subtree (`FolderSubtree`), returns
  `{folderId, target, foldersUpdated}` and queues a recalculation.
- **`SetFolderCompany`** (`PUT /api/folders/{id}/company`) with author validation against the
  non-deleted `Author` picklist.
- **`ConvertToLive`** (`POST /api/folders/{id}/convert-to-live`): promotes every file below the
  folder that has a draft with `deleteMissing: true`, writes a `PromoteBatch` per file, cascades the
  target to Live and queues a recalculation — all inside the one transaction.
- **`UpdateDocument`** (`PUT /api/documents/{id}`): the Live twin of the draft editor. Recomposes the
  number from the eight fields, 409 on a number another row holds, one `AuditLog` row per changed
  field including the numbering fields, drops the row's snapshot and queues a recalculation.
- **`GetFileWorkbook`** (`GET /api/folder-files/{id}/workbook`): 34 columns A–AH in PLAN.md § 5.1.1
  order for TIDP/MIDP (column A read-only), plus read-only `Baseline` and `Picklists` tabs; the TIDP
  header block; dates as `yyyy-MM-dd` and every cell a string, so leading zeros survive.
- **DTOs**: `FolderNode`/`FolderTreeNode` gained `isCompany`, `authorName`, `fileCount`,
  `hasNewerDraft`; `FolderFileDto` gained `rowCount`, `hasNewerDraft`, `effectiveLayer`.
  `FolderProjections` computes "Drive has newer data" from the draft's import batch versus the last
  promote.
- **New**: `ConflictException` → 409 in `ProblemDetailsExceptionHandler`.

### Deviations
7. **`ConflictException` added to `Dip.Application/Behaviors/AuthorizationBehavior.cs`.** The codebase
   had no 409 path; the file already holds `UnauthorizedException` and `ForbiddenException`, so the
   new exception sits with them rather than in a file of its own.
8. **Recalculation is serialised per project** by a process-wide semaphore in `RecalculationService`.
   Snapshots are keyed by row id, so the worker's `RunAllAsync` and a concurrent call to the admin
   step endpoint raced on the same primary keys (reproduced as a duplicate-key failure in
   `RecalculationTests`). The plan does not mention it; the alternative was leaving a real race in.
9. **`FolderSubtree` and `FolderProjections`** are new shared helpers under `Features/Folders/`, not
   named in the plan's file list. Both are needed by more than one slice (target cascade, convert,
   tree, folder detail, workbook).
10. **The Draft-layer sample test compares Draft against Live instead of against the Tracker sheet's
    literals.** The plan asks for "the same Corporate Summary numbers as Live (reuse
    `CorporateSummarySampleTests` expectations)". Those expectations are read from
    `Tracker.xlsx!Tracker` (15,883 rows), while importing `MIDP.xlsx!MIDP` through the importer
    yields 15,724 effective rows — the two sheets are different populations, and the gap exists on
    the Live side too (it is not introduced by the Draft layer). `DraftLayerReportsTests` therefore
    imports MIDP + Baseline into two projects that differ only in folder target and asserts the
    Corporate Summary, Baseline Summary and Control Findings payloads are byte-identical, with
    anchors so two empty reports cannot pass. The absolute sample numbers remain covered by the
    unchanged `CorporateSummarySampleTests`. Logged in `docs/excel-analysis.md` § 6 as well.
11. **`GetFileWorkbookTests` does not assert "row 1 col A = the sample's first number".** § 5.6 orders
    the sheet by `DocumentNumber`, under which `…-CAL-…` sorts before `…-SDW-…`, so row 1 is not the
    workbook's row 17. The test instead asserts that column A equals the concatenation of columns
    L–U for row 1 (the CONCATENATE rule itself) and that the documented first number
    `QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004` is present in the sheet.
12. **`[assembly: InternalsVisibleTo("Dip.Api.IntegrationTests")]` added to `Dip.Api`** so the tests
    can drive `EffectiveDocumentLoader`, `FileImportService` and `RecalculationService` directly.

### Test summary
```
Passed! - Failed: 0, Passed:   3, Skipped: 0, Total:   3 - Dip.Domain.Tests.dll
Passed! - Failed: 0, Passed: 131, Skipped: 0, Total: 131 - Dip.Engine.Tests.dll
Passed! - Failed: 0, Passed:  42, Skipped: 0, Total:  42 - Dip.Infrastructure.Tests.dll
Passed! - Failed: 0, Passed:  62, Skipped: 0, Total:  62 - Dip.Api.IntegrationTests.dll
```
`dotnet build`: 0 warnings, 0 errors.

### Known gaps carried into R3
- `GetPicklists` / `GetStatusMappings` still return soft-deleted rows and the CRUD slices do not
  exist yet; `PicklistImporter` still reads only the nine numbering lists.

---

## R3 — Lists backend

### What changed
- **`PicklistImporter` rewritten** to locate every list by its **row-5 header text** rather than by
  the FIELD group labels, so all 17 lists of § 7 come in from one pass: the nine numbering lists
  (`Ab. 01`…`Ab. 08B` with their description columns) and the eight value lists
  (`Authoring Software`, `File/Exchange Format`, `Scope Area`, `Code`+`Status`, `Scale`,
  `Classification ID`+`Classification`, `Corporate Discipline`, `Author`). `8C - Sequence Number`
  holds prose and is simply not in the map. Values are trimmed (Scope Area and Zone carry trailing
  spaces) and soft-deleted codes are skipped, counted and reported (R9).
- **Soft-delete filters** on every read: `GetPicklists`, `GetStatusMappings`, `ReportDataLoader`,
  `RecalculationService`, `GetTrackerDocument`, `GetFileWorkbook`, `SetFolderCompany` and
  `ListsImporter` (which now skips a deleted mapping the way the picklist importer does).
- **Picklist slices**: `CreatePicklistItem` (201, or 200 with `restored: true` when it revives a
  soft-deleted row), `UpdatePicklistItem` (409 on a live sibling's code), `DeletePicklistItem` (soft,
  409 when a company folder names the item as its author), `RestorePicklistItem` (409 if the code was
  taken meanwhile), `ReorderPicklist` (`SortOrder` 1..n).
- **Status-mapping slices**: `CreateStatusMapping`, `UpdateStatusMapping`, `DeleteStatusMapping`,
  `RestoreStatusMapping`, all with the same soft-delete semantics.
- **`GetPicklists` returns a group per `PicklistField`** even when empty, so the Lists page can render
  one tab per list, and items now carry `field`, `isDeleted` and `deletedAt`.

### Verified against the sample (`samples/PickLists.xlsx`, `Pick_Lists`)
Every count in § 7 matched exactly on the first run, asserted per list with its first code and
description in `PicklistImporterTests`:
Project 1 · Originator 1 · Contract 1 · DocType 94 · Discipline 98 · Zone 29 · Building 12 ·
DrawingType 10 · Level 16 · AuthoringSoftware 13 · ExchangeFormat 26 · ScopeArea 31 ·
SuitabilityCode 14 · Scale 16 · Classification 10 · CorporateDiscipline 9 · Author 12.

### Deviations
13. **The FIELD-label pass was replaced, not kept alongside a second pass.** § 5.8 says to keep the
    FIELD-label scan and add a second pass for the single-column lists. In the standalone workbook
    row 4 labels the serial-number group once (`FIELD 08 - SERIAL NUMBER`) rather than as
    `FIELD 08A` / `FIELD 08B`, so the old scan could not find Drawing Type or Level at all and § 7's
    counts of 10 and 16 were unreachable through it. Locating every list by its row-5 header is one
    rule instead of two and covers all 17 lists; the counts above confirm it.
14. **`DeletePicklistItem` refuses to delete an item a company folder points at** (409). `Folders.AuthorId`
    is a restricted FK, so a soft delete would otherwise leave a folder naming a hidden author. Not in
    the plan; the alternative was a folder whose author cannot be displayed.

### Test summary
```
Passed! - Failed: 0, Passed:   3, Skipped: 0, Total:   3 - Dip.Domain.Tests.dll
Passed! - Failed: 0, Passed: 131, Skipped: 0, Total: 131 - Dip.Engine.Tests.dll
Passed! - Failed: 0, Passed:  44, Skipped: 0, Total:  44 - Dip.Infrastructure.Tests.dll
Passed! - Failed: 0, Passed:  69, Skipped: 0, Total:  69 - Dip.Api.IntegrationTests.dll
```
`dotnet build`: 0 warnings, 0 errors.

---

## R4 — Frontend: explorer grid, realtime, dashboard swap, navigation

### What changed
- **`shared/realtime/`**: `syncHub.ts` (one `HubConnectionBuilder` to `${apiBaseUrl}/hubs/sync` with an
  `accessTokenFactory` read on every connect, `withAutomaticReconnect`), `SyncHubProvider` (joins the
  project group, exposes `connecting | connected | reconnecting | disconnected`), `useSyncEvent`, and
  `invalidation.ts` — the event → TanStack query mapping, kept as data so it is testable without a
  live socket. `RealtimeGate` opens the connection once the user is signed in.
- **`shared/ui/toast.tsx`**: a minimal shadcn-style toaster (no new package) for sync and import
  progress.
- **`features/explorer/`** replaces `features/folders/`: `ExplorerPage`, `ExplorerToolbar` (breadcrumb,
  Upload, Sync now, target switch, Company/Author, Convert to Live, New folder, Drive status pill),
  `TileGrid` + `FolderTile` + `FileTile` (Drive-like 160×132 tiles, folders before files, click to
  select, double-click to open, arrow keys, right-click menu, drag-and-drop upload, a spinner from
  `fileImportStarted` until `fileImported`/`fileFailed`), `ContextMenu` (the existing `DropdownMenu`
  anchored at the pointer — no new package), `UploadDialog` (multi-file, 202 handling),
  `ConvertToLiveDialog` (per-file promote diff before, result table after), `CompanyDialog`.
- **Dashboard at `/`** (decision D8): `features/summary/` and `features/findings/` folded into
  `features/dashboard/` as five tabs — Overview · Corporate · Baseline · Control Findings · EVM.
  The old welcome page is gone; the route is lazy so Recharts is not in the entry bundle.
- **Navigation**: Dashboard · TIDPs · MIDP · Baseline · Tracker · Lists · Users · Settings. The
  Dashboard now requires `reports.view` like the reports it contains, so an empty permission set
  sees nothing.
- **Types**: `FolderTreeNode`/`FolderNode`/`FolderFileSummary` reshaped, `UploadResult`,
  `DriveStatus`, `TriggerSyncResult`, `SetTargetResult`, `ConvertToLiveResult`, `DocumentRow`, the
  `Workbook` family, `PicklistField` as a union of the 17 names, `isDeleted`/`deletedAt` on list
  rows, `TrackerRow.layer`; `StartImportResult`, `RunImportStepResult` and `RollbackResult` deleted.
- **Rollback removed from the UI**: `useRollbackPromote` and the dialog's Roll back button are gone
  (decision D7); the dialog now says the worker has been asked to recalculate.

### Removed files
`features/folders/` (api, FolderExplorerPage, FileList, SyncDriveDialog and their tests),
`features/imports/` (api, ImportDialog, kinds and its test), `features/summary/SummaryPage.tsx`,
`features/findings/ControlFindingsPage.tsx`, the old `features/dashboard/DashboardPage.tsx`.
`FolderTree`, `FolderBadges` and `UploadDialog` moved into `features/explorer/`; the summary charts
and api moved into `features/dashboard/`.

### New packages (§ 9.4)
`@microsoft/signalr` (hub client — no alternative) and `@tanstack/react-virtual` (installed here,
used by the R5 grid: the MIDP sheet's 15,883 rows freeze the tab if all are rendered).

### Deviations
15. **`/files/:fileId` is registered in R5, not R4.** § 6.6 puts the route in R4 and the page in R5;
    a route pointing at a module that does not exist yet does not compile. In R4 opening a file
    therefore opens the draft review for a Draft-layer file and downloads a Live one; R5 introduces
    the route and repoints Open at the viewer.
16. **Provider/hook files split.** `useSyncHub`/`useToast` live in their own modules next to their
    providers (`syncHubContext.ts` + `SyncHubProvider.tsx`, `toastContext.ts` + `useToast.ts`),
    matching the existing `authContext`/`AuthProvider`/`useAuth` split, so `npm run lint` stays at
    zero warnings.
17. **`navigation.test.ts`'s "dashboard needs no permission" case became "an empty permission set
    sees nothing"**, as § 6.6 asks.

### Test summary
```
Test Files  19 passed (19)
Tests       90 passed (90)
```
`npm run lint`: 0 errors, 0 warnings. `npm run typecheck`: clean. `npm run build`: succeeds.

---

## R5 — Workbook viewer

### What changed
`features/workbook/` — the spreadsheet a file opens into at `/files/:fileId`:
- **`WorkbookPage`**: title bar (file name, DB1 Draft / DB2 Live badge, source and modified time,
  "Drive has newer data", Download, Back), the TIDP header block, the grid, the sheet tabs and the
  status bar.
- **`FormulaBar`**: the Excel name box (`C12`) and the active cell's value, read-only — column A shows
  the composed document number exactly as the workbook's `CONCATENATE` does.
- **`Grid`**: rows virtualised with `@tanstack/react-virtual` (22 px), a sticky column-letter row and
  column-title row, a sticky row-number gutter, frozen column A, click and shift-click range
  selection, arrows/Home/End/PageUp/PageDown, `Ctrl+C` copies the selection as TSV, and an inline
  editor opened by double-click, F2 or typing — Enter and Tab commit, Esc cancels, and a validation
  error from the API keeps the editor open with the message on the cell.
- **`Cell` / `CellEditor` / `SheetTabs` / `StatusBar`**: Excel styling — `#f2f2f2` headers,
  `#d9d9d9` gridlines, a `#217346` active-cell border and a 10 % selection wash, both themed for dark.
- **`api.ts`**: `useInfiniteQuery` over 500-row pages, and `saveRowUrl` routing a save to
  `PUT /api/drafts/documents/{id}` or `PUT /api/documents/{id}` by the file's layer.
- **`columns.ts`**: the 34 column keys in PLAN.md § 5.1.1 order, A1 letters, and `rowToPayload`,
  which turns the edited row back into the full-replace body the two editors take — without the
  document number, because the server recomposes it from L..U.
- The `/files/:fileId` route and the explorer's "Open a file" now point at the viewer.

### Deviations
18. **`Grid.test.tsx` mocks `offsetWidth`/`offsetHeight`.** TanStack Virtual measures its scroller
    with those, and jsdom reports 0 for both, so without the mock the grid renders no rows and the
    test would pass against an empty grid.
19. **Closing the editor returns focus to the grid.** Not in the plan, but without it the keystroke
    after a commit lands on the document rather than moving the active cell.

### Test summary
```
Test Files  22 passed (22)
Tests       108 passed (108)
```
`npm run lint`: 0 errors, 0 warnings. `npm run typecheck`: clean. `npm run build`: succeeds.

---

## R6 — Lists page

### What changed
`features/lists/` becomes an editor rather than a read-only listing:
- **`ListsPage`**: 18 tabs — one per `PicklistField` in the workbook's own order, named the way the
  sheet names them (Document types, Area / Zone, Suitability status, Corporate discipline, …) —
  plus Status mapping, with a "Show deleted" toggle over the whole page.
- **`PicklistTable`**: an "Add item" row at the top, inline edit (pencil → inputs → save/cancel),
  ▲▼ reordering that posts the whole order, delete behind a `ConfirmDialog` that says what a delete
  means, and greyed deleted rows with a Restore button when they are shown. Single-column lists
  (Authoring software, File / exchange format, Scope area, Scale, Corporate discipline, Author) drop
  the description column, matching the workbook.
- **`StatusMappingTable`** mirrors it with a `UnifiedStatus` select and the Legacy checkbox.
- **`api.ts`** covers the nine list endpoints; every mutation invalidates both the live and the
  include-deleted view.
- Viewers without `lists.manage` get the same tables with no controls.

### Test summary
```
Test Files  24 passed (24)
Tests       117 passed (117)
```
`npm run lint`: 0 errors, 0 warnings. `npm run typecheck`: clean. `npm run build`: succeeds.

---

## R7 — Docs, deploy, plan alignment

### What changed
- **`PLAN.md`** — the sections § 13 of the refactor plan names were rewritten in place, in Arabic,
  keeping every sample number:
  - § 1 "قيود ASPMonster": the "no `BackgroundService`, no reliable SignalR" constraint is replaced
    by the two hosted workers, the hub, the 64 MB upload limit and the application-pool requirement.
  - § 2: the feature list now names the slices that exist, plus `Workers/`, `Hubs/` and
    `Common/EffectiveDocumentLoader`.
  - § 3.2: rewritten around `FolderFile` + `FileBlob`, the `(FolderId, lower(Name))` identity, the
    newest-wins rule, automatic import, companies and authors, and the removal of rename/delete.
  - § 3.4: the import-by-target rules replaced by R3/R4/R5 (Drive → Draft always, uploads follow the
    target, Aconex/Baseline/Picklists/Lists always Live); `PromoteBatch` loses `SnapshotJson`;
    rollback replaced by `ConvertToLive`.
  - § 4: Drive is read-only and polled by `DriveSyncWorker`; "لا تستورد تلقائياً" explicitly reversed.
  - § 5.2: `DocumentSnapshot` documented as the materialised tracker row with `Layer` and no FK.
  - § 5.4: the engine's `Documents` input documented as the effective set (R8).
  - § 6: importers take streams from `FileBlob`, sheet routing per R5, `PicklistImporter` over
    `Pick_Lists` with 17 lists.
  - § 8: `folders/` → `explorer/`, new `workbook/`, `summary/` + `findings/` → `dashboard/` at `/`,
    `realtime/`, the new sidebar order, and the tile explorer described as built.
  - § 9: phases 2.1, 3.5, 4.2(d), 6.2 and 6.5 struck through with what replaced them, and a v3 entry
    added pointing at this log.
- **`CLAUDE.md`**: rule 9 reworded to the hosted workers + hub (decision D4), rule 4 extended with
  the read-only-Drive and no-disk rules, "Read first" now puts `docs/refactor-plan.md` before
  `PLAN.md` and adds this log, and the stack line names SignalR, the workers, `@microsoft/signalr`,
  `@tanstack/react-virtual` and the Neon test guard.
- **`docs/deploy.md`**: `App_Data/files` removed from the deploy steps (bytes live in `FileBlobs`);
  a new § 1.1 with the IIS settings the workers need (Start Mode `AlwaysRunning`, Idle Time-out `0`,
  Regular Time Interval `0`, Preload Enabled, WebSocket Protocol optional); `GoogleDrive__PollHours`
  and `GoogleDrive__StartupDelaySeconds` documented; the CORS section explains why the origin is
  named rather than wildcarded (the hub's credentialed negotiate); the "after deploying" walkthrough
  and the operational notes rewritten around automatic import and the workers.
- **`backend/src/Dip.Api/web.config`**: a comment pointing at § 1.1 (the file itself is unchanged).
- **`docs/excel-analysis.md`**: § 2.3 rewritten with the verified 17-list layout of `Pick_Lists`
  (row-4 groups, row-5 headers, row-6 data), the correction that the FIELD-label scan cannot find
  Drawing Type or Level, and every count and first value; finding 22 added for the MIDP-sheet vs
  Tracker-sheet population gap.
- **`.github/workflows/backend-publish.yml`**: the comment now says the connection must not point at
  Neon (the guard refuses it). No behavioural change — the CI service container is already local.

---

## Commits

| Phase | Commit | Subject |
|---|---|---|
| R0 | `2a8273c` | v3 refactor plan |
| R1 | `ff4fb2a` | Backend core: schema v3, no disk storage, read-only Drive, hosted workers, automatic import |
| R2 | `6e18868` | Effective document set, company conversion, materialised tracker, Live edits |
| R3 | `09bce07` | Lists backend: every picklist imported, CRUD with soft delete |
| R4 | `61b08dd` | Frontend: Drive-like explorer, SignalR progress, Dashboard at / |
| R5 | `ee86f9b` | Workbook viewer: the file opens as a spreadsheet |
| R6 | `b546911` | Lists page: every list editable, with soft delete and restore |
| R7 | `d907d15` | Docs, deploy and plan aligned with v3 |

---

## Final test summary

Backend (`cd backend && dotnet test`, `TEST_POSTGRES_CONNECTION` → local Postgres 16):
```
Passed! - Failed: 0, Passed:   3, Skipped: 0, Total:   3 - Dip.Domain.Tests.dll
Passed! - Failed: 0, Passed: 131, Skipped: 0, Total: 131 - Dip.Engine.Tests.dll
Passed! - Failed: 0, Passed:  44, Skipped: 0, Total:  44 - Dip.Infrastructure.Tests.dll
Passed! - Failed: 0, Passed:  69, Skipped: 0, Total:  69 - Dip.Api.IntegrationTests.dll
```
247 tests, 0 failed, 0 skipped. `dotnet build`: 0 warnings, 0 errors.
(Baseline before the run: 201 tests.)

Frontend (`cd frontend`):
```
Test Files  24 passed (24)
Tests       117 passed (117)
```
`npm run lint`: 0 errors, 0 warnings. `npm run typecheck`: clean. `npm run build`: succeeds.
(Baseline before the run: 64 tests.)

---

## Self-check against § 14

| # | Item | Result |
|---|---|---|
| 1 | § 0.4 and § 5.9 greps empty; `IDriveClient` has two members | **Pass.** Both greps over `backend/src` return nothing. `IDriveClient` = `ListChildrenAsync`, `DownloadAsync`. |
| 2 | Nothing under `backend/src` writes to the file system except Serilog | **Pass.** No `File.Create`/`File.WriteAll`/`Directory.CreateDirectory`/`StreamWriter` anywhere; Serilog's file sink is configured in `appsettings.json`. |
| 3 | Every new endpoint is a slice with a validator and a `[Permission]` | **Pass.** All 19 new/changed slices (`SetFolderCompany`, `TriggerDriveSync`, `GetDriveStatus`, `DownloadFile`, `GetFileWorkbook`, `UploadFile`, `ConvertToLive`, `UpdateDocument`, and the 11 Lists slices) have a controller, a validator and a permission attribute. Validators were added to the new *queries* too, which the plan only requires for commands. |
| 4 | `FolderFiles` filtered unique index; the R2 upsert covered both ways | **Pass.** `IX_FolderFiles_FolderId_NameLower_Active` is unique over `(FolderId, NameLower)` filtered on `NOT "IsDeleted"`, with `NameLower` a stored `lower("Name")` column. Covered by `FoldersFlowTests.Upload_ImportsAutomatically_AndTheSameNameReplacesTheRow` (upload over upload) and `DriveSyncServiceTests` (Drive over Drive, and Drive refusing to overwrite a newer upload). |
| 5 | Drive-sourced content never reaches Live; uploads follow the target | **Pass.** `FileImportServiceTests.DriveSourcedTidp_InALiveFolder_LandsInDraft` and `UploadSourcedTidp_InALiveFolder_LandsInLive`, plus the `LayerFor` theory over all seven combinations. |
| 6 | Dedupe and tie-break tested; snapshots carry `Layer`; Tracker reads snapshots only | **Pass.** `EffectiveDocumentLoaderTests` covers all four rules; `RecalculationTests` asserts `Layer` and the display columns on Draft rows; `ListTrackerDocumentsHandler` and `GetTrackerDocumentHandler` query `DocumentSnapshots` alone. |
| 7 | The sample tests still pass, and pass for a Draft-target import | **Pass, with a change of method.** `CorporateSummarySampleTests`, `BaselineSummarySampleTests`, `ControlFindingsSampleTests`, `EvmSampleTests` and `TrackerEngineSampleTests` are unchanged and green. The Draft half is `DraftLayerReportsTests`, which asserts Draft/Live parity rather than the sheet's literals — see R2 deviation 10 and `docs/excel-analysis.md` finding 22 for why. |
| 8 | `ConvertToLive` is transactional, cascades, writes a `PromoteBatch` per file, queues recalculation | **Pass.** One `SaveChanges` for the whole command; `ConvertToLiveTests` asserts the promote counts, the flipped target and the repeatability. |
| 9 | Picklist counts match § 7; deleted codes skipped on re-import; restore-on-create works | **Pass.** All 17 counts and first values asserted in `PicklistImporterTests`; `ReImportSkipsSoftDeletedCodes` and `PicklistCrudTests.ReImportOfThePicklistsWorkbook_SkipsDeletedCodes` cover the skip; `CreatingADeletedCode_RestoresTheSameRow` covers the restore. |
| 10 | No frontend references to deleted endpoints; hub reconnects with a fresh token; tiles and viewer tests pass; `/` is the Dashboard; Lists has 18 tabs | **Pass.** Grep for the removed routes returns nothing; `accessTokenFactory` is read per connect; `TileGrid`/`FileTile`/`ExplorerToolbar`/`Grid`/`ListsPage` tests green; `/` renders `DashboardPage`; `picklistFields` has 17 entries and the page adds Status mapping. |
| 11 | Viewer: 34 columns in PLAN order, column A read-only and recomposed, leading zeros preserved | **Pass.** `columns.test.ts` pins the 34 keys, the letters A/V/W/Z/AA/AC/AH and the L..U block; `GetFileWorkbookTests` asserts the same from the API and that column A equals the concatenation of L..U; `UpdateDocumentTests` proves `0004 → 0005` recomposes the number with the zeros intact. |
| 12 | The log lists commits, deviations, test summaries, removed files and the Neon reset timestamp | **Pass.** This file. |
| 13 | Neon holds only seed rows plus what the first Drive poll imported; the test guard is in place | **Pass.** After the reset: Disciplines 10, StatusMappings 22, Users 1, PicklistItems 0, Folders 0, FolderFiles 0, Documents 0, DocumentDrafts 0 — seed only; no Drive poll has run yet because `GoogleDrive__RootFolderId` is not set in this environment. `TestConnectionGuard` refuses any `neon.tech` connection string and has its own unit test. |
| 14 | `PLAN.md`, `CLAUDE.md`, `docs/deploy.md` updated; no secrets in the diff | **Pass.** All three rewritten above. The only `neon.tech` strings in the diff are the guard and its test; the only passwords are `postgres/postgres` for the local test database. The Neon connection string was read from user-secrets into shell variables and never printed, written or committed. |

---

## Known gaps

1. **No Drive poll has run against the reset Neon database.** `GoogleDrive__RootFolderId` is not set
   in this environment, so the worker logs that polling is disabled and stops. Set the root folder id
   and API key in Plesk and the first poll (30 s after startup) will import `00.Picklists`,
   the TIDPs and the Tracker workbook on its own.
2. **The Corporate Summary from an imported MIDP is 15,724 rows, not the sheet's 15,883.** The gap is
   in the sample data, not the layer logic (finding 22), and predates this refactor. Worth putting to
   the owner: the 159 rows are ones `MidpImporter` skips or collapses.
3. **`ImportBatch.Kind` for a Tracker workbook is `AconexHistory`** even though the same batch also
   imports its `Baseline` and `Lists` sheets. The counts are aggregated across the three. Splitting
   it into three batches would report each sheet separately.
4. **The workbook viewer edits one row at a time.** Paste into a range is not implemented; `Ctrl+C`
   copies, `Ctrl+V` does not paste.
5. **`RecalculationService.RunStepAsync` rebuilds the effective set on every step.** The worker's
   `RunAllAsync` loads it once, so the cost only lands on the admin fallback endpoint.
6. **`DriveSyncWorker` polls every project in the database.** Single-project today; a real
   multi-tenant deployment would want per-project scheduling.

---

## Owner review (2026-09-08, after R7) — findings and fixes

Reviewed by the planning session against `docs/refactor-plan.md` § 3, § 5, § 6, § 7 and § 14, with
two independent read-only reviews (backend R1–R3, frontend R4–R7) and a live run of the pipeline
against Neon through a local mirror of the Drive tree.

### Why the screens were empty
1. **No Drive configuration on this machine.** `GoogleDrive__ApiKey` / `__RootFolderId` were never set
   in user-secrets or the environment, so the poller logged "polling is disabled" and Neon held seed
   rows only. Production needs both values in Plesk; locally the new `GoogleDrive:LocalMirrorPath`
   (Development only) serves the checked-out `Qpac_1/` folder through the same read-only
   `IDriveClient` contract (`Dip.Infrastructure/Drive/LocalMirrorDriveClient.cs`).
2. **Drive folders defaulted to the Live layer** (`DriveSyncService.UpsertRootAsync`) while Drive
   content imports into Draft (R3), so the effective set — and with it Tracker, Summary, Control
   Findings and the workbook viewer — was empty even after a successful poll. Drive-created folders
   now start in **Draft** (D3: Drive is the Draft layer's source); `DriveSyncServiceTests.DriveRoot_IsTargetedAtDraft`.
3. **Picklists were not seeded** (§ 9.2 left them to the first poll). `SeedPicklists.cs` now carries
   all 17 lists (393 rows, generated from `samples/PickLists.xlsx`) and `IdentitySeeder` inserts the
   ones missing by `(Field, Code)`; a later workbook import upserts the same keys.

### Product corrections (owner's clarification of D8)
- The **Summary page is restored** at `/summary` (Overview · Corporate · Baseline · EVM) and
  **Control Findings** at `/findings`; both are back in the sidebar.
- A **new Dashboard** at `/` shows the Engineering Tracker aggregations as an overview: key figures
  (documents, submitted, approved with quality, under review, completed vs planned, SPI), delivery
  S-curve, progress by discipline and by company (`features/reports/ProgressChart.tsx`, with a table
  view), baseline package status, control-findings counts, SPI by discipline, and the Drive/import
  pipeline state (last sync, next poll, queued imports, companies by layer, recent imports).
  Shared report hooks and charts moved to `features/reports/`.

### Backend fixes from the review
- `DriveSyncWorker`/`WorkerState`: atomic `TryStartSync`, a real run-again flag for "Sync now"
  during a run (the timer loop and the trigger loop could previously start two concurrent walks),
  and `TriggerDriveSync` reports `queued = false` when polling is disabled.
- `DriveSyncService`: a folder that fails no longer poisons the rest of the walk (its pending
  entities are detached); file row and blob commit in one `SaveChanges` (a row with an md5 but no
  blob could never be re-downloaded nor imported).
- `FileImportService`: a file without stored content is marked `Failed` with a message instead of
  being silently skipped and re-queued forever.
- Control Findings report 1 ("delivered but unplanned") decides MIDP membership against the
  **effective set** (`Features/Reports/ReportData.cs: UnplannedRevisions`) instead of the
  `AconexRevision.InMidp` flag, which was computed at import time against Live only and reported
  every revision of a Draft-layer company as unplanned.
- `ImportWorker` enqueues a recalculation for every project at startup, so snapshots catch up with
  targets flipped while the process was down.
- Test fixtures **throw** without `TEST_POSTGRES_CONNECTION` (§ 11.3) instead of passing vacuously.
- `appsettings.json` no longer ships a default admin password.
- New tests: `NewerDriveContentWithADifferentMd5_ReplacesTheStoredCopy` (R2 positive path),
  `DriveRoot_IsTargetedAtDraft`.

### Frontend fixes from the review
- Hub events invalidate the query cache **app-wide** (`SyncHubProvider`), not only on the TIDPs page;
  the initial hub connection is retried with back-off when the API is cold.
- Lists is readable with `reports.view` (mutations still need `lists.manage`).
- Convert-to-Live previews the **whole subtree**'s files (`useSubtreeFiles`, `subtreeFolderIds`),
  matching what the backend converts.
- Context-menu "New folder" creates inside the right-clicked folder.
- A failed cell save in the workbook grid no longer surfaces as an unhandled rejection.
- Dead code removed (`downloadUrl`, unused badges, `isEditable`, `ImportKind`, duplicate
  `usePromoteDiff`); toast timers cleared on unmount; stale "no background workers" comment fixed.
- New tests: `syncHub.test.ts`, `tree.test.ts`, `DashboardPage.test.tsx` (new page),
  `SummaryPage.test.tsx`; navigation/sidebar tests updated for the restored pages.

### Left as recorded (minor, not changed)
- `ConvertToLive` with the same document number in two TIDPs of one company fails as a whole with
  500 instead of a 409 (atomicity is right; the error surface is not).
- `GetFolderHandler` issues several queries per subfolder for `hasNewerDraft`; noticeable on Neon
  latency for wide folders.
- `WorkbookDto` has no `lastImportedAt`, so the viewer's status bar cannot show it.
- Tracker workbook is parsed once per routed sheet (four times); CPU only.
- `ImportBatch.Kind` is `AconexHistory` for the Tracker workbook although the batch also imports
  Baseline and Lists.
- Corporate Summary from an imported MIDP is 15,724 rows against the sheet's 15,883 (excel-analysis
  finding 22); pre-existing and worth the owner's look.

## R9 — Local development on SQLite (2026-09-11)

Asked for: a local run — API and frontend — that works off a local database instead of Neon.

- `Database:Provider` (`Postgres` | `Sqlite`, inferred from the connection string when unset)
  chooses the provider in `AddInfrastructure`. Every deployed environment stays on Npgsql.
- `SqliteModelTweaks` normalises the Postgres-only parts of the model in one pass when the
  context runs on SQLite: `timestamp without time zone` / `jsonb` / `bytea` cleared,
  `decimal` mapped to `double` (SQLite stores decimals as text, which sorts wrongly). EF caches
  a model per provider, so the Postgres model and its SQL are untouched.
- `ISqlDialect` is the one place the engine leaks into query code: the four search handlers
  (Tracker, Drafts, Users, Baseline) pick `ILIKE` or `LIKE` from it. SQLite has no `ILIKE`, and
  its `LIKE` is already case-insensitive for ASCII.
- SQLite has no migrations of its own — the `Init` migration is Npgsql SQL — so its schema comes
  from `EnsureCreated`, followed by `PRAGMA journal_mode=WAL` because the sync and import workers
  write while the API serves requests. `scripts/reset-local-db.sh` drops the file after a model
  change; `scripts/run-local.sh` runs the API.
- `LocalDevDefaults` makes `dotnet run` work with nothing exported: the SQLite file, a generated
  `Jwt:Key` and SuperAdmin password in git-ignored `App_Data/dev-secrets.json` (hard rule 5 —
  nothing secret is committed), and `Qpac_1/` standing in for Drive. It deliberately **overrides**
  a Neon connection string left in user-secrets, with a warning, so a local run cannot write to
  the shared database; `Database__Provider=Postgres` opts back in. `EF.IsDesignTime` is excluded,
  so `dotnet ef` still authors Npgsql migrations (verified: `dbcontext info` reports Npgsql).
- `DriveHealthCheck` counts a configured local mirror as configured instead of reporting Degraded
  for the missing API key.
- The integration test hosts pin `Database:Provider=Postgres`, so the Development-only defaults
  can never redirect a test onto SQLite.
- Frontend: the default API origin was `5080` while `launchSettings` serves `5001`; both now say
  `5001`, so `npm run dev` next to `dotnet run` needs no `.env`.

Verified end to end on SQLite: schema created, `Qpac_1/` mirrored, all 40 workbooks imported
(26,609 Aconex revisions, 1,363 baseline activities, 393 picklist rows, 32,154 draft documents),
15,965 snapshots recalculated, and the four search endpoints returning rows. `dotnet build` clean,
`dotnet test` 249/249 against a local Postgres, frontend 150/150.

New docs: `docs/local-dev.md`.

## R10 — Production moves from Neon Postgres to SQL Server (2026-09-12)

Asked for: replace Neon Postgres with SQL Server everywhere it is used, after the Neon free
tier's usable compute/storage ran out. No production data existed to carry over — see
`docs/decisions/004-sql-server.md` for the full decision and what it changed.

- `Npgsql.EntityFrameworkCore.PostgreSQL` → `Microsoft.EntityFrameworkCore.SqlServer`;
  `DatabaseProvider` is now `SqlServer | Sqlite`. `ISqlDialect`/`SqlDialect` removed —
  neither remaining provider has `ILIKE`, so the three search handlers call
  `EF.Functions.Like` unconditionally now.
- Postgres-only column types recast to their SQL Server equivalents across every
  `IEntityTypeConfiguration`: `timestamp without time zone` → `datetime2`,
  `jsonb`/`text` → `nvarchar(max)`. `SqliteModelTweaks` normalises the new types away
  for local SQLite runs exactly as it did the old ones.
- The four Npgsql-authored migrations are replaced by one fresh `Init` migration.
  `AconexLineHashSql` (the raw-Postgres-SQL hash backfill used only by the deleted
  `S3_LineHashAndAppend` migration) and the test pinning it are removed — nothing
  replaces either, since a migration with no data to backfill needs no backfill SQL.
- Applying the fresh migration to a real SQL Server surfaced two behavioral gaps
  invisible from the code alone (both detailed in the ADR): SQL Server refuses a
  foreign key that creates a second cascade path to a table already reachable by
  another cascade, so `Document.ProjectId`, `ImportBatch.ProjectId`, and the two
  shadow FKs EF infers on `DocumentSnapshot` (from `ProjectId`/`TidpFileId`
  properties with no navigation, still enough to trigger EF's by-name FK
  convention) are now `Restrict`; and SQL Server's default collation is
  case-insensitive where Postgres's is case-sensitive, which collided
  `SeedData`'s two deliberately-different-case `StatusMapping` rows — fixed with an
  explicit case-sensitive collation on `AconexStatus`, cleared for SQLite (whose own
  default collation is already case-sensitive).
- Test infrastructure: `PostgresFixture` → `SqlServerFixture`, isolating each run in
  its own throwaway database (`CREATE`/`DROP DATABASE`) rather than a schema — a
  `Search Path=` connection-string trick has no SQL Server equivalent.
  `TEST_POSTGRES_CONNECTION` → `TEST_SQLSERVER_CONNECTION`; `TestConnectionGuard`
  refuses `databaseasp.net` (the ASPMonster instance) alongside `neon.tech`.
- `docs/local-dev.md`, `docs/deploy.md` and `CLAUDE.md` updated for SQL Server.

Verified against a real SQL Server 2022 container (`mcr.microsoft.com/mssql/server`, not
Neon or ASPMonster): migration applies cleanly to an empty database, all 22 tables created.
`dotnet build` clean (0 warnings), `dotnet test` 243/243 (3 Domain + 135 Engine + 66
Infrastructure + 39 Api.IntegrationTests), the Infrastructure and Api.IntegrationTests suites
running against that container rather than skipping.

New docs: `docs/decisions/004-sql-server.md`.

## R11 — The TIDP folder is uploaded whole and synced incrementally (2026-09-12)

Until now a TIDP arrived one workbook at a time and the database held nothing that
could identify it again: `TidpFile` had a `FileName` and no path, no timestamp, no
hash. Re-uploading the folder meant deciding by hand which of 36 files had changed.

The folder itself is the specification, and `src/Dip.Api/02.TIDPs` is the reference
copy of it. Three things it taught us, none of which were in PLAN.md:

- **The workbook is not a reliable source for discipline or numbering.**
  `06.Subcontractor - Unassigned/…-KNL-…xlsx` carries `DISCIPLINE = Architectural` in
  its header, and most files carry the placeholder `…-TDP-XXX-00-000000-000001` in
  DOCUMENT REFERENCE. The folder and the file name are, so they are what is parsed
  and stored. The importer still reads the header for the corporate `Discipline` —
  that is unchanged.
- **Folder discipline codes are a different namespace from `Discipline.Code`.** The
  folders use two letters (`AR`, `EL`, `FY`, `IN`), the corporate list three (`ARC`,
  `ELE`, `FLS`, `INF`). They are separate tables, not one reused.
- **The file name is not unique.**
  `QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx` appears three times in the
  sample, under `01.NAP/AR-Architectural`, `08. Provisional Sum` and
  `11. NAP PMO/AR-Architectural`. The key is the path relative to the root.

### What was added

- `Dip.Application/Documents/TidpPathParser.cs` — every folder and file-name rule as
  pure string logic, no I/O, keyword lists injected as `TidpFolderRules` so
  `appsettings.json` can extend them. 61 unit tests cover every shape in the sample.
- `TidpFolderOwner`, `TidpFolderDiscipline`, `TidpFolderSync` entities, and
  `TidpFile` extended with `RelativePath`, `ContentHash`, `LastModifiedUtc`,
  `SizeBytes`, `LastSeenAt`, `FolderStatus`, `MissingSince`, the owner and discipline
  folder keys, and the eight fields of its own name. Migration `TidpFolderSync`.
- `POST /api/projects/{id}/tidp-folder/sync` — the whole folder as one multipart
  request described by a `manifest` field. `GET …/tidp-folder/syncs/{syncId}` for the
  outcome, `GET …/tidp-folder` for the tree.

### Decisions worth recording

- **Multipart, not a zip.** A browser's `webkitdirectory` already yields
  `webkitRelativePath` and `lastModified`; a zip would need a packing library on the
  client (rule 10) and its DOS timestamps round to two seconds, losing the precision
  PLAN.md § 1 requires. Parts are named by a manifest rather than by position,
  because position silently attaches one file's bytes to another file's path.
- **No new worker kind.** The request does only hashing, comparing and upserting;
  each changed file becomes an `ImportBatch` on the existing `WorkQueue` and is parsed
  by `ImportService` exactly as a single-file upload is. So § 9 holds — no workbook is
  opened on the request thread — and `ImportWorker`, `ImportService` and `TidpImporter`
  are untouched.
- **`FolderStatus`, not `Status`.** `TidpFile.Status` already means how the *import*
  went (`Importing | Imported | Failed`). Presence in the folder is a second,
  independent fact — a file can be `Imported` and `Missing` at once — so it is its own
  column. The migration backfills existing rows to `Present`, not to EF's default
  empty string, which would not read back as an enum value at all.
- **A failed import is retried even when the file has not changed.** Otherwise a
  workbook that broke once could never be re-imported without someone editing it.
- **Nothing is deleted.** A path that stops appearing is marked `Missing` with a
  timestamp; if its name and hash turn up elsewhere, both halves carry a move
  suggestion. Acting on it is a person's decision.
- **First-file-wins is unchanged.** The three copies of the same document numbers
  still resolve to whichever file the importer reaches first, and the other two import
  zero rows with a counted warning naming the owner. The sync reports `rowsImported`
  per file so that is visible rather than silent; changing the ownership rule is a
  much larger change than this one and was deliberately not made.

Verified against the checked-in folder: the first sync adds all 36 workbooks and none
fails to parse; the second adds 0, updates 0, skips 36 and queues no batch at all.
