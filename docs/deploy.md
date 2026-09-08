# Deploying DIP

Two pieces, deployed separately: the **API** on ASPMonster (IIS via Plesk) and the
**frontend** on Vercel. They find each other through two settings — `Cors__Origins`
on the API and `VITE_API_URL` on the frontend — and nothing else.

---

## 1. API — ASPMonster (Plesk, IIS in-process)

### Build the package

The `backend-publish` workflow produces `dip-api.zip` on every push to `main`, and
runs the full test suite against a real Postgres first. Download it from the run's
artifacts. To build the same zip locally:

```bash
cd backend
dotnet publish src/Dip.Api/Dip.Api.csproj -c Release -o publish
cd publish && zip -r ../dip-api.zip .
```

`web.config` ships in the package. It selects `hostingModel="inprocess"`, so the app
runs inside the IIS worker process — no reverse-proxy hop, and one process holding
the Npgsql pool, the Drive-sync and import workers, and the SignalR hub.

### Deploy

1. Plesk → **Files** → upload `dip-api.zip` into the site's `httpdocs` and extract.
2. Plesk → **Dedicated .NET Core** (or *IIS Application settings*) → confirm the
   application root is the folder containing `Dip.Api.dll`.
3. Create `App_Data/logs/` under the site root and give the application pool identity
   **write** permission. It is the only path the app writes to — the rolling Serilog
   files. Workbook bytes live in the database (`FileBlobs`), never on disk.
4. Configure the application pool for a process that must stay alive (§ 1.1), set the
   environment variables below, then **Restart** the app.

> Nothing outside `App_Data/logs/` needs backing up: the database holds the workbooks
> as well as the data extracted from them, so a database restore is a complete restore.

### 1.1 The app must not be recycled — it runs the workers

Drive polling, importing and recalculation all run inside the IIS worker process
(`DriveSyncWorker`, `ImportWorker`). An idled-out or unstarted process does none of
them, and nothing outside will wake it. In Plesk → *IIS Application settings*, or in
IIS Manager:

| Setting | Value | Why |
|---|---|---|
| Application pool → **Start Mode** | `AlwaysRunning` | the process starts with IIS rather than on the first request |
| Application pool → **Idle Time-out (minutes)** | `0` | a quiet night must not stop the poller |
| Application pool → **Regular Time Interval (minutes)** | `0` | disables the default 29-hour recycle mid-import |
| Site → Advanced settings → **Preload Enabled** | `True` | the app is loaded without waiting for a request |
| Server Manager → *Web Server → Application Development* | **WebSocket Protocol** installed | lets SignalR use WebSockets |

WebSockets are not required: the hub negotiates transports and falls back to long
polling when the feature is absent. Do not force a transport in either direction.

### Environment variables (Plesk → *Environment variables*)

Everything is read at **startup** — changing any of these needs a restart.

| Variable | Required | What it is |
|---|---|---|
| `ConnectionStrings__Default` | yes | Neon connection string, Npgsql key/value format |
| `Jwt__Key` | yes | ≥ 32 bytes of random secret; signs access tokens |
| `Cors__Origins` | yes | Comma-separated frontend origins |
| `Seed__AdminEmail` | first boot | Email of the SuperAdmin created on an empty database |
| `Seed__AdminPassword` | first boot | Its password |
| `GoogleDrive__ApiKey` | optional | Only needed for Drive sync |
| `GoogleDrive__RootFolderId` | optional | The folder sync starts from; unset disables polling entirely |
| `GoogleDrive__LocalMirrorPath` | never in production | Development only: a local copy of the Drive tree served instead of the Google API (`LocalMirrorDriveClient`); ignored outside `ASPNETCORE_ENVIRONMENT=Development` |
| `GoogleDrive__PollHours` | optional | How often `DriveSyncWorker` polls Drive (default `5`; `0` disables the timer and leaves "Sync now") |
| `GoogleDrive__StartupDelaySeconds` | optional | Grace period before the first poll (default `30`) |
| `Cors__PreviewOriginSuffix` | optional | e.g. `.vercel.app`, to allow preview deployments |
| `ASPNETCORE_ENVIRONMENT` | yes | `Production` |

