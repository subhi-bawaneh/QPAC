# ADR 002 — What "Live changed after the import" means at Promote

**Status:** Accepted (2026-09-05)
**Context:** Phase 4.2 (`GetPromoteDiff` / `Promote`).

PLAN.md § 3.4 defines a promote conflict as:

> Conflicts (وثيقة موجودة Live من **ملف آخر** بنفس الرقم، أو Live **عُدّل بعد تاريخ الاستيراد**).

Taken literally, the second condition fires on a file's own promote. Promoting a
draft stamps every written Live row with `UpdatedAt = now`, which is by definition
later than that draft's `ImportBatch.ImportedAt`. The very next promote of the same
file would therefore report **every** row as "changed after the import" and write
nothing — the second promote of a file could never succeed.

## Decision

The comparison baseline is

```
baseline = max(ImportBatch.ImportedAt, last non-rolled-back PromoteBatch.At for this FolderFileId)
```

and a Live row conflicts when `Document.UpdatedAt > baseline`.

The rule's intent is preserved exactly: a Live row that someone else changed behind
the reviewer's back is still a conflict. Only the file's own promotes are excluded,
because those changes *are* the reviewer's own accepted work.

The first condition (a Live row whose `FolderFileId` is a different file) is
implemented literally, with no exception for file kind — confirmed with the product
owner on 2026-09-05. A Live row with no origin file (`FolderFileId is null`) is
adopted by the promoting file rather than treated as a conflict.

Consequences:
1. Promote is repeatable: import → promote → edit → promote works without manual
   conflict clearing.
2. A row edited directly in Live (Phase 4.x `documents.editLive`) between import and
   promote still surfaces as a conflict and is never silently overwritten.
3. Rolled-back promote batches are excluded from the baseline, because a rollback
   restores the rows' original `UpdatedAt` values.

**Proposed PLAN.md edit:** append to § 3.4, Conflicts — "أو Live عُدّل بعد تاريخ
الاستيراد (أو بعد آخر Promote لنفس الملف، أيهما أحدث)".
