# DIP v3 — Complete implementation plan

> **Audience:** the implementing agent (Opus 5). Read `CLAUDE.md`, then `PLAN.md`, then this file end to end before touching code.
> **Status:** approved by the product owner on 2026-09-08. Execute all phases R1 → R7 in order.
> **Precedence:** where this file and `PLAN.md` disagree, **this file wins** (the superseded sections are listed in § 13). Where this file and the Excel samples disagree, **the Excel wins** — log it in `docs/refactor-log.md` and `docs/excel-analysis.md`, never silently change a rule.

---

## 0. Execution protocol (read this twice)

1. **Autonomous run.** The owner has pre-approved the file lists in § 12 as the "list files and wait" step of CLAUDE.md rule 1. Do **not** stop to ask for approval between phases. If you must touch a file that is not listed for the phase, do it and record it under "Deviations" in `docs/refactor-log.md`.
2. **Green before every commit.** `cd backend && dotnet build` with **zero warnings**, `dotnet test` green with `TEST_POSTGRES_CONNECTION` exported (see § 11.3 — tests that silently skip do not count as green). `cd frontend && npm run lint && npm run typecheck && npm test` green. Commit messages start with the phase id: `[R1] …`, `[R1.2] …` for intermediate green commits within a phase.
3. **No secrets in the repo, ever.** Connection strings, API keys, JWT keys come from env vars / `dotnet user-secrets`. `appsettings.json` keeps empty values. Do not print the Neon connection string into any file or log.
4. **No disk storage.** After R1 there must be no code path that writes a workbook to the file system. `grep -rn "App_Data/files\|ILocalFileStorage\|StoragePath" backend/src` must return nothing.
5. **Google Drive is read-only.** `IDriveClient` keeps exactly two members: `ListChildrenAsync` and `DownloadAsync`. Never add a write.
6. **Engine stays pure.** `Dip.Application/Engine` gets no DbContext, no I/O, no new dependencies.
7. **Feature-slice shape is mandatory** for every new endpoint: `Dip.Api/Features/<Area>/<Feature>/{Command|Query}.cs, Validator.cs (commands), Handler.cs, Controller.cs`. Controllers only call `IDispatcher`. Permission goes on the command/query via `[Permission(Permissions.X)]`.
8. **Delete, don't deprecate.** Removed features are deleted with their tests, DTOs and frontend hooks. No `[Obsolete]`, no commented-out code, no TODOs.
9. **Expected values come from `samples/`** (cached cell values), read by the tests where possible, not hard-coded.
10. **Keep a log.** Create `docs/refactor-log.md` at the start of R1 and append, per phase: commits, what was removed, deviations from this plan and why, test run output summary (counts), known gaps. This log is the input to the owner's review.
11. **New packages** — only the three named in § 9.4. State the reason in the commit message.
12. **Neon reset (R1 only).** The owner authorised one destructive reset of the Neon database at the start of R1, following § 11.1 exactly. Never run the reset script at any other time or from application code.

---

## 1. Background: what the code does today and why it changes

| Today | Problem | v3 |
|---|---|---|
| `IDriveClient` is already read-only (list + download). The "write" features (CreateFolder, RenameFolder, DeleteFolder, UploadFile, DeleteFile) write to Postgres and to the server disk, never to Drive. | Folder rename/delete diverge from Drive; disk storage is forbidden by the owner. | Rename, DeleteFolder, DeleteFile removed. Bytes go to Neon (`FileBlob`). |
| Sync is a chunked request loop (`SyncFromDrive`, one folder per call); import is a separate chunked loop (`StartImport` / `RunImportStep`). Nothing is imported unless a user clicks Import. | Owner wants uploads (Drive or UI) to land in the database automatically; hosting allows a hosted worker and SignalR (owner has tested this). | `DriveSyncWorker` polls every N hours and on demand; `ImportWorker` imports every new/changed file; `SyncHub` pushes progress. |
| Reports read only the Live `Documents` table. The Live table in Neon is empty (the only rows are integration-test leftovers), so Tracker/Summary/Findings are blank. | Owner's model: some companies work in Drive (Draft layer, "DB1"), others in the system (Live layer, "DB2"); reports must show every company from whichever layer it is targeted at. | `EffectiveDocumentLoader` (§ 5.7) + `DocumentSnapshot.Layer`; Neon reset. |
| `PicklistImporter` reads 9 of the 19 lists in the Picklists workbook; the Lists page is read-only. | Owner wants every list, with add/edit/soft-delete. | § 7. |
| `PromoteBatch.SnapshotJson` + `RollbackPromote`, `ImportStagingRow`, manual Import dialog, Dashboard landing page. | Over-engineering per owner. | Removed. |

---

## 2. Locked decisions

| # | Decision |
|---|---|
| D1 | Google Drive is a **read-only source**. |
| D2 | **No file storage on the server.** Workbook bytes live in Neon in `FileBlob`; parsing uses `MemoryStream`. |
| D3 | Two logical layers in **one Neon database**: **DB1 = Draft** ("Google Drive database"), **DB2 = Live** ("system database"). No second physical database or schema. |
| D4 | A hosted `BackgroundService` polls Drive every `GoogleDrive:PollHours` (default **5**) and on demand; **SignalR** pushes progress. CLAUDE.md rule 9 is amended in R7 to: "long work runs in the hosted workers with hub progress; recalculation is chunked inside the worker". |
| D5 | **Import is automatic** for every new or changed workbook, from Drive or from a UI upload. |
| D6 | Folder **rename and delete are removed**. `CreateFolder` stays only for non-Drive (manual) folders. |
| D7 | Removed: Rollback + `SnapshotJson`, `ImportStagingRow`, `StartImport`/`RunImportStep`/`ImportDispatcher` step loop, `SyncFromDrive` step loop, `DeleteFile`, `DeleteFolder`, `RenameFolder`, Dashboard landing page, manual Import dialog, chunked Sync dialog. |
| D8 | The **Summary page becomes the Dashboard** at `/` with tabs Overview · Corporate · Baseline · Control Findings · EVM. The `/findings` route and nav item are folded into the Dashboard. |
| D9 | The Lists page manages **every list in the Picklists workbook** (§ 7) plus Status Mapping, with add, edit, **soft delete** and restore. |
| D10 | Tests never run against Neon: the fixtures throw if the host contains `neon.tech`. |
| D11 | `DocumentSnapshot` becomes the **materialised tracker row** (carries the document display columns), so Tracker paging/filtering is SQL over one table for both layers. |

---

## 3. Business rules

**R1 — File identity.** A file is identified inside a folder by `(FolderId, Name)` case-insensitively. Exactly one non-deleted `FolderFile` per pair (unique filtered index).

**R2 — Same name, two sources: newest wins.** Drive sync and UI upload upsert the *same* row.
- UI upload: always replaces the blob, sets `ContentSource = Upload`, `ContentModifiedAt = now (UTC)`, `ContentMd5`, `SizeBytes`, `State = NotImported`, enqueues import.
- Drive poll: replaces the blob only if `entry.ModifiedTime > file.ContentModifiedAt` **and** `entry.Md5Checksum != file.ContentMd5`. Then `ContentSource = Drive`, `ContentModifiedAt = entry.ModifiedTime`, `State = NotImported`, enqueue.
- A Drive file that disappears from Drive is soft-deleted (`IsDeleted = true`) only if `ContentSource == Drive`; uploaded content is never deleted by the poller.
- No version history.

**R3 — Drive writes to Draft only.** Content whose `ContentSource == Drive` is imported into the **Draft** layer regardless of the folder target. Drive never touches Live. After a company is converted to Live, Drive keeps refreshing its Draft rows; the file shows *"Drive has newer data"* (`hasNewerDraft`, § 5.2) and *Convert* can be run again.

**R4 — UI uploads write to the folder's target.** `ContentSource == Upload` → import into `folder.Target` (Draft or Live). Live import = upsert by DocumentNumber with `AuditLog` per changed field (existing `TidpImporter.ImportLiveAsync` behaviour).

**R5 — Sheet routing (which sheets of which workbook are imported).** Sheet names verified against the Drive files on 2026-09-08:

