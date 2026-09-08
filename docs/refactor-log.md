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
