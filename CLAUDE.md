# CLAUDE.md — DIP (Digital Information Delivery Platform)

.NET 8 Web API (controllers, vertical-slice CQRS, custom dispatcher) + React/TS (Vite, shadcn) that replaces an Excel-based TIDP → MIDP → Aconex → Tracker workflow for construction document control. PostgreSQL on Neon. Google-Drive-like folder explorer with Draft (staging) and Live data layers.

## Read first
- `PLAN.md` — single source of truth: stack, repo structure, feature-slice shape, data model (identity, folders, Live, Draft), Excel column maps, every Excel formula translated to a rule, importers, EVM, frontend structure, phases with exact prompts and expected test values.
- `docs/refactor-plan.md` — v3 refactor (2026-09-08): Drive read-only, no disk storage, hosted worker + SignalR, automatic import, effective document set, editable Lists. Overrides the PLAN.md sections it lists in its § 10.
- `docs/excel-analysis.md` — written in Phase 0; what the sample workbooks actually contain and any discrepancy with PLAN.md.
- `samples/` — TIDP-STL.xlsx, MIDP.xlsx, Tracker.xlsx. Every computed number must match these files.

## Hard rules
1. Before coding a phase: list the files you will create/change and wait for approval.
2. `dotnet build` with zero warnings, `dotnet test` green (frontend: lint + typecheck + test) before every commit. Commit message starts with the phase id, e.g. `[5.2] Corporate summary engine`.
3. One feature = one folder under `Dip.Api/Features/<Area>/<Feature>/` containing Command/Query, Validator, Handler, Controller. Controllers contain no logic. Handlers use `IDipDbContext` directly; no generic repositories.
4. Engine code (`Dip.Application/Engine`) is pure: no DbContext, no I/O. Excel and Drive access live only in `Dip.Infrastructure`.
5. No secrets in the repo, ever. Config comes from env vars / user-secrets: `ConnectionStrings__Default`, `Jwt__Key`, `GoogleDrive__ApiKey`, `GoogleDrive__RootFolderId`, `Seed__AdminEmail`, `Seed__AdminPassword`, `Cors__Origins`.
6. Preserve leading zeros (Revision `00`, Sequence `0004`, Zone `00`) and sub-second precision on Aconex `Date Modified`.
7. If the Excel contradicts PLAN.md, the Excel wins: log it in `docs/excel-analysis.md` and propose a PLAN.md edit — never silently change a rule.
8. Expected test values are read from the sample workbooks (cached cell values) where possible, not hard-coded.
9. Long operations (Drive sync, imports, recalculation) must be chunked request/response — shared hosting has no background workers.
10. No new NuGet/npm package without stating why.

## Stack
.NET 8 · ASP.NET Core Web API (controllers) · EF Core 8 + Npgsql (Neon) · ASP.NET Identity + JWT · FluentValidation · Scrutor · ClosedXML · Serilog · Swashbuckle · xUnit + FluentAssertions + Testcontainers
React 18 + TypeScript + Vite · Tailwind + shadcn/ui · TanStack Query/Table · React Router · react-hook-form + zod · Recharts · Vitest
Hosting: API on ASPMonster (IIS in-process), frontend on Vercel.

## Commands
```
cd backend && dotnet build && dotnet test
dotnet ef migrations add <Name> -p src/Dip.Infrastructure -s src/Dip.Api
dotnet ef database update      -p src/Dip.Infrastructure -s src/Dip.Api
dotnet run --project src/Dip.Api
cd frontend && npm i && npm run dev && npm run gen:api
```