| `FileKind` | Detected by | Sheets imported | Layer |
|---|---|---|---|
| `Tidp` | `-TDP-` / `TIDP` in name | `TIDP_Sheet` → `TidpImporter` | R3/R4 |
| `Midp` | `-MDP-` / `MIDP` | `MIDP` → `MidpImporter`; `Aconex History` (if present) → `AconexHistoryImporter` | docs R3/R4; Aconex always Live |
| `AconexHistory` | `TRACKER` / `ACONEX` (the Engineering Tracker workbook) | `SHD_History` → `AconexHistoryImporter`; `Baseline` → `BaselineImporter(replace: true)`; `Lists` → `ListsImporter` | Live |
| `Baseline` | `BASELINE` (sheet `ENG_BL`) | `BaselineImporter(replace: true)` | Live |
| `Picklists` | `PICKLIST` (sheet `Pick_Lists`) | `PicklistImporter` (§ 7) | Live |
| `Lists` | `LIST` | `ListsImporter` | Live |
| `Unknown` | — | nothing; `State = Failed`, `ImportError = "Unrecognised workbook name"` | — |

The `Baseline` and `Picklists` sheets inside TIDP workbooks are **not** imported (they are copies). Verify that `AconexHistoryImporter` accepts both sheet names `Aconex History` and `SHD_History`; if it does not, make it try both.

**R6 — Target lives on the folder and cascades.** `SetFolderTarget` updates the folder and every non-deleted descendant. `CreateFolder` inherits the parent's target. Every folder has a target; a **company** is a folder with `IsCompany = true` and optional `AuthorId` (a `PicklistItem` with `Field = Author`). Not every folder is a company.

**R7 — Conversion = promote + flip.** `ConvertToLive(folderId)` (§ 5.5): for every non-deleted file in the folder subtree that has a `TidpDraft`, run the promote with `deleteMissing: true`; write one `PromoteBatch` per file; then set the folder target to Live (cascading). Repeatable. No rollback; `AuditLog` is the undo trail.

**R8 — Effective document set** (what every report and the Tracker read). For each non-deleted `FolderFile`: if its folder's target is **Live**, take its `Document` rows; if **Draft**, take its `DocumentDraft` rows (excluding `IsDuplicate`). `Document` rows with `FolderFileId == null` count as Live. Union. Deduplicate by upper-cased `DocumentNumber`: the row whose file has the **latest `ContentModifiedAt`** wins; on a tie Live wins. Snapshots are keyed by the winning row's id and carry `Layer`.

**R9 — Soft delete of list items.** `PicklistItem.IsDeleted/DeletedAt`, `StatusMapping.IsDeleted/DeletedAt`. Deleted rows are excluded from every read unless `includeDeleted=true`. Re-importing the Picklists workbook **skips** codes that are soft-deleted (counted in `RowsSkipped`, listed in warnings) and never resurrects them. Creating an item whose `(Field, Code)` exists soft-deleted restores that row instead of inserting. Editing a code does not rewrite documents.

**R10 — Permissions (existing constants, no new ones).** Sync now: `drive.sync`. Upload / create folder: `folders.manage`. Target, author, convert: `folders.assignTarget` (convert additionally requires `drafts.promote`). Cell edit in Draft layer: `drafts.edit` (+ discipline scope via `DraftScope`). Cell edit in Live layer: `documents.editLive`. Lists CRUD: `lists.manage`. Reports: `reports.view`.

---

## 4. Target architecture

```
Google Drive (read-only, API key)                          Browser (React)
        │ files.list / files.get?alt=media                      │ POST upload      ▲ SignalR /hubs/sync
        ▼                                                       ▼                  │
┌──────────────────────────────────────── Dip.Api ─────────────────────────────────────────────┐
│ Workers/DriveSyncWorker  (BackgroundService: PeriodicTimer(PollHours) + on-demand trigger)    │
│   └─ DriveSyncService: BFS from RootFolderId → upsert Folder/FolderFile → download changed     │
│        → FileBlob → WorkQueue.Enqueue(ImportFile)                                              │
│ Workers/ImportWorker     (BackgroundService: consumes WorkQueue)                               │
│   └─ FileImportService: FileBlob → MemoryStream → sheet routing (R5) → importers               │
│        → layer by R3/R4 → RecalculationService (chunked) → SyncHub events                      │
│   └─ Recalculate job: EffectiveDocumentLoader → TrackerEngine → DocumentSnapshot(Layer)       │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
        │                                             │
        ▼                                             ▼
   DB1 = Draft tables                            DB2 = Live tables                (one Neon DB)
   TidpDraft / DocumentDraft / DataExchangeDraft Tidp / Document / DataExchange
                                                 AconexRevision / BaselineActivity (always Live)
        └──────────── ConvertToLive (promote per file + flip target) ────────────►
                              │
                              ▼
   Effective set (R8) → DocumentSnapshot(Layer) → Tracker · Dashboard · Control Findings · Export
```

---

## 5. Backend specification

### 5.1 Entities (delta against `PLAN.md § 3`)

```csharp
// Dip.Domain/Entities/Folders.cs
public class Folder : Entity {
    Guid ProjectId; Guid? ParentId; string Name; string Path; string? DriveFolderId;
    DataTarget Target = Live; int SortOrder; DateTime? LastSyncedAt; bool IsDeleted;
    bool IsCompany;                 // NEW
    Guid? AuthorId;                 // NEW — FK PicklistItem (Field = Author), nullable
    /* navs unchanged */ }

public class FolderFile : Entity {
    Guid FolderId; string Name; FileKind Kind;
    string? DriveFileId; DateTime? DriveModifiedAt;        // Drive identity (unchanged)
    FileSource ContentSource;                              // RENAMED from Source
    DateTime ContentModifiedAt;                            // NEW (R2)
    string ContentMd5 = "";                                // RENAMED from Md5, non-null
    long SizeBytes;
    ImportState State; string? ImportError;                // ImportError NEW
    Guid? LastImportBatchId; DateTime? LastImportedAt;     // LastImportedAt NEW
    bool IsDeleted;                                        // NEW
    // REMOVED: StoragePath
}

public class FileBlob { Guid FolderFileId /*PK, FK cascade*/; byte[] Content; }   // NEW, own table "FileBlobs"

// Dip.Domain/Entities/Picklists.cs
public class PicklistItem  : Entity { …existing…; bool IsDeleted; DateTime? DeletedAt; }   // NEW fields
public class StatusMapping : Entity { …existing…; bool IsDeleted; DateTime? DeletedAt; }   // NEW fields

// Dip.Domain/Enums/Enums.cs
public enum PicklistField { …existing 0–13…, Classification = 14, CorporateDiscipline = 15, Author = 16 }
public enum WorkItemKind { ImportFile = 0, Recalculate = 1 }                                // NEW (in-memory only)

// Dip.Domain/Entities/Snapshots.cs — materialised tracker row (D11)
public class DocumentSnapshot {
    Guid DocumentId;                // PK — Document.Id (Live) or DocumentDraft.Id (Draft); NO FK
    Guid ProjectId; DataTarget Layer; Guid? FolderFileId; DateTime ComputedAt;
    // display columns copied from the source row (so Tracker never joins the source tables)
    string DocumentNumber; string Title; string Type /*F04*/; string Discipline /*CorporateDiscipline*/;
    string Building /*F07*/; string Level /*F08B*/; string Trade /*F05*/; string? Author /*Exchange 1*/;
    DateTime? DeliveryMilestone; string? ActivityId; string? PackageName;
    // computed columns — unchanged
    int? SubmissionsCount; string? Revision; string? AconexStatus; UnifiedStatus? Status;
    DateTime? SubmissionDate; DateTime? DateModified; string? Transmittal;
    DateTime? PlannedStart; DateTime? PlannedFinish; DateTime? ActualStart; DateTime? ActualFinish;
    // REMOVED: Document navigation (no FK any more)
}

// Dip.Domain/Entities/Drafts.cs
public class PromoteBatch : Entity { Guid ProjectId; Guid FolderId; Guid FolderFileId; DateTime At; string By;
    int Added, Updated, Deleted, Skipped; }               // REMOVED: SnapshotJson, RolledBack

// Dip.Domain/Entities/Imports.cs — ImportStagingRow DELETED. ImportBatch unchanged.
```

