# ADR 003 — Google Drive and the two-layer model are removed

Date: 2026-09-11
Status: Accepted (supersedes ADR 002, which described promote-conflict semantics)

## Context

v3 mirrored Google Drive read-only into a folder tree, imported every workbook it
found automatically, and kept two logical layers in one database: Draft (DB1) for
companies still working in Drive, Live (DB2) for companies working in the system.
Reports read a deduplicated union of the two, the "effective document set".

The owner's decision is that the database is the only source of truth. Engineers
edit rows in the system; nobody edits Excel any more. The initial load is a set of
files uploaded once by the super admin.

Two layers only ever existed to bridge the gap between the two ways of working.
With one way of working they are two tables for every concept, a dedupe rule on
every read, and a promote flow with conflict handling — all for a distinction that
no longer exists.

## Decision

Removed: `DriveSyncWorker`, `DriveSyncService`, `IDriveClient` and its local mirror,
`FileKindDetector`, the folder tree (`Folders`, `FolderFiles`, `FileBlobs`), the
draft tables (`TidpDrafts`, `DocumentDrafts`, `DataExchangeDrafts`), `PromoteBatches`,
`DraftDiff`, `PromoteClassifier`, the promote and convert-to-live slices, and the
`MidpImporter`.

Kept and reshaped: `Tidps` becomes `TidpFiles`, one row per uploaded workbook, and
owns its documents. `Documents` gains `TidpFileId` and the edit-tracking columns.
`DocumentSnapshots` gets its foreign key back — a snapshot can only come from one
table now, so a deleted document takes its tracker row with it instead of needing a
sweep.

The MIDP workbook is no longer a source. Across the 36 TIDP workbooks there are
15,939 distinct document numbers against the MIDP's 15,727; only 38 MIDP documents
appear in no TIDP. The TIDPs are the better source and the consolidated file was a
second copy of the same truth.

Ownership is first-file-wins. A document number another `TidpFile` already owns is
skipped on import with a counted warning naming both files. The alternative — last
upload wins — would silently move a document between disciplines, and the replace
path would then delete it.

## Consequences

- There is no automatic import. Stage 3 adds upload endpoints; between stage 2 and
  stage 3 the system can read its data but not receive any.
- Uploaded bytes are never stored. They are parsed and discarded, so an interrupted
  import cannot be resumed: `ImportWorker` sweeps unfinished batches to `Failed` at
  startup with "interrupted, upload again" rather than leaving a row that never
  completes.
- `ADR 002` is superseded. Promote conflicts, the comparison baseline and the
  `RolledBack` flag it describes no longer exist.
- The `Qpac_1/` mirror is deleted from the repository. `samples/` stays: the tests
  read those workbooks.
