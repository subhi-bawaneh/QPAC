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