The double underscore is the .NET convention for nesting: `Cors__Origins` binds to
`Cors:Origins`.

**The seed only creates.** On a database that already has the admin account,
changing `Seed__AdminPassword` does nothing — the seeder never overwrites an
existing user. Reset a forgotten password from the Users screen with another
admin account, or against the database directly.

### The database — Neon

Npgsql wants key/value format, **not** the `postgresql://` URI. Neon's console has a
.NET / ADO.NET snippet that emits it directly:

```
Host=ep-xxxx.eu-central-1.aws.neon.tech;Database=dip;Username=dip_owner;Password=...;SSL Mode=Require;Timeout=30
```

- **Use the direct endpoint, not `-pooler`.** This is a long-lived process with
  Npgsql's own client-side pool that runs EF migrations at startup; PgBouncer's
  transaction pooling is the wrong side of that trade. The pooled endpoint suits
  serverless bursts of short connections, which is not this shape.
- **Scale-to-zero:** the free tier suspends when idle, so the first request after a
  quiet period pays the wake-up. `EnableRetryOnFailure(3)` is configured, and a
  generous `Timeout=30` stops the first attempt failing before the wake completes.
  Expect the first request of the morning to be slow; that is not a fault.
- **The user needs DDL rights** — `Program.cs` runs `MigrateAsync()` on every boot
  unless `Startup__SkipMigration=true`.
- Start with `SSL Mode=Require` alone. Add `Trust Server Certificate=true` only if
  the host's certificate store actually throws a chain error.

### Google Drive (optional)

The client uses an **API key**, not OAuth — two unauthenticated REST calls with
`?key=` appended. That has consequences:

1. Enable the **Google Drive API** on the Cloud project. Not doing so is the usual
   cause of the 403.
2. Create an API key and restrict it: *API restrictions* → Drive API only.
3. Leave *Application restrictions* as **None**. An HTTP-referrer restriction breaks
   it outright (server-to-server calls send no `Referer`), and an IP restriction
   needs a static outbound IP that shared hosting rarely guarantees.
4. **Share the root folder as "Anyone with the link → Viewer".** An API key carries
   no user identity, so Drive serves only publicly readable files. Children inherit
   the setting. If policy forbids link-sharing, this approach cannot work at all —
   that is what a `ServiceAccountDriveClient` behind the same `IDriveClient` is for.

`GoogleDrive__RootFolderId` is the id from the folder URL
(`drive.google.com/drive/folders/<THIS>`). Shared Drives work: the client sends
`supportsAllDrives=true`.

Verify before deploying — this is the exact call the app makes:

```bash
curl -s "https://www.googleapis.com/drive/v3/files?q=%27<FOLDER_ID>%27+in+parents+and+trashed+%3D+false&key=<API_KEY>"
```

Files listed means you are done. A 403 means the folder is not link-shared or the
key has no Drive access — the app rewords both into that sentence.

> The key travels in the query string, so it appears in Google's request logs and in
> any proxy in between. Keep it read-only and Drive-only.

Leaving Drive unconfigured is fine: `/health/detail` reports it **Degraded**, polling
is skipped (the worker logs this once and stops), and everything else — uploads,
automatic imports, drafts, reports — works.

Drive is **read-only**: the client lists folders and downloads files, and has no other
members. Nothing the platform does is ever written back to Google.

### TLS

Plesk → **SSL/TLS Certificates** → issue a Let's Encrypt certificate for the API
hostname, then enable **Permanent SEO-safe 301 redirect from HTTP to HTTPS**. The
frontend is served over HTTPS, so a browser blocks a plain-HTTP API call as mixed
content regardless of CORS.