EF configuration notes: `FileBlobs.Content` is `bytea`; `FolderFiles` unique index on `(FolderId, lower(Name)) WHERE NOT "IsDeleted"` (use `HasFilter`); `PicklistItems` unique index `(ProjectId, Field, Code) WHERE NOT "IsDeleted"`; `DocumentSnapshots` indexes on `(ProjectId, Layer)`, `(ProjectId, DocumentNumber)`, `(ProjectId, Discipline)`, `(ProjectId, Status)`; `Folders.AuthorId` FK to `PicklistItems` with `OnDelete(Restrict)`.

### 5.2 Folders area — endpoints after v3

| Method & route | Slice | Permission | Notes |
|---|---|---|---|
| `GET /api/projects/{projectId}/folders/tree` | `GetTree` (keep) | reports.view | `FolderTreeNode` gains `isCompany`, `authorName`, `fileCount`, `hasNewerDraft` (any file below). |
| `GET /api/folders/{id}` | `GetFolder` (change) | reports.view | `FolderDetail` per DTOs below. |
| `POST /api/folders` | `CreateFolder` (keep, restrict) | folders.manage | 400 if parent has a `DriveFolderId` ("Drive folders are managed in Google Drive"). Inherits target. |
| `PUT /api/folders/{id}/target` | `SetFolderTarget` (change) | folders.assignTarget | Cascades to descendants (R6). Returns count of folders updated. |
| `PUT /api/folders/{id}/company` | `SetFolderCompany` (new) | folders.assignTarget | Body `{ isCompany: bool, authorId: guid? }`. 400 if `authorId` is not a non-deleted `Author` picklist item. |
| `POST /api/folders/{id}/convert-to-live` | `ConvertToLive` (new, in `Features/Drafts`) | folders.assignTarget **and** drafts.promote | § 5.5. |
| `POST /api/folders/{folderId}/files` | `UploadFile` (change) | folders.manage | multipart `file`; `.xlsx/.xlsm/.xls`; ≤ 64 MB; 400 on `FileKind.Unknown`; writes `FileBlob`, upserts by name (R2), enqueues `ImportFile`; returns **202** `{ fileId, name, replaced }`. |
| `GET /api/folder-files/{id}/download` | `DownloadFile` (new) | reports.view | Streams `FileBlob.Content` with the original name and xlsx content type. |
| `GET /api/folder-files/{id}/workbook?sheet=TIDP&page=1&pageSize=500` | `GetFileWorkbook` (new) | reports.view | § 5.6. |
| `POST /api/projects/{projectId}/drive/sync` | `TriggerDriveSync` (change) | drive.sync | Enqueues a sync run; returns **202** `{ queued: true, alreadyRunning: bool }`. |
| `GET /api/projects/{projectId}/drive/status` | `GetDriveStatus` (new) | reports.view | `{ isRunning, lastRunStartedAt, lastRunFinishedAt, lastRunError, nextRunAt, queuedImports }` from `WorkerState` (§ 5.4). |

Removed slices and their routes: `RenameFolder` (`PUT /api/folders/{id}/name`), `DeleteFolder` (`DELETE /api/folders/{id}`), `DeleteFile` (`DELETE /api/folder-files/{id}`), `SyncFromDrive` (old chunked handler; the route is reused by `TriggerDriveSync`), `Imports/StartImport` (`POST /api/imports/start`), `Imports/RunImportStep` (`POST /api/imports/{batchId}/step`), `Imports/ImportDispatcher.cs`, `Drafts/RollbackPromote` (`POST /api/drafts/promote/{id}/rollback`), `Drafts/PromoteSnapshot.cs`. Keep `Imports/GetImportStatus` and `Imports/ListImportBatches`.

DTOs (`Features/Folders/FolderDtos.cs`):

```csharp
public sealed record FolderNode(Guid Id, Guid? ParentId, string Name, string Path, DataTarget Target,
    string? DriveFolderId, DateTime? LastSyncedAt, bool IsCompany, Guid? AuthorId, string? AuthorName,
    int ChildCount, int FileCount, bool HasNewerDraft);
public sealed record FolderTreeNode(Guid Id, Guid? ParentId, string Name, string Path, DataTarget Target,
    bool IsCompany, string? AuthorName, int FileCount, bool HasNewerDraft, IReadOnlyList<FolderTreeNode> Children);
public sealed record FolderFileDto(Guid Id, string Name, FileKind Kind, FileSource ContentSource,
    DateTime ContentModifiedAt, string? DriveFileId, DateTime? DriveModifiedAt, long SizeBytes,
    ImportState State, string? ImportError, DateTime? LastImportedAt, int? RowCount /*draft or live docs from this file*/,
    bool HasNewerDraft, DataTarget EffectiveLayer /*Draft if folder.Target==Draft else Live*/);
public sealed record FolderDetail(FolderNode Folder, IReadOnlyList<FolderNode> Subfolders, IReadOnlyList<FolderFileDto> Files);
```

`HasNewerDraft` for a file = folder target is Live **and** a `TidpDraft` exists for the file **and** its `ImportBatch.ImportedAt` is later than the latest `PromoteBatch.At` for that file (or no promote exists).

### 5.3 Drive sync service (`Dip.Api/Workers/DriveSyncService.cs`, scoped)

```
SyncProjectAsync(projectId, ct):
  root = upsert Folder(DriveFolderId = options.RootFolderId, Name = "Drive Root", Path = "DriveRoot")   // as today
  queue = [root]; seen = {}
  while queue not empty:
     folder = dequeue; children = drive.ListChildrenAsync(folder.DriveFolderId)
     for each child folder  → upsert Folder by DriveFolderId (name, path, parent, IsDeleted=false, Target inherited only on insert); enqueue
     for each child .xlsx/.xlsm/.xls → UpsertFileAsync(folder, entry)      // R2
     mark Drive-sourced folders/files under `folder` that were not seen as IsDeleted = true (folders: only if no non-deleted uploaded files remain below)
     folder.LastSyncedAt = now; SaveChanges; hub.FolderSynced(projectId, folder.Id, folder.Path, files: n)
  hub.SyncFinished(projectId, foldersSynced, filesQueued, error: null)

UpsertFileAsync(folder, entry):
  file = FolderFiles.FirstOrDefault(f => f.FolderId == folder.Id && f.Name.ToLower() == entry.Name.ToLower() && !f.IsDeleted)
        ?? FolderFiles.FirstOrDefault(f => f.DriveFileId == entry.Id && !f.IsDeleted)   // renamed in Drive → same row, new name
  if file is null: create (Kind = FileKindDetector.Detect(name), ContentSource = Drive, ContentModifiedAt = entry.ModifiedTime, ContentMd5 = "", State = NotImported)
  file.DriveFileId = entry.Id; file.DriveModifiedAt = entry.ModifiedTime; file.Name = entry.Name
  shouldDownload = file.ContentMd5 == "" || (entry.ModifiedTime > file.ContentModifiedAt && entry.Md5Checksum != file.ContentMd5)
  if shouldDownload:
      bytes = await drive.DownloadAsync(entry.Id) → MemoryStream → byte[]
      upsert FileBlob; file.ContentSource = Drive; file.ContentModifiedAt = entry.ModifiedTime; file.ContentMd5 = md5(bytes); file.SizeBytes = bytes.Length
      file.State = NotImported; file.ImportError = null; queue.Enqueue(ImportFile(file.Id))
```

One `SaveChangesAsync` per Drive folder. Drive errors on one folder are logged, reported via `hub.SyncFinished(error)` at the end, and do not abort the other folders. `LastSyncedAt` is informational only (the old "next unsynced folder" logic is gone).

### 5.4 Workers, queue, hub (`Dip.Api/Workers/`, `Dip.Api/Hubs/`)

