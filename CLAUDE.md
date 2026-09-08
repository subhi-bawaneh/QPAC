# CLAUDE.md — DIP (Digital Information Delivery Platform)

.NET 8 Web API (controllers, vertical-slice CQRS, custom dispatcher) + React/TS (Vite, shadcn) that replaces an Excel-based TIDP → MIDP → Aconex → Tracker workflow for construction document control. PostgreSQL on Neon. Google Drive is mirrored read-only into a Drive-like explorer; every workbook it holds is imported automatically into one of two layers — Draft (DB1, companies still working in Drive) and Live (DB2, companies working in the system) — and the reports read both.

## Read first
- `PLAN.md` — the original v2 plan, still authoritative for the Excel column maps, every formula translated to a rule, the engine rules and the expected sample numbers. Its § 1, 3.2, 3.4, 4, 6, 8 and 9 are superseded by `docs/refactor-plan.md`.
- `docs/refactor-plan.md` — **the v3 specification (2026-09-08) and the current shape of the system**: Drive read-only, no disk storage, hosted workers + SignalR, automatic import, the effective document set, editable Lists. It overrides the PLAN.md sections listed in its § 13; read it before PLAN.md.
- `docs/refactor-log.md` — what the v3 run actually did, per phase: commits, removals, deviations and the reasons for them.
- `docs/excel-analysis.md` — written in Phase 0; what the sample workbooks actually contain and any discrepancy with PLAN.md.
- `samples/` — TIDP-STL.xlsx, MIDP.xlsx, Tracker.xlsx. Every computed number must match these files.

## Hard rules
1. Before coding a phase: list the files you will create/change and wait for approval.
2. `dotnet build` with zero warnings, `dotnet test` green (frontend: lint + typecheck + test) before every commit. Commit message starts with the phase id, e.g. `[5.2] Corporate summary engine`.
3. One feature = one folder under `Dip.Api/Features/<Area>/<Feature>/` containing Command/Query, Validator, Handler, Controller. Controllers contain no logic. Handlers use `IDipDbContext` directly; no generic repositories.
4. Engine code (`Dip.Application/Engine`) is pure: no DbContext, no I/O. Excel and Drive access live only in `Dip.Infrastructure`. Google Drive is read-only — `IDriveClient` has exactly two members, `ListChildrenAsync` and `DownloadAsync` — and nothing is written to the file system: workbook bytes live in `FileBlobs` and are parsed from a `MemoryStream`.
5. No secrets in the repo, ever. Config comes from env vars / user-secrets: `ConnectionStrings__Default`, `Jwt__Key`, `GoogleDrive__ApiKey`, `GoogleDrive__RootFolderId`, `Seed__AdminEmail`, `Seed__AdminPassword`, `Cors__Origins`.
6. Preserve leading zeros (Revision `00`, Sequence `0004`, Zone `00`) and sub-second precision on Aconex `Date Modified`.
7. If the Excel contradicts PLAN.md, the Excel wins: log it in `docs/excel-analysis.md` and propose a PLAN.md edit — never silently change a rule.
8. Expected test values are read from the sample workbooks (cached cell values) where possible, not hard-coded.
9. Long work runs in the hosted workers (`Dip.Api/Workers`) with progress pushed over the `/hubs/sync` SignalR hub; recalculation is chunked inside the worker so no single transaction spans 16k rows. Nothing long is driven by the browser one request at a time any more.
10. No new NuGet/npm package without stating why.

## Stack
.NET 8 · ASP.NET Core Web API (controllers) · SignalR (`/hubs/sync`) · hosted `BackgroundService` workers · EF Core 8 + Npgsql (Neon) · ASP.NET Identity + JWT · FluentValidation · Scrutor · ClosedXML · Serilog · Swashbuckle · xUnit + FluentAssertions
React 18 + TypeScript + Vite · Tailwind + shadcn/ui · TanStack Query + `@tanstack/react-virtual` · `@microsoft/signalr` · React Router · react-hook-form + zod · Recharts · Vitest
Hosting: API on ASPMonster (IIS in-process — the pool must not idle out, see `docs/deploy.md` § 1.1), frontend on Vercel.
Tests run against a local Postgres only; `TEST_POSTGRES_CONNECTION` pointing at Neon is refused.

## Commands
```
cd backend && dotnet build && dotnet test
dotnet ef migrations add <Name> -p src/Dip.Infrastructure -s src/Dip.Api
dotnet ef database update      -p src/Dip.Infrastructure -s src/Dip.Api
dotnet run --project src/Dip.Api
cd frontend && npm i && npm run dev && npm run gen:api
```
