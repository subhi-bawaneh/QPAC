# Running DIP locally

A local run uses **SQLite** — one file under `backend/src/Dip.Api/App_Data/` — and never
touches the Neon database. Production stays on PostgreSQL; nothing about the deployed
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

A Postgres `ConnectionStrings:Default` left in user-secrets is deliberately **ignored** in
Development, with a warning in the log. The point of a local run is that it cannot write
to the shared database. To use Postgres locally anyway — the `dotnet ef` commands do, and
so do the integration tests:

```bash
Database__Provider=Postgres dotnet run --project src/Dip.Api
```

`dotnet ef migrations add` / `database update` are unaffected: the design-time host
(`EF.IsDesignTime`) always keeps the configured Postgres connection, so migrations are
still authored as Npgsql SQL.

## Changing the model

The local schema comes from `EnsureCreated`, not from migrations — the `Init` migration is
Npgsql SQL and cannot be replayed on SQLite. So after any change to an entity or its
configuration:

```bash
cd backend && ./scripts/reset-local-db.sh
```

The next run rebuilds the schema, re-mirrors Drive and re-imports the workbooks.
Migrations remain the Postgres story, exactly as before.

## What differs on SQLite

All of this is local-only; EF caches a model per provider, so the Postgres model and its
SQL are unchanged.

| Postgres | SQLite | Where |
| --- | --- | --- |
| `timestamp without time zone`, `jsonb`, `bytea` | cleared, SQLite picks TEXT/BLOB | `SqliteModelTweaks` |
| `decimal(10,4)` | `double` — SQLite stores decimals as text, which sorts wrongly | `SqliteModelTweaks` |
| `ILIKE` | `LIKE`, already case-insensitive for ASCII | `ISqlDialect`, the four search handlers |
| migrations | `EnsureCreated` + `PRAGMA journal_mode=WAL` | `Program.cs` |

WAL matters: the sync and import workers write while the API serves requests, and the
default rollback journal would lock readers out with `SQLITE_BUSY`.

Two consequences worth knowing:

- Case-insensitive search only covers ASCII, so Arabic titles match case-sensitively
  (Arabic has no case, so in practice this is invisible).
- `decimal` weights are held as doubles, so the last digit of a budget weight can differ
  from Postgres. Sample-workbook numbers are verified by `dotnet test` against Postgres,
  which is where they count.

## Tests

Unchanged: `dotnet test` runs against a **local Postgres**, not SQLite, and the
integration tests pin `Database:Provider=Postgres` so the local defaults never apply.

```bash
export TEST_POSTGRES_CONNECTION="Host=localhost;Port=5432;Database=dip_test;Username=postgres;Password=postgres"
cd backend && dotnet test
```

Without that variable the Postgres-backed tests skip themselves. A connection string
pointing at Neon is refused.

## Ports

| Piece | URL |
| --- | --- |
| API | `http://localhost:5001` (`/swagger`, `/health/detail`, `/hubs/sync`) |
| Frontend | `http://localhost:5173` |

The frontend defaults to `http://localhost:5001` with no `.env` at all; set `VITE_API_URL`
to override. The API already allows `http://localhost:5173` through CORS in Development.