- `WorkQueue` (singleton): `Channel<WorkItem>` unbounded; `record WorkItem(WorkItemKind Kind, Guid Id /*FolderFileId or ProjectId*/)`; `Enqueue` dedupes against a `ConcurrentDictionary` of pending items; `TryDequeueAsync`.
- `WorkerState` (singleton): `IsSyncRunning`, `LastRunStartedAt`, `LastRunFinishedAt`, `LastRunError`, `NextRunAt`, `QueuedImports` (from `WorkQueue`).
- `DriveSyncWorker : BackgroundService`: `PeriodicTimer(TimeSpan.FromHours(options.PollHours))` **and** a `Channel<Guid>` trigger (`ISyncTrigger.Request(projectId)`). First run 30 s after startup. A trigger during a run sets a "run again" flag. Runs `DriveSyncService.SyncProjectAsync` inside `IServiceScopeFactory.CreateScope()`. `PollHours <= 0` disables the timer (tests). Skips entirely when `RootFolderId` is empty (logs once).
- `ImportWorker : BackgroundService`: on start, enqueues `ImportFile` for every non-deleted file with `State ∈ {NotImported, Outdated}`; then loops `WorkQueue`. For `ImportFile` → `FileImportService.ImportAsync(fileId)`; for `Recalculate` → `RecalculationService.RunAllAsync(projectId)` (loops the chunked step until done). One item at a time. Exceptions are caught per item and logged; the worker never dies.
- `FileImportService` (scoped, `Workers/FileImportService.cs`):
  1. Load `FolderFile` + `Folder` + `FileBlob`. If deleted or no blob → return.
  2. Layer: `Kind ∈ {Tidp, Midp}` → `ContentSource == Drive ? Draft : folder.Target`; all other kinds → Live.
  3. Create `ImportBatch { Kind, Target = layer, FolderFileId, FileName, ImportedAt = now, ImportedBy = "worker" }`.
  4. `hub.FileImportStarted(projectId, fileId)`. Open `new MemoryStream(blob.Content, writable: false)`; run the sheet routing (R5) — each importer gets its own stream position reset or a fresh `MemoryStream` over the same array. Aggregate `ImportResult`s into the batch (RowsRead/Inserted/Updated/Skipped, warnings JSON in `Log`).
  5. On success: `file.State = Imported`, `LastImportBatchId`, `LastImportedAt`, `ImportError = null`, `batch.Completed = true`; enqueue `Recalculate(projectId)`; `hub.FileImported(projectId, fileId, folderId, batchSummary)`.
  6. On failure: `file.State = Failed`, `ImportError = ex.Message` (≤ 2000 chars), batch `Log` with error; `hub.FileFailed(projectId, fileId, folderId, error)`.
- `SyncHub : Hub` at `/hubs/sync`, `[Authorize]`. Client method `JoinProject(Guid projectId)` → `Groups.AddToGroupAsync(ConnectionId, $"project:{projectId}")`. Server → client events (all include `projectId`, camelCase JSON):
  `syncStarted {at}`, `folderSynced {folderId, path, files}`, `syncFinished {foldersSynced, filesQueued, error}`, `fileQueued {fileId, folderId}`, `fileImportStarted {fileId}`, `fileImported {fileId, folderId, batch: ImportBatchSummary}`, `fileFailed {fileId, folderId, error}`, `recalculationFinished {documents}`.
  `ISyncNotifier` (Application-free, lives in `Dip.Api/Hubs/ISyncNotifier.cs`) wraps `IHubContext<SyncHub>` so services do not depend on SignalR types.
- JWT for the hub: in `AddJwtBearer`, `OnMessageReceived` reads `access_token` from the query string when the path starts with `/hubs/`. CORS policy already uses `WithOrigins(...).AllowCredentials()` outside Development; in Development replace `AllowAnyOrigin` with `WithOrigins("http://localhost:5173").AllowCredentials()` (SignalR negotiate cannot use `*` with credentials).
- `Program.cs`: `builder.Services.AddSignalR()`, `app.MapHub<SyncHub>("/hubs/sync")`, `AddHostedService<DriveSyncWorker>()`, `AddHostedService<ImportWorker>()`. Tests: `DipApiFactory` sets `GoogleDrive:PollHours = 0` and registers a `NoopSyncNotifier`; the `ImportWorker` stays on (the import flow tests wait on it, § 11.4).
- Config (`appsettings.json`, empty/default values only): `GoogleDrive:PollHours = 5`, `GoogleDrive:StartupDelaySeconds = 30`.

### 5.5 Drafts area

- Extract the body of `PromoteHandler.Handle` into `Features/Drafts/PromoteExecutor.cs` (`internal sealed class PromoteExecutor { Task<PromoteResultDto> ExecuteAsync(Guid folderFileId, bool deleteMissing, string by, CancellationToken) }`). `PromoteHandler` delegates to it. Remove the `PromoteSnapshot` creation and the `SnapshotJson`/`RolledBack` writes. Stale snapshot rows for touched documents are still deleted (keep that block, key is `DocumentId`).
- `PromoteLoader`: the `!b.RolledBack` filter goes away.
- **`ConvertToLive`** (`Features/Drafts/ConvertToLive/`): `ConvertToLiveCommand(Guid FolderId) : ICommand<ConvertToLiveResultDto>`, `[Permission(FoldersAssignTarget)] [Permission(DraftsPromote)]`. Handler: load folder subtree ids; for each non-deleted file in those folders that has a `TidpDraft`, `PromoteExecutor.ExecuteAsync(fileId, deleteMissing: true, by)`; then cascade `Target = Live`; enqueue `Recalculate`. Returns `{ folderId, filesConverted, added, updated, deleted, skipped, conflicts, perFile: [{fileId, name, added, updated, deleted, conflicts}] }`. Runs inside the `TransactionBehavior` transaction (all or nothing).
- `GetPromoteDiff`, `Promote`, `ListDraftDocuments`, `UpdateDraftDocument`, `BulkUpdateDraftDocuments` stay. `PromoteResultDto` and `PromoteDiffDto` unchanged.
- **`UpdateDocument`** (`Features/Documents/UpdateDocument/`): `PUT /api/documents/{id}`; `[Permission(DocumentsEditLive)]`; body = the same editable fields as `UpdateDraftDocumentCommand` (title … corporateDiscipline, optional exchanges). Handler: `DraftScope.EnsureCanEdit`-equivalent on `F05Discipline` (reuse it), normalise `F06Zone`/`F08CSequence` via `DocumentNumbering`, recompose `DocumentNumber`, 409 if another Live document in the project has the new number, `AuditLog` per changed field, `UpdatedAt/By`, delete the document's snapshot, enqueue `Recalculate`. Returns a `DocumentDto` (new record in `Features/Documents/DocumentDto.cs`, same shape as `DraftDocumentDto` minus draft-only fields).

### 5.6 Workbook read model (`Features/Folders/GetFileWorkbook/`)

`GET /api/folder-files/{id}/workbook?sheet={TIDP|Baseline|Picklists}&page=1&pageSize=500`, `[Permission(ReportsView)]`.

```csharp
public sealed record WorkbookDto(Guid FileId, string FileName, FileKind Kind, DataTarget Layer, FileSource ContentSource,
    DateTime ContentModifiedAt, bool HasNewerDraft, IReadOnlyList<string> Sheets, WorkbookSheetDto Sheet);
public sealed record WorkbookSheetDto(string Name, IReadOnlyList<WorkbookHeaderCell> HeaderBlock /*TIDP only: label/value rows 3–11*/,
    IReadOnlyList<WorkbookColumn> Columns /*letter, title, key, width, editable, kind(text|date|int)*/,
    int TotalRows, int Page, int PageSize, IReadOnlyList<WorkbookRow> Rows);
public sealed record WorkbookRow(Guid RowId, int RowNumber, IReadOnlyList<string?> Cells /*same order as Columns*/, string? State /*draft row state or null*/);
```

