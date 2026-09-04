# ADR 001 — DateTime handling with Npgsql 8

**Status:** Accepted (2026-09-04)
**Context:** Phase 1.2 seeder failed with

> Cannot write DateTime with Kind=UTC to PostgreSQL type 'timestamp without time zone'

Npgsql 6+ split PostgreSQL `timestamp` into two mapping paths:
- `timestamp with time zone` ↔ `DateTime` with `Kind=Utc`
- `timestamp without time zone` ↔ `DateTime` with `Kind=Unspecified`

Mixing them is a hard error unless the legacy switch is turned back on.

## Decision

Every timestamp in the DIP domain is stored as `timestamp without time zone`
because the source of truth (Excel) has no timezone information and the whole
platform runs in a single project locale (Riyadh, UTC+3). Producing UTC values
on write and then losing the zone on read would break exact-match tests against
the sample workbooks.

We flip the legacy switch **once, statically, inside `DipDbContext`**:

```csharp
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
```

Consequences:
1. `DateTime.UtcNow` and `DateTime.Now` both round-trip as naive values — no
   silent conversion.
2. Aconex `DateModified` retains its microsecond precision (up to 6 fractional
   digits — verified against `Tracker.xlsx` row 12: `2026-04-26 13:04:49.041`).
3. Explicit `HasColumnType("timestamp without time zone")` on every DateTime
   property keeps the intent visible in the migration SQL, even though the
   switch alone would suffice.
4. If we ever expose a public API that surfaces timestamps to non-project
   clients, the ISO string will lack a `Z` — document that in the OpenAPI spec.

## Alternatives considered

- **Store as `timestamp with time zone` and convert on read.** Rejected: adds
  a translation layer that will silently drift from Excel values under DST
  edge cases, and every engine test would have to normalize before comparing.
- **Store `DateTimeOffset`.** Rejected: needs an offset that Excel doesn't
  provide; would have to invent one.
- **Turn the switch on in `Program.cs`.** Rejected: infrastructure tests spin
  up a `DipDbContext` without touching Program, and would fail. Keeping the
  switch in the DbContext static constructor makes it impossible to forget.
