# ADR 004 — Production moves from Neon Postgres to SQL Server

Date: 2026-09-12
Status: Accepted

## Context

Neon's free tier ran out of usable compute/storage for the project. The owner has a
free SQL Server instance on the same ASPMonster account the API is already hosted
on, and asked to move production onto it rather than pay for Neon. There is no
production data to carry over — the move starts from an empty database.

## Decision

Production, staging and every deployed environment run on SQL Server. Local
development is unaffected: it still runs on a SQLite file, never on the shared
database. Changed to get there:

- `Npgsql.EntityFrameworkCore.PostgreSQL` → `Microsoft.EntityFrameworkCore.SqlServer`
  in `Dip.Infrastructure`. `DatabaseProvider` is now `SqlServer | Sqlite`.
- Column types written for Postgres become their SQL Server equivalents:
  `timestamp without time zone` → `datetime2`, `jsonb`/`text` → `nvarchar(max)`.
  `SqliteModelTweaks` normalises these away for local SQLite runs exactly as it did
  for the Postgres ones before.
- The four Npgsql-authored migrations are replaced by one fresh `Init` migration
  against SQL Server — there was no data to migrate, so a backfill migration was
  unnecessary. `AconexLineHashSql`, the raw-Postgres-SQL hash backfill used only by
  the old `S3_LineHashAndAppend` migration, is deleted along with the test that
  pinned it; nothing replaces it because nothing backfills any more.
- `ISqlDialect`/`SqlDialect` (the `ILIKE` vs `LIKE` switch) is removed. Neither
  SQL Server nor SQLite has Postgres's `ILIKE`, so the three search handlers
  (Tracker, Users, Baseline) now call `EF.Functions.Like` unconditionally.
- Test infrastructure: `PostgresFixture` → `SqlServerFixture`, isolating each test
  run in its own throwaway **database** (`CREATE DATABASE`/`DROP DATABASE`) rather
  than a schema, since a `Search Path=`-style connection-string trick has no SQL
  Server equivalent. `TEST_POSTGRES_CONNECTION` → `TEST_SQLSERVER_CONNECTION`.
  `TestConnectionGuard` now refuses `databaseasp.net` (the ASPMonster instance) as
  well as `neon.tech`.

Two genuine behavioral differences surfaced only by applying the migration to a real
SQL Server instance, not by inspection:

- **Multiple cascade paths.** Postgres tolerates a row being reachable through more
  than one `ON DELETE CASCADE` path; SQL Server refuses to create the FK at all
  ("may cause cycles or multiple cascade paths"). `Document.ProjectId`,
  `ImportBatch.ProjectId`, and the shadow FKs EF infers on
  `DocumentSnapshot.ProjectId`/`TidpFileId` (real columns, no navigation property —
  named to match a principal entity, which is enough for EF's convention to build a
  FK) are now `Restrict` rather than the inferred/explicit `Cascade`, since each is
  already reachable through `Project → TidpFile → Document` (both genuinely
  `Cascade`, and exercised by `DeleteTidpFileHandler`). No feature deletes a
  `Project` directly, so nothing depended on the removed paths.
- **Collation.** Postgres's default collation is case-sensitive; SQL Server's is not.
  `SeedData.StatusMappings` deliberately seeds both `"No Longer In Use"` (modern
  Aconex) and `"No Longer in Use"` (legacy) as distinct rows — under SQL Server's
  default collation these collide on `IX_StatusMappings_ProjectId_AconexStatus`.
  `StatusMapping.AconexStatus` now has an explicit `Latin1_General_100_CS_AS`
  (case-sensitive) collation, matching what Postgres did by default.
  `SqliteModelTweaks` clears it for local SQLite runs, whose own default collation
  (`BINARY`) is already case-sensitive.

## Consequences

- `docs/local-dev.md` and `docs/deploy.md` are updated for SQL Server; see those for
  the connection-string shape and environment variables.
- Anyone with `TEST_POSTGRES_CONNECTION` set locally needs to export
  `TEST_SQLSERVER_CONNECTION` instead, pointed at a local SQL Server (e.g. the
  `mcr.microsoft.com/mssql/server` Docker image), not the production instance.
- Aconex `Date Modified` sub-second precision (hard rule 6) is unaffected:
  `datetime2` carries more precision than Postgres's default, not less.