- Sheet `TIDP`: columns A–AH exactly in `PLAN.md § 5.1.1` order (A DocumentNumber … V CorporateDiscipline, W–AB exchange 01, AC–AH exchange 02). Rows come from `DocumentDraft` when `Layer == Draft` else from `Document`, filtered by `FolderFileId == id`, ordered by `DocumentNumber`. `HeaderBlock` from `TidpDraft`/`Tidp` (CLIENT, PROJECT, ORGANISATION, DISCIPLINE, APPROVER, DATE CREATED, DATE LAST UPDATED, REVISION NUMBER, DOCUMENT REFERENCE). Column A `editable = false`; B–V editable; W–AH editable. For `Kind == Midp` the same sheet is named `MIDP` and has no header block.
- Sheet `Baseline`: `BaselineActivity` rows for the project (read-only). Sheet `Picklists`: `PicklistItem`s grouped as in the workbook layout (read-only). Both are offered for every file kind so the tabs match the source workbook.
- Dates are serialised as `yyyy-MM-dd`; leading zeros are preserved (all cells are strings).

### 5.7 Effective set, recalculation, reports

- `Dip.Api/Common/EffectiveDocumentLoader.cs` (internal static):
  ```csharp
  internal sealed record EffectiveDocument(Document Document /*in-memory for drafts*/, DataTarget Layer, Guid RowId, Guid? FolderFileId, DateTime SourceModifiedAt);
  Task<IReadOnlyList<EffectiveDocument>> LoadAsync(DipDbContext db, Guid projectId, CancellationToken ct);
  Task<IReadOnlyList<EffectiveDocument>> LoadPageAsync(DipDbContext db, Guid projectId, int offset, int take, CancellationToken ct); // ordered by RowId
  ```
  Implementation: load `(FolderFile.Id, Folder.Target, ContentModifiedAt)` for the project; Live docs (`Include(Exchanges)`) where file target is Live or `FolderFileId == null`; Draft docs (`Include(Exchanges)`, `!IsDuplicate`) where file target is Draft; map drafts to `Document` objects (`Id = draft.Id`, all fields, exchanges → `DataExchange`); dedupe by `DocumentNumber.ToUpperInvariant()` per R8. Paging is done in memory over the deduplicated list (≤ 20k rows; acceptable).
- `RecalculationService`: `RunStepAsync(projectId, offset, take)` now pages the effective set, runs `TrackerEngine.Compute`, and upserts `DocumentSnapshot` keyed by `RowId`, filling the display columns and `Layer`. Add `RunAllAsync(projectId)` (loops steps, then deletes snapshots whose `DocumentId` is not in the effective set, then `hub.RecalculationFinished`). Keep the `POST /api/projects/{projectId}/recalculate/step` endpoint as the admin fallback.
- `ReportDataLoader`: `Documents` = effective set's `Document`s; `TrackerRows` from snapshots by `RowId`; `DocumentsWithoutSnapshot` counts effective rows lacking a snapshot. `GetCorporateSummary`, `GetBaselineSummary`, `GetEvm`, `GetControlFindings`, `ExportReport` need no logic change beyond consuming the loader.
- `ListTrackerDocuments`: query `DocumentSnapshots` only (filters: discipline, status, hasAconex, search over `DocumentNumber`/`Title`), page in SQL, map to `TrackerRowDto` (add `layer`). `GetTrackerDocument`: load the snapshot by id; revisions as today. The `TrackerRowDto` gains `Layer`.

### 5.8 Lists area (§ 7 has the data)

| Method & route | Slice | Body / result |
|---|---|---|
| `GET /api/projects/{projectId}/picklists?includeDeleted=false` | `GetPicklists` (change) | `PicklistGroupDto[]` — items gain `isDeleted`, `deletedAt`. Every `PicklistField` appears as a group even when empty (so the page has a tab per list). |
| `POST /api/projects/{projectId}/picklists` | `CreatePicklistItem` | `{ field, code, description, sortOrder? }` → 201 `PicklistItemDto`, or 200 with `restored: true` per R9. Validator: code non-empty ≤ 100, description ≤ 500, field is a defined enum value. |
| `PUT /api/picklists/{id}` | `UpdatePicklistItem` | `{ code, description, sortOrder }` → `PicklistItemDto`. 409 if the new code collides with a non-deleted sibling. |
| `DELETE /api/picklists/{id}` | `DeletePicklistItem` | soft delete → 204. |
| `POST /api/picklists/{id}/restore` | `RestorePicklistItem` | → `PicklistItemDto`. 409 if a non-deleted sibling now holds the code. |
| `PUT /api/projects/{projectId}/picklists/{field}/order` | `ReorderPicklist` | `{ ids: guid[] }` → sets `SortOrder` 1..n. |
| `GET /api/projects/{projectId}/status-mappings?includeDeleted=false` | `GetStatusMappings` (change) | adds `isDeleted`. |
| `POST /api/projects/{projectId}/status-mappings` | `CreateStatusMapping` | `{ aconexStatus, status, isLegacy }`. |
| `PUT /api/status-mappings/{id}` | `UpdateStatusMapping` | `{ aconexStatus, status, isLegacy }`. |
| `DELETE /api/status-mappings/{id}` / `POST …/restore` | `DeleteStatusMapping`, `RestoreStatusMapping` | soft delete / restore. |

All mutations `[Permission(ListsManage)]`. `StatusMappingLookup` and every importer must read only non-deleted mappings/items.

`PicklistImporter` (R3 phase): keep the FIELD-label scan for the numbering fields, add a second pass for the single-column lists located by their **row-5 header text**: `Authoring Software`→AuthoringSoftware, `File/Exchange Format`→ExchangeFormat, `Scope Area`→ScopeArea, `Code` (under `SUITABILITY STATUS`)→SuitabilityCode with `Status` as description, `Scale`→Scale, `Classification ID`→Classification with `Classification` as description, `Corporate Discipline`→CorporateDiscipline, `Author`→Author. Trim values (Scope Area cells carry trailing spaces). Skip the `8C - Sequence Number` column. Skip soft-deleted codes (R9). `SortOrder` = row order.

### 5.9 Removals checklist (grep must be empty after R1/R2)

`ILocalFileStorage`, `LocalFileStorage`, `StoragePath`, `App_Data/files`, `ImportStagingRow`, `ImportStagingRows`, `SnapshotJson`, `RolledBack`, `RollbackPromote`, `PromoteSnapshot`, `StartImport`, `RunImportStep`, `ImportDispatcher`, `SyncStepResult`, `RenameFolder`, `DeleteFolder`, `DeleteFile`, `FileSource Source` (renamed), `Md5 ` on FolderFile (renamed).

---

## 6. Frontend specification

### 6.1 Shared

- `shared/api/types.ts`: update `FolderTreeNode`, `FolderNode`, `FolderFileSummary` (→ `FolderFileDto` shape), add `UploadResult`, `DriveStatus`, `WorkbookDto` family, `ConvertToLiveResult`, `DocumentDto`, `PicklistItem { isDeleted, deletedAt }`, `PicklistField` union of the 17 names, `StatusMappingRow.isDeleted`; delete `StartImportResult`, `RunImportStepResult`, `RollbackResult`.
- `shared/realtime/syncHub.ts`: one `HubConnectionBuilder` (`@microsoft/signalr`) to `${apiBaseUrl}/hubs/sync` with `accessTokenFactory: () => tokenStorage.getAccessToken() ?? ''`, `withCredentials: false`, `withAutomaticReconnect()`; `useSyncHub(projectId)` hook that joins the project group, exposes `status: 'connecting'|'connected'|'reconnecting'|'disconnected'` and a typed `subscribe(event, handler)`; a `useSyncEvents()` helper that invalidates the TanStack queries (`folders`, `imports`, `tracker`, `summary`, `workbook`) on `fileImported`, `fileFailed`, `syncFinished`, `recalculationFinished`.
- `shared/ui/toast.tsx`: minimal shadcn-style toast (no new package): a context + `useToast()`; used for sync/import progress.
- `shared/ui/context-menu.tsx`: shadcn context menu over `@radix-ui/react-context-menu`? **No** — reuse the existing `DropdownMenu` opened at the pointer position on right-click (no new package).

### 6.2 TIDPs explorer (`features/explorer/`, route `/tidps`, replaces `features/folders/`)

Component tree:

```
ExplorerPage
├─ ExplorerToolbar        breadcrumb (clickable segments) · Upload · Sync now · target switch (company folders) ·
│                         Company/Author popover · Convert to Live · New folder (non-Drive parents only) · Drive status pill
├─ FolderTree (left)      existing component, kept; shows target badge + author + "newer draft" dot
└─ TileGrid (right)       CSS grid, minmax(160px, 1fr); folders first (FolderTile), then files (FileTile);
   ├─ FolderTile          large folder glyph (lucide `Folder`, amber fill, subtle gradient), name (2-line clamp), author, target badge, file count
   ├─ FileTile            spreadsheet glyph (green), name, state badge (NotImported/Importing/Imported/Failed/Outdated), source glyph (cloud = Drive, upload arrow = Upload), modified time, "newer draft" badge
   └─ EmptyState          "This folder is empty" / "Drop a workbook here"
```

Behaviour: single click selects (ring), double click opens (folder → navigate, file → `/files/:fileId`), Enter/Backspace/arrow keys, right-click or `⋯` opens the menu (folder: Open · Set target · Company/Author · Convert to Live · New folder; file: Open · Download · Re-import · Promote diff (Draft layer)). Drag-and-drop upload onto the grid. A file whose import is in progress shows a spinner (from `fileImportStarted` until `fileImported`/`fileFailed`). Toasts on `syncStarted`/`syncFinished`/`fileImported`/`fileFailed`. The page never calls the removed endpoints.

`ConvertToLiveDialog`: shows the per-file promote diff counts (from `GET /api/drafts/promote-diff` for each file) before confirming; on success shows the result table. `UploadDialog` is kept (multi-file, progress per file, 202 handling). `SyncDriveDialog` and `ImportDialog` are deleted.

Design: tiles 160×132 px, 8 px radius, `bg-card`, hover `shadow-md` + `translate-y-[-1px]`, selected `ring-2 ring-primary`, names `text-sm font-medium`, secondary `text-xs text-muted-foreground`; badges reuse `FolderBadges.tsx` (moved). Light and dark themes both supported (existing tokens).

### 6.3 Workbook viewer (`features/workbook/`, route `/files/:fileId`)

```
WorkbookPage
├─ WorkbookTitleBar     file name · layer badge (DB1 Draft / DB2 Live) · source + modified · "Drive has newer data" · Download · Back
├─ FormulaBar           name box (e.g. "C12") + read-only value of the active cell (column A shows the composed number, like the Excel formula)
├─ Grid                 @tanstack/react-virtual rows (22 px), sticky column-letter header row, sticky row-number gutter,
│                       frozen column A, cell selection (click, shift-click range, arrows, Home/End, PageUp/Down), Ctrl+C copies TSV,
│                       double-click / F2 / typing opens the inline editor when the column is editable and the user has the layer's permission,
│                       Enter/Tab commit, Esc cancels; edited row is saved via UpdateDraftDocument (Draft) or UpdateDocument (Live);
│                       validation errors from the API appear in a cell tooltip and keep the editor open
├─ SheetTabs            bottom: TIDP (or MIDP) · Baseline · Picklists — white active tab, grey inactive, like Excel
└─ StatusBar            "Rows: 1,283" · page/pageSize navigator · layer · last import time
```

Styling: Calibri/Segoe-like stack (`font-[Calibri,Segoe_UI,sans-serif]`), header fill `#f2f2f2` / dark `#2b2b2b`, gridlines `#d9d9d9` / `#3a3a3a`, active cell border `2px solid #217346`, selected range `bg-[#217346]/10`, row numbers centred, column widths from `WorkbookColumn.width`. Page size 500 rows via the API, infinite paging as the user scrolls (`useInfiniteQuery`).

### 6.4 Dashboard (`features/dashboard/`, route `/`)

Rename `SummaryPage` → `DashboardPage`; tabs `overview` (the current "dashboard" tab: stat tiles + S-curve), `corporate`, `baseline`, `findings` (move `ControlFindingsPage` content here as `ControlFindingsPanel`), `evm`. Delete `features/dashboard/DashboardPage.tsx` (the welcome page), `features/summary/` (moved), `features/findings/` (moved), route `/summary` and `/findings`, nav items "Summary" and "Control Findings". Nav: Dashboard · TIDPs · MIDP · Baseline · Tracker · Lists · Users · Settings. `Recalculate` button stays (calls the step endpoint until done).

### 6.5 Lists (`features/lists/`)

`ListsPage`: `Tabs` with one tab per `PicklistField` (17, in workbook order) plus `Status mapping`; each panel is `PicklistTable`: columns `#`, `Code`, `Description`, actions; "Add item" row at the top; inline edit (click pencil → inputs → save/cancel); delete with `ConfirmDialog`; "Show deleted" toggle reveals soft-deleted rows greyed with a Restore button; drag handle or ▲▼ to reorder (calls `ReorderPicklist`). `StatusMappingTable` mirrors it with a `UnifiedStatus` select and the Legacy checkbox. All mutations gated by `lists.manage`; viewers see read-only tables.

### 6.6 Routing and navigation after v3

```
/login
/                → DashboardPage (reports.view)
/tidps           → ExplorerPage (reports.view)
/files/:fileId   → WorkbookPage (reports.view)
/drafts/:folderFileId → DraftReviewPage (kept; reachable from a file's menu "Review draft")
/midp, /tracker, /baseline, /lists, /admin/users, /admin/settings — unchanged
```

`navigation.ts` order: Dashboard (`/`, reports.view), TIDPs, MIDP, Baseline, Tracker, Lists, Users, Settings. Update `navigation.test.ts` and `Sidebar.test.tsx` accordingly (the "dashboard needs no permission" test becomes "an empty permission set sees nothing").

---

## 7. Lists in the Picklists workbook

Source: `Qpac_1/01.Project Control/00.Picklists/QPAC-PickLists.xlsx` (same content as `samples/PickLists.xlsx`), sheet `Pick_Lists`, group labels in row 4, column headers in row 5, data from row 6.

| Columns | List | `PicklistField` | Rows | Expected values (verified 2026-09-08) |
|---|---|---|---|---|
| A/B | Project | Project | 1 | `QF01012` |
| C/D | Originator | Originator | 1 | `NES` |
| E/F | Contract | Contract | 1 | `C04518` |
| G/H | Document types | DocType | 94 | first `AGD` "Agenda" |
| I/J | Discipline | Discipline | 98 | first `ACO` "Acoustic" |
| K/L | Area/Zone | Zone | 29 | `00` "Overall" … (leading zeros kept) |
| M/N | Venue/Building | Building | 12 | `Z00000`, `CENT01`, … |
| O/P | Drawing type | DrawingType | 10 | `0`–`9` as text |
| Q/R | Level | Level | 16 | `00, B1, B2, B3, FL, L1, L2, L3, L4, L5, L6, L8, M1, M2, RL, ZZ` |
| S | Sequence number | — | — | descriptive text only; **not imported** |
| T | Authoring Software | AuthoringSoftware | 13 | `Revit, Civil3D, MicrosoftWord, MicrosoftExcel, Navisworks, inDesign, Inviso, JPG, Recap, Rhino, Sketchup, Autocad, ArcGIS` |
| U | File/Exchange Format | ExchangeFormat | 26 | `.rvt` … `.gdb` (includes combos like `.dwg,.pdf`) |
| V | Scope Area | ScopeArea | 31 | `Design Management` … `Wind` (trimmed) |
| W/X | Suitability status | SuitabilityCode | 14 | `S0 (WIP)` "Initial status or WIP" … `A8(Published)` |
| Y | Scale | Scale | 16 | `NTS, AS INDICATED, 1:1, 1:2, 1:5, 1:10, 1:20, 1:25, 1:50, 1:100, 1:150, 1:200, 1:250, 1:300, 1:350, 1:500` |
| Z/AA | Classification | **Classification** | 10 | `FI_60_25` "Drawing", `FI_90_75` "Schedule or table", … |
| AB | Corporate Discipline | **CorporateDiscipline** | 9 | `Architectural, Electrical, Façade, Fire & Life Safety, Interior Design, Infrastructure, Landscape, Mechanical, Structural` |
| AC | Author | **Author** | 12 | `Nesma & Partners, JINGGONG, ALUTEC, AFCO, DOKA, RAWABI, FALLPROTEC, TKE, Subcontractor - Unassigned, Provisional Sum, NAP PMO, Sana Al-Jazerah` |

