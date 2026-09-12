# Running DIP locally

A local run uses **SQLite** — one file under `backend/src/Dip.Api/App_Data/` — and never
touches the shared database. Production stays on SQL Server; nothing about the deployed
setup changes.

```bash
# API  (http://localhost:5001, Swagger at /swagger)
cd backend && ./scripts/run-local.sh

# Frontend (http://localhost:5173)
cd frontend && npm i && npm run dev
```

`./scripts/run-local.sh` is only `dotnet run` with `Database__Provider=Sqlite` exported;
plain `dotnet run --project src/Dip.Api` in Development does exactly the same thing.

## What the first run creates

Everything lands in `backend/src/Dip.Api/App_Data/`, which is git-ignored:

| File | What it is |
| --- | --- |
| `dip-local.db` (+ `-wal`, `-shm`) | The whole database. Delete it to start over. |
| `dev-secrets.json` | The generated `Jwt:Key` and SuperAdmin password. Never committed (hard rule 5). |
| `logs/` | Serilog's rolling file sink. |

The startup log prints the sign-in credentials:

```
[INF] Sign in as admin@dip.local / Dev3f9a21c4A1 (kept in App_Data/dev-secrets.json)
```

If you already set `Seed:AdminEmail` / `Seed:AdminPassword` yourself (user-secrets or env
vars), those are used and the password is not echoed into the log.

worker mirrors it, the import worker reads every workbook in it, and the reports are
computed from the result. No Google API key is involved. Give it a minute after startup —
`/health/detail` reports what the workers are doing.

## Why local wins over user-secrets

A SQL Server `ConnectionStrings:Default` left in user-secrets is deliberately **ignored**
in Development, with a warning in the log. The point of a local run is that it cannot
write to the shared database. To use SQL Server locally anyway — the `dotnet ef` commands
do, and so do the integration tests:

```bash
Database__Provider=SqlServer dotnet run --project src/Dip.Api
```

`dotnet ef migrations add` / `database update` are unaffected: the design-time host
(`EF.IsDesignTime`) always keeps the configured SQL Server connection, so migrations are
still authored as SQL Server SQL.

## Changing the model

The local schema comes from `EnsureCreated`, not from migrations — the `Init` migration is
SQL Server SQL and cannot be replayed on SQLite. `EnsureCreated` also does nothing at all when
the file already exists, so a changed entity used to leave the old schema in place and the
API would start cleanly and then fail every request that touched the changed table with
`SQLite Error 1: no such column`.

So the startup path fingerprints the model's tables and columns, keeps that fingerprint
inside the file, and rebuilds the file when the two disagree (`LocalSqliteSchema`). You
will see this once, and the previous database is moved to `dip-local.db.stale-<timestamp>`
rather than deleted:

```
WRN The local SQLite schema no longer matches the model, so it has been rebuilt empty.
```

A rebuilt database is empty: re-upload the TIDPs, the baseline, the picklists and the
Aconex export. To force the same thing by hand:

```bash
cd backend && ./scripts/reset-local-db.sh
```

Migrations remain the SQL Server story, exactly as before.

## What differs on SQLite

All of this is local-only; EF caches a model per provider, so the SQL Server model and its
SQL are unchanged.

| SQL Server | SQLite | Where |
| --- | --- | --- |
| `datetime2`, `nvarchar(max)` | cleared, SQLite picks TEXT | `SqliteModelTweaks` |
| `decimal(10,4)` | `double` — SQLite stores decimals as text, which sorts wrongly | `SqliteModelTweaks` |
| migrations | `EnsureCreated` + `PRAGMA journal_mode=WAL` | `Program.cs` |
| `__EFMigrationsHistory` | `__DipLocalSchema`, one model fingerprint | `LocalSqliteSchema` |

WAL matters: the sync and import workers write while the API serves requests, and the
default rollback journal would lock readers out with `SQLITE_BUSY`.

Two consequences worth knowing:

- Case-insensitive search only covers ASCII, so Arabic titles match case-sensitively
  (Arabic has no case, so in practice this is invisible).
- `decimal` weights are held as doubles, so the last digit of a budget weight can differ
  from SQL Server. Sample-workbook numbers are verified by `dotnet test` against SQL
  Server, which is where they count.

## Tests

Unchanged: `dotnet test` runs against a **local SQL Server**, not SQLite, and the
integration tests pin `Database:Provider=SqlServer` so the local defaults never apply.

```bash
export TEST_SQLSERVER_CONNECTION="Server=localhost;User Id=sa;Password=Test_1234!;TrustServerCertificate=true"
cd backend && dotnet test
```

Without that variable the SQL-Server-backed tests skip themselves. A connection string
pointing at the production ASPMonster instance is refused.

## Ports

| Piece | URL |
| --- | --- |
| API | `http://localhost:5001` (`/swagger`, `/health/detail`, `/hubs/sync`) |
| Frontend | `http://localhost:5173` |

The frontend defaults to `http://localhost:5001` with no `.env` at all; set `VITE_API_URL`
to override. The API already allows `http://localhost:5173` through CORS in Development.