---

## 2. Frontend — Vercel

1. Import the repository; set **Root Directory** to `frontend`. `vercel.json`
   supplies the framework, build command, SPA rewrites and cache headers.
2. Environment variables → `VITE_API_URL=https://api.your-domain.com` — the API's
   origin, **no trailing slash, no `/api` suffix**. The client appends the paths, and
   the SignalR client appends `/hubs/sync`.
3. Deploy.

`VITE_*` values are baked in at **build** time, not read at runtime. Changing
`VITE_API_URL` requires a redeploy, not a restart.

---

## 3. How the two find each other (CORS)

There is no discovery. `Cors:Origins` is read once at startup, split on commas, and
handed to `WithOrigins(...)`, which does an **exact string match** against the
browser's `Origin` header:

```
Cors__Origins=https://dip.vercel.app,https://dip.qpac.example
```

Scheme + host + non-default port. **No trailing slash, no path.**
`https://dip.vercel.app/` (trailing slash) does not match.

**Outside Development, a missing `Cors__Origins` fails at startup** rather than
falling back to "any origin". A boot error is found immediately; an open CORS policy
is not.

The policy sends `AllowCredentials`, which a browser refuses to combine with `*` — so
even in Development the origin is named (`http://localhost:5173`) rather than
wildcarded. SignalR's `/hubs/sync` negotiate call is what makes this matter: it is a
credentialed request, and a wildcard origin fails it.

**Vercel preview deployments** get their own hostname per commit, and `WithOrigins`
has no wildcards. To let previews reach the API, set:

```
Cors__PreviewOriginSuffix=.vercel.app
```

Any HTTPS origin whose host ends with that suffix is then accepted. It is a
deliberate widening — every preview of every project on that suffix qualifies — so
prefer leaving it unset for a production API and pointing previews at a staging one.

---

## 4. After deploying

```bash
curl -s https://api.your-domain.com/health          # liveness: process + database
curl -s https://api.your-domain.com/health/detail   # per-check JSON, including Drive
```

`/health` is what an uptime monitor should poll: it checks the process and the
database only. Drive is tagged `external` and reported on `/health/detail`, so a
Google outage never makes the API look down and a health poll never spends API quota.

Then, in the app:

1. Sign in with `Seed__AdminEmail` / `Seed__AdminPassword`.
2. **TIDPs** → **Sync now**, or create a folder and drop a workbook on it. The import
   starts on its own; the tile spins and the toast reports the result.
3. **Dashboard** → the numbers rebuild after each import; **Recalculate** is the
   manual fallback.

The Drive status pill on the TIDPs toolbar reads
`GET /api/projects/{id}/drive/status`, and `/health/detail` carries the same facts in
the `drive` check's description (`lastRunFinishedAt`, `queuedImports`).

### Operational notes

- **Logs**: `App_Data/logs/dip-YYYYMMDD.log`, rolling daily, 10 MB per file, 14 files
  retained. Console logging also goes to the Plesk stdout log when
  `stdoutLogEnabled` is turned on in `web.config` — leave it off in normal operation.
- **Rate limiting**: login and refresh are limited to 20 requests per minute per
  client address (`RateLimit__AuthPermitPerWindow`, `RateLimit__AuthWindowSeconds`),
  returning 429 beyond that. No other endpoint is limited — they all need a token.
- **Long operations run in the hosted workers**, not in the browser. A Drive poll, an
  import and a recalculation all continue with the tab closed; progress is pushed over
  `/hubs/sync` to whoever is watching. Recalculation is still chunked *inside* the
  worker so one pass never holds a single transaction over 16k rows.
- **A restart loses only the queue, not the work.** `ImportWorker` re-queues every
  file still `NotImported` or `Outdated` when it starts.
- **Migrations run at startup.** Deploying a build with a new migration applies it on
  the first request after the restart. Take a Neon branch first if the migration is
  destructive.