Single-column lists store the value in `Code` with `Description = ""`. The `Discipline` table keeps the code → corporate-name mapping used by importers; the CorporateDiscipline list is only the dropdown value set. Status Mapping is not in this workbook; it stays seeded from `SeedData.StatusMappings` (22 rows) and editable.

The Corporate Summary engine groups authors from the document data, so the Author list does not feed the engine; the sample's 13 author groups remain the expected engine output.

---

## 8. Realtime and worker details

- Hub path `/hubs/sync`; transport negotiation default (WebSockets, falling back to long polling if IIS lacks the WebSocket feature — do not force a transport).
- The frontend connects once per app session in `Providers` (after auth), joins `QPAC_PROJECT_ID`, and reconnects on token refresh (`accessTokenFactory` reads the current token each time).
- `GET /api/projects/{id}/drive/status` backs the toolbar pill; the pill also flips on hub events.
- Worker liveness: `DriveHealthCheck` gains `lastRunFinishedAt` and `queuedImports` in its description (from `WorkerState`).
- IIS: `docs/deploy.md` must instruct: application pool *Start Mode = AlwaysRunning*, *Idle Time-out = 0*, site *Preload Enabled = true*, WebSocket Protocol feature installed if available. `web.config` unchanged except a comment.

---

## 9. Data model reset, seeding, packages

### 9.1 Migration
Delete `Persistence/Migrations/20260904125246_Init.cs`, `…Init.Designer.cs` and regenerate `DipDbContextModelSnapshot.cs` by running `dotnet ef migrations add Init -p src/Dip.Infrastructure -s src/Dip.Api` on the v3 model. One migration named `Init` is the whole history.

### 9.2 Seeding
`IdentitySeeder` keeps: roles + permission claims, the QPAC project, disciplines (10), status mappings (22), SuperAdmin from `Seed:*`. It does **not** seed picklists or folders: the first Drive poll imports `00.Picklists/QPAC-PickLists.xlsx`; locally the owner can upload `samples/PickLists.xlsx`. Add to the seeder: `MigrationTests` must still pass.

### 9.3 Test fixtures
`PostgresFixture` and `DipApiFactory`: if `TEST_POSTGRES_CONNECTION` contains `neon.tech` (case-insensitive) throw `InvalidOperationException("Tests must not run against Neon")`. Add a unit test for the guard (pure string check extracted to `TestConnectionGuard.Assert(string)` in the test project).

### 9.4 Packages (the only additions allowed)
- backend: none (`Microsoft.AspNetCore.SignalR` ships in the shared framework).
- frontend: `@microsoft/signalr` (hub client; there is no alternative), `@tanstack/react-virtual` (the MIDP sheet has 15,883 rows; rendering them all freezes the tab).

---

## 10. Sample-based expected values (tests must read these from the workbooks where a cell exists)

| Fact | Value | Source |
|---|---|---|
| TIDP-STL row count | 1,283 document rows (observed from the importer on 2026-09-08 — **derive it in tests by counting the populated rows of the sheet**, never assert the literal) | `samples/TIDP-STL.xlsx!TIDP_Sheet` |
| TIDP-STL first document number | `QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004` | same, row 17 col A |
| TIDP-STL discipline | `Structural` (code `STL`) | header block |
| MIDP document count | 15,885 rows, Tracker filters to 15,883 | `PLAN.md § 5.1.2` |
| Corporate Summary, Baseline Summary, Control Findings, EVM | exactly the existing `*SampleTests` expectations | `samples/Tracker.xlsx` |
| Picklist counts | § 7 table | `samples/PickLists.xlsx` |
| Status mappings seeded | 22 | `SeedData` |
| Disciplines seeded | 10 | `SeedData` |

---

## 11. Operations the agent runs

### 11.1 Neon reset (once, at the start of R1, after the new migration compiles)
1. Create `backend/scripts/reset-neon.sql` containing exactly:
   ```sql
   DROP SCHEMA public CASCADE;
   CREATE SCHEMA public;
   GRANT ALL ON SCHEMA public TO neondb_owner;
   GRANT ALL ON SCHEMA public TO public;
   ```
2. Read the connection string from user-secrets **without printing it**: `dotnet user-secrets list --project backend/src/Dip.Api | grep '^ConnectionStrings:Default'`, parse Host/Database/Username/Password in the shell, and run `psql "host=… dbname=… user=… sslmode=require" -f backend/scripts/reset-neon.sql` with `PGPASSWORD` set from the parsed value. Do not echo the command line with the password.
3. Start the API once (`dotnet run --project src/Dip.Api`) so it migrates and seeds, or run `dotnet ef database update`. Verify with `psql … -c 'select count(*) from "Disciplines"'` = 10.
4. Record in `docs/refactor-log.md`: "Neon reset on <timestamp>, migration Init applied, seed OK".

### 11.2 Local Postgres for tests
Use the scratch database documented in the owner's memory: `export TEST_POSTGRES_CONNECTION="Host=localhost;Port=5432;Database=dip_test;Username=postgres;Password=postgres"` (container `postgres-catalogs`, Postgres 16). If it is not running, start it with `docker start postgres-catalogs`. Never use Neon.

### 11.3 Definition of "tests green"
`dotnet test backend/Dip.sln` with the env var set must show **0 failed, 0 skipped** in `Dip.Infrastructure.Tests` and `Dip.Api.IntegrationTests` (tests that require Postgres throw when it is missing — that is by design). Paste the summary lines into the log.

### 11.4 Integration test style for the worker
Tests that need an import to complete call `POST …/files`, then poll `GET /api/folders/{id}` until the file `state` is `Imported` or `Failed` (timeout 60 s, 250 ms interval). A helper `WaitForImportAsync(client, folderId, fileId)` goes in `Dip.Api.IntegrationTests/TestHelpers.cs`.

---

## 12. Phases

Each phase ends with a green build/test/lint and a commit. Intermediate green commits inside a phase are allowed (`[R1.1]`, `[R1.2]`).

### R1 — Backend core: schema v3, no disk, read-only Drive, workers, automatic import
**Do**
1. Entities/enums per § 5.1; delete `ImportStagingRow`; EF configurations and indexes; regenerate `Init` migration (§ 9.1); Neon reset (§ 11.1); fixture guard (§ 9.3).
2. Delete `ILocalFileStorage`, `LocalFileStorage`, `StoragePath` usages; `IExcelReader.Open(string)` removed (stream only); all importers take `Stream`; `ImportResult` unchanged.
3. `AconexHistoryImporter`: accept sheet `Aconex History` or `SHD_History`.
4. `Workers/` (`WorkQueue`, `WorkerState`, `DriveSyncWorker`, `DriveSyncService`, `ImportWorker`, `FileImportService`), `Hubs/` (`SyncHub`, `ISyncNotifier`, `HubSyncNotifier`, `NoopSyncNotifier` for tests), sheet routing (R5), JWT-on-query for hubs, CORS dev change, `Program.cs` wiring, options (`PollHours`, `StartupDelaySeconds`).
5. Folders slices: change `UploadFile` (blob + R2 + enqueue + 202), `TriggerDriveSync`, `GetDriveStatus`, `DownloadFile`; restrict `CreateFolder`; delete `RenameFolder`, `DeleteFolder`, `DeleteFile`, old `SyncFromDrive`.
6. Imports: delete `StartImport`, `RunImportStep`, `ImportDispatcher`; keep `GetImportStatus`, `ListImportBatches`.
7. Drafts: `PromoteExecutor`; delete `RollbackPromote`, `PromoteSnapshot`; drop `SnapshotJson`/`RolledBack`.
8. Health check description (§ 8).
9. Tests: rewrite `FoldersFlowTests` (create folder under manual parent, upload → wait → Imported, upload same name → replaced, Drive-linked parent rejects create), `ImportsFlowTests` (upload TIDP-STL to a Draft folder → 1,283 drafts; upload to a Live folder → 1,283 Live docs; unknown file name → 400), delete `PromoteFlowTests.Rollback_*`, importer tests switch to streams, `DriveSyncServiceTests` with a fake `IDriveClient` (new folder + file → blob + queued; older `modifiedTime` than an upload → not replaced; same md5 → not downloaded; file gone from Drive → soft-deleted), `FileImportServiceTests` (Drive-sourced TIDP in a Live folder lands in Draft; upload-sourced in a Live folder lands in Live; Midp workbook also imports Aconex History).
**Acceptance**: § 0.4 grep empty; `dotnet test` green; Neon migrated + seeded; log written.

