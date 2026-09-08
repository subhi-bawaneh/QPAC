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