### R2 — Targets, companies, conversion, effective set, materialised tracker, Live edits
**Do**
1. `SetFolderTarget` cascade; `SetFolderCompany`; DTO changes (§ 5.2) incl. `hasNewerDraft`.
2. `ConvertToLive` (§ 5.5). `UpdateDocument` (§ 5.5).
3. `EffectiveDocumentLoader`; `DocumentSnapshot` as materialised tracker row; `RecalculationService.RunStepAsync/RunAllAsync`; `ReportDataLoader`; `ListTrackerDocuments`/`GetTrackerDocument` over snapshots; `Recalculate` work item wired from import, promote, convert, update.
4. `GetFileWorkbook` (§ 5.6).
5. Tests: `EffectiveDocumentLoaderTests` (Live folder → Live rows; Draft folder → Draft rows; same number in both, newer file wins; tie → Live); `RecalculationTests` (snapshot rows carry `Layer` and display columns; stale snapshot removed after a row disappears); sample tests: MIDP + Aconex + Baseline imported into a **Draft**-target folder produce the same Corporate Summary numbers as Live (reuse `CorporateSummarySampleTests` expectations); `ConvertToLiveTests` (Draft folder with TIDP-STL → after convert: 1,283 Live docs, folder Target Live, PromoteBatch counts Added = 1,283; second convert with an edited draft row → Updated = 1; Drive-sourced re-import after convert → `hasNewerDraft = true`); `UpdateDocumentTests` (sequence `0004`→`0005` recomposes the number, audit rows written, duplicate number → 409); `GetFileWorkbookTests` (34 columns A–AH, row 1 col A = first number, paging).
**Acceptance**: Tracker/Dashboard endpoints return data for a project where the only imports are in a Draft folder.

### R3 — Lists backend
**Do**: `PicklistImporter` second pass (§ 5.8); soft delete fields + filters everywhere (`StatusMappingLookup`, importers, `GetPicklists`, `GetStatusMappings`); all slices in § 5.8; `PicklistImporterTests` extended to the § 7 counts and first values; `PicklistCrudTests` (create, restore-on-create, update collision 409, soft delete hidden, `includeDeleted`, re-import skips deleted `TKE` and reports 1 skipped, reorder).
**Acceptance**: importing `samples/PickLists.xlsx` yields exactly the § 7 row counts.

### R4 — Frontend: explorer grid, realtime, dashboard swap, navigation
**Do**: § 6.1, § 6.2, § 6.4, § 6.6; delete `features/folders/`, `features/imports/`, `features/summary/`, `features/findings/`, the old `features/dashboard/DashboardPage.tsx`, `SyncDriveDialog`, `ImportDialog`, `FileList` and their tests; new tests: `TileGrid.test.tsx` (folders before files, double-click navigates, right-click menu items by permission), `FileTile.test.tsx` (spinner while importing, badge flip on `fileImported` via a mocked hub), `ExplorerToolbar.test.tsx` (Convert visible only with both permissions; New folder hidden for Drive parents), `syncHub.test.ts` (invalidation map), `navigation.test.ts`/`Sidebar.test.tsx` updated, `DashboardPage.test.tsx` (five tabs).
**Acceptance**: `npm run lint && npm run typecheck && npm test` green; no import of a deleted module; `/` renders the Dashboard.

### R5 — Workbook viewer
**Do**: § 6.3; `features/workbook/{WorkbookPage,FormulaBar,Grid,Cell,CellEditor,SheetTabs,StatusBar,api.ts,columns.ts}`; tests: `Grid.test.tsx` (renders letters A…AH and row numbers; arrow keys move the active cell; F2 opens the editor only for editable columns and permitted users; Esc cancels), `api.test.ts` (save routes to the draft or live endpoint by layer), `columns.test.ts` (34 columns in PLAN order).
**Acceptance**: the TIDP-STL file opens with 1,283 rows; editing Sequence `0004` → `0005` updates column A after save.

### R6 — Lists page
**Do**: § 6.5; tests: `ListsPage.test.tsx` (18 tabs), `PicklistTable.test.tsx` (add row, inline edit, delete → hidden, Show deleted → Restore visible, read-only without `lists.manage`).

### R7 — Docs, deploy, plan alignment
**Do**: update `PLAN.md` § 1 (constraints), § 3.2, § 3.4, § 4, § 6, § 8, § 9 to match this file (edit the Arabic text in place, keep the sample numbers); `CLAUDE.md` rule 9 reworded (D4) and "Read first" pointing here; `docs/deploy.md` (§ 8 IIS settings, hub path, `GoogleDrive__PollHours`, WebSocket note); `docs/excel-analysis.md` gets a § 2.3 correction listing the 19 lists; `.github/workflows` unchanged unless the test env var handling needs it; final entry in `docs/refactor-log.md` with the full test summary and the list of every removed file.

---

## 13. `PLAN.md` sections superseded by this file

| PLAN.md | Change |
|---|---|
| § 1 "قيود ASPMonster" | Hosted workers and SignalR are allowed (D4). |
| § 2 Features list | Folders: no Rename/DeleteFile/SyncFromDrive step; Imports: no Start/RunStep; Drafts: no Rollback, + ConvertToLive; Documents: UpdateDocument; new `Workers/`, `Hubs/`. |
| § 3.2 FolderFile | Replaced by § 5.1 here. |
| § 3.4 import behaviour | R3/R4/R7 here; Promote kept; Rollback removed. |
| § 4 Drive client | Sync is the worker (§ 5.3); "لا تستورد تلقائياً" reversed (D5). |
| § 5.4 engine inputs | Effective set (R8). |
| § 6 importers | Streams from `FileBlob`; sheet routing R5; PicklistImporter § 5.8. |
| § 8 frontend | `folders/` → `explorer/`; `summary/` + `findings/` → `dashboard/` at `/`; new `workbook/`; editable Lists. |
| § 9 phases 2.1, 3.5, 4.2, 6.2, 6.5 | Replaced by R1–R7. |
| CLAUDE.md rule 9 | Reworded in R7. |

---

## 14. Owner's review checklist (used after the run; the agent should self-check against it before finishing)

1. `grep` from § 0.4 and § 5.9 return nothing; `IDriveClient` has two members.
2. No file under `backend/src` writes to the file system except Serilog logs.
3. Every new endpoint follows the slice shape with a validator on commands and a `[Permission]` attribute.
4. `FolderFiles` has the filtered unique index; R2 upsert path covered by tests in both directions.
5. Drive-sourced content never reaches Live tables (test exists); upload-sourced content follows the folder target (test exists).
6. `EffectiveDocumentLoader` dedupe rule and tie-break covered by tests; snapshots carry `Layer`; Tracker reads snapshots only.
7. Corporate/Baseline/Findings/EVM sample tests still pass unchanged **and** pass for a Draft-target import.
8. `ConvertToLive` is transactional, cascades the target, writes `PromoteBatch` per file, enqueues recalculation.
9. Picklist import counts match § 7; soft-deleted codes are skipped on re-import; restore-on-create works.
10. Frontend: no references to deleted endpoints; hub reconnects with a fresh token; tiles and viewer tests pass; `/` is the Dashboard; Lists has 18 tabs.
11. Workbook viewer: 34 columns in PLAN order, column A read-only and recomposed after save, leading zeros preserved in Zone/Sequence.
12. `docs/refactor-log.md` lists commits, deviations, test summaries, removed files, and the Neon reset timestamp.
13. Neon contains only seeded rows plus what the first Drive poll imported; `TEST_POSTGRES_CONNECTION` guard in place.
14. `PLAN.md`, `CLAUDE.md`, `docs/deploy.md` updated; no secrets anywhere in the diff.
